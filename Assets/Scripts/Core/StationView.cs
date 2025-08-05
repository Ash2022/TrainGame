using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StationView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform passengersHolder;
    [SerializeField] Transform exits;

    // Tune if needed
    [SerializeField] float verticalSpacingFactor = 0.5f; // fraction of cellSize
    [SerializeField] bool clearExistingOnInit = true;

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

        // Optional: clear any previous visuals
        if (clearExistingOnInit)
        {
            for (int i = passengersHolder.childCount - 1; i >= 0; i--)
                Destroy(passengersHolder.GetChild(i).gameObject);
        }

        // Stack passengers along local +Y inside the holder
        float spacing = Mathf.Max(0.01f, cellSize * verticalSpacingFactor);

        for (int i = 0; i < _pointModel.waitingPeople.Count; i++)
        {
            int colorIndex = _pointModel.waitingPeople[i];

            GameObject go = Instantiate(passengerPrefab, passengersHolder, false);
            go.name = "Passenger_" + colorIndex + "_" + (i + 1);

            // Local placement (one on top of another)
            go.transform.localPosition = new Vector3(0f, 0f, i * spacing);
            go.transform.localRotation = Quaternion.identity;

            // Optional sizing (comment out if prefab already sized)
            // go.transform.localScale = Vector3.one * (cellSize * 0.25f);

            // Initialize the PassengerView with its color
            PassengerView pv = go.GetComponent<PassengerView>();
            if (pv != null)
            {
                pv.Initialize(colorIndex);
            }
            else
            {
                Debug.LogWarning("PassengerView missing on passenger prefab.");
            }
        }
    }

    /// <summary>
    /// Remove the first 'count' passengers (queue head) visually and re-pack.
    /// Assumes visuals were created in waitingPeople order.
    /// </summary>
    /// <summary>
    /// Remove the first `count` passenger visuals in one batch,
    /// then re-stack the rest along local +Y using the same spacing.
    /// </summary>
    public void RemoveHeadPassengers(int count)
    {
        if (passengersHolder == null || count <= 0) return;

        int available = passengersHolder.childCount;
        int toRemove = Mathf.Min(count, available);

        // 1) Collect the first `toRemove` transforms
        var victims = new List<Transform>(toRemove);
        for (int i = 0; i < toRemove; i++)
            victims.Add(passengersHolder.GetChild(i));

        // 2) Destroy them
        foreach (var t in victims)
            Destroy(t.gameObject);

        // 3) Restack the remaining children along +Y
        for (int i = 0; i < passengersHolder.childCount; i++)
        {
            var c = passengersHolder.GetChild(i);
            c.localPosition = new Vector3(0f, 0f, i * 0.5f);
            c.localRotation = Quaternion.identity;
        }
    }
}

