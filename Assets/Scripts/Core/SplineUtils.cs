using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplineUtils
{
    /// <summary>
    /// Converts a cornered polyline into a smooth spline by treating each
    /// segment as a cubic Bézier with auto-computed tangents, then samples
    /// it at approximately uniform spacing.
    /// </summary>
    /// <param name="cornerPoints">Input polyline (must have ≥2 points).</param>
    /// <param name="handleRatio">
    /// Fraction of segment length to use as control-handle length (e.g. 0.3f).
    /// </param>
    /// <param name="sampleStep">
    /// Approximate distance between output samples (world units).
    /// </param>
    /// <param name="outPositions">Smoothed, sampled positions along the path.</param>
    /// <param name="outTangents">Normalized tangents (derivatives) at each sample.</param>
    public static void BuildSmoothSpline(
        IList<Vector3> cornerPoints,
        float handleRatio,
        float sampleStep,
        out List<Vector3> outPositions,
        out List<Vector3> outTangents)
    {
        if (cornerPoints == null || cornerPoints.Count < 2)
            throw new ArgumentException("Need at least 2 corner points", nameof(cornerPoints));

        int n = cornerPoints.Count;
        // 1) Compute auto-tangents at each corner
        Vector3[] tangents = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dirIn, dirOut;
            if (i == 0)
                dirIn = cornerPoints[1] - cornerPoints[0];
            else
                dirIn = cornerPoints[i] - cornerPoints[i - 1];

            if (i == n - 1)
                dirOut = cornerPoints[n - 1] - cornerPoints[n - 2];
            else
                dirOut = cornerPoints[i + 1] - cornerPoints[i];

            dirIn.Normalize();
            dirOut.Normalize();
            tangents[i] = (dirIn + dirOut).normalized;
            // if dirIn+dirOut is zero (180°), fall back to dirOut
            if (tangents[i] == Vector3.zero)
                tangents[i] = dirOut;
        }

        outPositions = new List<Vector3>();
        outTangents = new List<Vector3>();

        // 2) For each segment, build Bézier and sample
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = cornerPoints[i];
            Vector3 p1 = cornerPoints[i + 1];
            float segLen = Vector3.Distance(p0, p1);
            float handleLen = segLen * handleRatio;

            Vector3 c0 = p0 + tangents[i] * handleLen;
            Vector3 c1 = p1 - tangents[i + 1] * handleLen;

            // Determine number of samples for ~uniform spacing
            int steps = Mathf.Max(2, Mathf.CeilToInt(segLen / sampleStep));
            float dt = 1f / (steps - 1);

            for (int si = 0; si < steps; si++)
            {
                float t = si * dt;
                // De Casteljau for position
                Vector3 a = Vector3.Lerp(p0, c0, t);
                Vector3 b = Vector3.Lerp(c0, c1, t);
                Vector3 c = Vector3.Lerp(c1, p1, t);
                Vector3 d = Vector3.Lerp(a, b, t);
                Vector3 e = Vector3.Lerp(b, c, t);
                Vector3 pos = Vector3.Lerp(d, e, t);

                // Derivative for tangent: B'(t)
                Vector3 dp0 = (c0 - p0) * 3f;
                Vector3 dp1 = (c1 - c0) * 3f;
                Vector3 dp2 = (p1 - c1) * 3f;
                Vector3 da = Vector3.Lerp(dp0, dp1, t);
                Vector3 db = Vector3.Lerp(dp1, dp2, t);
                Vector3 deriv = Vector3.Lerp(da, db, t).normalized;

                outPositions.Add(pos);
                outTangents.Add(deriv);
            }
            // Avoid duplicating end-point except on last segment
            if (i < n - 2)
            {
                outPositions.RemoveAt(outPositions.Count - 1);
                outTangents.RemoveAt(outTangents.Count - 1);
            }
        }
    }
}
