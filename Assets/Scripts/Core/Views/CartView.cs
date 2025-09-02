
using UnityEngine;

public class CartView : MonoBehaviour
{
    [SerializeField] Renderer cartRenderer;
    [SerializeField] Rigidbody cartRigidBody;
    internal void SetCartColor(int colorIndex)
    {
        //cartRenderer.material.color = LevelVisualizer.Instance.GetColorByIndex(colorIndex);

        cartRenderer.material = LevelVisualizer.Instance.GetPassengersMaterialByIndex(colorIndex);
    }
}
