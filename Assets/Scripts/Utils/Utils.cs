
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Utils 
{

    /// <summary>
    /// Same logic you had in LevelVisualizer.GetExitT.
    /// Up/Right → t=0; Down/Left → t=1 (based on ExitDetails.direction).
    /// </summary>
    public static float GetExitT(PlacedPartInstance part, int exitIndex)
    {
        var dir = part.exits.First(e => e.exitIndex == exitIndex).direction;
        return (dir == 2 || dir == 3) ? 1f : 0f;
    }

    /// <summary>
    /// Convert a PathModel to a world-space polyline using level.parts[*].worldSplines.
    /// This mirrors the Visualizer’s DrawGlobalSplinePath (minus LineRenderer bits).
    /// </summary>
    public static List<Vector3> BuildPathWorldPolyline(LevelData level, PathModel pathModel)
    {
        var worldPts = new List<Vector3>(128);
        if (level == null || pathModel == null || pathModel.Traversals == null) return worldPts;

        for (int pi = 0; pi < pathModel.Traversals.Count; pi++)
        {
            var trav = pathModel.Traversals[pi];
            var inst = level.parts.First(p => p.partId == trav.partId);

            int splineIndex = -1;
            int groupIndex = -1;
            int pathIndex = -1;

            if (inst.allowedPathsGroup?.Count > 0 && inst.worldSplines != null)
            {
                bool found = false;
                for (int gi = 0; gi < inst.allowedPathsGroup.Count; gi++)
                {
                    var grp = inst.allowedPathsGroup[gi];
                    int idx = grp.allowedPaths.FindIndex(ap =>
                        (ap.entryConnectionId == trav.entryExit && ap.exitConnectionId == trav.exitExit) ||
                        (ap.entryConnectionId == trav.exitExit && ap.exitConnectionId == trav.entryExit)
                    );

                    if (idx >= 0)
                    {
                        if (gi < inst.worldSplines.Count)
                        {
                            groupIndex = gi;
                            pathIndex = idx;
                            splineIndex = gi; // 1:1 between groupIndex and splineIndex
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    if (inst.worldSplines.Count == 1)
                        splineIndex = 0;
                    else
                        continue; // skip this traversal if we can't match
                }
            }
            else
            {
                splineIndex = 0; // simple part
            }

            var full = inst.worldSplines?[splineIndex] ?? new List<Vector3>();

            bool simple = inst.exits.Count <= 2;
            bool first = pi == 0;
            bool last = pi == pathModel.Traversals.Count - 1;
            float t0, t1;

            if (simple)
            {
                if (!first && !last)
                {
                    t0 = 0f; t1 = 1f;
                }
                else
                {
                    t0 = trav.entryExit < 0 ? 0.5f : GetExitT(inst, trav.entryExit);
                    t1 = trav.exitExit < 0 ? 0.5f : GetExitT(inst, trav.exitExit);

                    if (Mathf.Approximately(t0, t1))
                    {
                        const float eps = 0.001f;
                        if (t1 + eps <= 1f) t1 += eps;
                        else t0 = Mathf.Max(0f, t0 - eps);
                    }
                }
            }
            else
            {
                if (last)
                {
                    float tx = trav.exitExit < 0 ? 0.5f : GetExitT(inst, trav.exitExit);
                    t0 = Mathf.Min(0.5f, tx);
                    t1 = Mathf.Max(0.5f, tx);

                    if (Mathf.Approximately(t0, t1))
                    {
                        const float eps = 0.001f;
                        if (t1 + eps <= 1f) t1 += eps;
                        else t0 = Mathf.Max(0f, t0 - eps);
                    }
                }
                else
                {
                    t0 = 0f; t1 = 1f;
                }
            }

            var seg = ExtractSegmentWorld(full, t0, t1);

            // reverse if entry is on the far side
            if (groupIndex >= 0 && pathIndex >= 0)
            {
                var ap = inst.allowedPathsGroup[groupIndex].allowedPaths[pathIndex];
                if (trav.entryExit != ap.entryConnectionId)
                    seg.Reverse();
            }

            // append with de-dup of immediate duplicates
            for (int i = 0; i < seg.Count; i++)
            {
                var w = seg[i];
                if (worldPts.Count == 0 || worldPts[worldPts.Count - 1] != w)
                    worldPts.Add(w);
            }
        }

        return worldPts;
    }

    /// <summary>
    /// Segment extractor used by both game & sim.
    /// </summary>
    public static List<Vector3> ExtractSegmentWorld(List<Vector3> pts, float tStart, float tEnd)
    {
        int n = pts.Count;
        if (n < 2) return new List<Vector3>(pts);

        var cum = new float[n];
        float total = 0f;
        for (int i = 1; i < n; i++)
        {
            total += Vector3.Distance(pts[i - 1], pts[i]);
            cum[i] = total;
        }
        if (total <= 0f) return new List<Vector3> { pts[0], pts[n - 1] };

        float sLen = tStart * total;
        float eLen = tEnd * total;

        Vector3 PointAt(float d)
        {
            for (int i = 1; i < n; i++)
            {
                if (d <= cum[i])
                {
                    float u = Mathf.InverseLerp(cum[i - 1], cum[i], d);
                    return Vector3.Lerp(pts[i - 1], pts[i], u);
                }
            }
            return pts[n - 1];
        }

        var outPts = new List<Vector3>();
        outPts.Add(PointAt(sLen));
        for (int i = 1; i < n - 1; i++)
            if (cum[i] > sLen && cum[i] < eLen)
                outPts.Add(pts[i]);
        outPts.Add(PointAt(eLen));
        return outPts;
    }

    public static readonly Color[] colors = new Color[]
    {
        new Color(0.4f, 0.6f, 0.8f),  // Dark Blue
        new Color(0.2f, 0.7f, 0.4f),  // Dark Green
        new Color(0.8f, 0.3f, 0.3f) // Dark Red
    };




    public static List<Vector3> BuildPathWorldPolylineFromTemplates(
        LevelData level,
        PathModel pathModel,
        Vector2 worldOrigin,
        int minX, int minY, int gridH,
        float cellSize,
        List<TrackPart> partsLibrary)
    {
        if (level == null || pathModel == null || pathModel.Traversals == null)
            return null;

        var outPts = new List<Vector3>(128);

        for (int pi = 0; pi < pathModel.Traversals.Count; pi++)
        {
            var trav = pathModel.Traversals[pi];
            var inst = level.parts.First(p => p.partId == trav.partId);

            // ----- compute world splines for THIS instance from templates -----
            var part = partsLibrary.Find(x => x.partName == inst.partType);
            if (part == null || part.splineTemplates == null || part.splineTemplates.Count == 0)
                continue;

            // half extents in GRID units
            Vector2 half;
            if (part.gridWidth > 0 && part.gridHeight > 0)
                half = new Vector2(part.gridWidth * 0.5f, part.gridHeight * 0.5f);
            else
            {
                int cminX = int.MaxValue, cminY = int.MaxValue, cmaxX = int.MinValue, cmaxY = int.MinValue;
                foreach (var c in inst.occupyingCells)
                { cminX = Mathf.Min(cminX, c.x); cminY = Mathf.Min(cminY, c.y); cmaxX = Mathf.Max(cmaxX, c.x); cmaxY = Mathf.Max(cmaxY, c.y); }
                float w = (cmaxX - cminX + 1), h = (cmaxY - cminY + 1);
                half = new Vector2(w * 0.5f, h * 0.5f);
            }

            // world center (same math as SimLevelBuilder / LevelVisualizer)
            int pminX = int.MaxValue, pminY = int.MaxValue, pmaxX = int.MinValue, pmaxY = int.MinValue;
            foreach (var c in inst.occupyingCells)
            { pminX = Mathf.Min(pminX, c.x); pminY = Mathf.Min(pminY, c.y); pmaxX = Mathf.Max(pmaxX, c.x); pmaxY = Mathf.Max(pmaxY, c.y); }
            float cx = (pminX + pmaxX + 1) * 0.5f - minX;
            float cy = (pminY + pmaxY + 1) * 0.5f - minY;
            Vector2 flipped = new Vector2(cx, gridH - cy);
            Vector3 pos = new Vector3(worldOrigin.x + flipped.x * cellSize,
                                      worldOrigin.y + flipped.y * cellSize, 0f);
            Quaternion rot = Quaternion.Euler(0f, 0f, -inst.rotation);

            // build this instance's world splines
            var worldSplines = new List<List<Vector3>>(part.splineTemplates.Count);
            for (int s = 0; s < part.splineTemplates.Count; s++)
            {
                var tmpl = part.splineTemplates[s];
                var pts = new List<Vector3>(tmpl.Count);
                for (int i = 0; i < tmpl.Count; i++)
                {
                    Vector3 local = new Vector3(tmpl[i][0] - half.x, half.y - tmpl[i][1], -0.05f);
                    Vector3 world = pos + rot * (local * cellSize);
                    pts.Add(world);
                }
                worldSplines.Add(pts);
            }

            // ----- choose spline index exactly like the game path drawer -----
            int splineIndex = -1;
            int groupIndex = -1;
            int pathIndex = -1;

            if (inst.allowedPathsGroup?.Count > 0 && worldSplines != null)
            {
                bool found = false;
                for (int gi = 0; gi < inst.allowedPathsGroup.Count; gi++)
                {
                    var grp = inst.allowedPathsGroup[gi];
                    int idx = grp.allowedPaths.FindIndex(ap =>
                        (ap.entryConnectionId == trav.entryExit && ap.exitConnectionId == trav.exitExit) ||
                        (ap.entryConnectionId == trav.exitExit && ap.exitConnectionId == trav.entryExit)
                    );

                    if (idx >= 0)
                    {
                        if (gi < worldSplines.Count)
                        {
                            groupIndex = gi;
                            pathIndex = idx;
                            splineIndex = gi; // 1:1 assumption holds
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    if (worldSplines.Count == 1) splineIndex = 0;
                    else continue;
                }
            }
            else
            {
                splineIndex = 0;
            }

            var full = worldSplines[splineIndex];

            // ----- t0/t1 selection like the game -----
            bool simple = inst.exits.Count <= 2;
            bool first = pi == 0;
            bool last = pi == pathModel.Traversals.Count - 1;
            float t0, t1;

            if (simple)
            {
                if (!first && !last)
                {
                    t0 = 0f; t1 = 1f;
                }
                else
                {
                    t0 = trav.entryExit < 0 ? 0.5f : GetExitT(inst, trav.entryExit);
                    t1 = trav.exitExit < 0 ? 0.5f : GetExitT(inst, trav.exitExit);
                    if (Mathf.Approximately(t0, t1))
                    {
                        const float eps = 0.001f;
                        if (t1 + eps <= 1f) t1 += eps; else t0 = Mathf.Max(0f, t0 - eps);
                    }
                }
            }
            else
            {
                if (last)
                {
                    float tx = trav.exitExit < 0 ? 0.5f : GetExitT(inst, trav.exitExit);
                    t0 = Mathf.Min(0.5f, tx);
                    t1 = Mathf.Max(0.5f, tx);
                    if (Mathf.Approximately(t0, t1))
                    {
                        const float eps = 0.001f;
                        if (t1 + eps <= 1f) t1 += eps; else t0 = Mathf.Max(0f, t0 - eps);
                    }
                }
                else
                {
                    t0 = 0f; t1 = 1f;
                }
            }

            var seg = ExtractSegmentWorld(full, t0, t1);

            // reverse if entry is on far side
            if (groupIndex >= 0 && pathIndex >= 0)
            {
                var ap = inst.allowedPathsGroup[groupIndex].allowedPaths[pathIndex];
                if (trav.entryExit != ap.entryConnectionId)
                    seg.Reverse();
            }

            for (int i = 0; i < seg.Count; i++)
            {
                var w = seg[i];
                if (outPts.Count == 0 || outPts[outPts.Count - 1] != w)
                    outPts.Add(w);
            }
        }

        return outPts;
    }
}



