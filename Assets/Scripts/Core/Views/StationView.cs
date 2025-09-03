using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class StationView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform passengersHolder;
    [SerializeField] Transform exits;
    [SerializeField] Transform stationHolder;

    // fraction of cellSize used as passenger size/spacing
    [SerializeField] float passengerDepth = 0.25f;
    [SerializeField] bool clearExistingOnInit = true;

    List<PassengerView> passengers = new List<PassengerView>();
    GameObject lockedIndication;



    // computed once per Initialize
    private float _spacing;
    Coroutine buildRoutine;

    public GamePoint PointModel { get => _pointModel; private set => _pointModel = value; }

    /// <summary>
    /// Call this right after Instantiate to wire up the model.
    /// </summary>
    public void Initialize(GamePoint point, PlacedPartInstance part, float cellSize, GameObject passengerPrefab,float delay)
    {
        _pointModel = point;

        if (exits != null && part != null)
            exits.localEulerAngles = new Vector3(0f, 0f, -part.rotation);

        if (passengersHolder == null || passengerPrefab == null || _pointModel == null)
            return;

        stationHolder.localScale = new Vector3(cellSize, cellSize, cellSize);

        // clear old visuals
        if (clearExistingOnInit)
        {
            for (int i = passengersHolder.childCount - 1; i >= 0; i--)
                Destroy(passengersHolder.GetChild(i).gameObject);
        }

        buildRoutine = StartCoroutine(BuildPassengers(passengerPrefab,delay));
    }


    private IEnumerator BuildPassengers(GameObject passengerPrefab,float StartDelay)
    {
        yield return new WaitForSeconds(StartDelay);

        List<PassengerView> lockedPassengers = new List<PassengerView>();

        // compute spacing = size of one passenger
        _spacing = Mathf.Max(0.01f, passengerDepth) + passengerDepth / 5f;

        int count = _pointModel.waitingPeople.Count;
        // draw in reverse: last in list at stackIdx=0, then backward
        for (int stackIdx = 0; stackIdx < count; stackIdx++)
        {
            int dataIdx = count - 1 - stackIdx;
            int colorIndex = _pointModel.waitingPeople[dataIdx];

            GameObject go = Instantiate(passengerPrefab, passengersHolder, false);
            go.name = $"Passenger_{colorIndex}_{dataIdx + 1}";

            // position at -(0.5 + stackIdx) * spacing along local -Z
            float z = -(0.5f + stackIdx) * _spacing- passengerDepth/2f;
            go.transform.localPosition = new Vector3(0f, 0f, z + 2);
            go.transform.localRotation = Quaternion.identity;


            //locking will be managed in the station level 

            // init color
            PassengerView pv = go.GetComponent<PassengerView>();

            passengers.Add(pv);

            bool isPassengerLocked = _pointModel.waitingDelays[dataIdx] > GameManager.Instance.level.totalCollectedPassengers;

            if(isPassengerLocked)
                lockedPassengers.Add(pv);

            if (pv != null) pv.Initialize(colorIndex, _pointModel.waitingDelays[dataIdx]);
            else Debug.LogWarning("PassengerView missing on passenger prefab.");

            go.transform.localScale = Vector3.zero;

            go.transform.DOScale(Vector3.one*passengerDepth, 0.1f);
            go.transform.DOLocalMove(new Vector3(0f, 0f, z), 0.2f);

            yield return new WaitForSeconds(0.15f);
        }

        //check if we have locked and if so display 1 lock on the middle passengerView
        if(lockedPassengers.Count>0)
        {
            yield return new WaitForSeconds(0.15f);

            int indexToShowOn = lockedPassengers.Count/2;

            PassengerView passengerView = lockedPassengers[indexToShowOn];

            lockedIndication = UIManager.Instance.GenerateLockedIndication(passengerView.transform.position, passengerView.isLocked);

            lockedIndication.transform.localScale = Vector3.zero;

            lockedIndication.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutElastic);
        }
    }

    /// <summary>
    /// Remove the first 'count' passenger visuals (head of queue) and re-stack.
    /// 
    /// now should also see if some other passengers of it are unlocked
    /// 
    /// </summary>
    public void RemoveHeadPassengers(int count)
    {
        if (passengersHolder == null || count <= 0) return;

        
        float delay = 0.25f;

        for (int i = 0; i < count; i++)
            passengers[passengers.Count -1 - i].DestroyPassenger(delay * i);        

        passengers.RemoveRange(passengers.Count-count, count);

        // 3) Restack what’s left at the same offsets
        
        for (int passengerIndex = 0; passengerIndex < passengers.Count; passengerIndex++)
        {
            var c = passengers[passengerIndex].transform;
            float z = -(0.5f + passengerIndex) * _spacing - passengerDepth / 2f;
            c.localPosition = new Vector3(0f, 0f, z);
            c.localRotation = Quaternion.identity;
        }
    }

    public void UpdatePassengersLocking()
    {
        bool passengersUnlocked = false;

        for (int passengerIndex = 0; passengerIndex < passengers.Count; passengerIndex++)
        {
            var c = passengers[passengers.Count - 1 - passengerIndex].transform;
            //check if this passenger was locked - and if so check if its now unlocked - and if so update visuals.
            PassengerView passengerView = c.gameObject.GetComponent<PassengerView>();
            if (passengerView.isLocked!=0)
            {
                if (_pointModel.waitingDelays[passengerIndex] <= GameManager.Instance.level.totalCollectedPassengers)
                {
                    passengerView.UnlockPassenger(_pointModel.waitingPeople[passengerIndex]);

                    passengersUnlocked = true;

                    //if any passegner unlocked - it means the entire stack unlocked
                    if (lockedIndication!=null)
                        Destroy(lockedIndication);
                }
            }
        }

        if (passengersUnlocked)
            SoundsManager.Instance.HiddenUnlocked();
    }

    internal void DoSelectedAnimation()
    {
        SoundsManager.Instance.SelectStation();

        transform.localScale = Vector3.one;

        transform.DOPunchScale(Vector3.one * 0.1f, 0.1f);
    }
}
