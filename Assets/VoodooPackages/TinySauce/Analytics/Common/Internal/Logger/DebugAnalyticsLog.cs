using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Voodoo.Tiny.Sauce.Internal.Analytics {
    public struct DebugAnalyticsLog
    {
        private const string TAG = "DebugAnalyticsLog";
        internal string EventId { get; }
        internal string WrapperName { get; }
        internal string EventName { get; }
        internal Dictionary<string, object> Parameters { get; }
        internal string Error { get; }
        internal DebugAnalyticsStateEnum StateEnum { get; }
        internal DateTime Timestamp { get; }
        internal string SessionId { get; }
        internal string AdditionalInformation { get; }
        
        internal DebugAnalyticsLog(string wrapperName, string eventName, Dictionary<string, object> param, 
            DebugAnalyticsStateEnum stateEnum, string eventId, string error,
            string sessionId = "", string additionalInformation = "")
        {
            WrapperName = wrapperName;
            EventName = eventName;
            Parameters = param;
            StateEnum = stateEnum;
            Timestamp = DateTime.Now;
            EventId = eventId;
            Error = error;
            SessionId = sessionId;
            AdditionalInformation = additionalInformation;
        }

        public override string ToString()
        {
            return
                $"EventId: {EventId}, WrapperName: {WrapperName}, " +
                $"EventName: {EventName}, StateEnum: {StateEnum}, " +
                $"Timestamp: {Timestamp}, Parameters: {Parameters}" +
                $"Error: {Error}";
        }
    }

    public enum DebugAnalyticsStateEnum
    {
        ForwardedTo3rdParty = 1,
        ErrorSending = 2,
        Sent = 3,
        SentButErrorFromServer = 4,
        Error = 5
    }
}