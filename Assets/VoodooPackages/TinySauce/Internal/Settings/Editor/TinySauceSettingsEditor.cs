using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Voodoo.Sauce.Internal.Editor;
using Voodoo.Tiny.Sauce.Internal.Analytics.Editor;
using Voodoo.Tiny.Sauce.Privacy;

namespace Voodoo.Tiny.Sauce.Internal.Editor
{
    [CustomEditor(typeof(TinySauceSettings))]
    public class TinySauceSettingsEditor : UnityEditor.Editor
    {
        private const string TAG = "TinySauceSettingsEditor";
        private const string EditorPrefEditorIDFA = "EditorIDFA";
        private TinySauceSettings SauceSettings => target as TinySauceSettings;
        
        private static EIdFillingStatus isGAGameKeyFilled = EIdFillingStatus.NotFilled;
        private static EIdFillingStatus isGASecretKeyFilled = EIdFillingStatus.NotFilled;
        private static EIdFillingStatus isFBAppIdFilled = EIdFillingStatus.NotFilled;
        private static EIdFillingStatus isFBClientTokenFilled = EIdFillingStatus.NotFilled;
        private static EIdFillingStatus isAdjustFilled = EIdFillingStatus.NotFilled;
        private static bool isPrivacySettingsFilled = false;
        private static bool isPrivacyURLValid = false;
        private static bool isPrivacyEmailValid = false;
        private bool isAllFilled = false;
        private bool isSceneSetupGood;

        private static bool IsGaSettingsFilled => isGASecretKeyFilled != EIdFillingStatus.NotFilled && isGAGameKeyFilled != EIdFillingStatus.NotFilled;
        private static bool IsFBSettingsFilled => isFBAppIdFilled != EIdFillingStatus.NotFilled && isFBClientTokenFilled != EIdFillingStatus.NotFilled;
        private static bool IsAdjustFilled => isAdjustFilled != EIdFillingStatus.NotFilled;

        private static EIdFillingStatus IsFbSettingsFilled
        {
            get { return isGASecretKeyFilled; }
        }


        [MenuItem("TinySauce/TinySauce Settings/Edit Settings", false, 100)]
        
        private static void EditSettings()
        {
            Selection.activeObject = CreateTinySauceSettings();
        }

        private static TinySauceSettings CreateTinySauceSettings()
        {
            TinySauceSettings settings = TinySauceSettings.Load();
            if (settings == null) {
                settings = CreateInstance<TinySauceSettings>();
                //create tinySauce folders if it not exists
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                if (!AssetDatabase.IsValidFolder("Assets/Resources/TinySauce"))
                    AssetDatabase.CreateFolder("Assets/Resources", "TinySauce");
                //create TinySauceSettings file
                AssetDatabase.CreateAsset(settings, "Assets/Resources/TinySauce/Settings.asset");
                settings = TinySauceSettings.Load();
            }

            return settings;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(15);

            GUIStyle style = new GUIStyle(GUI.skin.button);
            GUI.backgroundColor = isAllFilled ? Color.green : Color.red;
            string buttonText = isAllFilled ? "All Good!" : "FILL THE SETTINGS AND PRESS";
            buttonText = Environment.NewLine + buttonText + Environment.NewLine;
            if (!IsGaSettingsFilled) buttonText += Environment.NewLine + "You need to fill GA Settings" + Environment.NewLine;
            if (!IsFBSettingsFilled) buttonText += Environment.NewLine + "You need to fill Facebook Settings" + Environment.NewLine;
            if (!IsAdjustFilled) buttonText += Environment.NewLine + "You need to fill Adjust Settings" + Environment.NewLine;
            if (!isPrivacySettingsFilled) buttonText += Environment.NewLine + "You need to fill Privacy Settings" + Environment.NewLine;
            if (!isPrivacyURLValid) buttonText += Environment.NewLine + "Make sure you entered a valid URL (with http:// or https://)" + Environment.NewLine;
            if (!isPrivacyEmailValid) buttonText += Environment.NewLine + "Make sure you entered a valid Developer contact email" + Environment.NewLine;

#if UNITY_IOS || UNITY_ANDROID      
            if (GUILayout.Button(buttonText, style)) {
                TrimAllFields(SauceSettings);
                isAllFilled = CheckAndUpdateSdkSettings(SauceSettings);
                isSceneSetupGood = CheckSceneSetup();
            }

            if (isGAGameKeyFilled == EIdFillingStatus.LengthProblem 
                || isGASecretKeyFilled == EIdFillingStatus.LengthProblem
                || isFBAppIdFilled == EIdFillingStatus.LengthProblem
                || isFBClientTokenFilled == EIdFillingStatus.LengthProblem
                || isAdjustFilled == EIdFillingStatus.LengthProblem)
            {
                GUI.backgroundColor = Color.yellow;
                string lengthWarningText = "These IDs don't match their usual length, are you sure they are correct?\n\n";
                if (isGAGameKeyFilled == EIdFillingStatus.LengthProblem) lengthWarningText += "- GameAnalytics Game Key\n";
                if (isGASecretKeyFilled == EIdFillingStatus.LengthProblem) lengthWarningText += "- GameAnalytics Secret Key\n";
                if (isFBAppIdFilled == EIdFillingStatus.LengthProblem) lengthWarningText += "- Facebook App Id\n";
                if (isFBClientTokenFilled == EIdFillingStatus.LengthProblem) lengthWarningText += "- Facebook Client Token\n";
                if (isAdjustFilled == EIdFillingStatus.LengthProblem) lengthWarningText += "- Adjust Token\n";
                EditorGUILayout.HelpBox(lengthWarningText, MessageType.Warning, true);
            }
            
#else
            EditorGUILayout.HelpBox(BuildErrorConfig.ErrorMessageDict[BuildErrorConfig.ErrorID.INVALID_PLATFORM], MessageType.Error);   
#endif

            if (!isSceneSetupGood)
            {
                GUI.backgroundColor = Color.red;
                string sceneWarningText = "There are issues with your scene setup, please make sure there is only one TinySauce prefab and no GameAnalytics prefab.";
                EditorGUILayout.HelpBox(sceneWarningText, MessageType.Error, true);
            }

            string editorIdfa = EditorPrefs.GetString(EditorPrefEditorIDFA);
            if (string.IsNullOrEmpty(editorIdfa))
            {
                editorIdfa = Guid.NewGuid().ToString();
                EditorPrefs.SetString(EditorPrefEditorIDFA, editorIdfa);
            }

            SauceSettings.EditorIdfa = editorIdfa;
            
            GUI.backgroundColor = Color.grey;
            EditorGUILayout.HelpBox("Please note that events won’t be sent when you run your game in Unity Editor or simulator.", MessageType.Info);
        }

