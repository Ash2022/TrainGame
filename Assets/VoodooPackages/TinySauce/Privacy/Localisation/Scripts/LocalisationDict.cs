using System.Collections.Generic;
#if NEWTONSOFT
using Newtonsoft.Json;
#endif
using UnityEngine;

namespace VoodooPackages.TinySauce.Privacy.Localisation
{
    [CreateAssetMenu(fileName = "TranslationData", menuName = "Localization/Translation Data")]
    public class LocalisationDict : ScriptableObject
    {
        public TextAsset jsonFile;
        private Dictionary<string, Dictionary<string, string>> _dictionary;
        
        private void OnEnable()
        {
            LoadLocalisationString();
        }
        
        
        public Dictionary<string, string> GetLocalisationDict(string countryCode)
        {
            if (_dictionary.ContainsKey(countryCode))
            {
                return _dictionary[countryCode];
            }
            return _dictionary["GB"];
        }
        
        public string GetLocalisationString(string countryCode, string fieldKey)
        {
            if (_dictionary.ContainsKey(countryCode) && _dictionary[countryCode].ContainsKey(fieldKey))
            {
                return _dictionary[countryCode][fieldKey];
            }
            return "";
        }

        internal void LoadLocalisationString()
        {
            
#if NEWTONSOFT
            _dictionary = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(jsonFile.text);
#endif
        }
    }

    public enum GdprLocalisationStrings
    {
        gdpr_text,
        gdpr_advertising,
        gdpr_analytics,
        gdpr_title,
        gdpr_age,
        gdpr_privacy,
        gdpr_data_sharing_partners,
        gdpr_continue,
        gdpr_learn_more
    }
}