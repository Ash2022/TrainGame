using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    [Serializable]
    public class CustomEventParameters
    {
        /// <summary>
        /// Name of the event.
        /// </summary>
        public string eventName = "";
        
        /// <summary>
        /// The score sent with the event. Can be null if there is no score.
        /// </summary>
        public float? score = null;
        
        /// <summary>
        /// Additional custom parameters, if a key does not exist in eventContextProperties merge into it
        /// (Deprecated, use eventContextProperties instead)
        /// </summary>
        public Dictionary<string, object> eventProperties = new Dictionary<string, object>();
        
        public string eventType = null;
        
        /// <summary>
        /// The list of analytics provider you want to track your custom event to.
        /// If this list is null or empty, the event will be tracked in
        /// GameAnalytics and Mixpanel (if the user is in a cohort)
        /// </summary>
        public List<TinySauce.AnalyticsProvider> analyticsProviders = new List<TinySauce.AnalyticsProvider>();
    }
}

