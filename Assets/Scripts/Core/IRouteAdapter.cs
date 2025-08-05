// IRouteAdapter.cs
using System.Collections.Generic;

public interface IRouteAdapter
{
    // Build a world-space polyline from source → target using your RouteModel.
    bool TryFindPathPolyline(ScenarioModel scenario, int fromPointId, int toPointId, out List<UnityEngine.Vector3> polyline, out string error);
}
