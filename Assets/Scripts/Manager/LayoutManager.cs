//-----------------------------------------------------------------------
// <copyright file="LayoutManager.cs" company="Google">
//
// Copyright 2019 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

namespace SIS {

    public interface ILayoutManagerDelegate {
        void LayoutManagerLoadedNewLayout(LayoutManager manager, Layout newLayout);
        void LayoutManagerHotspotListChanged(LayoutManager manager, Layout layout);
        void LayoutManagerLoadedAudioClipsChanged(LayoutManager manager, int hotspotCount);
        void StartCoroutineOn(IEnumerator e);
    }

    public class LayoutManager : ILayoutDelegate, IAudioClipLoadingDelegate {

        public ILayoutManagerDelegate layoutManagerDelegate = null;

        public Layout layout; // Current scene layout in memory
        public AudioClipLoadingManager onDemandLoadingManager = new AudioClipLoadingManager();

        public Dictionary<string, SoundFile> soundDictionary;
        public Dictionary<string, SoundFile> getSoundDictionary() { return soundDictionary; }
        public int getNumLoadedInSoundDictionary() { return loadedAudioClipCount; }
        public int loadingAudioClipCount {
            get {
                return soundDictionary.Values.Aggregate(0, (acc, x) => {
                    // Don't count the loading of the default sound file...
                    return acc + (!x.isDefaultSoundFile && x.loadState == LoadState.Loading ? 1 : 0);
                });
            }
        }
        public int loadedAudioClipCount {
            get { 
                return soundDictionary.Values.Aggregate(0, (acc, x) => {
                    // Don't count the loading of the default sound file...
                    return acc + (!x.isDefaultSoundFile && x.loadState == LoadState.Success ? 1 : 0);
                });
            }
        }

        // Top level save state int
        public int currentLayoutId {
            get {
                return PlayerPrefs.GetInt("layout", 0);
            }
            set {
                PlayerPrefs.SetInt("layout", value);
            }
        }

        // ===========
        // HOTSPOT METHODS
        public Hotspot AddNewHotspot(Vector3 localPos, Vector3 rotation, float minDist, float maxDist) {
            Hotspot h = new Hotspot(localPos, rotation, minDist, maxDist);
            layout.AddHotspot(h);
            layoutManagerDelegate?.LayoutManagerHotspotListChanged(this, layout);
            return h;
        }

        public void EraseHotspot(Hotspot hotspot) {
            if (layout == null) return;

            layout?.EraseHotspot(hotspot);
            layoutManagerDelegate?.LayoutManagerHotspotListChanged(this, layout);
        }

        public void EraseAllHotspots() {
            layout.EraseHotspots();
            layoutManagerDelegate?.LayoutManagerHotspotListChanged(this, layout);
        }


        // ============
        // SOUND FILE METHODS
        public void Bind(SoundMarker obj, Hotspot hotspot, bool startPlayback, bool reloadSoundClips) {
            AudioClip clip = soundDictionary.TryGetValue(hotspot.soundID, out SoundFile sf)
                ? sf.clip  // If the sound is found, use it
                : SoundFile.defaultSoundFile.clip; // Fallback to default

            // bind these together
            obj.SetHotspot(hotspot, true); // with override color etc
            obj.LaunchNewClip(clip, playAudio: startPlayback);
        }

        public void Bind(SoundMarker obj, SoundFile sf, bool reloadSoundClips) {
            // bind these
            obj.hotspot.Set(sf.filename);
            obj.LaunchNewClip(sf.clip);
            
            // When a new binding occurs, we SHOULD refresh the loaded sound clips
            // if (reloadSoundClips) { LoadSoundClipsExclusivelyForCurrentLayout(() => { }); }
            if (reloadSoundClips && layout.onDemandActive) { RefreshLoadStateForSoundMarkers(MainController.soundMarkers, () => { }); }
        }

        // =================
        // LAYOUT METHODS

