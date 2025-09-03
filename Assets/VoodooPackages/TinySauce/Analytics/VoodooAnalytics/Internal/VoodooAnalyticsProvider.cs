using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using Voodoo.Analytics;
using Voodoo.Sauce.Common.Utils;
using Voodoo.Tiny.Sauce.Common.Extension;
using VAC = Voodoo.Tiny.Sauce.Internal.Analytics.VoodooAnalyticsConstants;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    [Preserve]
    internal class VoodooAnalyticsProvider : IAnalyticsProvider
    {
        private const string TAG = "VoodooAnalyticsProvider";
        
        private readonly VoodooAnalyticsParameters _parameters;

        // Needed for the VoodooGDPRAnalytics class. Do not call it directly.
        public VoodooAnalyticsProvider() { }

        internal VoodooAnalyticsProvider(VoodooAnalyticsParameters parameters)
        {
            _parameters = parameters;
            if (!_parameters.UseVoodooAnalytics) return;
            RegisterEvents();
        }

        public void Instantiate(string mediation)
        {
            if (!_parameters.UseVoodooAnalytics) return;
            AnalyticsEventLogger.GetInstance().Init();
            VoodooAnalyticsWrapper.Instantiate(VoodooAnalyticsConfig.AnalyticsConfig ?? new AnalyticsConfig(),
                _parameters.UseVoodooTune,
                _parameters.LegacyABTestName,
                mediation,
                _parameters.ProxyServer,
                _parameters.EditorIdfa);
        }

        public void Initialize(bool consent)
        {
            Instantiate("");
        }

        private void RegisterEvents()
        {
            AnalyticsManager.OnApplicationFirstLaunchEvent += OnApplicationFirstLaunch;
            AnalyticsManager.OnApplicationLaunchEvent += OnApplicationLaunchEvent;
            AnalyticsManager.OnGameStartedEvent += OnGameStarted;
            AnalyticsManager.OnGameFinishedEvent += OnGameFinished;
            AnalyticsManager.OnTrackCustomEvent += TrackCustomEvent;
            AnalyticsManager.OnTrackPerformanceMetricsEvent += TrackPerformanceMetrics;
            AnalyticsManager.OnInterstitialShowEvent += OnInterstitialShowEvent;
            AnalyticsManager.OnInterstitialClickedEvent += OnInterstitialClickedEvent;
            AnalyticsManager.OnRewardedShowEvent += OnRewardedShowEvent;
            AnalyticsManager.OnRewardedClickedEvent += OnRewardedClickedEvent;
            AnalyticsManager.OnTrackItemTransactionEvent += OnTrackItemTransactionEvent;
        }

    #region Application Launch Events

        private static void OnApplicationFirstLaunch()
        {
            var data = new Dictionary<string, object> {{VAC.TOTAL_MEMORY_MBYTE, SystemInfo.systemMemorySize}};

            VoodooAnalyticsWrapper.TrackEvent(VanEventName.app_install, data);
        }

        private static void OnApplicationLaunchEvent()
        {
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.app_open);
        }
        

    #endregion

    #region Progression Events

        
        private static void OnGameStarted(GameStartedParameters parameters)
        {
            var level = parameters.levelDimension1;
            if (!string.IsNullOrEmpty(parameters.levelDimension2)) level += ":" + parameters.levelDimension2;
            if (!string.IsNullOrEmpty(parameters.levelDimension3)) level += ":" + parameters.levelDimension3;
            
            var data = new Dictionary<string, object> {
                {VAC.LEVEL, level},
                {VAC.GAME_COUNT, AnalyticsStorageHelper.GetGameCount()},
                {VAC.HIGHEST_SCORE, AnalyticsStorageHelper.GetGameHighestScore()},
                {VAC.GAME_ROUND_ID, AnalyticsStorageHelper.CreateRoundId()},
                {VAC.ORDINAL, parameters.ordinal},
                {VAC.LOOP, parameters.loop},
                {VAC.LEVEL_MOVES, parameters.levelMoves},
                {VAC.ADDITIONAL_MOVES_GRANTED, parameters.additionalMovesGranted},
            };
            
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.game_start, data, null, parameters.eventProperties, parameters.eventContextProperties);
        }

        private static void OnGameFinished(GameFinishedParameters parameters)
        {
            var data = new Dictionary<string, object> {
                {VAC.LEVEL, parameters.levelDimension1},
                {VAC.GAME_COUNT, AnalyticsStorageHelper.GetGameCount()},
                {VAC.GAME_LENGTH, parameters.gameDuration},
                {VAC.STATUS, parameters.status},
                {VAC.SCORE, parameters.score},
                {VAC.SOFT_CURRENCY_USED, parameters.softCurrencyUsed},
                {VAC.HARD_CURRENCY_USED, parameters.hardCurrencyUsed},
                {VAC.EGPS_USED, parameters.egpsUsed},
                {VAC.EGPS_RV_USED, parameters.egpsRvUsed},
            };

            AnalyticsUtil.AddParameterEnum(ref data, VAC.GAME_END_REASON, parameters.gameEndReason, "other");

            AnalyticsUtil.AddParameterString(ref data, VAC.LEVEL_DEFINITION_ID, parameters.levelDefinitionID);

            AnalyticsUtil.AddParameterNullable(ref data, VAC.NB_STARS, parameters.nbStars);
            AnalyticsUtil.AddParameterNullable(ref data, VAC.MOVES_USED, parameters.movesUsed);
            AnalyticsUtil.AddParameterNullable(ref data, VAC.MOVES_LEFT, parameters.movesLeft);
            AnalyticsUtil.AddParameterNullable(ref data, VAC.OBJECTIVES_LEFT, parameters.objectivesLeft);

            VoodooAnalyticsWrapper.TrackEvent(VanEventName.game_finish, data, null, parameters.eventProperties,
                parameters.eventContextProperties);
        }

    #endregion

    #region Custom Event
    
        private static void  TrackCustomEvent(CustomEventParameters eventParameters)
        {
            if (eventParameters.analyticsProviders.Contains(TinySauce.AnalyticsProvider.VoodooAnalytics)) {
                VoodooAnalyticsWrapper.TrackCustomEvent(eventParameters.eventName, null, eventParameters.eventType, eventParameters.eventProperties);
            }
        }

    #endregion

    #region Performance Event

        private static void TrackPerformanceMetrics(PerformanceMetricsAnalyticsInfo performanceMetrics)
        {
            var data = new Dictionary<string, object> {
                {VAC.BATTERY_LEVEL, performanceMetrics.GetBatteryLevelAsString()},
                {VAC.MIN + VAC.SEPARATOR_SYMBOL + VAC.FPS, (int) performanceMetrics.Fps.Min},
                {VAC.MAX + VAC.SEPARATOR_SYMBOL + VAC.FPS, (int) performanceMetrics.Fps.Max},
                {VAC.AVERAGE + VAC.SEPARATOR_SYMBOL + VAC.FPS, (int) performanceMetrics.Fps.Average},
                {VAC.MIN + VAC.SEPARATOR_SYMBOL + VAC.MEMORY_USAGE, ConvertUtils.ByteToMegaByte(performanceMetrics.MemoryUsage.Min)},
                {VAC.MAX + VAC.SEPARATOR_SYMBOL + VAC.MEMORY_USAGE, ConvertUtils.ByteToMegaByte(performanceMetrics.MemoryUsage.Max)},
                {VAC.AVERAGE + VAC.SEPARATOR_SYMBOL + VAC.MEMORY_USAGE, ConvertUtils.ByteToMegaByte(performanceMetrics.MemoryUsage.Average)},
                {VAC.AVERAGE + VAC.SEPARATOR_SYMBOL + VAC.MEMORY_USAGE_PERCENTAGE, performanceMetrics.AverageMemoryUsagePercentage},
                {VAC.BAD_FRAMES, performanceMetrics.BadFrames},
                {VAC.TERRIBLE_FRAMES, performanceMetrics.TerribleFrames}
            };

            VoodooAnalyticsWrapper.TrackEvent(VanEventName.performance_metrics, data);
        }

        #endregion

    #region Interstitial Events

        private static void OnInterstitialShowEvent(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            var data = new Dictionary<string, object> {
                {VAC.INTERSTITIAL_TYPE, adAnalyticsInfo.AdTag},
                {VAC.GAME_COUNT, adAnalyticsInfo.GameCount},
                {VAC.AD_COUNT, adAnalyticsInfo.AdCount},
                {VAC.NETWORK_NAME, adAnalyticsInfo.AdNetworkName},
                {VAC.AD_LOADING_TIME, adAnalyticsInfo.AdLoadingTime}
            };
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.fs_shown, data);
        }
        
        private static void OnInterstitialClickedEvent(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            var data = new Dictionary<string, object> {
                {VAC.INTERSTITIAL_TYPE, adAnalyticsInfo.AdTag},
                {VAC.GAME_COUNT, adAnalyticsInfo.GameCount},
                {VAC.AD_COUNT, adAnalyticsInfo.AdCount},
                {VAC.NETWORK_NAME, adAnalyticsInfo.AdNetworkName},
                {VAC.AD_LOADING_TIME, adAnalyticsInfo.AdLoadingTime}
            };
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.fs_click, data);
        }

    #endregion   
    
    #region Rewarded Events

        private static void OnRewardedShowEvent(AdShownEventAnalyticsInfo adAnalyticsInfo)
        {
            var data = new Dictionary<string, object> {
                {VAC.REWARDED_TYPE, adAnalyticsInfo.AdTag},
                {VAC.GAME_COUNT, adAnalyticsInfo.GameCount},
                {VAC.AD_COUNT, adAnalyticsInfo.AdCount},
                {VAC.NETWORK_NAME, adAnalyticsInfo.AdNetworkName},
                {VAC.AD_LOADING_TIME, adAnalyticsInfo.AdLoadingTime}
            };
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.rv_shown, data);
        }
        
        private static void OnRewardedClickedEvent(AdClickEventAnalyticsInfo adAnalyticsInfo)
        {
            var data = new Dictionary<string, object> {
                {VAC.REWARDED_TYPE, adAnalyticsInfo.AdTag},
                {VAC.GAME_COUNT, adAnalyticsInfo.GameCount},
                {VAC.AD_COUNT, adAnalyticsInfo.AdCount},
                {VAC.NETWORK_NAME, adAnalyticsInfo.AdNetworkName},
                {VAC.AD_LOADING_TIME, adAnalyticsInfo.AdLoadingTime}
            };
            VoodooAnalyticsWrapper.TrackEvent(VanEventName.rv_click, data);
        }

    #endregion

    private static void OnTrackItemTransactionEvent(ItemTransactionParameters parameters)
    {
        var data = new Dictionary<string, object> {
            { VAC.ITEM, parameters.item.itemName.FormatKeyName() },
            { VAC.ITEM_TYPE, parameters.item.itemType.ToString() },
            { VAC.TRANSACTION_TYPE, parameters.transactionType.ToString().ToLowerInvariant() },
            { VAC.NB_UNITS, parameters.nbUnits },
        };

        if (parameters.unitCost.HasValue) {
            float totalPrice = parameters.unitCost.Value * parameters.nbUnits;
            if (totalPrice > 0) {
                data.Add(VAC.TOTAL_PRICE, totalPrice);
            }
        }
            
        data.AddIfNotNull(VAC.UNIT_COST, parameters.unitCost);
        data.AddIfNotNull(VAC.BALANCE, parameters.balance);
        data.AddIfNotNull(VAC.LEVEL, parameters.level);
        data.AddIfNotNull(VAC.CURRENCY_USED, parameters.currencyUsed);
        data.AddIfNotNull(VAC.IAP_LOCAL_CURRENCY, parameters.iapLocalCurrency);
        data.AddIfNotNull(VAC.PLACEMENT, parameters.placement);
        data.AddIfNotNull(VAC.SUB_PLACEMENT, parameters.subPlacement);
        data.AddIfNotNull(VAC.PLACEMENT_ID, parameters.placementId);
        VoodooAnalyticsWrapper.TrackEvent(VanEventName.item_transaction, data, null, null,
            parameters.eventContextProperties);
    }
        
    }
}