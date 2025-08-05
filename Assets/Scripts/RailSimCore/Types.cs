using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RailSimCore
{


    public class Types
    {
        public enum MoveOutcome { Arrived, Blocked }

        public struct MoveCompletion
        {
            public MoveOutcome Outcome;
            public int BlockerId;
            public Vector3 HitPos;
            public TrainController SourceController; // NEW
        }
    }

}