// TrainGameState.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TrainMode { Collecting, Returning, Finished }

public sealed class TrainRuntime
{
    public int trainPointId;
    public int colorIndex;
    public int depotPointId;
    public int carried;
    public TrainMode mode;
    public bool isMoving;
}

public sealed class StationRuntime
{
    public int stationPointId;
    // counts per colorIndex
    public Dictionary<int, int> byColor = new();
    public int Total => byColor.Values.Sum();
    public int TakeAllOfColor(int colorIdx)
    {
        if (!byColor.TryGetValue(colorIdx, out var n) || n == 0) return 0;
        byColor[colorIdx] = 0;
        return n;
    }
}

public sealed class TrainGameState
{
    public Dictionary<int, TrainRuntime> trains = new();
    public Dictionary<int, StationRuntime> stations = new();
    public HashSet<int> depots = new();

    public bool AllFinished => trains.Values.All(t => t.mode == TrainMode.Finished);

    public static TrainGameState BuildFrom(ScenarioModel scenario)
    {
        var gs = new TrainGameState();

        // Index depots by color for mapping
        var depotsByColor = scenario.points
            .Where(p => p.type == GamePointType.Depot)
            .GroupBy(p => p.colorIndex)
            .ToDictionary(g => g.Key, g => g.Select(p => p.id).ToList());

        // Stations
        foreach (var p in scenario.points.Where(p => p.type == GamePointType.Station))
        {
            var st = new StationRuntime { stationPointId = p.id };
            // waitingPeople: assume each int is a passenger colorIndex
            foreach (var c in p.waitingPeople)
            {
                if (!st.byColor.ContainsKey(c)) st.byColor[c] = 0;
                st.byColor[c]++;
            }
            gs.stations[p.id] = st;
        }

        // Depots
        foreach (var d in scenario.points.Where(p => p.type == GamePointType.Depot))
            gs.depots.Add(d.id);

        // Trains (map each train to a unique depot of the same color)
        foreach (var t in scenario.points.Where(p => p.type == GamePointType.Train))
        {
            if (!depotsByColor.TryGetValue(t.colorIndex, out var depotIds) || depotIds.Count != 1)
            {
                Debug.LogError($"Train {t.id} color {t.colorIndex} has {(depotIds == null ? 0 : depotIds.Count)} matching depots (need exactly 1).");
                continue;
            }
            gs.trains[t.id] = new TrainRuntime
            {
                trainPointId = t.id,
                colorIndex = t.colorIndex,
                depotPointId = depotIds[0],
                carried = 0,
                mode = TrainMode.Collecting,
                isMoving = false
            };
        }

        return gs;
    }
}
