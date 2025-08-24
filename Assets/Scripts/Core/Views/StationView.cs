using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StationView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform passengersHolder;
    [SerializeField] Transform exits;

    // fraction of cellSize used as passenger size/spacing
    [SerializeField] float passengerDepth = 0.25f;
    [SerializeField] bool clearExistingOnInit = true;

    // computed once per Initialize
    private float _spacing;

    public GamePoint PointModel { get => _pointModel; set => _pointModel = value; }

    /// <summary>
    /// Call this right after Instantiate to wire up the model.
    /// </summary>
    public void Initialize(GamePoint point, PlacedPartInstance part, float cellSize, GameObject passengerPrefab)
    {
        _pointModel = point;

        if (exits != null && part != null)
            exits.localEulerAngles = new Vector3(0f, 0f, -part.rotation);

        if (passengersHolder == null || passengerPrefab == null || _pointModel == null)
            return;

        // clear old visuals
        if (clearExistingOnInit)
        {
            for (int i = passengersHolder.childCount - 1; i >= 0; i--)
                Destroy(passengersHolder.GetChild(i).gameObject);
        }

        // compute spacing = size of one passenger
        _spacing = Mathf.Max(0.01f, passengerDepth)+passengerDepth/5f;

        int count = _pointModel.waitingPeople.Count;
        // draw in reverse: last in list at stackIdx=0, then backward
        for (int stackIdx = 0; stackIdx < count; stackIdx++)
        {
            int dataIdx = count - 1 - stackIdx;
            int colorIndex = _pointModel.waitingPeople[dataIdx];

            GameObject go = Instantiate(passengerPrefab, passengersHolder, false);
            go.name = $"Passenger_{colorIndex}_{dataIdx + 1}";

            // position at -(0.5 + stackIdx) * spacing along local -Z
            float z = -(0.5f + stackIdx) * _spacing;
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localRotation = Quaternion.identity;

            // init color
            PassengerView pv = go.GetComponent<PassengerView>();
            if (pv != null) pv.Initialize(colorIndex);
            else Debug.LogWarning("PassengerView missing on passenger prefab.");
        }
    }

    /// <summary>
    /// Remove the first 'count' passenger visuals (head of queue) and re-stack.
    /// </summary>
    public void RemoveHeadPassengers(int count)
    {
        if (passengersHolder == null || count <= 0) return;

        int available = passengersHolder.childCount;
        int toRemove = Mathf.Min(count, available);

        // 1) Collect the last 'toRemove' children now (indices won’t change)
        var victims = new List<Transform>(toRemove);
        for (int i = 0; i < toRemove; i++)
        {
            int idx = available - 1 - i;                // e.g. if available=5 and toRemove=2, idx=4,3
            victims.Add(passengersHolder.GetChild(idx));
        }

        // 2) Destroy them (Destroy is deferred, but we’ve already captured them)
        foreach (var t in victims)
            Destroy(t.gameObject);

        // 3) Restack what’s left at the same offsets
        int rem = passengersHolder.childCount;
        for (int stackIdx = 0; stackIdx < rem; stackIdx++)
        {
            var c = passengersHolder.GetChild(stackIdx);
            float z = -(0.5f + stackIdx) * _spacing;
            c.localPosition = new Vector3(0f, 0f, z);
            c.localRotation = Quaternion.identity;
        }
    }
}
