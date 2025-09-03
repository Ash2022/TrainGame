using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIScreen : MonoBehaviour
    {
        private const string TAG = "TSDebugUIScreen";
        protected TinySauceSettings _tsSettings;

        public TinySauceSettings TSSettings
        {
            get => _tsSettings;
            set
            {
                _tsSettings = value;
                UpdateInfo();
            }
        }

        public virtual void OnScreenHide() {}
        protected virtual void UpdateInfo() { }
    }
}
