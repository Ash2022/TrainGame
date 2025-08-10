using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CartView : MonoBehaviour
{
    [SerializeField] Renderer renderer;
    internal void SetCartColor(int colorIndex)
    {
        renderer.material.color = Utils.colors[colorIndex];
    }
}
