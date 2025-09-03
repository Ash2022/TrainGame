using System;
using System.Collections.Generic;

namespace Voodoo.Tiny.Sauce.Common.Extension
{
    public static class DictionaryExtension
    {
        public static void AddIfNotNull(this Dictionary<string, object> dic, string key, object value)
        {
            if (value == null) {
                return;
            }
            
            dic.Add(key, value);
        }
        
        public static void AddIfNotNull<T>(this Dictionary<string, object> dic, string key, T? value) where T : struct
        {
            if (value.HasValue == false) {
                return;
            }
            
            dic.Add(key, value.Value);
        }
        
        public static void AddIfNotNull(this Dictionary<string, object> dic, string key, Enum value, string defaultValue = null)
        {
            if (value == null) {
                if (defaultValue != null) {
                    dic.Add(key, defaultValue);
                }
                return;
            }
            
            dic.Add(key, value.ToString());
        }
        
        public static void AddIfNotNull(this Dictionary<string, object> dic, string key, string value, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(value)) {
                if (defaultValue != null) {
                    dic.Add(key, defaultValue);
                }
                return;
            }
            
            dic.Add(key, value);
        }

        
        //public static string ToJson(this Dictionary<string, object> dic) => JsonConvert.SerializeObject(dic.RemoveNullValues());
        
        public static Dictionary<string, object> RemoveNullValues(this Dictionary<string, object> dic)
        {
            if (dic == null) {
                return null;
            }

            var dictionaryWithoutNullValues = new Dictionary<string, object>();

            foreach (KeyValuePair<string, object> kvp in dic) {
                if (kvp.Value == null) {
                    continue;
                }

                object value = kvp.Value;
                if (kvp.Value is Dictionary<string, object> objects) {
                    value = objects.RemoveNullValues();
                }
                
                dictionaryWithoutNullValues.Add(kvp.Key, value);
            }

            return dictionaryWithoutNullValues;
        }
    }
}