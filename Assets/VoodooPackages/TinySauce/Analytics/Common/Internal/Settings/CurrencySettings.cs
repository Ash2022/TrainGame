using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    [CreateAssetMenu(fileName = "Assets/Resources/TinySauce/CurrencySettings", menuName = "TinySauce/Currency Settings file")]
    public class CurrencySettings : ScriptableObject
    {

        private const string TAG = "CurrencySettings";
        private const string SETTING_RESOURCES_PATH = "TinySauce/CurrencySettings";
        public static CurrencySettings Load() => Resources.Load<CurrencySettings>(SETTING_RESOURCES_PATH);

        [SerializeField] public List<string> currencies;
        [SerializeField] public List<string> itemTypes;
        

    }

}
