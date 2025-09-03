using System;
using System.Collections.Generic;
using JetBrains.Annotations;
#if NEWTONSOFT
    using Newtonsoft.Json.Linq;
#endif
namespace VoodooPackages.TinySauce.Common.Utils
{
    public static class JsonUtils
    {
        public static string AddToJson(string json, string key, string value) =>
            ReplaceLastOccurrence(json, "}", ",\"" + key + "\":\"" + value + "\"}");

        private static string ReplaceLastOccurrence(string json, string oldValue, string newValue)
        {
            int index = json.LastIndexOf(oldValue, StringComparison.Ordinal);
            if (index == -1) return json;
            return json.Remove(index, oldValue.Length).Insert(index, newValue);
        }
    
        [CanBeNull]
        internal static Dictionary<string, object> DeserializeAsDictionary(string json)
        {
            #if NEWTONSOFT
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, object>();
            return JObjectToDictionary(JObject.Parse(json));
            #else
            return null;
            #endif

        }
        #if NEWTONSOFT
        [CanBeNull]
        private static Dictionary<string, object> JObjectToDictionary(JObject jObject)
        {
            var baseDict = jObject.ToObject<Dictionary<string, object>>();
            if (baseDict == null)
            {
                return null;
            }
            var resultDict = new Dictionary<string, object>(baseDict);
            foreach (KeyValuePair<string,object> keyValuePair in baseDict)
            {
                if (keyValuePair.Value is JObject subJObject)
                {
                    resultDict[keyValuePair.Key] = JObjectToDictionary(subJObject);
                }
                else
                {
                    resultDict[keyValuePair.Key] = keyValuePair.Value;
                }
            }
            return resultDict;
        }
        #endif

    }
}