        // Directory for save files
        public int NextLayoutId() {
            var lIds = AllLayouts().Select((l) => { return l.id; });
            if (lIds.Count() == 0) return 0;
            return lIds.Max() + 1;
        }

        public int NearestLayoutId(int to) {
            (int, int) closest = (0, int.MaxValue);
            foreach (var l in AllLayouts()) {
                int diff = Math.Abs(l.id - to);
                if (diff < int.MaxValue) {
                    closest = (l.id, diff);
                }
            }
            return closest.Item1;
        }

        public void DuplicateLayout(Layout layout) {
            Debug.Log("DuplicateLayout: " + layout.filename);
            // to duplicate, take all data of the current layout, but overwrite the id
            // to the next available
            var newLayout = layout;
            newLayout.id = NextLayoutId();
            newLayout.Save();
            currentLayoutId = newLayout.id;
        }

        public void DeleteLayout(Layout layout) {
            // Remove the layout from hdd, then select the closest layout to load
            DeleteLayoutsWith(id: layout.id);
            // Order dependent, the nearest layout is derived from disk
            currentLayoutId = NearestLayoutId(to: layout.id);
        }

        // ILayoutDelegate
        public SoundFile GetSoundFileFromSoundID(string soundID) {
            if (soundID != null && soundDictionary.TryGetValue(soundID, out SoundFile sf)) { return sf; }
            return SoundFile.defaultSoundFile;
        }

        public IEnumerable<SoundMarker> SoundMarkersUsingSoundFileID(string soundFileID, string hotspotIDToExclude = null) {
            return hotspotIDToExclude == null ? 
                MainController.soundMarkers.Where(marker => marker.hotspot.soundID == soundFileID) 
              : MainController.soundMarkers.Where(marker => marker.hotspot.id != hotspotIDToExclude && marker.hotspot.soundID == soundFileID);
        }

        // ============
        // IO METHODS]
        private static Regex jsonRegex = new Regex("json$"); // Ends in json
        private static string[] jsonFiles {
            get {
                return Directory.GetFiles(Layout.saveDirectory).Where(f => jsonRegex.IsMatch(f)).ToArray();
            }
        }
        private static Layout LayoutFromFile(string filename) {
            // IO is risky business. Let's not make assumptions
            try {
                string dataAsJson = File.ReadAllText(filename);
                var l = JsonUtility.FromJson<Layout>(dataAsJson);
                l.filename = filename; // filename is not guaranteed same, so we store it on load
                return l;
            } catch (Exception e) {
                Debug.Log("failed to load layout: " + e);
                return null;
            }
        }

        public static Layout LayoutWithId(int LayoutID) {
            var layouts = AllLayouts();
            var withID = layouts.Where( l => { return l.id == LayoutID; } );
            if (withID.Count() < 1) {
                Debug.LogError(string.Format("There is no file with the id {}.", LayoutID));
                throw new Exception(string.Format("There is no file with the id {}.", LayoutID));
            }
            if (withID.Count() > 1) {
                Debug.LogError(string.Format("There are multiple files with id: {}.", LayoutID));
                throw new Exception(string.Format("There are multiple files with id: {}.", LayoutID));
            }
            // Nice, exactly one
            return withID.First();
        }

        public static List<Layout> AllLayouts() {
            List<Layout> lays = new List<Layout>();
            foreach (var filename in jsonFiles) {
                Layout l = LayoutFromFile(filename);
                if (l != null) lays.Add(l);
            }
            return lays;
        }

        public void LoadCurrentLayout() {
            // Try to load a single layout with the current id
            // If anything goes wrong, make a new layout with the next valid id
            try {
                this.layout = LayoutWithId(currentLayoutId);
            } catch {
                Debug.LogError("FAILED to create layout with currentLayoutId: " + currentLayoutId);
                // Create a new layout instead
                this.currentLayoutId = NextLayoutId();
                this.layout = new Layout(currentLayoutId);
                this.layout.Save();
            }

            // Set appropriate references
            layout.layoutDelegate = this;
            layout.SetHotspotDelegates();
            layoutManagerDelegate?.LayoutManagerLoadedNewLayout(this, this.layout);
        }

