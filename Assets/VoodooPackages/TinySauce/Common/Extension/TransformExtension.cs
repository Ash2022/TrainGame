using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Common.Extension
{
    public static class TransformExtension
    {
        public static void RefreshHierarchySize(this Transform transform)
        {
            if (transform.GetComponent<ContentSizeFitter>() != null && transform.GetComponent<RectTransform>() != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform.GetComponent<RectTransform>());
            }
            Transform parent = transform.parent;
            if (parent != null) RefreshHierarchySize(parent);
        }
    }
}