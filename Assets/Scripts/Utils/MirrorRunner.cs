using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MirrorRunner
{
    public readonly struct Step
    {
        public readonly int Id;      // mirror train id
        public readonly float Speed; // m/s
        public Step(int id, float speed) { Id = id; Speed = speed; }
    }

    public delegate IEnumerable<Step> StepFeed();
    public delegate RailSimCore.AdvanceResult PreviewById(int mirrorId, float wantMeters);
    public delegate void CommitById(int mirrorId, float allowedMeters, out Vector3 headPos, out Vector3 headTan);

    private readonly StepFeed _feed;
    private readonly PreviewById _preview;
    private readonly CommitById _commit;

    private float? _fixedDt;
    private float _defaultSpeed = 1f;

    public bool Enabled { get; set; } = true;

    public MirrorRunner(StepFeed feed, PreviewById preview, CommitById commit)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
    }

    public void SetFixedDt(float? dtSeconds) => _fixedDt = dtSeconds;
    public void SetDefaultSpeed(float metersPerSecond) => _defaultSpeed = Mathf.Max(0f, metersPerSecond);

    public void Tick(float dtSeconds)
    {
        if (!Enabled) return;
        if (_fixedDt.HasValue) dtSeconds = _fixedDt.Value;
        if (dtSeconds <= 0f) return;

        var steps = _feed();
        if (steps == null) return;

        foreach (var s in steps)
        {
            float speed = s.Speed > 0f ? s.Speed : _defaultSpeed;
            if (speed <= 0f) continue;

            float want = speed * dtSeconds;
            if (want <= 1e-6f) continue;

            var res = _preview(s.Id, want);
            _commit(s.Id, res.Allowed, out _, out _);

            if (res.Kind == RailSimCore.AdvanceResultKind.EndOfPath || res.Kind == RailSimCore.AdvanceResultKind.Blocked || (res.Allowed <= 1e-6f && want > 0f)) MirrorManager.Instance.MarkInactiveById(s.Id);

            //if (res.Kind != RailSimCore.AdvanceResultKind.None || res.Allowed + 1e-6f < want) Debug.Log($"[MIR/TICK] id={s.Id} want={want:F3} allowed={res.Allowed:F3} kind={res.Kind}");
        }
    }
}
