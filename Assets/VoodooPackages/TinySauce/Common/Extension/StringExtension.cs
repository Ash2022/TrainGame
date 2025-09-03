using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Common.Extension
{

    public static class StringExtension
    {
        /// <summary>
        /// This method can only be used for version following the conventional version format (System.Version)
        /// </summary>
        /// <param name="currentVersion"></param>
        /// <param name="minimalVersion"></param>
        /// <returns></returns>
        public static int CompareVersionTo(this string currentVersion, string minimalVersion)
        {
            string[] splitedCurrentVersion = currentVersion.Split('.');
            string[] splitedReferenceVersion = minimalVersion.Split('.');
        
            int minLength = Math.Min(splitedCurrentVersion.Length, splitedReferenceVersion.Length);
            
            for (int i = 0; i < minLength; i++)
            {
                try
                {
                    int comparison = Convert.ToInt32(splitedCurrentVersion[i]).CompareTo(Convert.ToInt32(splitedReferenceVersion[i]));

                    if (comparison > 0)
                        return +1;
                    if (comparison < 0)
                        return -1;
                }
                catch (Exception e)
                {
                    string message = "This method is using the System.Version convention. " +
                                     $"One of your version is not in the correct format : {splitedCurrentVersion[i]}, {splitedReferenceVersion[i]}";
                    FormatException exception = new FormatException(message, e);
                    throw exception;
                }
            }

            if (splitedCurrentVersion.Length < splitedReferenceVersion.Length)
            {
                for (int i = minLength; i < splitedReferenceVersion.Length; i++)
                {
                    if (splitedReferenceVersion[i] != "0")
                        return -1;
                }
            }

            if (splitedCurrentVersion.Length > splitedReferenceVersion.Length)
            {
                for (int i = minLength; i < splitedCurrentVersion.Length; i++)
                {
                    if (splitedCurrentVersion[i] != "0")
                        return +1;
                }
            }

            return 0;
        }
        
        public static string RemoveVersionLastDigit(this string version)
        {
            int index = version.LastIndexOf(".");
            return index == -1 ? version : version.Substring(0,index);
        }
        
        public static void CopyToClipboard(this string text)
        {
            var textEditor = new TextEditor {text = text};
            textEditor.SelectAll();
            textEditor.Copy();
        }

        public static string BoldText(this string text) => "<b>" + text + "</b>";
        
        public static string RemoveSpace(this string text) => Regex.Replace(text, @"\s+", "");
        
        public static string Remove(this string text, string textToRemove) => text.Replace(textToRemove, "");
        
        public static string StringValue(this int value) => value.ToString(CultureInfo.InvariantCulture);

        public static string StringValue(this float value) => value.ToString(CultureInfo.InvariantCulture);
        
        public static string Capitalize(this string text) => Regex.Replace(text, "^[a-z]", match => match.Value.ToUpperInvariant());

        public static string FormatKeyName(this object obj) => obj.ToString().ToLowerInvariant().Replace(' ', '_');
        
        public static string Truncate(this string value, int maxChars) => string.IsNullOrEmpty(value) || maxChars <= 0 || value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
    }
}