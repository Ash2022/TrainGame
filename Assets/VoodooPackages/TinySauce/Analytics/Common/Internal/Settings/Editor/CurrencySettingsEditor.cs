using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Analytics.Editor
{
    [CustomEditor(typeof(CurrencySettings))]
    public class CurrencySettingsEditor : UnityEditor.Editor
    {
        private CurrencySettings CurrencySettings => target as CurrencySettings;
        
        [MenuItem("TinySauce/Currency Settings/Edit Settings", false, 100)]
        private static void EditSettings()
        {
            Selection.activeObject = CreateCurrencySauceSettings();
        }
        
        private static CurrencySettings CreateCurrencySauceSettings()
        {
            CurrencySettings settings = CurrencySettings.Load();
            if (settings == null) {
                settings = CreateInstance<CurrencySettings>();
                //create tinySauce folders if it not exists
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                if (!AssetDatabase.IsValidFolder("Assets/Resources/TinySauce"))
                    AssetDatabase.CreateFolder("Assets/Resources", "TinySauce");
                //create TinySauceSettings file
                AssetDatabase.CreateAsset(settings, "Assets/Resources/TinySauce/CurrencySettings.asset");
                settings = CurrencySettings.Load();
            }

            return settings;
        }
        
        
        
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Apply CurrencySettings")) {
                ApplyCurrencySettings();
            }
        }

        private void ApplyCurrencySettings()
        {
            GameAnalyticsProvider.SetCurrencies(CurrencySettings.currencies);
            GameAnalyticsProvider.SetItemTypes(CurrencySettings.itemTypes);
        }
    }
}