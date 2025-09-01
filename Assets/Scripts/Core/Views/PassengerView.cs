
using System;
using UnityEngine;

public class PassengerView : MonoBehaviour
{

    [SerializeField] Renderer passengerRenderer;
    public int isLocked = 0;
    public int colorIndex = 0;
    internal void Initialize(int _colorIndex, int _isLocked)  
    {
        //passengerRenderer.material.color = LevelVisualizer.Instance.GetColorByIndex(colorIndex);

        isLocked = _isLocked;
        colorIndex = _colorIndex;

        if(isLocked!=0)
            passengerRenderer.material = LevelVisualizer.Instance.GetPassengersEmptyMaterial();
        else
            passengerRenderer.material = LevelVisualizer.Instance.GetPassengersMaterialByIndex(colorIndex);
    }

    public void UnlockPassenger(int colorIndex)
    {
        isLocked=0;
        passengerRenderer.material = LevelVisualizer.Instance.GetPassengersMaterialByIndex(colorIndex);
    }

    internal void DestroyPassenger(float delay)
    {
        Destroy(gameObject, delay);
    }
}
