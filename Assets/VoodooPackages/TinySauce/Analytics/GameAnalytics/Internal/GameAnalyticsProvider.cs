using System.Collections.Generic;
using GameAnalyticsSDK;
using UnityEditor;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    internal class GameAnalyticsProvider : IAnalyticsProvider
    {
        private const string TAG = "GameAnalyticsProvider";
        
        internal GameAnalyticsProvider()
        {
            RegisterEvents();
        }

        public void Initialize(bool consent)
        {
            if (!GameAnalyticsWrapper.Initialize(consent)) {
                UnregisterEvents();
            }
        }

        private void RegisterEvents()
        {
            AnalyticsManager.OnGameStartedEvent += OnGameStarted;
            AnalyticsManager.OnGameFinishedEvent += OnGameFinished;
            AnalyticsManager.OnTrackCustomEvent += TrackCustomEvent;
            AnalyticsManager.OnInterstitialShowEvent += OnInterstitialShowEvent;
            AnalyticsManager.OnInterstitialClickedEvent += OnInterstitialClickedEvent;
            AnalyticsManager.OnRewardedShowEvent += OnRewardedShowEvent;
            AnalyticsManager.OnRewardedClickedEvent += OnRewardedClickedEvent;
            AnalyticsManager.OnDeclareResourceCurrency += OnDeclareCurrency;
            AnalyticsManager.OnDeclareResourceItemType += OnDeclareItemType;
            AnalyticsManager.OnResourceSinkEvent += OnResourceSinkEvent;
            AnalyticsManager.OnResourceSourceEvent += OnResourceSourceEvent;
            AnalyticsManager.OnBusinessEvent += OnBusinessEvent;
        }

        private void UnregisterEvents()
        {
            AnalyticsManager.OnGameStartedEvent -= OnGameStarted;
            AnalyticsManager.OnGameFinishedEvent -= OnGameFinished;
            AnalyticsManager.OnTrackCustomEvent -= TrackCustomEvent;
            AnalyticsManager.OnInterstitialShowEvent -= OnInterstitialShowEvent;
            AnalyticsManager.OnInterstitialClickedEvent -= OnInterstitialClickedEvent;
            AnalyticsManager.OnRewardedShowEvent -= OnRewardedShowEvent;
            AnalyticsManager.OnRewardedClickedEvent -= OnRewardedClickedEvent;
            AnalyticsManager.OnDeclareResourceCurrency -= OnDeclareCurrency;
            AnalyticsManager.OnDeclareResourceItemType -= OnDeclareItemType;
            AnalyticsManager.OnBusinessEvent -= OnBusinessEvent;
        }

        public static void SetCurrencies(List<string> currencies)
        {
            GameAnalytics.SettingsGA.IntroScreen = false;
            GameAnalytics.SettingsGA.CurrentInspectorState = GameAnalyticsSDK.Setup.Settings.InspectorStates.Basic;
            foreach (var currency in currencies)
            {
                if(!GameAnalytics.SettingsGA.ResourceCurrencies.Contains(currency))
                {
                    GameAnalytics.SettingsGA.ResourceCurrencies.Add(currency);
                }
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(GameAnalytics.SettingsGA);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
        public static void SetItemTypes(List<string> itemTypes)
        {
            GameAnalytics.SettingsGA.IntroScreen = false;
            GameAnalytics.SettingsGA.CurrentInspectorState = GameAnalyticsSDK.Setup.Settings.InspectorStates.Basic;
            foreach (var itemType in itemTypes)
            {
                if(!GameAnalytics.SettingsGA.ResourceItemTypes.Contains(itemType))
                {
                    GameAnalytics.SettingsGA.ResourceItemTypes.Add(itemType);
                }
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(GameAnalytics.SettingsGA);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
        
        private static void OnGameStarted(GameStartedParameters parameters)
        {
            GameAnalyticsWrapper.TrackProgressEvent(GAProgressionStatus.Start, parameters.levelDimension1, parameters.levelDimension2, parameters.levelDimension3, null);
        }

        private static void OnGameFinished(GameFinishedParameters parameters)
        {
            GameAnalyticsWrapper.TrackProgressEvent(parameters.status ? GAProgressionStatus.Complete : GAProgressionStatus.Fail, parameters.levelDimension1, parameters.levelDimension2, parameters.levelDimension3, (int) parameters.score);
        }

        private static void TrackCustomEvent(CustomEventParameters eventParameters)
        {
            if (eventParameters.analyticsProviders.Contains(TinySauce.AnalyticsProvider.GameAnalytics))
            {
                GameAnalyticsWrapper.TrackDesignEvent(eventParameters.eventName, eventParameters.score);
            }
        }

        private static void OnInterstitialShowEvent(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            
            GameAnalyticsWrapper.TrackAdEvent(GAAdAction.Show, GAAdType.Interstitial, adAnalyticsInfo.AdNetworkName, adAnalyticsInfo.adPlacement);
        }

        private static void OnInterstitialClickedEvent(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            GameAnalyticsWrapper.TrackAdEvent(GAAdAction.Clicked, GAAdType.Interstitial, adAnalyticsInfo.AdNetworkName, adAnalyticsInfo.adPlacement);
        }
        
        
        private static void OnRewardedShowEvent(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            GameAnalyticsWrapper.TrackAdEvent(GAAdAction.Show, GAAdType.RewardedVideo, adAnalyticsInfo.AdNetworkName, adAnalyticsInfo.adPlacement);
        }

        private static void OnRewardedClickedEvent(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            GameAnalyticsWrapper.TrackAdEvent(GAAdAction.Clicked, GAAdType.RewardedVideo, adAnalyticsInfo.AdNetworkName, adAnalyticsInfo.adPlacement);
        }

        private static void OnDeclareItemType(string itemTypeName)
        {
            GameAnalyticsWrapper.DeclareItemType(itemTypeName);
        }        
        private static void OnDeclareCurrency(string currencyName)
        {
            GameAnalyticsWrapper.DeclareCurrency(currencyName);
        }

        private static void OnResourceSinkEvent(ResourceEventParameters resourceParameters)
        {
            GameAnalyticsWrapper.TrackResourceEvent(GAResourceFlowType.Sink, resourceParameters.currency, resourceParameters.amount, resourceParameters.itemType, resourceParameters.itemName);
        }
        private static void OnResourceSourceEvent(ResourceEventParameters resourceParameters)
        {
            GameAnalyticsWrapper.TrackResourceEvent(GAResourceFlowType.Source, resourceParameters.currency, resourceParameters.amount, resourceParameters.itemType, resourceParameters.itemName);
        }        
        
        private static void OnBusinessEvent(BusinessEventParameters businessParameters)
        {
            GameAnalyticsWrapper.TrackBusinessEvent(businessParameters.currency, businessParameters.amount, businessParameters.itemType, businessParameters.itemId, businessParameters.cartType);
        }
    }
}