        private static void DeleteLayoutsWith(int id) {
            // observe every file for any with the correct id
            foreach (var filename in jsonFiles) {
                Layout l = LayoutFromFile(filename);
                if (l.id != id) continue;
                // Must have the same id now
                File.Delete(filename);
            }
        }

        // Load all Audio File's into the SoundFile dictionary
        public void LoadSoundMetaFiles(Action completion) {
            
            // Make sure all 'audio files on disk' have meta files
            SoundFile.CreateNewMetas();

            // Populate the SoundFile dictionary
            int numLoaded = 0;
            Dictionary<string, SoundFile> sfDict = this.soundDictionary;
            foreach (var filename in SoundFile.metaFiles) {
                SoundFile newSoundFile = SoundFile.ReadFromMeta(filename);

                // If the SoundFile has already been loaded, don't reload it
                if (sfDict.TryGetValue(newSoundFile.filename, out SoundFile sf)) {
                    if (sf.loadState == LoadState.Success) { ++numLoaded; }
                    continue;
                }

                newSoundFile.loadState = LoadState.NotLoaded;
                sfDict[newSoundFile.filename] = newSoundFile;
            }

            layoutManagerDelegate?.LayoutManagerLoadedAudioClipsChanged(this,
                hotspotCount: layout != null ? layout.hotspots.Count : 0);
            // Debug.Log("Reloaded Metafiles... " + numLoaded + " SoundClip(s) are loaded. " 
            //                            + ( SoundFile.metaFiles.Count() - numLoaded ) + " NOT loaded.");
            completion();
        }

        // - - - - - - - - - - - -

        #region IOnDemandLoadingDelegate

        public void OnDemandLoadingLoadedAudioClipsChanged(AudioClipLoadingManager manager) {
            layoutManagerDelegate?.LayoutManagerLoadedAudioClipsChanged(this, hotspotCount: this.layout != null ? this.layout.hotspots.Count : 0);
        }
        public void StartCoroutineOn(IEnumerator e) {
            layoutManagerDelegate?.StartCoroutineOn(e);
        }

        #endregion
        

        public void LoadClipInSoundFile(SoundFile soundFile, System.Action<SoundFile> completion) {
            onDemandLoadingManager.LoadClipInSoundFile(soundFile, completion);
        }

        public void UnloadSoundMarkerAndSyncedClips(SoundMarker marker, IEnumerable<SoundMarker> syncedMarkers) {
            onDemandLoadingManager.UnloadSoundMarkerAndSyncedClips(marker, syncedMarkers);
        }

        public void LoadSoundMarkerAndSyncedClips(SoundMarker marker, Action<HashSet<SoundMarker>> completion) {
            onDemandLoadingManager.LoadSoundMarkerAndSyncedClips(marker, this.layout, completion);
        }

        public void RefreshLoadStateForSoundMarkers(List<SoundMarker> markers, Action completion) {
            onDemandLoadingManager.RefreshLoadStateForSoundMarkers(markers, this.layout, completion);
        }

        public void LoadAllAudioClipsIntoMemory(List<SoundMarker> markers, Action completion) {
            onDemandLoadingManager.LoadAllAudioClipsIntoMemory(markers, this.layout, completion);
        }

        public void ReloadSoundFiles(Action completion) {
            // LoadSoundFiles(completion);
            LoadSoundMetaFiles(completion);
        }

        public List<SoundFile> AllSoundFiles() {
            return soundDictionary.Values.ToList();
        }

        public LayoutManager() {
            // load the latest scene from memory or create new save
            soundDictionary = new Dictionary<string, SoundFile>();
            soundDictionary[SoundFile.DEFAULT_CLIP] = SoundFile.defaultSoundFile;

            onDemandLoadingManager.setDelegate(this);
        }
    }


}