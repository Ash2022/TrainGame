
using UnityEngine;

public class PassengerView : MonoBehaviour
{

    [SerializeField] Renderer passengerRenderer;
    public bool isLocked = false;
    internal void Initialize(int colorIndex, bool _isLocked)  
    {
        //passengerRenderer.material.color = LevelVisualizer.Instance.GetColorByIndex(colorIndex);

        isLocked = _isLocked;

        if(isLocked)
            passengerRenderer.material = LevelVisualizer.Instance.GetPassengersEmptyMaterial();
        else
            passengerRenderer.material = LevelVisualizer.Instance.GetPassengersMaterialByIndex(colorIndex);
    }

    public void UnlockPassenger(int colorIndex)
    {
        isLocked=false;
        passengerRenderer.material = LevelVisualizer.Instance.GetPassengersMaterialByIndex(colorIndex);
    }
    
}
