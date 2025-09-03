using System.Collections.Generic;
using GameAnalyticsSDK;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Voodoo.Tiny.Sauce.Internal;
using Voodoo.Tiny.Sauce.Internal.Editor;

namespace Voodoo.Tiny.Sauce.Internal.Analytics.Editor
{
    public class GameAnalyticsPreBuild : IPreprocessBuildWithReport
    {
        private const string TAG = "GameAnalyticsPreBuild";
        private const int GAME_ANALYTICS_GAME_KEY_STRING_LENGHT = 32;
        private const int GAME_ANALYTICS_SECRET_KEY_STRING_LENGHT = 40;
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            CheckAndUpdateGameAnalyticsSettings(TinySauceSettings.Load());
        }

        public static List<EIdFillingStatus> CheckAndUpdateGameAnalyticsSettings(TinySauceSettings settings)
        {
            List<EIdFillingStatus> idFillingStatus = new List<EIdFillingStatus>
                { EIdFillingStatus.NotFilled, EIdFillingStatus.NotFilled };
            
#if UNITY_ANDROID
            if (settings != null)
            {
                idFillingStatus = CheckGameAnalyticsSettings(settings.gameAnalyticsAndroidGameKey,
                    settings.gameAnalyticsAndroidSecretKey, RuntimePlatform.Android);
            }
#elif UNITY_IOS
            if (settings != null)
            {
                idFillingStatus = CheckGameAnalyticsSettings(settings.gameAnalyticsIosGameKey,
                    settings.gameAnalyticsIosSecretKey, RuntimePlatform.IPhonePlayer);
            }
#endif
            return idFillingStatus;
        }

        private static List<EIdFillingStatus> CheckGameAnalyticsSettings(string gameKey, string secretKey, RuntimePlatform platform)
        {
            List<EIdFillingStatus> idFillingStatus = new List<EIdFillingStatus>() { EIdFillingStatus.NotFilled, EIdFillingStatus.NotFilled};
            idFillingStatus[0] = TinySauceSettingsChecker.IsIdFilled(gameKey, GAME_ANALYTICS_GAME_KEY_STRING_LENGHT);
            idFillingStatus[1] = TinySauceSettingsChecker.IsIdFilled(secretKey, GAME_ANALYTICS_SECRET_KEY_STRING_LENGHT);

            if (!GameAnalytics.SettingsGA.Platforms.Contains(platform))
                GameAnalytics.SettingsGA.AddPlatform(platform);

            int platformIndex = GameAnalytics.SettingsGA.Platforms.IndexOf(platform);
            GameAnalytics.SettingsGA.UpdateGameKey(platformIndex, gameKey);
            GameAnalytics.SettingsGA.UpdateSecretKey(platformIndex, secretKey);
            GameAnalytics.SettingsGA.Build[platformIndex] = Application.version;
            GameAnalytics.SettingsGA.InfoLogBuild = false;
            GameAnalytics.SettingsGA.InfoLogEditor = false;
            
            return idFillingStatus;
        }
    }
}