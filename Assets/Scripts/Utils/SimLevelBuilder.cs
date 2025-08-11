// SimLevelBuilder.cs  (pure data → fills worldSplines like the scene would)
using System.Collections.Generic;
using UnityEngine;

public static class SimLevelBuilder
{
    public struct GridBounds { public int minX, minY, maxX, maxY, gridW, gridH; }

    public static GridBounds ComputeGridBounds(LevelData level)
    {
        var gb = new GridBounds { minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue };
        foreach (var inst in level.parts)
            foreach (var c in inst.occupyingCells)
            { gb.minX = Mathf.Min(gb.minX, c.x); gb.minY = Mathf.Min(gb.minY, c.y); gb.maxX = Mathf.Max(gb.maxX, c.x); gb.maxY = Mathf.Max(gb.maxY, c.y); }
        gb.gridW = gb.maxX - gb.minX + 1; gb.gridH = gb.maxY - gb.minY + 1;
        return gb;
    }

    // Builds inst.worldSplines to match your LevelVisualizer math (data-only).
    // SimLevelBuilder.BuildWorld — scene-agnostic, matches TrackPartView math exactly.
    // Call with getSprite=LevelVisualizer.Instance.GetSpriteFor when in-Unity; pass null in headless.
    public static void BuildWorldFromData(
        LevelData level,
        Vector2 worldOrigin,
        int minX, int minY, int gridH,
        float cellSize,
        List<TrackPart> partsLibrary)
    {
        if (level?.parts == null) return;

        foreach (var inst in level.parts)
        {
            // templates must exist
            /*
            var splines = inst.splines;
            if (splines == null || splines.Count == 0)
            {
                inst.worldSplines = new List<List<Vector3>>();
                continue;
            }
            */
            // half extents in GRID units from partsLibrary (fallback to occupyingCells)
            Vector2 half;
            var part = (partsLibrary != null) ? partsLibrary.Find(x => x.partName == inst.partType) : null;

            inst.splines = part.splineTemplates;
            //var splines = inst.splines;

            if (part != null && part.gridWidth > 0 && part.gridHeight > 0)
            {
                half = new Vector2(part.gridWidth * 0.5f, part.gridHeight * 0.5f);
            }
            else
            {
                int cminX = int.MaxValue, cminY = int.MaxValue, cmaxX = int.MinValue, cmaxY = int.MinValue;
                foreach (var c in inst.occupyingCells)
                { cminX = Mathf.Min(cminX, c.x); cminY = Mathf.Min(cminY, c.y); cmaxX = Mathf.Max(cmaxX, c.x); cmaxY = Mathf.Max(cmaxY, c.y); }
                float w = (cmaxX - cminX + 1), h = (cmaxY - cminY + 1);
                half = new Vector2(w * 0.5f, h * 0.5f);
            }

            // world center (same math as LevelVisualizer)
            int pminX = int.MaxValue, pminY = int.MaxValue, pmaxX = int.MinValue, pmaxY = int.MinValue;
            foreach (var c in inst.occupyingCells)
            { pminX = Mathf.Min(pminX, c.x); pminY = Mathf.Min(pminY, c.y); pmaxX = Mathf.Max(pmaxX, c.x); pmaxY = Mathf.Max(pmaxY, c.y); }
            float cx = (pminX + pmaxX + 1) * 0.5f - minX;
            float cy = (pminY + pmaxY + 1) * 0.5f - minY;
            Vector2 flipped = new Vector2(cx, gridH - cy);
            Vector3 pos = new Vector3(worldOrigin.x + flipped.x * cellSize,
                                      worldOrigin.y + flipped.y * cellSize, 0f);

            // rotation (identical to TrackPartView)
            Quaternion rot = Quaternion.Euler(0f, 0f, -inst.rotation);

            // build world splines (EXACT local mapping)
            var worldList = new List<List<Vector3>>(inst.splines.Count);
            for (int s = 0; s < inst.splines.Count; s++)
            {
                var tmpl = inst.splines[s];
                var pts = new List<Vector3>(tmpl.Count);
                for (int i = 0; i < tmpl.Count; i++)
                {
                    Vector3 local = new Vector3(tmpl[i][0] - half.x, half.y - tmpl[i][1], -0.05f);
                    Vector3 world = pos + rot * (local * cellSize); // scale → rotate → translate
                    pts.Add(world);
                }
                worldList.Add(pts);
            }

            // overwrite for visual check
            inst.worldSplines = worldList;
        }
    }


    // Helper to convert a (gridX,gridY,dir) train start into world pos/forward (data-only).
    public static void GetTrainStart(GamePoint p, Vector2 worldOrigin, int minX, int minY, int gridH, float cellSize, out Vector3 headPos, out Vector3 headFwd)
    {
        // if anchored to an exit pin, use that grid cell; else the point’s own grid
        float gx = (p.anchor.exitPin >= 0) ? p.part.exits[p.anchor.exitPin].worldCell.x : p.gridX;
        float gy = (p.anchor.exitPin >= 0) ? p.part.exits[p.anchor.exitPin].worldCell.y : p.gridY;

        float cellX = gx - minX + 0.5f, cellY = gy - minY + 0.5f;
        Vector2 flipped = new Vector2(cellX, gridH - cellY);
        headPos = new Vector3(worldOrigin.x + flipped.x * cellSize, worldOrigin.y + flipped.y * cellSize, 0f);

        headFwd = p.direction switch
        {
            TrainDir.Up => Vector3.up,
            TrainDir.Right => Vector3.right,
            TrainDir.Down => Vector3.down,
            TrainDir.Left => Vector3.left,
            _ => Vector3.up
        };
    }
}
