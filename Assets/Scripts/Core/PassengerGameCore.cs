using System;
using System.Collections.Generic;

public enum PassengerGameOutcome { Running, Won, Lost }

public sealed class PassengerGameCore
{
    private readonly ScenarioModel _scenario;
    private readonly Dictionary<int, int> _trainColor = new Dictionary<int, int>(); // trainPointId -> colorIndex
    private readonly Dictionary<int, int> _trainCarried = new Dictionary<int, int>(); // trainPointId -> count
    private readonly Dictionary<int, int> _depotColor = new Dictionary<int, int>(); // depotPointId -> colorIndex;
    private PassengerGameOutcome _outcome = PassengerGameOutcome.Running;
    private int _loseTrainId = 0;
    private int _losePointId = 0;
    private string _loseReason = "";

    public PassengerGameCore(ScenarioModel scenario)
    {
        _scenario = scenario;
        // index depots
        for (int i = 0; i < _scenario.points.Count; i++)
        {
            var p = _scenario.points[i];
            if (p.type == GamePointType.Depot)
                _depotColor[p.id] = p.colorIndex;
        }
    }

    public PassengerGameOutcome Outcome { get { return _outcome; } }
    public string LoseReason { get { return _loseReason; } }
    public int LoseTrainId { get { return _loseTrainId; } }
    public int LosePointId { get { return _losePointId; } }

    public void RegisterTrain(int trainPointId, int colorIndex)
    {
        if (!_trainColor.ContainsKey(trainPointId))
            _trainColor.Add(trainPointId, colorIndex);
        _trainCarried[trainPointId] = 0;
    }

    public void OnCollision(int trainA, int trainB)
    {
        if (_outcome != PassengerGameOutcome.Running) return;
        _outcome = PassengerGameOutcome.Lost;
        _loseTrainId = trainA;
        _loseReason = "Collision";
    }

    public void OnArrivedAtPoint(int trainPointId, int pointId)
    {
        if (_outcome != PassengerGameOutcome.Running) return;

        int colorIndex;
        if (!_trainColor.TryGetValue(trainPointId, out colorIndex)) return;

        GamePoint point = FindPoint(pointId);
        if (point == null) return;

        if (point.type == GamePointType.Station)
        {
            // pickup: from head while head==train color
            int taken = 0;
            while (point.waitingPeople.Count > 0 && point.waitingPeople[0] == colorIndex)
            {
                point.waitingPeople.RemoveAt(0);
                taken++;
            }
            _trainCarried[trainPointId] = _trainCarried[trainPointId] + taken;
            return;
        }

        if (point.type == GamePointType.Depot)
        {
            int depotColor;
            bool isDepot = _depotColor.TryGetValue(pointId, out depotColor);
            if (!isDepot)
                return;

            // Rule: wrong-color depot => Lose
            if (depotColor != colorIndex)
            {
                _outcome = PassengerGameOutcome.Lost;
                _loseTrainId = trainPointId;
                _losePointId = pointId;
                _loseReason = "Arrived at wrong-color depot";
                return;
            }

            // Rule: arriving at own depot while stations still have this color => Lose
            if (AnyStationHasColor(colorIndex))
            {
                _outcome = PassengerGameOutcome.Lost;
                _loseTrainId = trainPointId;
                _losePointId = pointId;
                _loseReason = "Arrived at depot before collecting all passengers of this color";
                return;
            }

            // Deliver
            _trainCarried[trainPointId] = 0;

            // Win check: all stations empty AND all trains carry 0
            if (AllStationsEmpty() && AllTrainsEmpty())
                _outcome = PassengerGameOutcome.Won;

            return;
        }
    }

    public int GetCarried(int trainPointId)
    {
        int v;
        return _trainCarried.TryGetValue(trainPointId, out v) ? v : 0;
    }

    private GamePoint FindPoint(int id)
    {
        for (int i = 0; i < _scenario.points.Count; i++)
            if (_scenario.points[i].id == id) return _scenario.points[i];
        return null;
    }

    private bool AnyStationHasColor(int colorIndex)
    {
        for (int i = 0; i < _scenario.points.Count; i++)
        {
            var p = _scenario.points[i];
            if (p.type != GamePointType.Station) continue;
            for (int k = 0; k < p.waitingPeople.Count; k++)
                if (p.waitingPeople[k] == colorIndex) return true;
        }
        return false;
    }

    private bool AllStationsEmpty()
    {
        for (int i = 0; i < _scenario.points.Count; i++)
        {
            var p = _scenario.points[i];
            if (p.type != GamePointType.Station) continue;
            if (p.waitingPeople.Count > 0) return false;
        }
        return true;
    }

    private bool AllTrainsEmpty()
    {
        foreach (var kv in _trainCarried)
            if (kv.Value > 0) return false;
        return true;
    }
}
