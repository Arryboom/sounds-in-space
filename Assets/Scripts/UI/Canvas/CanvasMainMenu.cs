﻿//-----------------------------------------------------------------------
// <copyright file="CanvasMainMenu.cs" company="Google">
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
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SIS {
    public interface ICanvasMainMenuDelegate {
        void SoundPlaybackBTNClicked(bool playbackIsStopped);
        void ResetCameraBTNClicked();
        void PlaceSoundsBTNClicked();
        void LoadLayoutBTNClicked();
        void LoadSoundBTNClicked();
        void CurrentLayoutWasRenamed(string layoutName);
        Layout GetCurrentLayout();
    }

    public class CanvasMainMenu : CanvasBase {
        public ICanvasMainMenuDelegate canvasDelegate = null;
        public override CanvasController.CanvasUIScreen canvasID { get { return CanvasController.CanvasUIScreen.Main; } }

        public Button playbackButton = null;

        private bool _playbackIsStopped = false;
        public bool playbackIsStopped {
            get { return _playbackIsStopped; }
            private set {
                _playbackIsStopped = value;
                playbackButton.transform.GetChild(0).GetComponent<Image>().sprite = playbackIsStopped ? playSprite : stopSprite;
            }
        }

        AndroidJavaClass jc; // Java class for Flic integration
        public GameObject layoutNameGameObj = null;
        public InputField layoutNameInputField = null;
        public Text numSoundMarkersText = null;

        public Image menuBGIMG = null;
        public CanvasGroup menuButtonsCanvasGroup = null;
        public Button menuButton = null;
        public Button[] buttons = null;

        public Image menuBtnImg = null;
        public Sprite menuNormalSprite = null;
        public Sprite menuCloseSprite = null;

        public Sprite playSprite = null;
        public Sprite stopSprite = null;

        static string StartingResetCamString = "Set Start Position";
        static string ResetCamString = "Reset Start Pos.";
        public Text resetCameraText = null;
        public RectTransform resetCamButtonRect = null;

        public Text kioskModeText = null;
        public Text kioskModeTextShadow = null;
        string kioskText { get { return kioskModeText.text; } set { kioskModeText.text = value; kioskModeTextShadow.text = value; } }

        // ---------------------------

        bool initialStartPosHasBeenSet = false;
        bool menuIsOpen = false;  // !!! Start with the menu closed for 'ease of use' / testing

        // ===========================

        // Start is called before the first frame update
        void Start() {
            playbackIsStopped = false;

            UpdateMenuState(animated: false);

            // Hide the menu to start, forcing the user to Set Start Position on app startup
            SetResetCamButtonToCenterOrLeft(isCenter: true);

            jc = new AndroidJavaClass("com.google.cl.syd.solo.flic.MyReceiver");
            jc.CallStatic("createInstance");
        }

        void Update() {
            DetectFlicIntent();
        }

        private void DetectFlicIntent() {
            // Flic button resets camera on android
            try {
                if (jc.GetStatic<string>("text") == "clicked") {
                    // Simulate clicking the reset button
                    BtnClickedResetCamera();
                    jc.CallStatic("clearText");
                }
            } catch (NullReferenceException e) {
                Debug.Log(e); // Not sure why we're getting a null exception here. Pass over it for now.
            }
        }

        public void LayoutChanged(Layout layout) {
            if (layout == null) { return; }

            layoutNameInputField.text = layout.layoutName;
            numSoundMarkersText.text = "" + layout.hotspots.Count + " Sound Marker" + (layout.hotspots.Count == 1 ? "" : "s");
            playbackButton.gameObject.SetActive(layout.hotspots.Count > 0);
        }

        void SetResetCamButtonToCenterOrLeft(bool isCenter, bool animated = false) {
            float rightDist = isCenter ? -resetCamButtonRect.offsetMin.x : -280f;
            if (!animated) {
                resetCamButtonRect.offsetMax = new Vector2(rightDist, resetCamButtonRect.offsetMax.y);
                resetCameraText.text = isCenter ? StartingResetCamString : ResetCamString;
            } else {
                // Fade the text
                resetCameraText.DOColor(new Color(1f, 1f, 1f, 0), 0.4f)
                .OnComplete(() => {
                    resetCameraText.text = isCenter ? StartingResetCamString : ResetCamString;
                    resetCameraText.DOColor(Color.white, 0.6f);
                });

                DOTween.To(getter: () => resetCamButtonRect.offsetMax.x,
                           setter: x => resetCamButtonRect.offsetMax = new Vector2(x, resetCamButtonRect.offsetMax.y),
                         endValue: rightDist,
                         duration: 0.6f)
                         .SetEase(Ease.InOutExpo)
                         .OnComplete(() => resetCameraText.text = isCenter ? StartingResetCamString : ResetCamString);
            }
        }

        void UpdateMenuState(bool animated = false, float animDelay = 0) {

            int hotspotCount = canvasDelegate == null ? 0 : canvasDelegate.GetCurrentLayout().hotspots.Count;
            if (!animated) {
                titleText?.gameObject.SetActive(initialStartPosHasBeenSet);
                layoutNameGameObj.SetActive(initialStartPosHasBeenSet);
                layoutNameInputField.gameObject.SetActive(initialStartPosHasBeenSet);
                numSoundMarkersText.gameObject.SetActive(initialStartPosHasBeenSet);
                menuButton.gameObject.SetActive(initialStartPosHasBeenSet);
                playbackButton.gameObject.SetActive(initialStartPosHasBeenSet && hotspotCount > 0);

                menuBtnImg.sprite = menuIsOpen ? menuCloseSprite : menuNormalSprite;

                menuBGIMG.raycastTarget = initialStartPosHasBeenSet && menuIsOpen;
                menuBGIMG.color = new Color(0, 0, 0, menuBGIMG.raycastTarget ? 0.75f : 0);

                menuButtonsCanvasGroup.alpha = initialStartPosHasBeenSet && menuIsOpen ? 1 : 0;
                for (int i = 0; i < buttons.Length; i++) {
                    Button b = buttons[i];
                    b.gameObject.SetActive(menuIsOpen);
                }
            } else {
                titleText?.gameObject.SetActive(initialStartPosHasBeenSet);
                layoutNameGameObj.SetActive(initialStartPosHasBeenSet);
                layoutNameInputField.gameObject.SetActive(initialStartPosHasBeenSet);
                numSoundMarkersText.gameObject.SetActive(initialStartPosHasBeenSet);
                menuButton.gameObject.SetActive(initialStartPosHasBeenSet);

                menuBtnImg.sprite = menuIsOpen ? menuCloseSprite : menuNormalSprite;
                playbackButton.gameObject.SetActive(initialStartPosHasBeenSet && hotspotCount > 0);

                menuBGIMG.raycastTarget = initialStartPosHasBeenSet && menuIsOpen;

                if (initialStartPosHasBeenSet && menuButtonsCanvasGroup.alpha < 1) {
                    menuButtonsCanvasGroup.DOFade(1f, 0.6f).SetDelay(animDelay);
                } else if (menuButtonsCanvasGroup.alpha > 0 && !initialStartPosHasBeenSet) {
                    menuButtonsCanvasGroup.DOFade(0, 0.6f).SetDelay(animDelay);
                }

                menuBGIMG.DOColor(new Color(0, 0, 0, menuBGIMG.raycastTarget ? 0.75f : 0), 0.4f).SetDelay(animDelay);

                for (int i = 0; i < buttons.Length; i++) {
                    Button b = buttons[i];
                    b.gameObject.SetActive(menuIsOpen);
                }

            }
        }

        private void CloseMenu() {
            menuIsOpen = false;
            UpdateMenuState(animated: true);
        }

        // - - - - - - - - - -
        #region Textfield Callbacks

        public void LayoutNameTextfieldChanged(string str) {
            titleTextShadow.text = str;
        }

        public void LayoutNameTextfieldFinishedEditing(string str) {
            canvasDelegate?.CurrentLayoutWasRenamed(str);
        }

        #endregion
        #region Button Callbacks

        public void MenuBGClicked() {
            if (!menuIsOpen) { return; }
            BtnClickedMenu();
        }

        public void BtnClickedRenameLayout() {
            // TODO: THIS...
        }

        public void BtnClickedMenu() {
            menuIsOpen = !menuIsOpen;
            UpdateMenuState(animated: true); // TODO: change to animated, when implemented
        }

        public void BtnClickedResetCamera() {
            if (!initialStartPosHasBeenSet) {
                initialStartPosHasBeenSet = true;

                UpdateMenuState(animated: true, animDelay: 0.4f);
                SetResetCamButtonToCenterOrLeft(isCenter: false, animated: true);
            }

            if (canvasDelegate == null) { return; }
            canvasDelegate.ResetCameraBTNClicked();
        }

        public void BtnClickedPlayback() {
            playbackIsStopped = !playbackIsStopped; // setter updates the view state

            if (canvasDelegate == null) { return; }
            canvasDelegate.SoundPlaybackBTNClicked(playbackIsStopped);
        }

        // - - - - - - - - - -

        public void BtnClickedLoadLayout() {
            if (canvasDelegate == null) { return; }
            canvasDelegate.LoadLayoutBTNClicked();
            CloseMenu();
        }

        public void BtnClickedPlaceSounds() {
            if (canvasDelegate == null) { return; }
            canvasDelegate.PlaceSoundsBTNClicked();
            CloseMenu();
        }

        public void BtnClickedSoundList() {
            if (canvasDelegate == null) { return; }
            canvasDelegate.LoadSoundBTNClicked();
            CloseMenu();
        }

        public void BtnClickedRecordSounds() {
            ShowNativeUnsupportedDialog();
        }

        public void BtnClickedKioskMode() {
            ShowNativeUnsupportedDialog();
        }

        #endregion
    }
}