

using DG.Tweening;
using System;
using UnityEngine;


public class DepotView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform exits;
    [SerializeField] Renderer depotBaseRenderer;
    [SerializeField] Renderer depotRoofRenderer;
    [SerializeField] Transform depotHolder;

    [SerializeField] Transform gateHinge;
    [SerializeField] GameObject key;
    [SerializeField] Transform keyHolder;
    [SerializeField] Renderer gateRenderer;

    public bool depotGateLocked = true;

    Sequence keyRotateSeq = null;

    public GamePoint PointModel { get => _pointModel; private set => _pointModel = value; }

    /// <summary>
    /// Call this right after Instantiate to wire up the model.
    /// </summary>
    public void Initialize(GamePoint point, PlacedPartInstance part, float cellSize)
    {
        _pointModel = point;

        depotHolder.localScale = new Vector3(cellSize,cellSize,cellSize);

        exits.transform.localEulerAngles = new Vector3(0, 0, -part.rotation);

        depotBaseRenderer.material = LevelVisualizer.Instance.GetDepotBaseMaterialByIndex(point.colorIndex);
        depotRoofRenderer.material = LevelVisualizer.Instance.GetDepotTopMaterialByIndex(point.colorIndex);

        gateRenderer.material = LevelVisualizer.Instance.GetGateMaterialByIndex(point.colorIndex);

        if(point.DepotLockingColorIndex != -1)
        {
            //means this depot is locking some other depot 
            keyHolder.gameObject.SetActive(true);
            key.GetComponent<Renderer>().material = LevelVisualizer.Instance.GetKeyMaterialByIndex(point.DepotLockingColorIndex);

            keyRotateSeq = DOTween.Sequence();

            //if key exists - make him spin so he is more noticable
            keyRotateSeq.Append(keyHolder.DOLocalRotate(new Vector3(0, 0, 360), 2.5f,RotateMode.LocalAxisAdd).SetLoops(1000,LoopType.Restart));

            keyRotateSeq.Play();

        }
        else
            keyHolder.gameObject.SetActive(false);


        if(point.direction == TrainDir.Right)
        {
            //rotate
            exits.transform.localEulerAngles = new Vector3(0, 0, -part.rotation-180);
        }
        if(point.direction == TrainDir.Up)
        {
            exits.transform.localEulerAngles = new Vector3(0, 0, -part.rotation - 180);
        }

    }

    public void ShowMyDepotUnlocking()
    {
        if(depotGateLocked)
        {
            SoundsManager.Instance.DepotGateOpens();
            depotGateLocked = false;
            gateHinge.DOLocalRotate(new Vector3(-180, 0, 0),1f);
        }

    }

    private void OnDestroy()
    {
        if (keyRotateSeq != null)
            keyRotateSeq.Kill();
    }

    public void CollectKey()
    {
        if (_pointModel.MyDepotIsLockingDepotPointID != -1)
        {
            GameManager.Instance.UpdateDepotItsKeyWasCollected(_pointModel.MyDepotIsLockingDepotPointID);
            _pointModel.MyDepotIsLockingDepotPointID = -1;

            if(keyRotateSeq!=null)
                keyRotateSeq.Kill();

            SoundsManager.Instance.KeyCollected();

            keyHolder.gameObject.SetActive(false);
        }
        
    }

    internal void DoSelectedAnimation()
    {
        SoundsManager.Instance.SelectDepot();

        transform.localScale = Vector3.one;

        transform.DOPunchScale(Vector3.one * 0.1f, 0.1f);
    }
}