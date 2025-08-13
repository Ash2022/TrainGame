using System.Collections.Generic;
using UnityEngine;


public class EmptyTrackPartView : MonoBehaviour
{
    
    [SerializeField] Transform partObject;
    

    /// <summary>
    /// Called from LevelVisualizer.BuildCoroutine after Instantiate.
    /// </summary>
    public void Setup(Material partsMaterial)
    {
       
        partObject.transform.localPosition = Vector3.zero;
        partObject.transform.localEulerAngles = Vector3.zero;

        partObject.GetComponent<Renderer>().material = partsMaterial;

        // 2) size so that 1 grid-cell = CellSize world units
        //    our sprites import at 100px = 1 unit, and a 2×1 part is 200×100 px → 2×1 world units.
        float s = LevelVisualizer.Instance.CellSize;
        transform.localScale = new Vector3(s, s, 1f);
    }


}
