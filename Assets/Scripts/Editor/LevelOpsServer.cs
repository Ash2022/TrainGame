#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using System.Collections.Specialized;
public static class LevelOpsServer
{
    // ---- Config ----
    const string Url = "http://127.0.0.1:7777/"; // local only

    // ---- Server state ----
    static HttpListener _listener;
    static Thread _thread;
    static volatile bool _running;

    // ---- Main-thread dispatcher ----
    static readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();
    static bool _updateHookInstalled;

    // ===== Menu =====
    [MenuItem("Tools/LevelOps/Start Server")]
    public static void StartServer()
    {
        if (_running) { Debug.Log("[LevelOpsServer] Already running."); return; }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
            _listener.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelOpsServer] Failed to bind {Url}. {e.Message}");
            return;
        }

        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "LevelOpsServer" };
        _thread.Start();
        EnsureUpdateHook();
        Debug.Log($"[LevelOpsServer] Listening on {Url}");
    }

    [MenuItem("Tools/LevelOps/Stop Server")]
    public static void StopServer()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
        _listener = null;
        try { _thread?.Join(250); } catch { }
        _thread = null;
        Debug.Log("[LevelOpsServer] Stopped.");
    }

    // ===== Listener loop =====
    static void Loop()
    {
        while (_running && _listener != null && _listener.IsListening)
        {
            HttpListenerContext ctx = null;
            try { ctx = _listener.GetContext(); }
            catch { if (!_running) break; else continue; }

            try
            {
                var path = ctx.Request.Url.AbsolutePath.Trim('/').ToLowerInvariant();
                NameValueCollection q = ctx.Request.QueryString ?? new NameValueCollection();

                string json = Handle(path, q);
                WriteResponse(ctx, 200, json);
            }
            catch (HandledHttpError ex)
            {
                WriteResponse(ctx, ex.Status, JsonErr(ex.Message));
            }
            catch (Exception ex)
            {
                WriteResponse(ctx, 500, JsonErr(ex.ToString()));
            }
        }
    }

    // ===== Endpoint router =====
    static string Handle(string path, System.Collections.Specialized.NameValueCollection q)
    {
        switch (path)
        {
            case "ping": return JsonOk(new { pong = true });

            case "new_level":
                RunOnMainThread(() => LevelOpsService.NewLevel());
                return JsonOk();

            case "load_level":
                {
                    var p = Require(q, "path");
                    RunOnMainThread(() => LevelOpsService.LoadLevelFromPath(p));
                    return JsonOk(new { loaded = p });
                }

            case "save_level":
                {
                    var p = Require(q, "path");
                    RunOnMainThread(() => LevelOpsService.SaveLevelToPath(p));
                    return JsonOk(new { saved = p });
                }

            case "list_parts":
                {
                    var parts = RunOnMainThread(() => LevelOpsService.ListParts()
                        .Select(t => new { t.partName, t.gridWidth, t.gridHeight }).ToList());
                    return JsonOk(new { parts });
                }

            case "place_part":
                {
                    var name = Require(q, "name");
                    int x = Int(q, "x");
                    int y = Int(q, "y");
                    int rot = Int(q, "rot", 0);
                    var id = RunOnMainThread(() => LevelOpsService.PlacePart(name, x, y, rot));
                    return JsonOk(new { id });
                }

            case "rotate_part":
                {
                    var id = Require(q, "id");
                    int delta = Int(q, "delta", 90);
                    RunOnMainThread(() => LevelOpsService.RotatePart(id, delta));
                    return JsonOk();
                }

            case "move_part":
                {
                    var id = Require(q, "id");
                    int x = Int(q, "x");
                    int y = Int(q, "y");
                    RunOnMainThread(() => LevelOpsService.MovePart(id, x, y));
                    return JsonOk();
                }

            case "delete_part":
                {
                    var id = Require(q, "id");
                    RunOnMainThread(() => LevelOpsService.DeletePart(id));
                    return JsonOk();
                }

            case "add_point":
                {
                    int x = Int(q, "x");
                    int y = Int(q, "y");
                    var typeStr = Require(q, "type");
                    int color = Int(q, "color", 0);
                    var type = ParsePointType(typeStr);
                    RunOnMainThread(() => LevelOpsService.AddPoint(x, y, type, color));
                    return JsonOk();
                }

            case "set_point_color":
                {
                    int id = Int(q, "id");
                    int color = Int(q, "color");
                    RunOnMainThread(() => LevelOpsService.SetPointColor(id, color));
                    return JsonOk();
                }

            case "set_station_queue":
                {
                    int id = Int(q, "id");
                    var queueStr = Require(q, "queue"); // e.g. "2,0,2,1"
                    var list = queueStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => int.Parse(s.Trim())).ToList();
                    RunOnMainThread(() => LevelOpsService.SetStationQueue(id, list));
                    return JsonOk(new { count = list.Count });
                }

            case "clear_station_queue":
                {
                    int id = Int(q, "id");
                    RunOnMainThread(() => LevelOpsService.ClearStationQueue(id));
                    return JsonOk();
                }

            case "build_graph":
                RunOnMainThread(() => LevelOpsService.BuildGraph());
                return JsonOk();

            case "validate":
                {
                    var report = RunOnMainThread(() => LevelOpsService.Validate());
                    return JsonOk(new { report });
                }

            case "get_level_state":
                {
                    var state = RunOnMainThread(() =>
                    {
                        var ld = LevelOpsService.GetLevelData();
                        // bounds
                        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                        foreach (var inst in ld.parts)
                        {
                            inst.RecomputeOccupancy(null); // safe; uses internal data if present
                            foreach (var c in inst.occupyingCells)
                            {
                                minX = Math.Min(minX, c.x);
                                minY = Math.Min(minY, c.y);
                                maxX = Math.Max(maxX, c.x);
                                maxY = Math.Max(maxY, c.y);
                            }
                        }
                        if (ld.parts.Count == 0) { minX = minY = 0; maxX = maxY = 0; }

                        var parts = ld.parts.Select(p => new
                        {
                            p.partId,
                            p.partType,
                            x = p.position.x,
                            y = p.position.y,
                            p.rotation
                        }).ToList();

                        var points = (ld.gameData?.points ?? new List<GamePoint>()).Select(g => new
                        {
                            g.id,
                            type = g.type.ToString(),
                            g.colorIndex,
                            g.gridX,
                            g.gridY,
                            waitingPeople = g.waitingPeople ?? new List<int>()
                        }).ToList();

                        return new
                        {
                            bounds = new { minX, minY, maxX, maxY },
                            parts,
                            points
                        };
                    });
                    return JsonOk(state);
                }

            case "get_part_at":
                {
                    int x = Int(q, "x");
                    int y = Int(q, "y");
                    var inst = RunOnMainThread(() => LevelOpsService.GetPartAt(x, y));
                    if (inst == null) return JsonOk(new { part = (object)null });
                    return JsonOk(new
                    {
                        part = new
                        {
                            inst.partId,
                            inst.partType,
                            x = inst.position.x,
                            y = inst.position.y,
                            inst.rotation
                        }
                    });
                }

            default:
                throw new HandledHttpError(404, $"Unknown endpoint '{path}'");
        }
    }

    // ===== Helpers =====
    static string Require(System.Collections.Specialized.NameValueCollection q, string key)
    {
        var v = q[key];
        if (string.IsNullOrEmpty(v)) throw new HandledHttpError(400, $"Missing '{key}'");
        return v;
    }

    static int Int(System.Collections.Specialized.NameValueCollection q, string key, int def = 0)
    {
        var v = q[key];
        return int.TryParse(v, out var i) ? i : def;
    }

    static GamePointType ParsePointType(string s)
    {
        if (int.TryParse(s, out var i)) return (GamePointType)i;
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "station" => GamePointType.Station,
            "depot" => GamePointType.Depot,
            "train" => GamePointType.Train,
            _ => throw new HandledHttpError(400, $"Bad type '{s}' (use Station/Depot/Train)")
        };
    }

    static void WriteResponse(HttpListenerContext ctx, int status, string json)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var buf = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = buf.Length;
        ctx.Response.OutputStream.Write(buf, 0, buf.Length);
        ctx.Response.OutputStream.Close();
    }

    static string JsonOk(object data = null)
        => JsonConvert.SerializeObject(new { ok = true, data }, Formatting.Indented);

    static string JsonErr(string msg)
        => JsonConvert.SerializeObject(new { ok = false, error = msg }, Formatting.Indented);

    class HandledHttpError : Exception
    {
        public int Status { get; }
        public HandledHttpError(int status, string msg) : base(msg) { Status = status; }
    }

    // ---- Run code on Unity main thread synchronously ----
    static void EnsureUpdateHook()
    {
        if (_updateHookInstalled) return;
        _updateHookInstalled = true;
        EditorApplication.update += () =>
        {
            while (_mainQueue.TryDequeue(out var a))
            {
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        };
    }

    static T RunOnMainThread<T>(Func<T> fn)
    {
        T result = default;
        Exception ex = null;
        using (var done = new ManualResetEventSlim(false))
        {
            _mainQueue.Enqueue(() =>
            {
                try { result = fn(); }
                catch (Exception e) { ex = e; }
                finally { done.Set(); }
            });
            done.Wait();
        }
        if (ex != null) throw ex;
        return result;
    }

    static void RunOnMainThread(Action fn) => RunOnMainThread<object>(() => { fn(); return null; });
}
#endif
