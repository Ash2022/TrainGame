// RailSim.Core/SimpleTrainSim.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailSimCore
{
    public enum AdvanceResultKind { None, Blocked, EndOfPath }

    public struct AdvanceResult
    {
        public float Allowed;
        public AdvanceResultKind Kind;
        public int BlockerId;
        public Vector3 HitPos; // first contact (approx)
    }

    /// <summary>Data-only train simulator for one train. No scene or timing dependencies.</summary>
    public sealed class SimpleTrainSim
    {

        private float explicitTrainLengthMeters = -1f; // < 0 means "use offsets"
        private float PathEndTol => SimTuning.Eps(CellSize);          // default end tolerance

        // ----- Config / tunables -----
        public float CellSize { get; private set; } = 1f;
        public float SampleStep { get; private set; } = 0.25f;   // ≈ CellSize/8
        public float Eps { get; private set; } = 1f;// 1e-4f;   // ≈ 1e-4 * CellSize
        public float SafetyGap { get; private set; } = 0f;      // along tape
        public float TapeCapacityMeters { get => tapeCapacity; set { tapeCapacity = Mathf.Max(0f, value); tape.SetMaxLen(tapeCapacity); } }

        private bool noseLogged = false;

        public float PathLength => totalLen;

        // ----- Forward path (current leg) -----
        private readonly List<Vector3> pathPts = new List<Vector3>();
        private readonly List<float> cum = new List<float>(); // cumulative arc-lengths
        private float totalLen;
        private int segIndex;
        private Vector3 startFwd = Vector3.right;

        // Head arc-length
        public float SHead { get; private set; }
        private Vector3 lastHeadPos;

        // Back tape
        private readonly PathTape tape = new PathTape();
        private float tapeCapacity = 0f;

        // Cart geometry (for convenience)
        private readonly List<float> cartOffsets = new List<float>();
        public IReadOnlyList<float> CartOffsets => cartOffsets;

        // Debug (optional)
        public List<Vector3> LastMovingSlice { get; private set; }
        public List<Vector3> LastBlockedSlice { get; private set; }
        public int LastBlockerId { get; private set; } // you can set externally when iterating others

        public void Configure(float cellSize, float sampleStep = -1f, float eps = -1f, float safetyGap = 0f)
        {
            CellSize = Mathf.Max(1e-6f, cellSize);
            SampleStep = (sampleStep > 0f) ? sampleStep : SimTuning.SampleStep(CellSize);
            Eps = (eps > 0f) ? eps : SimTuning.Eps(CellSize);
            SafetyGap = Mathf.Max(0f, safetyGap);
            
        }

        // ----- Initialize a leg -----
        public void LoadLeg(IList<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2)
                throw new ArgumentException("Path must have at least 2 points.", nameof(worldPoints));

            BuildCum(worldPoints);
            startFwd = (pathPts[1] - pathPts[0]).normalized;
            SHead = 0f;
            segIndex = 0;
            lastHeadPos = pathPts[0];
        }

        public void SetCartOffsets(IList<float> offsets)
        {
            cartOffsets.Clear();
            if (offsets != null) cartOffsets.AddRange(offsets);
        }

        /// <summary>Seed a straight back prefix so this train is collidable before any movement.</summary>
        public void SeedTapePrefixStraight(Vector3 headPos, Vector3 forwardWorld, float length)
        {
            length = Mathf.Max(0f, length);
            int count = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(1e-5f, SampleStep)) + 1);

            Vector3 backDir = (-forwardWorld).normalized;
            Vector3 start = headPos + backDir * length;

            tape.AppendPoint(start);
            Vector3 prev = start;
            for (int i = 1; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 p = Vector3.Lerp(start, headPos, t);
                tape.AppendSegment(prev, p);
                prev = p;
            }
            if ((prev - headPos).sqrMagnitude > 1e-12f)
                tape.AppendSegment(prev, headPos);

            //tape.TrimToCapacity();
        }

        // ----- Queries / sampling -----
        public void SampleHead(float s, out Vector3 pos, out Vector3 tan)
        {
            SampleForward(s, out pos, out var rot);
            tan = rot * Vector3.up; // LookRotation(forward=Z, up=dirY) → tan is Y axis
        }

        public bool TryGetBackPose(float backMeters, out Vector3 pos, out Vector3 tan)
        {
            return tape.SampleBack(backMeters, out pos, out tan, out _);
        }


        public void EnsureTapeCapacity(float requiredMeters)
        {
            if (tape != null) tape.EnsurePrefix(requiredMeters);
        }

        public void GetCartPoses(List<Vector3> positions, List<Vector3> tangents)
        {
            if (positions == null || tangents == null) throw new ArgumentNullException();
            positions.Clear(); tangents.Clear();

            for (int i = 0; i < cartOffsets.Count; i++)
            {
                if (tape.SampleBack(cartOffsets[i], out var p, out var t, out _))
                {
                    positions.Add(p);
                    tangents.Add(t);
                }
            }
        }

        // ----- Advance with collision capping -----
        /// <summary>
        /// Compute how far (≤ want) we can move this tick before intersecting any other's occupied back slice.
        /// 'others' are stationary (single-mover model). Returns allowed distance.
        /// </summary>
        public AdvanceResult ComputeAllowedAdvance(float want, IList<SimpleTrainSim> others, Func<SimpleTrainSim, int> getId = null)
        {
            var result = new AdvanceResult { Allowed = 0f, Kind = AdvanceResultKind.None, BlockerId = 0, HitPos = Vector3.zero };

            want = Mathf.Max(0f, want);
            float remaining = Mathf.Min(want, totalLen - SHead);
            float advanced = 0f;

            LastMovingSlice = null;
            LastBlockedSlice = null;
            LastBlockerId = 0;

            bool blocked = false;
            bool hitRecorded = false;

            while (remaining > 1e-6f)
            {
                float distToEnd = totalLen - (SHead + advanced);
                float iter = Mathf.Min(remaining, SampleStep * 0.5f, distToEnd);
                if (iter <= 1e-6f) break;

                //var movingSlice = BuildForwardSlice(SHead + advanced, SHead + advanced + iter, SampleStep);

                // shift the slice forward by the train’s half‐length (“nose”)
                float nose = SimTuning.HeadHalfLen(CellSize);


                //Debug.Log($"[GAME/SETUP] cell={CellSize:F3} sample={SampleStep:F3} eps={Eps:F4} safety={SafetyGap:F3} nose={nose:F3} latTol=0.050 want={want:F3} S={SHead:F3} others={(others != null ? others.Count : 0)}");

                if (!noseLogged)
                    {
                    Debug.Log($"[SimpleTrainSim] headHalfLen (nose) = {nose:F3}m");
                    noseLogged = true;
                    }
                var movingSlice = BuildForwardSlice(SHead + advanced + nose,SHead + advanced + nose + iter,SampleStep);


                LastMovingSlice = movingSlice;

                float cap = iter;

                if (others != null)
                {
                    for (int i = 0; i < others.Count; i++)
                    {
                        var other = others[i];
                        if (ReferenceEquals(other, this))
                            continue;

                        if (!other.TryGetOccupiedBackSlice(SafetyGap, SampleStep*.5f, out var occupiedSlice))
                            continue;

                        //Debug.Log($"[OCC/USED] safety={SafetyGap:F3} sub={SampleStep * 0.5f:F3} occPts={occupiedSlice.Count} p0={occupiedSlice[0]} pN={occupiedSlice[occupiedSlice.Count - 1]}");

                        if (IntersectPolylines(movingSlice, occupiedSlice, Eps,0.05f, out float alongMoving, out Vector3 hitPos))
                        {
                            //Debug.Log($"[MOVE/USED] movePts={movingSlice.Count} p0={movingSlice[0]} pN={movingSlice[movingSlice.Count - 1]} along={alongMoving:F3}");

                            //Debug.Log($"[GAME/HIT/INPUTS] cell={CellSize:F3} sample={SampleStep:F3} eps={Eps:F4} nose={SimTuning.HeadHalfLen(CellSize):F3} safety={SafetyGap:F3} lat=0.050 S={SHead:F3} move[{movingSlice.Count}] {movingSlice[0]}->{movingSlice[movingSlice.Count - 1]} occ[{occupiedSlice.Count}] {occupiedSlice[0]}->{occupiedSlice[occupiedSlice.Count - 1]} blocker={(getId != null ? getId(other) : 0)}");

                            //Debug.Log($"[GAME/HIT] along={alongMoving:F3} hit={hitPos} movePts={movingSlice.Count} occPts={occupiedSlice.Count} blocker={(getId != null ? getId(other) : 0)}");

                            cap = Mathf.Min(cap, Mathf.Max(0f, alongMoving));
                            blocked = true;

                            if (!hitRecorded)
                            {
                                
                                LastBlockedSlice = occupiedSlice;
                                LastBlockerId = getId != null ? getId(other) : 0;

                                result.BlockerId = LastBlockerId;
                                result.HitPos = hitPos;
                                hitRecorded = true;
                            }
                            break; // first blocker this substep is enough
                        }
                    }
                }

                advanced += cap;

                // If we were blocked inside this substep, stop substepping this tick
                if (cap + 1e-6f < iter) break;

                remaining -= iter;
            }

            result.Allowed = advanced;

            // Decide reason
            if ((SHead + advanced) >= (totalLen - PathEndTol))
                result.Kind = AdvanceResultKind.EndOfPath;
            else if (blocked)
                result.Kind = AdvanceResultKind.Blocked;
            else
                result.Kind = AdvanceResultKind.None;

            return result;
        }


        /// <summary>
        /// Apply 'allowed' movement: updates SHead and appends to tape. Returns new head pos/tangent.
        /// </summary>
        public void CommitAdvance(float allowed, out Vector3 headPos, out Vector3 headTan)
        {
            allowed = Mathf.Max(0f, allowed);
            float sNew = Mathf.Min(SHead + allowed, totalLen);

            SampleForward(sNew, out var pos, out var rot);
            tape.AppendSegment(lastHeadPos, pos);
            //tape.TrimToCapacity();

            SHead = sNew;
            lastHeadPos = pos;
            headPos = pos;
            headTan = rot * Vector3.up;
        }

        /// <summary>Occupied slice = last (trainLength + safetyGap) meters of the back tape.</summary>
        public bool TryGetOccupiedBackSlice(float safetyGap, float sampleStep, out List<Vector3> points)
        {
            points = null;

            // Geometry
            float halfCart = SimTuning.CartHalfLen(CellSize);
            float headHalf = SimTuning.HeadHalfLen(CellSize);

            // Tail extent measured *behind* the head center
            float tailBehind = (cartOffsets.Count > 0)
                ? (cartOffsets[cartOffsets.Count - 1] + halfCart)
                : headHalf;

            // If explicit length set, use it; otherwise use tailBehind
            float baseLen = (explicitTrainLengthMeters >= 0f) ? explicitTrainLengthMeters : tailBehind;

            // Total occupied distance *behind* the head (include safety)
            float backLen = Mathf.Max(0f, baseLen + Mathf.Max(0f, safetyGap));
            if (backLen <= 1e-6f)
            {
                //Debug.Log($"[OCC/PARAMS] backLen<=0 tail={tailBehind:F3} base={baseLen:F3} safety={safetyGap:F3}");
                return false;
            }

            sampleStep = Mathf.Max(1e-5f, sampleStep);
            int count = Mathf.Max(2, Mathf.CeilToInt(backLen / sampleStep) + 1);
            float step = backLen / (count - 1);

            //Debug.Log($"[OCC/PARAMS] halfCart={halfCart:F3} headHalf={headHalf:F3} tail={tailBehind:F3} base={baseLen:F3} safety={safetyGap:F3} backLen={backLen:F3} sub={sampleStep:F3} n={count}");

            // Get head pose from TAPE (robust even before a leg is loaded)
            if (!tape.SampleBack(0f, out var headPos, out var headTan, out _))
            {
                //Debug.Log("[OCC/FAIL] no tape at d=0");
                return false; // no tape yet (should be seeded at spawn)
            }

            var pts = new List<Vector3>(count + 2);

            // Forward "nose" so head-on contact triggers before centers meet
            float noseLen = headHalf;
            var noseTip = headPos + headTan * noseLen;
            pts.Add(noseTip); // nose tip (ahead of head)
            pts.Add(headPos); // head center

            // Then sample along the back tape
            for (int i = 1; i < count; i++)
            {
                float d = i * step; // distance behind head
                if (!tape.SampleBack(d, out var p, out _, out _))
                {
                    //Debug.Log($"[OCC/FAIL] tape too short at d={d:F3} / backLen={backLen:F3} builtPts={pts.Count}");
                    return false; // tape/prefix not long enough yet
                }
                pts.Add(p);
            }

            points = pts;
            //Debug.Log($"[OCC/BUILT] pts={pts.Count} p0={pts[0]} pN={pts[pts.Count - 1]}");
            return true;
        }



        // ===== Internals =====

        private void BuildCum(IList<Vector3> worldPoints)
        {
            pathPts.Clear(); cum.Clear();
            for (int i = 0; i < worldPoints.Count; i++) pathPts.Add(worldPoints[i]);

            float acc = 0f;
            cum.Add(0f);
            for (int i = 1; i < pathPts.Count; i++)
            {
                float d = Vector3.Distance(pathPts[i - 1], pathPts[i]);
                if (d <= 1e-6f)
                {
                    pathPts[i] = pathPts[i] + new Vector3(1e-4f, 0f, 0f);
                    d = 1e-4f;
                }
                acc += d;
                cum.Add(acc);
            }
            totalLen = acc;
            segIndex = 0;
        }

        private void SampleForward(float s, out Vector3 pos, out Quaternion rot)
        {
            if (s <= 0f)
            {
                pos = pathPts[0];
                rot = Quaternion.LookRotation(Vector3.forward, startFwd);
                return;
            }
            if (s >= totalLen)
            {
                int n = pathPts.Count;
                Vector3 dirEnd = (pathPts[n - 1] - pathPts[n - 2]).normalized;
                pos = pathPts[n - 1];
                rot = Quaternion.LookRotation(Vector3.forward, dirEnd);
                return;
            }

            while (segIndex < cum.Count - 2 && s > cum[segIndex + 1]) segIndex++;
            while (segIndex > 0 && s < cum[segIndex]) segIndex--;

            float segStart = cum[segIndex];
            float segEnd = cum[segIndex + 1];
            float t = (s - segStart) / (segEnd - segStart);

            Vector3 a = pathPts[segIndex];
            Vector3 b = pathPts[segIndex + 1];

            pos = Vector3.LerpUnclamped(a, b, t);
            Vector3 dir = (b - a).normalized;
            rot = Quaternion.LookRotation(Vector3.forward, dir);
        }

        private List<Vector3> BuildForwardSlice(float s0, float s1, float step)
        {
            if (s1 < s0) (s0, s1) = (s1, s0);
            float len = Mathf.Max(0f, s1 - s0);
            int count = Mathf.Max(2, Mathf.CeilToInt(len / Mathf.Max(1e-5f, step)) + 1);
            var pts = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float u = (count == 1) ? 0f : i / (float)(count - 1);
                float s = Mathf.Lerp(s0, s1, u);
                SampleForward(s, out Vector3 pos, out _);
                pts.Add(pos);
            }
            return pts;
        }

        // Polyline–polyline intersection with colinear overlap detection.
        private static bool IntersectPolylines(List<Vector3> A, List<Vector3> B, float eps, float lateralTol, out float alongA, out Vector3 hitPos)
        {
            alongA = 0f; hitPos = Vector3.zero;
            float accA = 0f;
            for (int i = 0; i < A.Count - 1; i++)
            {
                Vector2 a0 = A[i]; Vector2 a1 = A[i + 1];
                float aLen = Vector2.Distance(a0, a1);
                if (aLen <= eps) { accA += aLen; continue; }

                for (int j = 0; j < B.Count - 1; j++)
                {
                    Vector2 b0 = B[j]; Vector2 b1 = B[j + 1];
                    float bLen = Vector2.Distance(b0, b1);
                    if (bLen <= eps) continue;

                    if (TryIntersectSegments2D(a0, a1, b0, b1, eps, lateralTol, out float ta, out float tb, out bool colinear, out Vector2 inter))
                    {
                        ta = Mathf.Clamp01(ta);
                        alongA = accA + ta * aLen;
                        hitPos = inter;
                        return true;
                    }
                }
                accA += aLen;
            }
            return false;
        }

        private static bool TryIntersectSegments2D(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, float eps, float lateralTol,
                                                   out float tP, out float tQ, out bool colinear, out Vector2 inter)
        {
            tP = tQ = 0f; inter = Vector2.zero; colinear = false;

            Vector2 r = p2 - p;
            Vector2 s = q2 - q;
            float aLen = r.magnitude;
            float bLen = s.magnitude;
            if (aLen <= eps || bLen <= eps) return false;

            float rxs = r.x * s.y - r.y * s.x;
            float q_pxr = (q.x - p.x) * r.y - (q.y - p.y) * r.x;

            // (1) Colinear overlap
            if (Mathf.Abs(rxs) < eps && Mathf.Abs(q_pxr) < eps)
            {
                colinear = true;
                float rr = Vector2.Dot(r, r);
                if (rr < eps) return false;
                float t0 = Vector2.Dot(q - p, r) / rr;
                float t1 = Vector2.Dot(q2 - p, r) / rr;
                float tmin = Mathf.Max(0f, Mathf.Min(t0, t1));
                float tmax = Mathf.Min(1f, Mathf.Max(t0, t1));
                if (tmax - tmin < eps) return false;
                tP = (tmin + tmax) * 0.5f;
                tQ = 0f;
                inter = p + r * tP;
                return true;
            }

            // (2) Proper intersection
            if (Mathf.Abs(rxs) >= eps)
            {
                float t = ((q.x - p.x) * s.y - (q.y - p.y) * s.x) / rxs;
                float u = ((q.x - p.x) * r.y - (q.y - p.y) * r.x) / rxs;
                if (t >= -eps && t <= 1f + eps && u >= -eps && u <= 1f + eps)
                {
                    t = Mathf.Clamp01(t);
                    u = Mathf.Clamp01(u);
                    inter = p + t * r;
                    tP = t; tQ = u;
                    return true;
                }
            }

            // (3) General capsule–capsule test via closest points between two segments
            //     (works for any angle; catches near-parallel slides and T-bones)
            // Real-Time Collision Detection (Ericson) style
            Vector2 uvec = r;
            Vector2 vvec = s;
            Vector2 w0 = p - q;

            float A = Vector2.Dot(uvec, uvec);        // |u|^2
            float B = Vector2.Dot(uvec, vvec);        // u·v
            float C = Vector2.Dot(vvec, vvec);        // |v|^2
            float D = Vector2.Dot(uvec, w0);          // u·w0
            float E = Vector2.Dot(vvec, w0);          // v·w0

            float denom = A * C - B * B;
            float sN, sD = denom;   // s parameter (on [0,1]) numerator/denominator
            float tN, tD = denom;   // t parameter (on [0,1]) numerator/denominator

            const float SMALL = 1e-8f;

            // parallel?
            if (denom < SMALL)
            {
                sN = 0f; sD = 1f;   // force s = 0
                tN = E; tD = C;    // t = clamp(E/C)
            }
            else
            {
                sN = (B * E - C * D);
                tN = (A * E - B * D);

                if (sN < 0f) { sN = 0f; tN = E; tD = C; }
                else if (sN > sD) { sN = sD; tN = E + B; tD = C; }
            }

            if (tN < 0f)
            {
                tN = 0f;
                if (-D < 0f) sN = 0f;
                else if (-D > A) sN = sD;
                else { sN = -D; sD = A; }
            }
            else if (tN > tD)
            {
                tN = tD;
                if (-D + B < 0f) sN = 0f;
                else if (-D + B > A) sN = sD;
                else { sN = (-D + B); sD = A; }
            }

            float sc = (Mathf.Abs(sN) < SMALL ? 0f : sN / sD);
            float tc = (Mathf.Abs(tN) < SMALL ? 0f : tN / tD);

            Vector2 Pc = p + sc * uvec;   // closest point on A-segment
            Vector2 Qc = q + tc * vvec;   // closest point on B-segment
            float dist = Vector2.Distance(Pc, Qc);

            // Debug: uncomment to see distances even when not counted as collisions
            // Debug.Log($"[CapsuleCheck] d={dist:F4} tol={lateralTol:F4} tA={sc:F3} tB={tc:F3}");

            if (dist <= lateralTol + eps)
            {
                tP = sc; tQ = tc; colinear = false;
                inter = (Pc + Qc) * 0.5f;
                return true;
            }

            return false;
        }



        public bool AtEnd(float tol = 1e-5f)
        {
            return SHead >= (PathLength - Mathf.Max(1e-6f, tol));
        }

        public void SetTrainLength(float meters)
        {
            explicitTrainLengthMeters = Mathf.Max(0f, meters);
        }

        public void EnsureBackPrefix(float minPrefixMeters)
        {
            if (tape != null) tape.EnsurePrefix(minPrefixMeters);
        }

        // ----- Back tape -----
        private sealed class PathTape
        {
            private readonly List<Vector3> pts = new List<Vector3>(); // earliest..latest (head)
            private readonly List<float> cum = new List<float>();    // cumulative
            private float maxLen = 0f;                                // trim capacity
            private Vector3 prefixDir = Vector3.right;
            private float prefixLen = 0f;

            public bool IsEmpty => pts.Count == 0;

            public void SetMaxLen(float lenMeters) => maxLen = Mathf.Max(0f, lenMeters);

            public void AppendPoint(Vector3 p)
            {
                if (IsEmpty) { pts.Add(p); cum.Add(0f); return; }
                Vector3 last = pts[pts.Count - 1];
                float d = Vector3.Distance(last, p);
                if (d <= 1e-6f) return;
                pts.Add(p); cum.Add(cum[cum.Count - 1] + d);
            }

            public void AppendSegment(Vector3 a, Vector3 b)
            {
                if (IsEmpty) AppendPoint(a);
                AppendPoint(b);
            }

            public void TrimToCapacity()
            {
                return;

                if (pts.Count < 2 || maxLen <= 0f) return;

                float totalSpan = cum[cum.Count - 1] - cum[0];
                while (pts.Count > 2 && totalSpan > maxLen)
                {
                    pts.RemoveAt(0);
                    float baseCum = cum[0];
                    cum.RemoveAt(0);
                    for (int i = 0; i < cum.Count; i++) cum[i] -= baseCum;
                    totalSpan = cum[cum.Count - 1] - cum[0];
                }
            }

            public bool SampleBack(float sBack, out Vector3 pos, out Vector3 tan, out float available)
            {
                sBack = Mathf.Max(0f, sBack);
                float tapeLen = (pts.Count > 1) ? (cum[cum.Count - 1] - cum[0]) : 0f;
                available = tapeLen + prefixLen;

                if (pts.Count == 0)
                {
                    pos = Vector3.zero; tan = Vector3.right; return false;
                }

                if (sBack <= tapeLen)
                {
                    float target = cum[cum.Count - 1] - sBack;
                    int lo = 0, hi = cum.Count - 1;
                    while (lo + 1 < hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (cum[mid] <= target) lo = mid; else hi = mid;
                    }
                    float segStart = cum[lo];
                    float segEnd = cum[hi];
                    float t = (segEnd > segStart) ? (target - segStart) / (segEnd - segStart) : 0f;
                    Vector3 a = pts[lo];
                    Vector3 b = pts[hi];
                    pos = Vector3.LerpUnclamped(a, b, t);
                    tan = (b - a).normalized;
                    if (tan.sqrMagnitude < 1e-8f)
                    {
                        tan = (lo > 0 ? (pts[lo] - pts[lo - 1]) : (pts[Mathf.Min(hi + 1, pts.Count - 1)] - pts[lo])).normalized;
                        if (tan.sqrMagnitude < 1e-8f) tan = Vector3.right;
                    }
                    return true;
                }

                // Beyond tape → allow straight prefix if set
                float needPrefix = sBack - tapeLen;
                if (needPrefix <= prefixLen)
                {
                    pos = pts[0] - prefixDir * needPrefix;
                    tan = prefixDir;
                    return true;
                }

                pos = pts[pts.Count - 1];
                tan = (pts.Count > 1 ? (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized : prefixDir);
                return false;
            }



            // at the top of your file, after fields:
            /// <summary>
            /// Guarantee the tape can answer back-samples up to this length
            /// by extending the straight-back prefix if needed.
            /// </summary>
            public void EnsurePrefix(float minPrefixMeters)
            {
                if (minPrefixMeters > prefixLen)
                {
                    prefixLen = minPrefixMeters;
                    // prefixDir should already be set in SeedStraight; no need to change it.
                }
            }

            public void SeedStraight(Vector3 headPos, Vector3 forward, float length, float step)
            {
                // your existing implementation plus:
                prefixDir = forward.normalized;
                prefixLen = length;
                // …populate pts & cum as you like…
            }


        }
    }
}
