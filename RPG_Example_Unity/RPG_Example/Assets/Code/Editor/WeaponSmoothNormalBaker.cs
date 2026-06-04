using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ─── Baker logic ─────────────────────────────────────────────────────────────

public static class WeaponSmoothNormalBaker
{
    // Averages normals per unique vertex position and stores them in the tangent
    // channel (xyz) of a NEW mesh copy. The source mesh is never modified.
    public static Mesh Bake(Mesh source)
    {
        Vector3[] vertices = source.vertices;
        Vector3[] normals  = source.normals;

        var accumulated = new Dictionary<Vector3, Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 key = Snap(vertices[i]);
            accumulated[key] = accumulated.TryGetValue(key, out Vector3 sum)
                ? sum + normals[i]
                : normals[i];
        }

        var tangents = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 avg = accumulated[Snap(vertices[i])].normalized;
            tangents[i] = new Vector4(avg.x, avg.y, avg.z, 1f);
        }

        Mesh copy     = Object.Instantiate(source);
        copy.name     = source.name + "_SmoothNormals";
        copy.tangents = tangents;

        string srcPath = AssetDatabase.GetAssetPath(source);
        string dir     = string.IsNullOrEmpty(srcPath)
            ? "Assets"
            : Path.GetDirectoryName(srcPath);
        string outPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{copy.name}.asset");

        AssetDatabase.CreateAsset(copy, outPath);
        return copy;
    }

    static Vector3 Snap(Vector3 v)
    {
        const float k = 1e4f;
        return new Vector3(
            Mathf.Round(v.x * k) / k,
            Mathf.Round(v.y * k) / k,
            Mathf.Round(v.z * k) / k);
    }
}

// ─── Editor window ───────────────────────────────────────────────────────────

public class WeaponQualityBakerWindow : EditorWindow
{
    [MenuItem("Window/Weapon Quality Baker")]
    static void Open() => GetWindow<WeaponQualityBakerWindow>("Weapon Quality Baker");

    private GameObject _target;
    private Vector2    _scroll;
    private string     _statusMsg;
    private bool       _statusOk;

    // Cached after each target change.
    private MeshFilter[]   _filters;
    private MeshRenderer[] _renderers;

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Smooth Normal Baker", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Asigna smooth normals al canal de tangentes para outline sin artefactos.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(6);

        EditorGUI.BeginChangeCheck();
        _target = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Target", "GameObject con MeshFilter + MeshRenderer (el child QualityGlow del arma)."),
            _target, typeof(GameObject), true);

        if (EditorGUI.EndChangeCheck())
        {
            _statusMsg = null;
            RefreshComponents();
        }

        if (_target == null)
        {
            EditorGUILayout.HelpBox("Arrastra aquí el GameObject del arma o el child QualityGlow.", MessageType.Info);
            return;
        }

        DrawComponentInfo();

        EditorGUILayout.Space(8);

        bool canBake = _filters != null && _filters.Length > 0;

        EditorGUI.BeginDisabledGroup(!canBake);
        if (GUILayout.Button("Bake & Assign", GUILayout.Height(32)))
            DoBake();
        EditorGUI.EndDisabledGroup();

        if (!canBake)
            EditorGUILayout.HelpBox("No se encontró ningún MeshFilter con mesh asignado.", MessageType.Warning);

        if (!string.IsNullOrEmpty(_statusMsg))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_statusMsg, _statusOk ? MessageType.Info : MessageType.Error);
        }
    }

    void DrawComponentInfo()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Componentes detectados", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(120));

        if (_filters == null || _filters.Length == 0)
        {
            EditorGUILayout.LabelField("  — Sin MeshFilters", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var mf in _filters)
            {
                if (mf == null) continue;
                string meshName  = mf.sharedMesh != null ? mf.sharedMesh.name : "null";
                bool   alreadyBaked = meshName.EndsWith("_SmoothNormals");
                string icon      = alreadyBaked ? "✓" : "○";
                string suffix    = alreadyBaked ? " (ya bakeado)" : "";
                EditorGUILayout.LabelField($"  {icon}  {mf.gameObject.name} → {meshName}{suffix}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    void RefreshComponents()
    {
        if (_target == null)
        {
            _filters   = null;
            _renderers = null;
            return;
        }

        var filterList   = new List<MeshFilter>();
        var rendererList = new List<MeshRenderer>();

        foreach (var mf in _target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            filterList.Add(mf);
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) rendererList.Add(mr);
        }

        _filters   = filterList.ToArray();
        _renderers = rendererList.ToArray();
    }

    void DoBake()
    {
        if (_filters == null || _filters.Length == 0) return;

        int baked   = 0;
        int skipped = 0;

        foreach (var mf in _filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;

            if (mf.sharedMesh.name.EndsWith("_SmoothNormals"))
            {
                skipped++;
                continue;
            }

            Mesh newMesh  = WeaponSmoothNormalBaker.Bake(mf.sharedMesh);
            mf.sharedMesh = newMesh;
            EditorUtility.SetDirty(mf);
            baked++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshComponents();

        if (baked > 0)
        {
            _statusMsg = skipped > 0
                ? $"✓  {baked} mesh(es) bakeados. {skipped} ya tenían smooth normals y se omitieron."
                : $"✓  {baked} mesh(es) bakeados y asignados correctamente.";
            _statusOk = true;
        }
        else
        {
            _statusMsg = "Todos los meshes ya estaban bakeados.";
            _statusOk  = true;
        }

        Repaint();
    }
}
