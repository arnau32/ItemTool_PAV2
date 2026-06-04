using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class AdvancedBulkRenamerWindow : EditorWindow
{
    private enum ScopeMode
    {
        Selection,
        Folder
    }

    private enum MatchMode
    {
        Contains,
        StartsWith,
        EndsWith,
        Exact
    }

    private enum ApplyMode
    {
        Anywhere,
        PrefixOnly,
        SuffixOnly
    }

    private enum ReplaceMode
    {
        PlainText,
        Regex
    }

    [Header("Scope")] [SerializeField] private ScopeMode scope = ScopeMode.Selection;
    [SerializeField] private DefaultAsset folder;
    [SerializeField] private bool includeSubfolders = true;

    [Header("Filter (optional)")] [SerializeField]
    private bool useFilter = true;

    [SerializeField] private MatchMode filterMatch = MatchMode.Contains;
    [SerializeField] private string filterText = "SM_Wep";

    [Header("Replace")] [SerializeField] private ReplaceMode replaceMode = ReplaceMode.PlainText;
    [SerializeField] private ApplyMode applyMode = ApplyMode.Anywhere;
    [SerializeField] private bool caseSensitive = true;
    [SerializeField] private string findText = "SM_Wep_";
    [SerializeField] private string replaceText = "Hero_";

    [Header("Safety")] [SerializeField] private bool previewOnly = true;
    [SerializeField] private bool skipIfNoChange = true;
    [SerializeField] private bool skipIfNameCollision = true;

    private Vector2 scroll;
    private readonly List<RenamePreview> previews = new();
    private string lastError;

    [MenuItem("Tools/Bulk Renamer")]
    public static void Open()
    {
        var w = GetWindow<AdvancedBulkRenamerWindow>("Bulk Renamer");
        w.minSize = new Vector2(720f, 420f);
        w.RebuildPreview();
    }

    private void OnSelectionChange()
    {
        if (scope == ScopeMode.Selection)
            RebuildPreview();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        DrawScope();
        EditorGUILayout.Space(6);
        DrawFilter();
        EditorGUILayout.Space(6);
        DrawReplace();
        EditorGUILayout.Space(6);
        DrawSafety();

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview", GUILayout.Height(28)))
                RebuildPreview();

            GUI.enabled = previews.Count > 0;
            if (GUILayout.Button(previewOnly ? "Apply (PreviewOnly ON)" : "Apply Rename", GUILayout.Height(28)))
                ApplyRename();
            GUI.enabled = true;
        }

        if (!string.IsNullOrEmpty(lastError))
        {
            EditorGUILayout.HelpBox(lastError, MessageType.Warning);
        }

        EditorGUILayout.Space(8);
        DrawPreviewList();
    }

    private void DrawScope()
    {
        EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            ScopeMode newScope = (ScopeMode)EditorGUILayout.EnumPopup("Mode", scope);
            if (newScope != scope)
            {
                scope = newScope;
                RebuildPreview();
            }

            using (new EditorGUI.DisabledScope(scope != ScopeMode.Folder))
            {
                var newFolder =
                    (DefaultAsset)EditorGUILayout.ObjectField("Folder", folder, typeof(DefaultAsset), false);
                if (newFolder != folder)
                {
                    folder = newFolder;
                    RebuildPreview();
                }

                bool newSub = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
                if (newSub != includeSubfolders)
                {
                    includeSubfolders = newSub;
                    RebuildPreview();
                }
            }
        }
    }

    private void DrawFilter()
    {
        EditorGUILayout.LabelField("Filter (Optional)", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            bool newUse = EditorGUILayout.Toggle("Use Filter", useFilter);
            if (newUse != useFilter)
            {
                useFilter = newUse;
                RebuildPreview();
            }

            using (new EditorGUI.DisabledScope(!useFilter))
            {
                MatchMode newMatch = (MatchMode)EditorGUILayout.EnumPopup("Match", filterMatch);
                if (newMatch != filterMatch)
                {
                    filterMatch = newMatch;
                    RebuildPreview();
                }

                string newText = EditorGUILayout.TextField("Text", filterText);
                if (newText != filterText)
                {
                    filterText = newText;
                    RebuildPreview();
                }
            }
        }
    }

    private void DrawReplace()
    {
        EditorGUILayout.LabelField("Replace", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            ReplaceMode newReplaceMode = (ReplaceMode)EditorGUILayout.EnumPopup("Mode", replaceMode);
            if (newReplaceMode != replaceMode)
            {
                replaceMode = newReplaceMode;
                RebuildPreview();
            }

            ApplyMode newApply = (ApplyMode)EditorGUILayout.EnumPopup("Apply To", applyMode);
            if (newApply != applyMode)
            {
                applyMode = newApply;
                RebuildPreview();
            }

            bool newCase = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
            if (newCase != caseSensitive)
            {
                caseSensitive = newCase;
                RebuildPreview();
            }

            string newFind =
                EditorGUILayout.TextField(replaceMode == ReplaceMode.Regex ? "Regex Pattern" : "Find", findText);
            if (newFind != findText)
            {
                findText = newFind;
                RebuildPreview();
            }

            string newReplace = EditorGUILayout.TextField("Replace With", replaceText);
            if (newReplace != replaceText)
            {
                replaceText = newReplace;
                RebuildPreview();
            }

            if (replaceMode == ReplaceMode.Regex)
            {
                EditorGUILayout.HelpBox(
                    "Regex mode uses .NET Regex. Example:\n" +
                    "Prefix: ^SM_Wep_  -> Hero_\n" +
                    "Suffix: _LOD\\d+$ -> _LOD",
                    MessageType.Info);
            }
        }
    }

    private void DrawSafety()
    {
        EditorGUILayout.LabelField("Safety", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            bool newPreview = EditorGUILayout.Toggle("Preview Only (Dry Run)", previewOnly);
            if (newPreview != previewOnly)
                previewOnly = newPreview;

            bool newSkipNoChange = EditorGUILayout.Toggle("Skip If No Change", skipIfNoChange);
            if (newSkipNoChange != skipIfNoChange)
            {
                skipIfNoChange = newSkipNoChange;
                RebuildPreview();
            }

            bool newSkipCollision = EditorGUILayout.Toggle("Skip If Name Collision", skipIfNameCollision);
            if (newSkipCollision != skipIfNameCollision)
            {
                skipIfNameCollision = newSkipCollision;
                RebuildPreview();
            }
        }
    }

    private void DrawPreviewList()
    {
        EditorGUILayout.LabelField($"Preview ({previews.Count})", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (previews.Count == 0)
            {
                EditorGUILayout.LabelField("No assets found for current settings.");
            }
            else
            {
                foreach (var p in previews)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = false;
                        EditorGUILayout.ObjectField(p.asset, typeof(UnityEngine.Object), false, GUILayout.Width(220));
                        GUI.enabled = true;

                        EditorGUILayout.LabelField(p.oldName, GUILayout.Width(240));
                        EditorGUILayout.LabelField("→", GUILayout.Width(18));
                        EditorGUILayout.LabelField(p.newName, GUILayout.Width(240));

                        if (!string.IsNullOrEmpty(p.note))
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField(p.note, EditorStyles.miniLabel, GUILayout.Width(160));
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void RebuildPreview()
    {
        lastError = null;
        previews.Clear();

        try
        {
            var assets = GatherAssets();
            if (assets.Count == 0)
                return;

            var existingNamesByFolder = BuildExistingNamesLookup(assets);

            foreach (var obj in assets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                string oldName = Path.GetFileNameWithoutExtension(path);
                if (useFilter && !MatchesFilter(oldName))
                    continue;

                string newName = ComputeNewName(oldName);
                if (skipIfNoChange && string.Equals(newName, oldName, StringComparison.Ordinal))
                    continue;

                string note = string.Empty;

                if (skipIfNameCollision)
                {
                    string folderPath = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
                    if (existingNamesByFolder.TryGetValue(folderPath, out var set))
                    {
                        // If the target name already exists in that folder (and isn't this same asset name), warn/skip.
                        if (set.Contains(newName) && !string.Equals(newName, oldName, StringComparison.Ordinal))
                            note = "Collision";
                    }
                }

                previews.Add(new RenamePreview(obj, oldName, newName, note));
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
        }
    }

    private void ApplyRename()
    {
        if (previewOnly)
        {
            Debug.Log("PreviewOnly is ON. Disable it to apply renames.");
            return;
        }

        if (previews.Count == 0)
            return;

        AssetDatabase.StartAssetEditing();
        try
        {
            int renamed = 0;
            int skippedCollisions = 0;

            foreach (var p in previews)
            {
                if (skipIfNameCollision && p.note == "Collision")
                {
                    skippedCollisions++;
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(p.asset);
                if (string.IsNullOrEmpty(path))
                    continue;

                string result = AssetDatabase.RenameAsset(path, p.newName);
                if (string.IsNullOrEmpty(result))
                {
                    renamed++;
                }
                else
                {
                    Debug.LogWarning($"Rename failed: {path} -> {p.newName}. Reason: {result}");
                }
            }

            Debug.Log($"Bulk rename done. Renamed: {renamed}. Skipped collisions: {skippedCollisions}.");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RebuildPreview();
        }
    }

    private List<UnityEngine.Object> GatherAssets()
    {
        var result = new List<UnityEngine.Object>(256);

        if (scope == ScopeMode.Selection)
        {
            foreach (var obj in Selection.objects)
            {
                if (obj == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                // Skip scene objects, only assets.
                if (path.StartsWith("Assets", StringComparison.Ordinal))
                    result.Add(obj);
            }

            return result;
        }

        // Folder scope
        if (folder == null)
            return result;

        string folderPath = AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return result;

        string[] searchInFolders = new[] { folderPath };
        string[] guids = AssetDatabase.FindAssets(string.Empty, searchInFolders);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!includeSubfolders)
            {
                // If not including subfolders, only allow direct children.
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.Equals(parent, folderPath, StringComparison.Ordinal))
                    continue;
            }

            // Load main asset at path
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null)
                result.Add(obj);
        }

        return result;
    }

    private bool MatchesFilter(string name)
    {
        if (string.IsNullOrEmpty(filterText))
            return true;

        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return filterMatch switch
        {
            MatchMode.Contains => name.IndexOf(filterText, cmp) >= 0,
            MatchMode.StartsWith => name.StartsWith(filterText, cmp),
            MatchMode.EndsWith => name.EndsWith(filterText, cmp),
            MatchMode.Exact => string.Equals(name, filterText, cmp),
            _ => true
        };
    }

    private string ComputeNewName(string oldName)
    {
        if (string.IsNullOrEmpty(oldName))
            return oldName;

        if (replaceMode == ReplaceMode.Regex)
        {
            return ComputeRegex(oldName);
        }

        return ComputePlain(oldName);
    }

    private string ComputePlain(string oldName)
    {
        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return applyMode switch
        {
            ApplyMode.Anywhere => ReplaceAnywhere(oldName, findText, replaceText, cmp),
            ApplyMode.PrefixOnly => ReplacePrefix(oldName, findText, replaceText, cmp),
            ApplyMode.SuffixOnly => ReplaceSuffix(oldName, findText, replaceText, cmp),
            _ => oldName
        };
    }

    private string ComputeRegex(string oldName)
    {
        if (string.IsNullOrEmpty(findText))
            return oldName;

        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        string pattern = applyMode switch
        {
            ApplyMode.Anywhere => findText,
            ApplyMode.PrefixOnly => "^" + findText,
            ApplyMode.SuffixOnly => findText + "$",
            _ => findText
        };

        try
        {
            return Regex.Replace(oldName, pattern, replaceText ?? string.Empty, options);
        }
        catch (ArgumentException e)
        {
            lastError = $"Invalid regex: {e.Message}";
            return oldName;
        }
    }

    private static string ReplaceAnywhere(string input, string find, string replace, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(find))
            return input;

        // Plain replace with case-insensitive support
        if (cmp == StringComparison.Ordinal)
            return input.Replace(find, replace);

        // Case-insensitive replace
        return Regex.Replace(input, Regex.Escape(find), replace ?? string.Empty, RegexOptions.IgnoreCase);
    }

    private static string ReplacePrefix(string input, string find, string replace, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(find))
            return input;

        if (input.StartsWith(find, cmp))
            return (replace ?? string.Empty) + input.Substring(find.Length);

        return input;
    }

    private static string ReplaceSuffix(string input, string find, string replace, StringComparison cmp)
    {
        if (string.IsNullOrEmpty(find))
            return input;

        if (input.EndsWith(find, cmp))
            return input.Substring(0, input.Length - find.Length) + (replace ?? string.Empty);

        return input;
    }

    private static Dictionary<string, HashSet<string>> BuildExistingNamesLookup(List<UnityEngine.Object> assets)
    {
        var dict = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var obj in assets)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            string folderPath = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string name = Path.GetFileNameWithoutExtension(path);

            if (!dict.TryGetValue(folderPath, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                dict.Add(folderPath, set);
            }

            set.Add(name);
        }

        return dict;
    }

    private readonly struct RenamePreview
    {
        public readonly UnityEngine.Object asset;
        public readonly string oldName;
        public readonly string newName;
        public readonly string note;

        public RenamePreview(UnityEngine.Object asset, string oldName, string newName, string note)
        {
            this.asset = asset;
            this.oldName = oldName;
            this.newName = newName;
            this.note = note;
        }
    }
}
