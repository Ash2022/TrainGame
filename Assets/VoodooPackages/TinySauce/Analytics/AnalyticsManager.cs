using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    internal static class AnalyticsManager
    {
        private const string TAG = "AnalyticsManager";
        private const string NO_GAME_LEVEL = "game";

        internal static bool HasGameStarted { get; private set; }
        private static readonly AnalyticsEventTimer _gameTimer = new AnalyticsEventTimer();

        // Voodoo sauce additional events
        
        #region Progression Events Declaration
        internal static event Action<int, bool> OnGamePlayed;
        internal static event Action<GameStartedParameters> OnGameStartedEvent;
        internal static event Action<GameFinishedParameters> OnGameFinishedEvent;
        
        #endregion
        
        #region Resource Event Declaration
        
        internal static event Action<string> OnDeclareResourceCurrency;
        internal static event Action<string> OnDeclareResourceItemType;
        internal static event Action<ResourceEventParameters> OnResourceSinkEvent;
        internal static event Action<ResourceEventParameters> OnResourceSourceEvent;
        
        #endregion
        
        
        internal static event Action<BusinessEventParameters> OnBusinessEvent;

        #region Metrics Event Declaration

        
        private const float PerfMetricsPeriod = 30f;
        internal static event Action<PerformanceMetricsAnalyticsInfo> OnTrackPerformanceMetricsEvent;
        

        #endregion

        #region Application Launch Events Declaration

        internal static event Action OnApplicationFirstLaunchEvent;
        internal static event Action OnApplicationLaunchEvent;
        

        #endregion

        #region Interstitial Events Declaration
        
        internal static event Action<AdShownEventAnalyticsInfo> OnInterstitialShowEvent;
        internal static event Action<AdClickEventAnalyticsInfo> OnInterstitialClickedEvent;

        #endregion
        
        #region Rewarded Events Declaration
        
        internal static event Action<AdShownEventAnalyticsInfo> OnRewardedShowEvent;
        internal static event Action<AdClickEventAnalyticsInfo> OnRewardedClickedEvent;

        #endregion

        #region Custom Event Declaration
        
        //internal static event Action<string, Dictionary<string, object>, string, List<TinySauce.AnalyticsProvider>> OnTrackCustomEvent;
        internal static event Action<CustomEventParameters> OnTrackCustomEvent;

        #endregion

        #region Unusued Event Declaration, TO CLEAN
        internal static event Action OnApplicationResumeEvent;
        
        #endregion
        
        
        internal static event Action<ItemTransactionParameters> OnTrackItemTransactionEvent;


        private static readonly List<TinySauce.AnalyticsProvider> DefaultAnalyticsProvider = new List<TinySauce.AnalyticsProvider>
            {TinySauce.AnalyticsProvider.GameAnalytics};

        private static List<IAnalyticsProvider> _analyticsProviders;

        internal static void Initialize(TinySauceSettings sauceSettings, bool consent)
        {
            _analyticsProviders = new List<IAnalyticsProvider>()
            {
                new GameAnalyticsProvider(), 
                new VoodooAnalyticsProvider(new VoodooAnalyticsParameters(false, true, ""))
            };
            
            // Initialize providers
            _analyticsProviders.ForEach(provider => provider.Initialize(consent));
            
            PerformanceMetricsManager.Initialize(PerfMetricsPeriod);
        }

        internal static void OnApplicationResume()
        {
            OnApplicationResumeEvent?.Invoke();
        }

        #region Track Game Events

        internal static void TrackApplicationLaunch()
        {
            AnalyticsStorageHelper.IncrementAppLaunchCount();
            //fire app launch events
            if (AnalyticsStorageHelper.IsFirstAppLaunch())
            {
                OnApplicationFirstLaunchEvent?.Invoke();
            }

            OnApplicationLaunchEvent?.Invoke();
        }
        
        #region Track Progression Events
        
        internal static void TrackGameStarted(string level, Dictionary<string, object> eventProperties = null)
        {
            TrackGameStarted(level, null, null, eventProperties);
        }
        
        internal static void TrackGameStarted(string dimension1 = null, string dimension2 = null, string dimension3 = null, Dictionary<string, object> eventProperties = null)
        {
            GameStartedParameters gameStartedParameters = new GameStartedParameters
            {
                levelDimension1 = dimension1 ?? NO_GAME_LEVEL,
                levelDimension2 = dimension2,
                levelDimension3 = dimension3,
                eventContextProperties = eventProperties,
            };
            TrackGameStarted(gameStartedParameters);
        }
        
        internal static void TrackGameStarted(GameStartedParameters parameters)
        {
            HasGameStarted = true;
            _gameTimer.Start();
            
            var level = parameters.levelDimension1;
            if (!string.IsNullOrEmpty(parameters.levelDimension2)) level += ":" + parameters.levelDimension2;
            if (!string.IsNullOrEmpty(parameters.levelDimension3)) level += ":" + parameters.levelDimension3;
            
            AnalyticsStorageHelper.UpdateLevel(level);
            AnalyticsStorageHelper.IncrementGameCount();
            

            if (string.IsNullOrEmpty(parameters.levelDefinitionID) && !string.IsNullOrEmpty(parameters.levelDimension1))
                parameters.levelDefinitionID = parameters.levelDimension1;

            OnGameStartedEvent?.Invoke(parameters);
        }

        internal static void TrackGameFinished(bool levelComplete, float score, string level, Dictionary<string, object> eventProperties)
        {
           TrackGameFinished(levelComplete, score, level, null, null, eventProperties);
        }
        internal static void TrackGameFinished(bool levelComplete, float score, string dimension1 = null, string dimension2 = null, string dimension3 = null, Dictionary<string, object> eventProperties = null)
        {
            HasGameStarted = false;


            GameFinishedParameters gameFinishedParameters = new GameFinishedParameters
            {
                levelDimension1 = dimension1 ?? NO_GAME_LEVEL,
                levelDimension2 = dimension2,
                levelDimension3 = dimension3,
                status = levelComplete,
                score = score,
                eventContextProperties = eventProperties,
            };
            TrackGameFinished(gameFinishedParameters);

        }
        
        
        internal static void TrackGameFinished(GameFinishedParameters parameters)
        {
            _gameTimer.Stop();
            HasGameStarted = false;
            var level = parameters.levelDimension1;
            if (!string.IsNullOrEmpty(parameters.levelDimension2)) level += ":" + parameters.levelDimension2;
            if (!string.IsNullOrEmpty(parameters.levelDimension3)) level += ":" + parameters.levelDimension3;
            AnalyticsStorageHelper.UpdateLevel(level);
            if (parameters.status) {
                // used to calculate the win rate (for VoodooTune)
                AnalyticsStorageHelper.IncrementSuccessfulGameCount();
            }

            bool newHighestScore = AnalyticsStorageHelper.UpdateGameHighestScore(parameters.score);
            OnGamePlayed?.Invoke(AnalyticsStorageHelper.GetGameCount(), newHighestScore);

            int gameDuration = _gameTimer.GetDuration();
            parameters.levelDimension1 = parameters.levelDimension1 ?? NO_GAME_LEVEL;
            parameters.gameDuration = gameDuration;


            if (string.IsNullOrEmpty(parameters.levelDefinitionID) && !string.IsNullOrEmpty(parameters.levelDimension1))
                parameters.levelDefinitionID = parameters.levelDimension1;

            string levelPlaytimeEventName = "LevelPlaytime";
            if (parameters.levelDimension1 != null) levelPlaytimeEventName = string.Concat(levelPlaytimeEventName, ":", parameters.levelDimension1);
            if (parameters.levelDimension2 != null) levelPlaytimeEventName = string.Concat(levelPlaytimeEventName, ":", parameters.levelDimension2);
            if (parameters.levelDimension3 != null) levelPlaytimeEventName = string.Concat(levelPlaytimeEventName, ":", parameters.levelDimension3);
            if (parameters.levelDimension1 != null) levelPlaytimeEventName = string.Concat(levelPlaytimeEventName, ":", parameters.status.ToString());
            TrackCustomEvent(levelPlaytimeEventName, parameters.gameDuration, parameters.eventContextProperties);
            OnGamePlayed?.Invoke(AnalyticsStorageHelper.GetGameCount(), AnalyticsStorageHelper.UpdateGameHighestScore(parameters.score));
            OnGameFinishedEvent?.Invoke(parameters);
            
        }
        
        
        
        
        #endregion

        #region Upgrade Event
        
        internal static void TrackUpgradeEvent( string upgradeCategory, string upgradeName, int upgradeLevel,
                                                Dictionary<string, object> eventProperties,
                                                string type = null,
                                                List<TinySauce.AnalyticsProvider> analyticsProviders = null)
        {
            if (analyticsProviders == null || analyticsProviders.Count == 0)
            {
                analyticsProviders = DefaultAnalyticsProvider;
            }

            string eventName = "Upgrade:" + (upgradeCategory != null ? upgradeCategory + ":" + upgradeName : upgradeName) + ":" + upgradeLevel;
            
            OnTrackCustomEvent?.Invoke(new CustomEventParameters()
            {
                eventName = eventName,
                eventProperties = eventProperties,
                eventType = type,
                analyticsProviders = analyticsProviders
            });
        }
        

        #endregion

        #region Resource Event

        internal static void DeclareItemType(string itemType)
        {
            //OnDeclareResourceItemType?.Invoke(itemType);
            GameAnalytics.SettingsGA.ResourceItemTypes.Add(itemType);
        }       
        internal static void DeclareCurrencyType(string currency)
        {
            //OnDeclareResourceCurrency?.Invoke(currency);
            GameAnalytics.SettingsGA.ResourceCurrencies.Add(currency);
        }

        internal static void TrackResourceSinkEvent(string currency, int amount, string itemType, string itemName)
        {
            ResourceEventParameters resourceEventParameters = new ResourceEventParameters
            {
                currency = currency,
                amount = amount,
                itemType = itemType,
                itemName = itemName
            };
            OnResourceSinkEvent?.Invoke(resourceEventParameters);
        }
        internal static void TrackResourceSourceEvent(string currency, int amount, string itemType, string itemName)
        {
            if (!CurrencySettings.Load().currencies.Contains(currency))
            {
                VoodooLog.LogE(TAG, "The currency you are using is not declared. Please check on TinySauce > Currency Settings > Edit Settings");
            }     
            if (!CurrencySettings.Load().itemTypes.Contains(itemType))
            {
                VoodooLog.LogE(TAG, "The currency you are using is not declared. Please check on TinySauce > Currency Settings > Edit Settings");
            }
            
            ResourceEventParameters resourceEventParameters = new ResourceEventParameters
            {
                currency = currency,
                amount = amount,
                itemType = itemType,
                itemName = itemName
            };
            OnResourceSourceEvent?.Invoke(resourceEventParameters);
        }
        
        internal static void TrackIAPEvent(string currency, int amount, string itemType, string itemId, string cartType)
        {
            BusinessEventParameters businessEventParameters = new BusinessEventParameters
            {
                currency = currency,
                amount = amount,
                itemType = itemType,
                itemId = itemId,
                cartType = cartType
            };
            OnBusinessEvent?.Invoke(businessEventParameters);
        }

        #endregion

        #region Track Custom Event
        
        internal static void TrackCustomEvent(string eventName,
                                              float? score = null,
                                              Dictionary<string, object> eventProperties = null,
                                              string type = null,
                                              List<TinySauce.AnalyticsProvider> analyticsProviders = null)
        {
            if (analyticsProviders == null || analyticsProviders.Count == 0)
            {
                analyticsProviders = DefaultAnalyticsProvider;
            }

            OnTrackCustomEvent?.Invoke(new CustomEventParameters()
            {
                eventName = eventName,
                score = score,
                eventProperties = eventProperties,
                eventType = type,
                analyticsProviders = analyticsProviders
            });
        }
        
        #endregion
        
        #region Track Metrics Event

        internal static void TrackPerformanceMetrics(PerformanceMetricsAnalyticsInfo info)
        {
            OnTrackPerformanceMetricsEvent?.Invoke(info);
        }
        #endregion
        
        #region Track Interstitial Event
        
        /*
        *
        * AnalyticsManager.TrackInterstitialShow(new AdShownEventAnalyticsInfo {
              AdTag = tag,
              AdNetworkName = MediationAdapter.GetRewardedVideoAdNetworkName(),
              AdLoadingTime = (int) MediationAdapter.GetRewardedVideoLoadingTime().TotalMilliseconds,
              AdCount = AnalyticsStorageHelper.GetShowRewardedVideoCount()
          });
        */
    
        internal static void TrackInterstitialShow(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            adAnalyticsInfo.GameCount = AnalyticsStorageHelper.GetGameCount();
            OnInterstitialShowEvent?.Invoke(adAnalyticsInfo);
        }
  
        
        internal static void TrackInterstitialClick(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            adAnalyticsInfo.GameCount = AnalyticsStorageHelper.GetGameCount();
            OnInterstitialClickedEvent?.Invoke(adAnalyticsInfo);
        }

        #endregion
        
        #region Track Rewarded Event
        
        internal static void TrackRewardedShow(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            adAnalyticsInfo.GameCount = AnalyticsStorageHelper.GetGameCount();
            OnRewardedShowEvent?.Invoke(adAnalyticsInfo);
        }
        
        internal static void TrackRewardedClick(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            adAnalyticsInfo.GameCount = AnalyticsStorageHelper.GetGameCount();
            OnRewardedClickedEvent?.Invoke(adAnalyticsInfo);
        }

        #endregion
        
        internal static void TrackMoveLeftEvent( int movesLeft, string level, Dictionary<string, object> eventProperties,
            string type = null,
            List<TinySauce.AnalyticsProvider> analyticsProviders = null)
        {
            if (analyticsProviders == null || analyticsProviders.Count == 0)
            {
                analyticsProviders = DefaultAnalyticsProvider;
            }

            string eventName = "MoveLeft" + level + ":" + movesLeft;

            OnTrackCustomEvent?.Invoke(new CustomEventParameters()
            {
                eventName = eventName,
                eventProperties = eventProperties,
                eventType = type,
                analyticsProviders = analyticsProviders
            });
        }

        internal static void TrackItemTransaction(ItemTransactionParameters parameters)
        {
            OnTrackItemTransactionEvent?.Invoke(parameters);
        }
        
        
        #endregion
    }
}