using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Common.Extension;


namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIEventInformationLineItem : MonoBehaviour
    {
        [SerializeField] private Text field;
        [SerializeField] private Button copyButton;

        private string _key, _value;
        
        private const int MAX_LENGTH = 50;
        
        private void Awake()
        {
            copyButton.onClick.AddListener(() => _value.CopyToClipboard());
        }

        public void UpdateData(KeyValuePair<string, object> information)
        {
            _key = information.Key;
            _value = (information.Value != null) ? information.Value.ToString() : "";

            // The length of the string (key and value) must not exceed MAX_LENGTH characters.
            string rawString = _key + ": " + _value;
            if (rawString.Length <= MAX_LENGTH)
            {
                field.text = _key + ": " + _value.BoldText();
            }
            else
            {
                // The 5 removed characters are for the ": " at the middle of the string and the "..." at the end of the string. 
                field.text = _key + ": " + _value.Truncate(MAX_LENGTH - _key.Length - 5).BoldText();
            }
        }
    }
}