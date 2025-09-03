using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Common.Extension;


namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIEventInformationList : MonoBehaviour
    {
        [SerializeField]
        private Text title;
        [SerializeField]
        private Transform container;

        public void Initialize(string header)
        {
            title.text = header.BoldText();
        }

        public Transform GetContainer => container;
    }
}