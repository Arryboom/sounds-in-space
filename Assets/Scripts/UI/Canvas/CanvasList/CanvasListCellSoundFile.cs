﻿//-----------------------------------------------------------------------
// <copyright file="CanvasListCellSoundFile.cs" company="Google">
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

namespace SIS {
    public class CanvasListCellSoundFile : CanvasListCell<SoundFile> {

        public override void SetDatum(SoundFile newDatum) {
            base.SetDatum(newDatum);

            // Specifics
            if (datum.isDefaultSoundFile) {
                titleLabel.text = datum.soundName;

            } else {
                titleLabel.text = datum.filenameWithExtension;
                // text.text = datum.filenameWithExtension;
            }

            int durSecs = datum.durationSafe;
            int durMins = durSecs / 60;
            durSecs = durSecs % 60;

            subtitleLabel.text = string.Format("{0:00}:{1:00}", durMins, durSecs);
        }
    }
}