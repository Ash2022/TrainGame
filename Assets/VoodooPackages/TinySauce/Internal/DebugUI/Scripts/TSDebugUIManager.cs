using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIManager : MonoBehaviour
    {
        private const string TAG = "TSDebugUIManager";
        [SerializeField] private TSDebugUIBehaviour tsDebugUIPrefab;

        private bool isDebugUIOpen = false;

        private float maxDurationBetweenTap = 2.5f;
        private float countDown;

        private int countTapTL = 0;
        private int countTapTR = 0;

        private Vector3 mousePos;
        private int smallerScreenSliceNb = 6;
        private int biggerScreenSliceNb = 8;
        private int screenWidthSliceNb;
        private int screenHeightSliceNb;

        private int _screenSliceWidth;
        public int ScreenSliceWidth { get => _screenSliceWidth; }

        private int _screenSliceHeight;
        public int ScreenSliceHeight { get => _screenSliceHeight; }


        private void Start()
        {
            if (Screen.width < Screen.height)
            {
                screenWidthSliceNb = smallerScreenSliceNb;
                screenHeightSliceNb = biggerScreenSliceNb;
            }
            else
            {
                screenWidthSliceNb = biggerScreenSliceNb;
                screenHeightSliceNb = smallerScreenSliceNb;
            }

            _screenSliceWidth = Screen.width / screenWidthSliceNb;
            _screenSliceHeight = Screen.height / screenHeightSliceNb;
        }

        private void Update()
        {
            isDebugUIOpen = TSDebugUIBehaviour.Instance != null;
            mousePos = GetPosition();

            if (!isDebugUIOpen)
            {
                if (countDown > 0)
                {
                    countDown -= Time.unscaledDeltaTime;

                    if (countDown <= 0) ResetCountsTap();
                }

                if (GetTap())
                {
                    if (countTapTL < 1) TapTopLeftElseReset();
                    else if (countTapTR < 2) TapTopRightElseReset();
                    else if (countTapTL < 4) TapTopLeftElseReset();
                    else if (countTapTR < 6) TapTopRightElseReset();
                }

                if (countTapTL == 4 && countTapTR == 6)
                {
                    OpenDebugUI();
                    ResetCountsTap();
                }
            }
        }

        private void OpenDebugUI()
        {
            Instantiate(tsDebugUIPrefab);
            TinySauce.TrackCustomEvent("DebugUI_OpeningEvent");
        }
        
        #region [HANDLING_INPUT_SYSTEMS]
        private bool GetTap()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
            return Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_INPUT_SYSTEM && (UNITY_ANDROID || UNITY_IOS)
            return Pointer.current.press.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
        
        private Vector3 GetPosition()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
            return Mouse.current.position.ReadValue();
#elif ENABLE_INPUT_SYSTEM && (UNITY_ANDROID || UNITY_IOS)
            return Pointer.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        #endregion

        #region [TAP_FUNCTIONS]


        
        private void TapTopLeftElseReset()
        {
            if (mousePos.x <= ScreenSliceWidth && mousePos.y >= ScreenSliceHeight * (screenHeightSliceNb - 1))
                ValidTap(ref countTapTL);
            else
                ResetCountsTap();
        }
        private void TapTopRightElseReset()
        {
            if (mousePos.x >= ScreenSliceWidth * (screenWidthSliceNb - 1) && mousePos.y >= ScreenSliceHeight * (screenHeightSliceNb - 1))
                ValidTap(ref countTapTR);
            else
                ResetCountsTap();
        }

        private void ValidTap(ref int countToIncrement)
        {
            countDown = maxDurationBetweenTap;
            countToIncrement++;
        }

        private void ResetCountsTap()
        {
            //Debug.Log("RESET : TL=" + countTapTL + " // TR=" + countTapTR + " // countDown=" + countDown);
            countDown = 0;
            countTapTL = 0;
            countTapTR = 0;
        }
        #endregion
    }
}