
using System.Collections.Generic;

namespace Voodoo.Sauce.Internal.Editor
{
    public static class BuildErrorConfig
    {
        private const string TAG = "BuildErrorConfig";
        // Individual offsets by error type 
         private const int no = 0;
         private const int ga = 300;

         public enum ErrorID
         {
             NoVoodooSettings = no,
             SettingsNoFacebookAppID,
             NoFacebookClientToken,
             GANoIOSKey = ga, 
             GANoAndroidAndKey,
             NoAdjustToken,
             INVALID_PLATFORM,
             NoCompanyName,
             NoPrivacyUrl,
             NoDeveloperEmail,
             NoTinySauce,
             MultipleTinySauce,
             GameAnalyticsPrefabFound
         }
         
         public  static readonly Dictionary<ErrorID, string> ErrorMessageDict = new Dictionary<ErrorID, string>
         {
             {ErrorID.NoVoodooSettings, "No Settings file found.  Check your path for Assets/Resources/TinySauce/Settings.asset"},
             {ErrorID.INVALID_PLATFORM, "Invalid Platform please switch to IOS or Android on your Build Settings"},
             {ErrorID.SettingsNoFacebookAppID, "TinySauce Settings is missing Facebook App Id"},
             {ErrorID.NoFacebookClientToken, "TinySauce Settings is missing Facebook Client Token"},
             {ErrorID.GANoIOSKey, "TinySauce Settings is missing iOS GameAnalytics keys"},
             {ErrorID.GANoAndroidAndKey, "TinySauce Settings is missing Android GameAnalytics keys! add 'ignore' in both fields to disable Android analytics"},
             {ErrorID.NoAdjustToken, "TinySauce Settings is missing Adjust App Token"},
             {ErrorID.NoCompanyName, "TinySauce Settings is missing your Company Name"},
             {ErrorID.NoPrivacyUrl, "TinySauce Settings is missing your Privacy URL"},
             {ErrorID.NoDeveloperEmail, "TinySauce Settings is missing your Developer Email"},
             {ErrorID.MultipleTinySauce, "Multiple Instances of TinySauce found in your scenes. Please make sure there is only one TinySauce prefab across all your scenes"},
             {ErrorID.GameAnalyticsPrefabFound, "A GameAnalytics prefab was found in one of your scenes. Please remove it as TinySauce will handle its initialization"},
             {ErrorID.NoTinySauce, "No TinySauce prefab found in your scenes. Please add it to your first scene, or use the Preloader scene at \"Assets/VoodooPackages/TinySauce/Preload/Scene/TSPreloadScene.unity\""}
         };
    }
 }
