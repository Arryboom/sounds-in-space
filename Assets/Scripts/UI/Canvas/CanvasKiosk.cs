﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SIS {
    public interface ICanvasKioskDelegate {
        void BackButtonClicked(CanvasController.CanvasUIScreen fromScreen);
        Layout GetCurrentLayout();
    }

    public class CanvasKiosk : CanvasBase, INumPadDelegate {
        public ICanvasKioskDelegate canvasDelegate = null;

        public override CanvasController.CanvasUIScreen canvasID { get { return CanvasController.CanvasUIScreen.Kiosk; } }
        public UnityEngine.UI.Text passcodeText;
        public NumPad numPad;

        private ushort _numIndex = 0;
        private int[] _numberData = new int[4];

        private int _camOriginalCullingMask = 0;

        private void Awake() {
            numPad.setNumPadDelegate(this);
        }

        // Start is called before the first frame update
        void Start() {
            
        }

        override public void CanvasWillAppear() {
            base.CanvasWillAppear();

            _numIndex = 0;
            updatePasscodeLabel();

            #if UNITY_ANDROID
            Screen.fullScreen = true;
            #endif

            // Make the rendering more efficient by turning rendering off that isn't required
            _camOriginalCullingMask = Camera.main.cullingMask;
            Camera.main.cullingMask = 0;

            GoogleARCore.ARCoreBackgroundRenderer arBGRend = Camera.main.GetComponent<GoogleARCore.ARCoreBackgroundRenderer>();
            arBGRend.enabled = false;
        }

        private void updatePasscodeLabel() {
            if (_numIndex < 1) {
                passcodeText.text = "";
            } else {
                string str = "";
                for (int i = 0; i < _numIndex; ++i) {
                    str += "*";
                }
                passcodeText.text = str;
            }
        }

        private bool validateNumInput() {
            string passcode = SingletonData.Instance.KioskPasscode;
            if (_numIndex != passcode.Length - 1) { return false; }
            for (int i = 0; i < passcode.Length; ++i) {
                int num = passcode[i] - '0';
                if (_numberData[i] != num) { return false; }
            }

            return true;
        }

        #region INumPadDelegate

        public void NumPadClicked(int number) {
            _numberData[_numIndex] = number;
            
            if (_numIndex >= SingletonData.Instance.KioskPasscode.Length - 1) {
                if (validateNumInput()) {
                    // TODO: Get outta here!
                    // ...
                    Debug.Log("PASS!");

                    if (canvasDelegate == null) { return; }
                    canvasDelegate.BackButtonClicked(this.canvasID);

                    #if UNITY_ANDROID
                    Screen.fullScreen = false;
                    #endif

                    // Turn rendering back on...
                    Camera.main.cullingMask = _camOriginalCullingMask;
                    GoogleARCore.ARCoreBackgroundRenderer arBGRend = Camera.main.GetComponent<GoogleARCore.ARCoreBackgroundRenderer>();
                    arBGRend.enabled = true;
                }
                _numIndex = 0;
            } else {
                ++_numIndex;
            }
            updatePasscodeLabel();
        }

        #endregion

        // Update is called once per frame
        // void Update() {
            
        // }
    }
}