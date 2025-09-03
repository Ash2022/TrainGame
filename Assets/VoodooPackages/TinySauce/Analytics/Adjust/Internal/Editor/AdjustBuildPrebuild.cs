using System.Collections.Generic;
using Facebook.Unity.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal;
using Voodoo.Tiny.Sauce.Internal.Editor;

namespace Voodoo.Tiny.Sauce.Internal.Analytics.Editor
{
    public class AdjustBuildPrebuild : IPreprocessBuildWithReport
    {
        private const string TAG = "AdjustBuildPrebuild";
        private const int ADJUST_TOKEN_STRING_LENGHT = 12;
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            CheckAndUpdateAdjustSettings(TinySauceSettings.Load());
        }

        public static EIdFillingStatus CheckAndUpdateAdjustSettings(TinySauceSettings sauceSettings)
        {
#if UNITY_ANDROID
            return TinySauceSettingsChecker.IsIdFilled(sauceSettings.adjustAndroidToken, ADJUST_TOKEN_STRING_LENGHT);
            //BuildErrorWindow.LogBuildError(BuildErrorConfig.ErrorID.NoAdjustToken);
#endif
#if UNITY_IOS
            return TinySauceSettingsChecker.IsIdFilled(sauceSettings.adjustIOSToken, ADJUST_TOKEN_STRING_LENGHT);
#endif
            return EIdFillingStatus.NotFilled;
        }
    }
}