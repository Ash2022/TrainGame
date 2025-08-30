

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DepotView : MonoBehaviour
{
    private GamePoint _pointModel;

    [SerializeField] Transform exits;
    [SerializeField] Renderer depotRenderer;
    [SerializeField] Transform depotHolder;

    public GamePoint PointModel { get => _pointModel; set => _pointModel = value; }

    /// <summary>
    /// Call this right after Instantiate to wire up the model.
    /// </summary>
    public void Initialize(GamePoint point, PlacedPartInstance part, float cellSize)
    {
        _pointModel = point;

        depotHolder.localScale = new Vector3(cellSize,cellSize,cellSize);

        exits.transform.localEulerAngles = new Vector3(0, 0, -part.rotation);

        //depotRenderer.material.color = LevelVisualizer.Instance.GetColorByIndex(point.colorIndex);

        depotRenderer.material = LevelVisualizer.Instance.GetDepotMaterialByIndex(point.colorIndex);
    }

}