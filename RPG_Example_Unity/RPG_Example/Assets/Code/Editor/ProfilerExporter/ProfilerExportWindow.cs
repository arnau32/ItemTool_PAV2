using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public class ProfilerExportWindow : EditorWindow
{
    #region Fields

    private int   _frameCount  = 120;
    private float _threshold   = 1f;
    private int   _topN        = 30;
    private int   _worstFrames = 5;
    private string _status     = "";

    private static readonly int[] COL_TOTAL   = { HierarchyFrameDataView.columnTotalTime };
    private static readonly int[] COL_SELF    = { HierarchyFrameDataView.columnSelfTime };
    private static readonly int[] COL_CALLS   = { HierarchyFrameDataView.columnCalls };
    private static readonly int[] COL_GC      = { HierarchyFrameDataView.columnGcMemory };

    #endregion

    #region Unity Callbacks

    [MenuItem("Tools/BetweenShadows/Profiler Export")]
    private static void Open() => GetWindow<ProfilerExportWindow>("Profiler Export");

    private void OnGUI()
    {
        GUILayout.Label("Profiler Export — AI Analysis", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _frameCount  = EditorGUILayout.IntField("Frames to sample",    _frameCount);
        _threshold   = EditorGUILayout.FloatField("Min avg ms (filter)", _threshold);
        _topN        = EditorGUILayout.IntField("Top N samples",        _topN);
        _worstFrames = EditorGUILayout.IntField("Worst frames to log",  _worstFrames);

        EditorGUILayout.Space();

        if (GUILayout.Button("Export to JSON", GUILayout.Height(30)))
            Export();

        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }
    }

    #endregion

    #region Export

    private void Export()
    {
        int lastFrame = ProfilerDriver.lastFrameIndex;
        if (lastFrame < 0)
        {
            _status = "No profiler data. Record a session first.";
            return;
        }

        int firstFrame = Mathf.Max(ProfilerDriver.firstFrameIndex, lastFrame - _frameCount + 1);
        int actualFrames = lastFrame - firstFrame + 1;

        var cpuTimes   = new List<float>(actualFrames);
        var gcFrames   = new List<bool>(actualFrames);
        var worstList  = new List<(int frame, float ms, string hot, float hotMs)>(actualFrames);
        var sampleAcc  = new Dictionary<string, SampleAcc>();
        var gcAcc      = new Dictionary<string, GcAcc>();
        long totalGcBytes = 0L;

        var children = new List<int>(128);

        for (int f = firstFrame; f <= lastFrame; f++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(
                f, 0,
                HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime,
                false);

            if (view == null || !view.valid) continue;

            float frameMs = view.GetItemColumnDataAsSingle(
                view.GetRootItemID(),
                HierarchyFrameDataView.columnTotalTime);

            cpuTimes.Add(frameMs);

            string hotName  = "";
            float  hotMs    = 0f;
            bool   hadGc    = false;

            WalkItems(view, view.GetRootItemID(), children, ref hadGc, ref hotName, ref hotMs,
                      sampleAcc, gcAcc, ref totalGcBytes);

            gcFrames.Add(hadGc);
            worstList.Add((f, frameMs, hotName, hotMs));
        }

        var json = BuildJson(
            actualFrames, cpuTimes, gcFrames, totalGcBytes,
            worstList, sampleAcc, gcAcc);

        string path = EditorUtility.SaveFilePanel(
            "Save profiler export", Application.dataPath,
            $"profiler_{DateTime.Now:yyyyMMdd_HHmm}.json", "json");

        if (string.IsNullOrEmpty(path)) { _status = "Cancelled."; return; }

        File.WriteAllText(path, json, Encoding.UTF8);
        _status = $"Saved {new FileInfo(path).Length / 1024} KB → {Path.GetFileName(path)}";
        Debug.Log($"[ProfilerExport] {_status}");
    }

    #endregion

    #region Walk

    private void WalkItems(
        HierarchyFrameDataView view,
        int itemId,
        List<int> tmp,
        ref bool hadGc,
        ref string hotName,
        ref float hotMs,
        Dictionary<string, SampleAcc> samples,
        Dictionary<string, GcAcc>     gcAcc,
        ref long totalGcBytes)
    {
        var stack = new Stack<int>();
        stack.Push(itemId);

        while (stack.Count > 0)
        {
            int id = stack.Pop();
            string name = view.GetItemName(id);

            float total   = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
            float self    = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime);
            float calls   = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls);
            float gcBytes = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);

            if (gcBytes > 0f)
            {
                hadGc = true;
                totalGcBytes += (long)gcBytes;
                if (!gcAcc.TryGetValue(name, out var ga))
                    gcAcc[name] = ga = new GcAcc();
                ga.TotalBytes += (long)gcBytes;
                ga.Count++;
            }

            if (total >= _threshold)
            {
                if (!samples.TryGetValue(name, out var sa))
                    samples[name] = sa = new SampleAcc();
                sa.TotalMs  += total;
                sa.SelfMs   += self;
                sa.Count++;
                sa.Calls    += (int)calls;
                sa.MaxMs     = Mathf.Max(sa.MaxMs, total);
                sa.GcBytes  += (long)gcBytes;

                if (total > hotMs) { hotMs = total; hotName = name; }
            }

            tmp.Clear();
            view.GetItemChildren(id, tmp);
            foreach (int child in tmp)
                stack.Push(child);
        }
    }

    #endregion

    #region JSON Build

    private string BuildJson(
        int frames,
        List<float> cpuTimes,
        List<bool>  gcFrames,
        long totalGcBytes,
        List<(int frame, float ms, string hot, float hotMs)> worstList,
        Dictionary<string, SampleAcc> samples,
        Dictionary<string, GcAcc> gcAcc)
    {
        cpuTimes.Sort();
        int count = cpuTimes.Count;
        float avg = count > 0 ? Average(cpuTimes) : 0f;
        float max = count > 0 ? cpuTimes[count - 1] : 0f;

        int gcFrameCount = 0;
        foreach (var b in gcFrames) if (b) gcFrameCount++;

        worstList.Sort((a, b) => b.ms.CompareTo(a.ms));

        var sortedSamples = new List<KeyValuePair<string, SampleAcc>>(samples);
        sortedSamples.Sort((a, b) =>
            (b.Value.TotalMs / Mathf.Max(1, b.Value.Count)).CompareTo(
             a.Value.TotalMs / Mathf.Max(1, a.Value.Count)));

        var sortedGc = new List<KeyValuePair<string, GcAcc>>(gcAcc);
        sortedGc.Sort((a, b) => b.Value.TotalBytes.CompareTo(a.Value.TotalBytes));

        var sb = new StringBuilder(65536);
        sb.Append("{");

        // Header
        sb.Append($"\"v\":2,\"engine\":\"{Application.unityVersion}\"");
        sb.Append($",\"ts\":\"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"");
        sb.Append($",\"platform\":\"{Application.platform}\"");
        sb.Append($",\"gfx_api\":\"{SystemInfo.graphicsDeviceType}\"");
        sb.Append($",\"frames\":{frames}");

        // CPU
        sb.Append($",\"cpu\":{{");
        sb.Append($"\"avg\":{avg:F2}");
        sb.Append($",\"max\":{max:F2}");
        sb.Append($",\"p95\":{Percentile(cpuTimes, 0.95f):F2}");
        sb.Append($",\"p75\":{Percentile(cpuTimes, 0.75f):F2}");
        sb.Append($",\"p50\":{Percentile(cpuTimes, 0.50f):F2}");
        sb.Append($",\"over16\":{CountOver(cpuTimes, 16f)}");
        sb.Append($",\"over33\":{CountOver(cpuTimes, 33f)}");
        sb.Append("}");

        // GC
        float gcPct = count > 0 ? gcFrameCount * 100f / count : 0f;
        sb.Append($",\"gc\":{{");
        sb.Append($"\"total_kb\":{totalGcBytes / 1024}");
        sb.Append($",\"frames_gc\":{gcFrameCount}");
        sb.Append($",\"pct_frames\":{gcPct:F1}");
        sb.Append("}");

        // Top N samples
        int sampleLimit = Mathf.Min(_topN, sortedSamples.Count);
        sb.Append(",\"samples\":[");
        for (int i = 0; i < sampleLimit; i++)
        {
            var kv  = sortedSamples[i];
            var sa  = kv.Value;
            int cnt = Mathf.Max(1, sa.Count);
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append($"\"n\":{JsonStr(kv.Key)}");
            sb.Append($",\"avg\":{(sa.TotalMs / cnt):F2}");
            sb.Append($",\"max\":{sa.MaxMs:F2}");
            sb.Append($",\"self\":{(sa.SelfMs / cnt):F2}");
            sb.Append($",\"gc_kb\":{sa.GcBytes / 1024}");
            sb.Append($",\"cpf\":{(sa.Calls / (float)cnt):F1}");
            sb.Append("}");
        }
        sb.Append("]");

        // GC hot spots
        int gcLimit = Mathf.Min(20, sortedGc.Count);
        sb.Append(",\"gc_hot\":[");
        for (int i = 0; i < gcLimit; i++)
        {
            var kv = sortedGc[i];
            var ga = kv.Value;
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append($"\"n\":{JsonStr(kv.Key)}");
            sb.Append($",\"total_kb\":{ga.TotalBytes / 1024}");
            sb.Append($",\"count\":{ga.Count}");
            sb.Append($",\"avg_kb\":{(ga.TotalBytes / 1024f / Mathf.Max(1, ga.Count)):F2}");
            sb.Append("}");
        }
        sb.Append("]");

        // Worst frames
        int worstLimit = Mathf.Min(_worstFrames, worstList.Count);
        sb.Append(",\"worst\":[");
        for (int i = 0; i < worstLimit; i++)
        {
            var w = worstList[i];
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append($"\"f\":{w.frame}");
            sb.Append($",\"ms\":{w.ms:F2}");
            sb.Append($",\"hot\":{JsonStr(w.hot)}");
            sb.Append($",\"hot_ms\":{w.hotMs:F2}");
            sb.Append("}");
        }
        sb.Append("]");

        // Render stats (from last recorded frame UnityStats)
        sb.Append(",\"render\":{");
        sb.Append($"\"drawcalls\":{UnityStats.drawCalls}");
        sb.Append($",\"batches\":{UnityStats.batches}");
        sb.Append($",\"dyn_batched\":{UnityStats.dynamicBatchedDrawCalls}");
        sb.Append($",\"sta_batched\":{UnityStats.staticBatchedDrawCalls}");
        sb.Append($",\"instanced\":{UnityStats.instancedBatchedDrawCalls}");
        sb.Append($",\"tris\":{UnityStats.triangles}");
        sb.Append($",\"verts\":{UnityStats.vertices}");
        sb.Append($",\"shadow_casters\":{UnityStats.shadowCasters}");
        sb.Append($",\"screen\":\"{Screen.width}x{Screen.height}\"");
        sb.Append("}");

        // Quality / device context
        var q = QualitySettings.names;
        int qi = QualitySettings.GetQualityLevel();
        sb.Append(",\"quality\":{");
        sb.Append($"\"level_name\":{JsonStr(qi < q.Length ? q[qi] : "unknown")}");
        sb.Append($",\"level_idx\":{qi}");
        sb.Append($",\"vsync\":{QualitySettings.vSyncCount}");
        sb.Append($",\"aa\":{QualitySettings.antiAliasing}");
        sb.Append($",\"shadow_dist\":{QualitySettings.shadowDistance:F1}");
        sb.Append($",\"lod_bias\":{QualitySettings.lodBias:F2}");
        sb.Append($",\"max_lod\":{QualitySettings.maximumLODLevel}");
        sb.Append($",\"tex_limit\":{QualitySettings.globalTextureMipmapLimit}");
        sb.Append($",\"gfx_tier\":{JsonStr(Graphics.activeTier.ToString())}");
        sb.Append($",\"gfx_device\":{JsonStr(SystemInfo.graphicsDeviceName)}");
        sb.Append($",\"gfx_mem_mb\":{SystemInfo.graphicsMemorySize}");
        sb.Append("}");

        sb.Append("}");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private class SampleAcc
    {
        public float TotalMs;
        public float SelfMs;
        public float MaxMs;
        public int   Count;
        public int   Calls;
        public long  GcBytes;
    }

    private class GcAcc
    {
        public long TotalBytes;
        public int  Count;
    }

    private static float Average(List<float> sorted)
    {
        float s = 0f;
        foreach (var v in sorted) s += v;
        return s / sorted.Count;
    }

    private static float Percentile(List<float> sorted, float p)
    {
        if (sorted.Count == 0) return 0f;
        int i = Mathf.Clamp(Mathf.RoundToInt(p * sorted.Count), 0, sorted.Count - 1);
        return sorted[i];
    }

    private static int CountOver(List<float> sorted, float threshold)
    {
        int n = 0;
        foreach (var v in sorted) if (v > threshold) n++;
        return n;
    }

    private static string JsonStr(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    #endregion
}