        private static void TrimAllFields(TinySauceSettings sauceSettings)
        {
            if (sauceSettings == null) return;
            sauceSettings.gameAnalyticsAndroidGameKey = sauceSettings.gameAnalyticsAndroidGameKey.Trim();
            sauceSettings.gameAnalyticsAndroidSecretKey = sauceSettings.gameAnalyticsAndroidSecretKey.Trim();
            sauceSettings.gameAnalyticsIosGameKey = sauceSettings.gameAnalyticsIosGameKey.Trim();
            sauceSettings.gameAnalyticsIosSecretKey = sauceSettings.gameAnalyticsIosSecretKey.Trim();
            
            sauceSettings.facebookAppId = sauceSettings.facebookAppId.Trim();
            sauceSettings.facebookClientToken = sauceSettings.facebookClientToken.Trim();

            sauceSettings.adjustAndroidToken = sauceSettings.adjustAndroidToken.Trim();
            sauceSettings.adjustIOSToken = sauceSettings.adjustIOSToken.Trim();

            sauceSettings.companyName = sauceSettings.companyName.Trim();
            sauceSettings.privacyPolicyURL = sauceSettings.privacyPolicyURL.Trim();
            sauceSettings.developerContactEmail = sauceSettings.developerContactEmail.Trim();
        }

        private static bool CheckAndUpdateSdkSettings(TinySauceSettings sauceSettings)
        {
            Console.Clear();
            List<EIdFillingStatus> gaIdFillingStatus =
                GameAnalyticsPreBuild.CheckAndUpdateGameAnalyticsSettings(sauceSettings);
            List<EIdFillingStatus> fBIdFillingStatus =
                FacebookPreBuild.CheckAndUpdateFacebookSettings(sauceSettings);
            
            isGAGameKeyFilled = gaIdFillingStatus[0];
            isGASecretKeyFilled = gaIdFillingStatus[1];
            isFBAppIdFilled = fBIdFillingStatus[0];
            isFBClientTokenFilled = fBIdFillingStatus[1];
            isAdjustFilled = AdjustBuildPrebuild.CheckAndUpdateAdjustSettings(sauceSettings);
            isPrivacySettingsFilled = PrivacyPrebuild.CheckAndUpdatePrivacySettings(sauceSettings);
            isPrivacyURLValid = PrivacyPrebuild.CheckPrivacyURLValidity(sauceSettings);
            isPrivacyEmailValid = PrivacyPrebuild.CheckPrivacyEmailValidity(sauceSettings);
            EditSettings();
            return IsGaSettingsFilled && IsFBSettingsFilled && IsAdjustFilled && isPrivacySettingsFilled && isPrivacyURLValid && isPrivacyEmailValid;
        }

        internal static bool AreAllIDsCorrect(TinySauceSettings sauceSettings)
        {
            List<EIdFillingStatus> gaIdFillingStatus =
                GameAnalyticsPreBuild.CheckAndUpdateGameAnalyticsSettings(sauceSettings);
            List<EIdFillingStatus> fBIdFillingStatus =
                FacebookPreBuild.CheckAndUpdateFacebookSettings(sauceSettings);

            if (gaIdFillingStatus[0] != EIdFillingStatus.Filled || gaIdFillingStatus[1] != EIdFillingStatus.Filled ||
                fBIdFillingStatus[0] != EIdFillingStatus.Filled || fBIdFillingStatus[1] != EIdFillingStatus.Filled ||
                AdjustBuildPrebuild.CheckAndUpdateAdjustSettings(sauceSettings) != EIdFillingStatus.Filled ||
                !PrivacyPrebuild.CheckAndUpdatePrivacySettings(sauceSettings) || !PrivacyPrebuild.CheckPrivacyURLValidity(sauceSettings)
                || !PrivacyPrebuild.CheckPrivacyEmailValidity(sauceSettings))
            {
                return false;
            }

            return true;
        }

        internal static bool CheckSceneSetup()
        {
            bool isSetupOk = SceneChecker.CheckAllScenes(out List<string> errors);
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }
            return isSetupOk;
        }
    }
}