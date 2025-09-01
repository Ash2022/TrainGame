using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LockedIndicationView : MonoBehaviour
{
    [SerializeField] TMP_Text lockedValue;

    public void SetValue(int displayValue)
    {
        lockedValue.text = displayValue.ToString();
    }
}
