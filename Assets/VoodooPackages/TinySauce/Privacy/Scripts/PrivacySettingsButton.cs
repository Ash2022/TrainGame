using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal;

namespace Voodoo.Tiny.Sauce.Privacy
{
    public class PrivacySettingsButton : MonoBehaviour
    {
        private const string TAG = "PrivacySettingsButton";
        [SerializeField] private Button gdprButton;

        private void Start()
        { 
            if(TinySauceBehaviour.Instance != null)
                gdprButton.onClick.AddListener(() => TinySauceBehaviour.Instance.privacyManager.OpenPrivacyScreen());
            else
                Debug.LogError("TinySauce not initialized, please make sure the TinySauce prefab is in the scene");
            // (1) privacy screen should show current choices when open
            // (2) only if choices are different we should init / terminate sdks
        }
    }
}
