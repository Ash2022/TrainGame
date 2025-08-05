// TrainGameRules.cs
using System.Collections.Generic;
using UnityEngine;

public sealed class TrainGameRules
{
    private readonly TrainGameState _gs;
    private readonly ScenarioModel _scenario;
    private readonly IRouteAdapter _route;

    // Provided by your game layer to actually move visuals/sim
    public System.Func<int, List<Vector3>, bool> StartMoveForTrain; // (trainId, polyline) -> started?

    public TrainGameRules(TrainGameState gs, ScenarioModel scenario, IRouteAdapter route)
    { _gs = gs; _scenario = scenario; _route = route; }

    public bool Command_GoToPoint(int trainId, int targetPointId, out string error)
    {
        error = null;
        if (!_gs.trains.TryGetValue(trainId, out var t)) { error = "Unknown train"; return false; }
        if (t.isMoving) { error = "Train already moving"; return false; }

        // Mode constraint: when Returning, must go to its depot
        if (t.mode == TrainMode.Returning && targetPointId != t.depotPointId)
        { error = "Must go to your depot"; return false; }

        if (!_route.TryFindPathPolyline(_scenario, trainId, targetPointId, out var poly, out error))
            return false;

        // Hand off to your mover; enforce sequential movement outside.
        if (StartMoveForTrain == null || !StartMoveForTrain(trainId, poly))
        { error = "Move start failed"; return false; }

        t.isMoving = true;
        return true;
    }

    public bool Command_FinishPicking(int trainId)
    {
        if (!_gs.trains.TryGetValue(trainId, out var t)) return false;
        if (t.mode != TrainMode.Collecting) return false;
        t.mode = TrainMode.Returning;
        return true;
    }

    // Call on arrival at the clicked/queued destination point
    public void OnArrivedAtPoint(int trainId, int pointId)
    {
        if (!_gs.trains.TryGetValue(trainId, out var t)) return;

        // Station pickup (only your color, unlimited capacity v1)
        if (_gs.stations.TryGetValue(pointId, out var st) && t.mode == TrainMode.Collecting)
        {
            var got = st.TakeAllOfColor(t.colorIndex);
            t.carried += got;
        }

        // Depot complete
        if (pointId == t.depotPointId && t.mode == TrainMode.Returning)
            t.mode = TrainMode.Finished;

        t.isMoving = false;
    }

    // Call when Sim reports a block
    public void OnBlocked(int trainId, int blockerId, Vector3 atPos)
    {
        if (_gs.trains.TryGetValue(trainId, out var t)) t.isMoving = false;
        // Surface to UI/log as you like.
    }
}
