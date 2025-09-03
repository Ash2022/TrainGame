using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal;
using VoodooPackages.TinySauce.Privacy.Localisation;

namespace Voodoo.Tiny.Sauce.Privacy
{
    public class PrivacyScreenBehaviour : MonoBehaviour
    {
        private const string TAG = "PrivacyScreenBehaviour";
        [SerializeField] private Toggle advertisingToggle;
        [SerializeField] private Toggle analyticsToggle;
        [SerializeField] private Toggle ageToggle;
        
        [SerializeField] private Button playButton;
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button learnMoreButton;
        
        [SerializeField] private Text labelText;
        [SerializeField] private Text advertisingText;
        [SerializeField] private Text analyticsText;
        [SerializeField] private Text ageText;
        [SerializeField] private Text playButtonText;
        [SerializeField] private Text privacyPolicyText;
        [SerializeField] private Text titleText;
        [FormerlySerializedAs("learnMoreText")] [SerializeField] private Text dataSharingPartnersText;
        [SerializeField] private LocalisationDict localisationDict;
        
        
        private TinySauceSettings _sauceSettings;
        
        [SerializeField] private GameObject mainUIObject;
        
        private EventSystem _eventSystemPrefab;
        private EventSystem _eventSystem;
        
        public TaskCompletionSource<bool[]> ConfirmWaitTask;
        
        private void Start()
        {
            
            ageToggle.onValueChanged.AddListener(OnToggleAge);

            learnMoreButton.onClick.AddListener(OnPressLearnMore);

            playButton.onClick.AddListener(OnPressPlay);
            privacyPolicyButton.onClick.AddListener(OnPressPrivacyPolicy);
            
            _sauceSettings = TinySauceSettings.Load();
            if (_sauceSettings == null)
            {
                throw new Exception("Can't find the Settings ScriptableObject in the Resources/TinySauce folder.");
            }
            
            
            InitEventSystem();
        }

        internal void Localize(string countryCode = "EN")
        {
            Dictionary<string,string> dict = localisationDict.GetLocalisationDict(countryCode);
            labelText.text = dict[GdprLocalisationStrings.gdpr_text.ToString()];
            advertisingText.text = dict[GdprLocalisationStrings.gdpr_advertising.ToString()];
            analyticsText.text = dict[GdprLocalisationStrings.gdpr_analytics.ToString()];
            ageText.text = dict[GdprLocalisationStrings.gdpr_age.ToString()];
            playButtonText.text = dict[GdprLocalisationStrings.gdpr_continue.ToString()];
            privacyPolicyText.text = dict[GdprLocalisationStrings.gdpr_privacy.ToString()];
            dataSharingPartnersText.text = dict[GdprLocalisationStrings.gdpr_data_sharing_partners.ToString()];
            titleText.text = dict[GdprLocalisationStrings.gdpr_title.ToString()];
        }

        private void OnToggleAge(bool consent)
        {
            playButton.interactable = consent;
        }

        public void SetToggleStates(bool adConsent, bool analyticsConsent)
        {
            advertisingToggle.isOn = adConsent;
            analyticsToggle.isOn = analyticsConsent;
            ageToggle.isOn = true;
            OnToggleAge(ageToggle);
        }

        private void OnPressLearnMore()
        {
            TinySauceBehaviour.Instance.privacyManager.OpenPrivacyPartnersScreen();
        }

        private void OnPressPlay()
        {
            mainUIObject.SetActive(false);
            
            ConfirmWaitTask?.TrySetResult(new []{advertisingToggle.isOn, analyticsToggle.isOn});
            
            RemoveListeners();
            
            if (_eventSystem != null)
                Destroy(_eventSystem.gameObject);
            
            Destroy(gameObject);
        }

        private void OnPressPrivacyPolicy()
        {
            if (_sauceSettings.privacyPolicyURL != "")
            {
                Application.OpenURL(_sauceSettings.privacyPolicyURL);
            }
        }

        private void RemoveListeners()
        {
            ageToggle.onValueChanged.RemoveAllListeners();
            playButton.onClick.RemoveAllListeners();
            privacyPolicyButton.onClick.RemoveAllListeners();
        }
        
        private void InitEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            
#if ENABLE_INPUT_SYSTEM
            if (_eventSystemPrefab == null)
            {
                _eventSystemPrefab = Resources.LoadAll<EventSystem>("Prefabs/NEW_INPUT_SYSTEM")[0];
            }
#else
            if (_eventSystemPrefab == null)
            {
                _eventSystemPrefab = Resources.LoadAll<EventSystem>("Prefabs/OLD_INPUT_SYSTEM")[0];
            }
#endif
        
            if (_eventSystemPrefab == null)
                Debug.LogError("There is no TSEventSystem prefab in the 'Assets/VoodooPackages/TinySauce/Resources/Prefabs' folder");

            _eventSystem = Instantiate(_eventSystemPrefab);
        }
    }
}
