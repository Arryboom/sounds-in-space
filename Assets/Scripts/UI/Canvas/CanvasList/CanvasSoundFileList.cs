﻿//-----------------------------------------------------------------------
// <copyright file="CanvasSoundFileList.cs" company="Google">
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
using UnityEngine.UI;

namespace SIS {
    public class CanvasSoundFileList : CanvasListBase<SoundFile> {
        public override CanvasController.CanvasUIScreen canvasID { get { return CanvasController.CanvasUIScreen.SoundFileList; } }

        public ScrollRect scrollRect;
        public Button refreshButton;

        private void Awake() {

        }


        override public void CanvasWillAppear() {
            base.CanvasWillAppear();

            confirmButton.interactable = false;

            ReloadSoundFiles();
        }

        private void ReloadSoundFiles() {
            confirmButton.interactable = false;
            refreshButton.enabled = false;
            scrollRect.enabled = false;

            canvasDelegate?.ReloadSoundFiles(() => {
                data = canvasDelegate?.AllSoundFiles();
                if (data != null) data.Sort((a, b) => { return string.Compare(a.filenameWithExtension, b.filenameWithExtension); });
                RefreshCells();

                refreshButton.enabled = true;
                scrollRect.enabled = true;
            });
        }

        public void RefreshSoundFileListBtnClicked() {
            ClearCells();
            ReloadSoundFiles();
            VoiceOver.main.StopPreview();
        }


        public override void CellClicked(CanvasListCell<SoundFile> listCell, SoundFile datum) {
            base.CellClicked(listCell, datum);

            confirmButton.interactable = (_selectedCells.Count > 0);
            
            // This will now be handled by CanvasController
            // VoiceOver.main.PlayPreview(datum);
        }

        public override void ConfirmButtonClicked() {
            VoiceOver.main.StopPreview();
            base.ConfirmButtonClicked();
        }


        public override void BackButtonClicked() {
            VoiceOver.main.StopPreview();
            base.BackButtonClicked();
        }


    }
}