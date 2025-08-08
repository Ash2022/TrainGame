using System.Collections.Generic;
using UnityEngine;

public static class SplineComparer
{
    /// <summary>
    /// Recomputes world-space splines using each part’s actual Transform setup
    /// and compares them to inst.worldSplines.
    /// </summary>
    public static void CompareAllSplines(LevelData level, Transform levelHolder, float cellSize, float tolerance = 0.001f)
    {
        foreach (var inst in level.parts)
        {
            var unitySplines = inst.worldSplines;
            if (unitySplines == null)
            {
                Debug.LogWarning($"[{inst.partId}] worldSplines is null");
                continue;
            }

            Transform partTransform = levelHolder.Find(inst.partId);
            if (partTransform == null)
            {
                Debug.LogWarning($"[{inst.partId}] no GameObject named '{inst.partId}' under levelHolder");
                continue;
            }

            var sprite = LevelVisualizer.Instance.GetSpriteFor(inst.partType);
            if (sprite == null)
            {
                Debug.LogWarning($"[{inst.partId}] no sprite for '{inst.partType}'");
                continue;
            }
            Vector2 halfExtents = sprite.bounds.extents;

            int splineCount = unitySplines.Count;
            var recomputedSplines = new List<List<Vector3>>(splineCount);
            for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
            {
                var templateSpline = inst.splines[splineIndex];
                var recomputedPoints = new List<Vector3>(templateSpline.Count);

                foreach (var pt in templateSpline)
                {
                    float x = pt[0] - halfExtents.x;
                    float y = halfExtents.y - pt[1];
                    Vector3 localPoint = new Vector3(x, y, -0.05f);
                    recomputedPoints.Add(partTransform.TransformPoint(localPoint));
                }

                recomputedSplines.Add(recomputedPoints);
            }

            if (recomputedSplines.Count != splineCount)
            {
                Debug.LogError($"[{inst.partId}] spline count mismatch: Unity={splineCount} vs Recomputed={recomputedSplines.Count}");
                continue;
            }

            bool mismatch = false;
            for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
            {
                var originalSpline = unitySplines[splineIndex];
                var newSpline = recomputedSplines[splineIndex];

                if (originalSpline.Count != newSpline.Count)
                {
                    Debug.LogError($"[{inst.partId}] spline {splineIndex} point count mismatch: Unity={originalSpline.Count} vs Recomputed={newSpline.Count}");
                    mismatch = true;
                    continue;
                }

                for (int pointIndex = 0; pointIndex < originalSpline.Count; pointIndex++)
                {
                    float distance = Vector3.Distance(originalSpline[pointIndex], newSpline[pointIndex]);
                    if (distance > tolerance)
                    {
                        Debug.LogError($"[{inst.partId}] spline {splineIndex} pt {pointIndex} mismatch: Unity={originalSpline[pointIndex]}, Recomputed={newSpline[pointIndex]}, dist={distance:F4}");
                        mismatch = true;
                    }
                }
            }

            if (!mismatch)
                Debug.Log($"[{inst.partId}] spline check passed");
        }
    }
}
