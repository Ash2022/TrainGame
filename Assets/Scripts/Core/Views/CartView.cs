
using UnityEngine;

public class CartView : MonoBehaviour
{
    [SerializeField] Renderer cartRenderer;
    internal void SetCartColor(int colorIndex)
    {
        cartRenderer.material.color = LevelVisualizer.Instance.GetColorByIndex(colorIndex);
    }
}
