using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;
using VoodooPackages.TinySauce.Common.Utils;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
    internal class AnalyticsEventLogger
    {
        private const string TAG = "AnalyticsEventLogger";
        private const string VOODOO_ANALYTICS_WRAPPER_NAME = "VoodooAnalytics";
        private const string PLAYER_PREF_RECORDING_AT_STARTUP_KEY = "van_debugger_recording_startup";
        private static AnalyticsEventLogger _instance;
        private readonly List<DebugAnalyticsLog> _logsList = new List<DebugAnalyticsLog>(500);
        private readonly HashSet<string> _logsIdList = new HashSet<string>();
        private static bool _isAnalyticsDebuggingEnabled;
        private static bool _isAnalyticsLoggingEnabled;
        internal List<DebugAnalyticsLog> GetLocalAnalyticsLog(string wrapperNameFilter = null)
        {
            return string.IsNullOrEmpty(wrapperNameFilter) 
                ? _logsList 
                : _logsList.Where(nameInList => nameInList.WrapperName.Contains(wrapperNameFilter)).ToList();
        }
        
        internal event Action<bool> OnRecordingStateChange;
        internal bool IsRecordingEvents { get; private set; }
        
        internal bool IsRecordingAtStartup
        {
            get {
                if (PlayerPrefs.HasKey(PLAYER_PREF_RECORDING_AT_STARTUP_KEY)) {
                    return PlayerPrefs.GetInt(PLAYER_PREF_RECORDING_AT_STARTUP_KEY) == 1;
                }

                return true;
            }
            set
            {
                if (value)
                {
                    PlayerPrefs.SetInt(PLAYER_PREF_RECORDING_AT_STARTUP_KEY, 1);
                }
                else
                {
                    PlayerPrefs.SetInt(PLAYER_PREF_RECORDING_AT_STARTUP_KEY, 0);
                }
            }
        }
        
        
        internal static event Action<DebugAnalyticsLog, bool> OnAnalyticsEventStateChanged;

        
        
        
        internal static AnalyticsEventLogger GetInstance() => _instance ?? (_instance = new AnalyticsEventLogger());


        internal void Init()
        {
            SetAnalyticsEventRecording(IsRecordingAtStartup);
            SetAnalyticsDebugging(true);
        }
        
       
        
        private void LogEventLocally(string wrapperName, string eventName, DebugAnalyticsStateEnum state, string eventId, Dictionary<string, object> param = null, string error = "")
        {
            var isAlreadyCaughtEvent = state != DebugAnalyticsStateEnum.ForwardedTo3rdParty && _logsIdList.Contains(eventId);

            if (!_isAnalyticsDebuggingEnabled && !IsRecordingEvents && !isAlreadyCaughtEvent) return;

            var localAnalyticsLog = new DebugAnalyticsLog(wrapperName, eventName, param, state, eventId, error,
                AnalyticsSessionHelper.DefaultHelper().SessionId);
            var isUpdateFromExisting = false;

            if (IsRecordingEvents || isAlreadyCaughtEvent) {
                if (!isAlreadyCaughtEvent) {
                    _logsList.Add(localAnalyticsLog);
                    _logsIdList.Add(localAnalyticsLog.EventId);
                } else {
                    var index = _logsList.FindIndex(logItem => logItem.EventId.Contains(localAnalyticsLog.EventId));
                    if (index == -1) {
                        _logsList.Add(localAnalyticsLog);
                    } else {
                        _logsList[index] = localAnalyticsLog;
                        isUpdateFromExisting = true;
                    }
                }
            }

            OnAnalyticsEventStateChanged?.Invoke(localAnalyticsLog, isUpdateFromExisting);
        }

        internal void LogEventSentTo3rdParty(string wrapperName, string eventName, string eventId, [CanBeNull] Dictionary<string, object> param = null)
        {
            if (!_isAnalyticsDebuggingEnabled) return;
            LogEventLocally(wrapperName, eventName, DebugAnalyticsStateEnum.ForwardedTo3rdParty, eventId, param);
        }

        internal void LogEventException(string wrapperName, string eventName, string eventId, [CanBeNull] Dictionary<string, object> param, Exception e)
        {
            if (!_isAnalyticsDebuggingEnabled) return;
            LogEventLocally(wrapperName, eventName, DebugAnalyticsStateEnum.Error, eventId, param, e != null ? e.ToString() : "");
        }

        internal void LogEventsSentSuccessfully(List<string> eventJsons)
        {
            if (!_isAnalyticsDebuggingEnabled) return;
            LogAnalyticsSentOrErrorEvent(VOODOO_ANALYTICS_WRAPPER_NAME, eventJsons, DebugAnalyticsStateEnum.Sent);
        }
        
        internal void LogEventsSentError(List<string> eventJsons, string error)
        {
            if (!_isAnalyticsDebuggingEnabled) return;
            LogAnalyticsSentOrErrorEvent(VOODOO_ANALYTICS_WRAPPER_NAME, eventJsons, DebugAnalyticsStateEnum.SentButErrorFromServer, error);
        }
        
        
        private void LogAnalyticsSentOrErrorEvent(string wrapperName, List<string> eventJsons,
            DebugAnalyticsStateEnum stateEnum, string error = "")
        {
            foreach (string eventJson in eventJsons)
            {
                if(string.IsNullOrEmpty(eventJson))
                    continue;
                
                Dictionary<string, object> param;
                try
                {
                    param = JsonUtils.DeserializeAsDictionary(eventJson);
                }
                catch (Exception e)
                {
                    VoodooLog.LogE(TAG, e.Message);
                    continue;
                }

                if (param != null)
                {
                    var eventName = TryToGetStringOrEmpty(AnalyticsEventLoggerConstant.EVENT_NAME, param);
                    var eventId = TryToGetStringOrEmpty(AnalyticsEventLoggerConstant.EVENT_ID, param);
                    LogEventLocally(wrapperName, eventName, stateEnum, eventId, param, error);
                }
            }
        }

        private static string TryToGetStringOrEmpty<T>(T key, Dictionary<T, object> data)
        {
            if (!data.ContainsKey(key)) return "";
            
            var value = data[key];
            return value != null ? value.ToString() : "";
        }
        
        private static string GetEventNameFromJson(string json)
        {
            return GetValueFromJsonWithRegex(json, AnalyticsEventLoggerConstant.EVENT_NAME_JSON_REGEX_PATTERN);
        }

        private static string GetEventIdFromJson(string json)
        {
            return GetValueFromJsonWithRegex(json, AnalyticsEventLoggerConstant.EVENT_ID_JSON_REGEX_PATTERN);
        }

        private static string GetValueFromJsonWithRegex(string json, string regex)
        {
            Match regexMatch = Regex.Match(json, regex);
            if (!regexMatch.Success) return "";
            string match = regexMatch.Groups[1].Value;
            int matchSubstrIndex =
                match.IndexOf(AnalyticsEventLoggerConstant.JSON_SEPARATOR, StringComparison.Ordinal);
            if (matchSubstrIndex < 1) return "";
            return match.Substring(0, matchSubstrIndex);
        }

        internal void SetAnalyticsDebugging(bool enabled)
        {
            _isAnalyticsDebuggingEnabled = enabled;
        }

        internal void SetAnalyticsLogging(bool enabled)
        {
            _isAnalyticsLoggingEnabled = enabled;
        }

        internal void FlushAnalyticsLogs()
        {
            _logsIdList.Clear();
            _logsList.Clear();
        }
        
        private string DictionaryToString(Dictionary < string, object > dictionary) {  
            string dictionaryString = "{";  
            foreach(KeyValuePair < string, object > keyValues in dictionary) {  
                dictionaryString += keyValues.Key + " : " + keyValues.Value + ", ";  
            }  
            return dictionaryString.TrimEnd(',', ' ') + "}";  
        }
        
        internal void SetAnalyticsEventRecording(bool enabled)
        {
            if (enabled != IsRecordingEvents)
            {
                IsRecordingEvents = enabled;
                OnRecordingStateChange?.Invoke(IsRecordingEvents);
            }
        }
        
        
        
    }
}