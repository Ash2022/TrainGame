

using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DepotView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform exits;
    [SerializeField] Renderer depotRenderer;
    [SerializeField] Transform depotHolder;

    [SerializeField] Transform gateHinge;
    [SerializeField] GameObject key;
    [SerializeField] Renderer gateRenderer;

    public bool depotGateLocked = true;

    public GamePoint PointModel { get => _pointModel; private set => _pointModel = value; }

    /// <summary>
    /// Call this right after Instantiate to wire up the model.
    /// </summary>
    public void Initialize(GamePoint point, PlacedPartInstance part, float cellSize)
    {
        _pointModel = point;

        depotHolder.localScale = new Vector3(cellSize,cellSize,cellSize);

        exits.transform.localEulerAngles = new Vector3(0, 0, -part.rotation);

        depotRenderer.material = LevelVisualizer.Instance.GetDepotMaterialByIndex(point.colorIndex);

        gateRenderer.material = LevelVisualizer.Instance.GetGateMaterialByIndex(point.colorIndex);

        if(point.DepotLockingColorIndex != -1)
        {
            //means this depot is locking some other depot 
            key.SetActive(true);
            key.GetComponent<Renderer>().material = LevelVisualizer.Instance.GetKeyMaterialByIndex(point.DepotLockingColorIndex);

            //if key exists - make him spin so he is more noticable
            key.transform.DOLocalRotate(new Vector3(360, 90, -90), 1,RotateMode.Fast).SetLoops(-1,LoopType.Restart);

        }
        else
            key.SetActive(false);


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
            depotGateLocked = false;
            gateHinge.DOLocalRotate(new Vector3(-180, 0, 0),1f);
        }

    }

    public void CollectKey()
    {
        if (_pointModel.MyDepotIsLockingDepotPointID != -1)
        {
            GameManager.Instance.UpdateDepotItsKeyWasCollected(_pointModel.MyDepotIsLockingDepotPointID);
            _pointModel.MyDepotIsLockingDepotPointID = -1;
            key.SetActive(false);
        }
        
    }
}