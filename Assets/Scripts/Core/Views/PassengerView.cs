
using UnityEngine;

public class PassengerView : MonoBehaviour
{

    [SerializeField] Renderer passengerRenderer;
    internal void Initialize(int colorIndex)
    {
        passengerRenderer.material.color = Utils.colors[colorIndex];
    }

    
}
