using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Voodoo.Tiny.Sauce.Internal.Editor;
using System.Collections.Generic;
#if UNITY_ANDROID || UNITY_IOS
using UnityEngine;
using Facebook.Unity.Settings;
using UnityEditor;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Analytics.Editor
{
    public class FacebookPreBuild : IPreprocessBuildWithReport
    {
        private const string TAG = "FacebookPreBuild";
        private const int FACEBOOK_APP_ID_STRING_MINIMUM_LENGHT = 15;
        private const int FACEBOOK_APP_ID_STRING_MAXIMUM_LENGHT = 17;
        private const int FACEBOOK_CLIENT_TOKEN_STRING_LENGHT = 32;
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            CheckAndUpdateFacebookSettings(TinySauceSettings.Load());
        }

        public static List<EIdFillingStatus> CheckAndUpdateFacebookSettings(TinySauceSettings sauceSettings)
        {
            List<EIdFillingStatus> idFillingStatus = new List<EIdFillingStatus>
                { EIdFillingStatus.NotFilled, EIdFillingStatus.NotFilled };
            
#if UNITY_ANDROID || UNITY_IOS
            idFillingStatus[0] = TinySauceSettingsChecker.IsIdFilled(sauceSettings.facebookAppId, FACEBOOK_APP_ID_STRING_MINIMUM_LENGHT, FACEBOOK_APP_ID_STRING_MAXIMUM_LENGHT);
            idFillingStatus[1] = TinySauceSettingsChecker.IsIdFilled(sauceSettings.facebookClientToken, FACEBOOK_CLIENT_TOKEN_STRING_LENGHT);
            
            FacebookSettings.AppIds = new List<string> {sauceSettings.facebookAppId};
            FacebookSettings.AppLabels = new List<string> {Application.productName};
            FacebookSettings.ClientTokens = new List<string> {sauceSettings.facebookClientToken};
            EditorUtility.SetDirty(FacebookSettings.Instance);
            AssetDatabase.SaveAssets();
#endif
            return idFillingStatus;
        }
    }
}