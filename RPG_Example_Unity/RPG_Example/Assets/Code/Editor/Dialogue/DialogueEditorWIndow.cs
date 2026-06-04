// ─────────────────────────────────────────────────────────────────────────────
// DialogueEditorWindow.cs
// Three tabs: Info | Node Editor | Graph View
//
// Layout: Left sidebar (all DialogueGraph assets) | Tab content
// Graph View: Canvas (centre) | Inspector panel (right)
//
// Bugs fixed in this version:
//   [1] NullRef _graphSO           — EnsureGraphSO() recreates stale SOs
//   [2] Node Editor scroll/height  — GUILayout.BeginArea(2000) replaced by
//       two-pass height measurement via _nodeHeights dictionary
//   [3] Graph toolbar buttons      — drawn OUTSIDE GUI.BeginClip so hit-test
//       coordinates are absolute, not clip-local
//   [4] isRouter not toggleable    — single so.Update() at top + single
//       ApplyModifiedProperties at bottom; no mid-draw Update() that discards changes
//   [5] Option name not editable   — TextField added to option header row
//   [6] + Choice Router button     — creates Choice node with isRouter preset
//   [7] EstimateHeight per option  — uses _optFolds to sum real heights
//   [8] LocalizedString data loss  — never call so.Update() inside draw loop;
//       each SO is updated once at entry, applied once at exit
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class DialogueEditorWindow : EditorWindow
{
    // ── Entry points ──────────────────────────────────────────────────────────

    public static void Open(DialogueGraph graph)
    {
        var win = GetWindow<DialogueEditorWindow>("Dialogue Editor");
        win.minSize = new Vector2(820, 500);
        win.Load(graph);
        win.Show();
    }

    [UnityEditor.Callbacks.OnOpenAsset]
    private static bool OnOpenAsset(int instanceID, int line)
    {
        var obj = EditorUtility.EntityIdToObject(instanceID) as DialogueGraph;
        if (obj == null) return false;
        Open(obj);
        return true;
    }

    // ── Palette ───────────────────────────────────────────────────────────────

    private static class Pal
    {
        public static readonly Color BgDark      = new Color(0.13f, 0.13f, 0.13f);
        public static readonly Color BgPanel     = new Color(0.18f, 0.18f, 0.18f);
        public static readonly Color BgNode      = new Color(0.22f, 0.22f, 0.25f);
        public static readonly Color BgNodeSel   = new Color(0.20f, 0.35f, 0.55f);
        public static readonly Color BgHeader    = new Color(0.16f, 0.16f, 0.19f);
        public static readonly Color AccentText   = new Color(0.28f, 0.55f, 0.85f);
        public static readonly Color AccentChoice = new Color(0.70f, 0.50f, 0.15f);
        public static readonly Color AccentRouter = new Color(0.55f, 0.20f, 0.75f);
        public static readonly Color AccentEnd    = new Color(0.55f, 0.20f, 0.20f);
        public static readonly Color AccentRandom = new Color(0.20f, 0.55f, 0.55f);
        public static readonly Color AccentStart  = new Color(0.20f, 0.65f, 0.30f);
        public static readonly Color TxtMain   = new Color(0.90f, 0.90f, 0.90f);
        public static readonly Color TxtMuted  = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color TxtAccent = new Color(0.45f, 0.75f, 1.00f);
        public static readonly Color WireNormal = new Color(0.55f, 0.55f, 0.65f, 0.85f);
        public static readonly Color WireHover  = new Color(0.65f, 0.85f, 1.00f, 0.95f);
        public static readonly Color BtnRed   = new Color(0.75f, 0.25f, 0.25f);
        public static readonly Color GridLine  = new Color(1f, 1f, 1f, 0.04f);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    private const float TAB_H      = 30f;
    private const float TOOLBAR_H  = 28f;
    private const float SIDEBAR_W  = 170f;
    private const float PANEL_W    = 340f;
    private const float NODE_W     = 160f;
    private const float NODE_H     = 52f;
    private const float NODE_H_CHC = 68f;
    private const float GRID_S     = 20f;
    private const float GRID_L     = 100f;
    private const float PORT_R     = 5f;
    private const float MIN_ZOOM   = 0.4f;
    private const float MAX_ZOOM   = 2.0f;

    // ── State ─────────────────────────────────────────────────────────────────

    private DialogueGraph    _graph;
    private SerializedObject _graphSO;
    private int              _tab;

    // Canvas
    private Vector2 _canvasOffset = Vector2.zero;
    private float   _canvasZoom   = 1f;
    private string  _selectedNodeId;
    private string  _draggingNodeId;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartNodePos;

    // Wiring
    private bool    _wiringActive;
    private string  _wiringFromId;
    private int     _wiringFromOpt;
    private Vector2 _wiringMouse;

    // Right panel scroll
    private Vector2 _panelScroll;

    // Scrolls
    private Vector2 _editorScroll;
    private Vector2 _infoScroll;

    // Two-pass height measurement for Node Editor rows.
    // Populated during EventType.Layout, consumed during Repaint.
    // Without this, GUILayout.BeginArea(fixedHeight) forces a rigid container that
    // breaks scroll — content larger than the area gets silently clipped.
    private readonly Dictionary<string, float> _nodeHeights = new();

    // Sidebar
    private Vector2             _sidebarScroll;
    private string              _sidebarSearch     = string.Empty;
    private List<DialogueGraph> _allGraphs;
    private double              _lastScanTime      = -1;

    // SO caches
    private readonly Dictionary<string, SerializedObject> _nodeSOs   = new();
    private readonly Dictionary<int,    SerializedObject> _optionSOs = new();
    private readonly Dictionary<int,    SerializedObject> _subSOs    = new();
    private readonly Dictionary<int,    bool>             _subFolds  = new();
    private readonly Dictionary<string, bool>             _nodeFolds = new();
    private readonly Dictionary<string, bool>             _optFolds  = new();

    // Deferred mutations — executed BEFORE the next draw frame to avoid mid-loop changes
    private DialogueNode   _pendingDeleteNode;
    private DialogueOption _pendingDeleteOpt;
    private DialogueNode   _pendingDeleteOptParent;

    // Styles
    private GUIStyle _styleTab, _styleTabSel;
    private GUIStyle _styleSectionLbl;
    private GUIStyle _styleInlineBox;
    private GUIStyle _stylePinnedBtn;
    private bool     _stylesBuilt;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity callbacks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        titleContent = new GUIContent("Dialogue Editor",
            EditorGUIUtility.IconContent("d_UnityEditor.HierarchyWindow").image);
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

    private void OnFocus() => _lastScanTime = -1; // force sidebar rescan

    private void OnUndoRedo()
    {
        _nodeSOs.Clear();
        _optionSOs.Clear();
        _subSOs.Clear();
        _nodeHeights.Clear();
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Load
    // ─────────────────────────────────────────────────────────────────────────

    private void Load(DialogueGraph graph)
    {
        _graph   = graph;
        _graphSO = graph != null ? new SerializedObject(graph) : null;
        _nodeSOs.Clear();
        _optionSOs.Clear();
        _subSOs.Clear();
        _nodeHeights.Clear();
        _nodeFolds.Clear();
        _optFolds.Clear();
        _selectedNodeId         = null;
        _pendingDeleteNode      = null;
        _pendingDeleteOpt       = null;
        _pendingDeleteOptParent = null;

        AutoLayoutNewNodes();

        // Auto-select start node so right panel shows something on first open
        if (graph != null && !string.IsNullOrEmpty(graph.startNodeId))
            _selectedNodeId = graph.startNodeId;
    }

    // Re-create SerializedObject if it became stale (domain reload / asset reimport).
    // This is the root cause of the NullReferenceException in DrawInfoTab:
    // _graphSO.FindProperty() throws when targetObject was destroyed.
    private SerializedObject EnsureGraphSO()
    {
        if (_graphSO == null || _graphSO.targetObject == null)
            _graphSO = _graph != null ? new SerializedObject(_graph) : null;
        return _graphSO;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto layout
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoLayoutNewNodes()
    {
        if (_graph == null) return;

        float xs = NODE_W + 40f, ys = NODE_H + 30f;
        var placed  = new HashSet<string>();
        var visited = new HashSet<string>();

        foreach (var n in _graph.nodes)
        {
            if (n != null && n.editorPosition != Vector2.zero)
                placed.Add(n.id);
        }

        visited.UnionWith(placed);
        var colRow  = new Dictionary<(int, int), bool>();
        var queue   = new Queue<(string, int, int)>();

        foreach (var n in _graph.nodes)
        {
            if (n != null && !placed.Contains(n.id) && !visited.Contains(n.id))
                queue.Enqueue((n.id, 0, 0));
        }

        if (!string.IsNullOrEmpty(_graph.startNodeId) && !visited.Contains(_graph.startNodeId))
            queue.Enqueue((_graph.startNodeId, 0, 0));

        var nodeMap = BuildNodeMap();

        while (queue.Count > 0)
        {
            var (id, col, row) = queue.Dequeue();
            if (visited.Contains(id)) continue;
            visited.Add(id);
            if (!nodeMap.TryGetValue(id, out var node)) continue;

            while (colRow.ContainsKey((col, row))) row++;
            colRow[(col, row)] = true;

            Undo.RecordObject(node, "Auto-layout");
            node.editorPosition = new Vector2(col * xs + 60f, row * ys + 80f);
            EditorUtility.SetDirty(node);

            if (!string.IsNullOrEmpty(node.nextNodeId))
                queue.Enqueue((node.nextNodeId, col, row + 1));

            if (node.options != null)
            {
                int c = col;
                foreach (var opt in node.options)
                {
                    if (opt != null && !string.IsNullOrEmpty(opt.nextNodeId))
                        queue.Enqueue((opt.nextNodeId, c++, row + 1));
                }
            }
        }

        if (_graph != null) EditorUtility.SetDirty(_graph);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        RefreshGraphList();
        FlushPendingOps(); // must run before any draw, never inside a loop

        // Left sidebar — always visible
        DrawSidebar(new Rect(0, 0, SIDEBAR_W, position.height));

        var main = new Rect(SIDEBAR_W, 0, position.width - SIDEBAR_W, position.height);

        if (_graph == null || EnsureGraphSO() == null)
        {
            DrawEmptyState(main);
            return;
        }

        _graphSO.Update();

        DrawTabBar(new Rect(main.x, 0, main.width, TAB_H));

        var content = new Rect(main.x, TAB_H, main.width, position.height - TAB_H);
        switch (_tab)
        {
            case 0: DrawInfoTab(content);   break;
            case 1: DrawEditorTab(content); break;
            case 2: DrawGraphTab(content);  break;
        }

        _graphSO.ApplyModifiedProperties();
        if (GUI.changed) EditorUtility.SetDirty(_graph);
    }

    // Pending ops are deferred to avoid mutating collections mid-draw, which causes
    // GUILayout group mismatch errors ("more Begin than End" assertions).
    private void FlushPendingOps()
    {
        if (_pendingDeleteNode != null)
        {
            var n = _pendingDeleteNode;
            _pendingDeleteNode = null;
            InvalidateNodeSO(n.id);
            _nodeHeights.Remove(n.id);
            if (_selectedNodeId == n.id) _selectedNodeId = null;
            _graph.Editor_DeleteNode(n);
            Repaint();
        }

        if (_pendingDeleteOpt != null && _pendingDeleteOptParent != null)
        {
            var opt    = _pendingDeleteOpt;
            var parent = _pendingDeleteOptParent;
            _pendingDeleteOpt       = null;
            _pendingDeleteOptParent = null;
            RemoveOption(_graph, parent, GetNodeSO(parent), opt);
            Repaint();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab bar
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawTabBar(Rect bar)
    {
        EditorGUI.DrawRect(bar, Pal.BgHeader);
        GUI.Label(new Rect(bar.x + 8f, bar.y, 200f, TAB_H), _graph.name,
            new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = Pal.TxtMuted }, fontSize = 11, alignment = TextAnchor.MiddleLeft });

        string[] lbls = { "  ★ Info  ", "  ✎ Node Editor  ", "  ◈ Graph View  " };
        float tw = 140f, total = tw * lbls.Length;
        float sx = bar.x + (bar.width - total) * 0.5f;

        for (int i = 0; i < lbls.Length; i++)
        {
            var r   = new Rect(sx + i * tw, bar.y, tw, TAB_H);
            bool sel = _tab == i;
            EditorGUI.DrawRect(r, sel ? Pal.AccentText * 0.35f : Color.clear);
            if (sel) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), Pal.AccentText);
            if (GUI.Button(r, lbls[i], sel ? _styleTabSel : _styleTab)) _tab = i;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 0 — Info
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawInfoTab(Rect area)
    {
        var gso = EnsureGraphSO();
        if (gso == null) return;  // [1] guard — prevents NullRef if SO became stale

        EditorGUI.DrawRect(area, Pal.BgPanel);
        _infoScroll = GUI.BeginScrollView(area, _infoScroll,
            new Rect(0, 0, area.width - 16f, 900f));

        float y = 20f, cx = area.width * 0.5f, pad = 20f;

        // Illustration preview
        float imgSz = 96f;
        var ir = new Rect(cx - imgSz * 0.5f, y, imgSz, imgSz);
        if (_graph.characterIllustration != null)
            GUI.DrawTexture(ir, _graph.characterIllustration.texture, ScaleMode.ScaleToFit);
        else
        {
            EditorGUI.DrawRect(ir, Pal.BgNode);
            GUI.Label(ir, "No Icon", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                { normal = { textColor = Pal.TxtMuted }, alignment = TextAnchor.MiddleCenter });
        }
        y += imgSz + 10f;

        // Character fields.
        // GUILayout.BeginArea gives PropertyField the layout context it needs so
        // the ObjectPicker callback can resolve which SerializedObject to write to.
        // Without this, PropertyField drawn with a manual Rect inside a ScrollView
        // loses the picker result silently — making the field appear read-only.
        float fw = 240f;
        gso.Update();
        GUILayout.BeginArea(new Rect(cx - fw * 0.5f, y, fw, 80f));
        EditorGUILayout.PropertyField(gso.FindProperty("characterIllustration"), new GUIContent("Illustration"));
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(gso.FindProperty("characterName"), new GUIContent("Name"));
        GUILayout.EndArea();
        gso.ApplyModifiedProperties();
        y += 80f;

        // Separator
        EditorGUI.DrawRect(new Rect(pad, y, area.width - pad * 2f - 16f, 1f), Pal.GridLine * 3f);
        y += 12f;

        // Start node
        GUI.Label(new Rect(pad, y, 120f, 18f), "Start Node", EditorStyles.boldLabel);
        y += 20f;
        DrawStartNodeSelector(new Rect(pad, y, area.width - pad * 2f - 16f, 20f));
        y += 30f;

        // Pinned nodes
        EditorGUI.DrawRect(new Rect(pad, y, area.width - pad * 2f - 16f, 1f), Pal.GridLine * 3f);
        y += 12f;
        GUI.Label(new Rect(pad, y, 200f, 18f), "Pinned Nodes", EditorStyles.boldLabel);
        y += 22f;
        DrawPinnedNodes(pad, y, area.width - 16f);

        GUI.EndScrollView();
    }

    private void DrawPinnedNodes(float x, float y, float w)
    {
        if (_graph.pinnedNodeIds == null) _graph.pinnedNodeIds = new List<string>();
        var map = BuildNodeMap();

        string toUnpin = null, toJump = null;

        foreach (var pid in _graph.pinnedNodeIds)
        {
            map.TryGetValue(pid, out var pn);
            string lbl = pn != null
                ? $"[{pn.nodeType}]  {(string.IsNullOrWhiteSpace(pn.name) ? pn.id : pn.name)}"
                : $"(missing: {pid})";

            var row = new Rect(x, y, w - 16f, 26f);
            EditorGUI.DrawRect(row, Pal.BgNode);
            if (GUI.Button(new Rect(x + 4f, y + 3f, row.width - 32f, 20f), lbl, _stylePinnedBtn))
                toJump = pid;

            Color prev = GUI.color;
            GUI.color = Pal.BtnRed;
            if (GUI.Button(new Rect(row.xMax - 26f, y + 3f, 22f, 20f), "✕")) toUnpin = pid;
            GUI.color = prev;
            y += 30f;
        }

        if (toUnpin != null)
        {
            Undo.RecordObject(_graph, "Unpin Node");
            _graph.pinnedNodeIds.Remove(toUnpin);
            EditorUtility.SetDirty(_graph);
        }

        if (toJump != null) { _tab = 1; _nodeFolds[toJump] = true; }

        var allLbls = new List<string> { "— Pin a node —" };
        var allIds  = new List<string> { null };
        foreach (var n in _graph.nodes)
        {
            if (n == null || _graph.pinnedNodeIds.Contains(n.id)) continue;
            allLbls.Add($"[{n.nodeType}]  {(string.IsNullOrWhiteSpace(n.name) ? n.id : n.name)}");
            allIds.Add(n.id);
        }

        int sel = EditorGUI.Popup(new Rect(x, y, w - 16f, 22f), 0, allLbls.ToArray());
        if (sel > 0 && allIds[sel] != null)
        {
            Undo.RecordObject(_graph, "Pin Node");
            _graph.pinnedNodeIds.Add(allIds[sel]);
            EditorUtility.SetDirty(_graph);
        }
    }

    private void DrawStartNodeSelector(Rect r)
    {
        var ids = new List<string> { string.Empty };
        var lbs = new List<string> { "— None —" };
        foreach (var n in _graph.nodes)
        {
            if (n == null) continue;
            ids.Add(n.id);
            lbs.Add($"[{n.nodeType}]  {(string.IsNullOrWhiteSpace(n.name) ? n.id : n.name)}");
        }
        int cur  = ids.IndexOf(_graph.startNodeId);
        if (cur < 0) cur = 0;
        int next = EditorGUI.Popup(r, cur, lbs.ToArray());
        string id = next > 0 ? ids[next] : string.Empty;
        if (id != _graph.startNodeId)
        {
            Undo.RecordObject(_graph, "Change Start Node");
            _graph.startNodeId = id;
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 1 — Node Editor
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawEditorTab(Rect area)
    {
        EditorGUI.DrawRect(area, Pal.BgPanel);

        // Toolbar
        var tb = new Rect(area.x, area.y, area.width, TOOLBAR_H);
        EditorGUI.DrawRect(tb, Pal.BgHeader);

        float bx = area.x + 8f, by = area.y + (TOOLBAR_H - 22f) * 0.5f;

        GUI.Label(new Rect(bx, by, 36f, 22f), "Add:", EditorStyles.boldLabel);
        bx += 40f;

        if (GUI.Button(new Rect(bx, by, 64f, 22f), "+ Text"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Text);   PlaceNewNode(n); }
        bx += 68f;

        if (GUI.Button(new Rect(bx, by, 74f, 22f), "+ Choice"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Choice); PlaceNewNode(n); }
        bx += 78f;

        if (GUI.Button(new Rect(bx, by, 108f, 22f), "+ Choice Router"))
        {
            var n = _graph.Editor_CreateNode(DialogueNodeType.Choice);
            Undo.RecordObject(n, "Create Router");
            n.isRouter = true;
            EditorUtility.SetDirty(n);
            PlaceNewNode(n);
        }
        bx += 112f;

        if (GUI.Button(new Rect(bx, by, 56f, 22f), "+ End"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.End); PlaceNewNode(n); }
        bx += 60f;

        if (GUI.Button(new Rect(bx, by, 76f, 22f), "+ Random"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Random); PlaceNewNode(n); }

        // Scroll area.
        // Content height is computed from _nodeHeights populated during EventType.Layout.
        // This fixes the broken scroll caused by GUILayout.BeginArea(2000) which forced a
        // rigid 2000px container — the scroll view couldn't know the real content size.
        var list = new Rect(area.x, area.y + TOOLBAR_H, area.width, area.height - TOOLBAR_H);

        float totalH = 8f;
        if (_graph.nodes != null)
        {
            foreach (var n in _graph.nodes)
            {
                if (n == null) continue;
                _nodeHeights.TryGetValue(n.id, out float nh);
                totalH += nh + 6f;
            }
        }
        totalH += 20f;

        var content = new Rect(0, 0, list.width - 20f, Mathf.Max(totalH, list.height));
        _editorScroll = GUI.BeginScrollView(list, _editorScroll, content);

        float y = 8f;
        if (_graph.nodes == null || _graph.nodes.Count == 0)
        {
            GUI.Label(new Rect(20f, y, 400f, 22f), "No nodes yet — use toolbar above.", EditorStyles.helpBox);
        }
        else
        {
            foreach (var node in new List<DialogueNode>(_graph.nodes))
            {
                if (node == null) continue;
                DrawEditorNode(node, 8f, y, content.width - 16f);
                _nodeHeights.TryGetValue(node.id, out float nh);
                y += nh + 6f;
            }
        }

        GUI.EndScrollView();
    }

    private void DrawEditorNode(DialogueNode node, float x, float y, float w)
    {
        bool isStart = node.id == _graph.startNodeId;
        if (!_nodeFolds.ContainsKey(node.id)) _nodeFolds[node.id] = false;

        Color accent = Accent(node, isStart);

        // Header background
        EditorGUI.DrawRect(new Rect(x, y, w, 28f), Pal.BgHeader);
        EditorGUI.DrawRect(new Rect(x, y, 3f, 28f), accent);

        float hx = x + 8f;

        // Foldout
        bool fold    = _nodeFolds[node.id];
        bool newFold = EditorGUI.Foldout(new Rect(hx, y + 5f, 14f, 18f), fold, GUIContent.none);
        if (newFold != fold)
        {
            _nodeFolds[node.id] = newFold;
            _nodeHeights.Remove(node.id); // force re-measure on next Layout pass
        }
        hx += 16f;

        // Badge
        string badge = node.isRouter ? "RTE" : node.nodeType == DialogueNodeType.Text ? "TXT"
                     : node.nodeType == DialogueNodeType.Choice ? "CHC"
                     : node.nodeType == DialogueNodeType.Random ? "RND" : "END";
        GUI.Label(new Rect(hx, y + 6f, 32f, 16f), badge,
            new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = accent }, fontStyle = FontStyle.Bold, fontSize = 9 });
        hx += 34f;

        string dispName = string.IsNullOrWhiteSpace(node.name) ? "(Unnamed)" : node.name;
        if (isStart) dispName += "  ★";
        GUI.Label(new Rect(hx, y + 5f, w - hx - 170f, 18f), dispName, EditorStyles.boldLabel);

        // Buttons right side
        float bx = x + w - 4f;
        bx -= 22f;
        Color prev = GUI.color; GUI.color = Pal.BtnRed;
        if (GUI.Button(new Rect(bx, y + 4f, 18f, 20f), "✕")) _pendingDeleteNode = node;
        GUI.color = prev;

        bx -= 70f;
        if (!isStart && GUI.Button(new Rect(bx, y + 4f, 66f, 20f), "Set Start"))
            _graph.Editor_SetStartNode(node);

        bx -= 60f;
        if (GUI.Button(new Rect(bx, y + 4f, 56f, 20f), "To Graph"))
        {
            _tab = 2; _selectedNodeId = node.id; CenterOn(node);
        }

        // Body — measured with GUILayout for correct scroll size
        if (_nodeFolds[node.id])
        {
            // Background drawn first so it appears behind content
            EditorGUI.DrawRect(new Rect(x, y + 28f, w, 4000f), Pal.BgNode * 0.55f);

            GUILayout.BeginArea(new Rect(x + 8f, y + 30f, w - 16f, 4000f));
            var measured = EditorGUILayout.BeginVertical();
            DrawNodeBody(node);
            EditorGUILayout.EndVertical();

            // Store height after layout pass; used next frame for scroll content sizing
            if (Event.current.type == EventType.Repaint && measured.height > 1f)
                _nodeHeights[node.id] = measured.height + 36f;

            GUILayout.EndArea();
        }
        else
        {
            _nodeHeights[node.id] = 28f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 2 — Graph View
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawGraphTab(Rect area)
    {
        float canvasW = area.width - PANEL_W;
        var canvas    = new Rect(area.x,              area.y, canvasW,  area.height);
        var panel     = new Rect(area.x + canvasW,    area.y, PANEL_W,  area.height);

        DrawCanvas(canvas);
        DrawInspectorPanel(panel);
    }

    // ── Canvas ────────────────────────────────────────────────────────────────

    private void DrawCanvas(Rect area)
    {
        EditorGUI.DrawRect(area, Pal.BgDark);

        // Input BEFORE BeginClip so coordinates are absolute (window-space).
        HandleCanvasInput(area);

        DrawGrid(area);

        // Toolbar also OUTSIDE BeginClip.
        // If drawn inside BeginClip, buttons use clip-local coords for rendering
        // but window-space coords for input — causing a mismatch where buttons
        // appear in the right place but never respond to clicks.
        DrawCanvasToolbar(area);

        GUI.BeginClip(area);

        DrawAllWires();

        if (_wiringActive)
        {
            var fn = FindNode(_wiringFromId);
            if (fn != null)
                DrawBezier(C2S(OutPort(fn, _wiringFromOpt)), _wiringMouse, Pal.WireHover, 2.5f);
        }

        DrawAllNodes();

        GUI.EndClip();
    }

    // ── Inspector panel ───────────────────────────────────────────────────────

    private void DrawInspectorPanel(Rect area)
    {
        // Separator
        EditorGUI.DrawRect(new Rect(area.x, area.y, 1f, area.height), Pal.GridLine * 3f);
        EditorGUI.DrawRect(new Rect(area.x + 1f, area.y, area.width - 1f, area.height), Pal.BgPanel);

        // Resolve which node to show: selected → start → nothing
        DialogueNode node = null;
        if (!string.IsNullOrEmpty(_selectedNodeId)) node = FindNode(_selectedNodeId);
        if (node == null && !string.IsNullOrEmpty(_graph.startNodeId))
            node = FindNode(_graph.startNodeId);

        if (node == null)
        {
            GUI.Label(new Rect(area.x + 10f, area.y + 20f, area.width - 20f, 40f),
                "Select a node to inspect.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Color accent = Accent(node, node.id == _graph.startNodeId);

        // Header
        EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, 28f), Pal.BgHeader);
        EditorGUI.DrawRect(new Rect(area.x, area.y, 3f, 28f), accent);

        string badge = node.isRouter ? "RTE" : node.nodeType == DialogueNodeType.Text ? "TXT"
                     : node.nodeType == DialogueNodeType.Choice ? "CHC"
                     : node.nodeType == DialogueNodeType.Random ? "RND" : "END";
        GUI.Label(new Rect(area.x + 8f, area.y + 5f, 34f, 18f), badge,
            new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = accent }, fontStyle = FontStyle.Bold, fontSize = 9 });

        string hdr = string.IsNullOrWhiteSpace(node.name) ? node.id : node.name;
        if (node.id == _graph.startNodeId) hdr += " ★";
        GUI.Label(new Rect(area.x + 44f, area.y + 5f, area.width - 54f, 18f), hdr, EditorStyles.boldLabel);

        // Scrollable body
        float estH  = EstimateHeight(node) + 100f;
        var   outer = new Rect(area.x, area.y + 28f, area.width, area.height - 28f);
        var   inner = new Rect(0, 0, area.width - 16f, Mathf.Max(estH, outer.height));

        _panelScroll = GUI.BeginScrollView(outer, _panelScroll, inner);
        GUILayout.BeginArea(new Rect(6f, 6f, area.width - 22f, Mathf.Max(estH, outer.height)));
        DrawNodeBody(node);
        GUILayout.EndArea();
        GUI.EndScrollView();
    }

    // ── Grid ──────────────────────────────────────────────────────────────────

    private void DrawGrid(Rect area)
    {
        DrawGridLines(area, GRID_S * _canvasZoom, Pal.GridLine);
        DrawGridLines(area, GRID_L * _canvasZoom, Pal.GridLine * 2.5f);
    }

    private void DrawGridLines(Rect area, float sp, Color col)
    {
        if (Event.current.type != EventType.Repaint || sp < 4f) return;
        Handles.BeginGUI();
        Handles.color = col;
        // Offset lines by area.x/area.y: Handles.DrawLine uses window-space coords,
        // so without the offset the grid renders at (0,0) — under the sidebar.
        float ox = area.x + _canvasOffset.x % sp;
        float oy = area.y + _canvasOffset.y % sp;
        for (float x = ox; x < area.xMax; x += sp)
            Handles.DrawLine(new Vector3(x, area.y), new Vector3(x, area.yMax));
        for (float y = oy; y < area.yMax; y += sp)
            Handles.DrawLine(new Vector3(area.x, y), new Vector3(area.xMax, y));
        Handles.EndGUI();
    }

    // ── Canvas input ──────────────────────────────────────────────────────────

    private void HandleCanvasInput(Rect area)
    {
        var e = Event.current;
        if (!area.Contains(e.mousePosition)) return;

        // Ignore events inside the right inspector panel region
        if (e.mousePosition.x > area.x + area.width - PANEL_W) return;

        // Zoom
        if (e.type == EventType.ScrollWheel)
        {
            float nz = Mathf.Clamp(_canvasZoom - e.delta.y * 0.05f, MIN_ZOOM, MAX_ZOOM);
            Vector2 mc = S2C(e.mousePosition - new Vector2(area.x, area.y));
            _canvasZoom = nz;
            _canvasOffset += (e.mousePosition - new Vector2(area.x, area.y)) - C2S(mc);
            e.Use(); Repaint();
        }

        // Pan
        bool pan = (e.type == EventType.MouseDrag && e.button == 2) ||
                   (e.type == EventType.MouseDrag && e.button == 0 && e.alt);
        if (pan) { _canvasOffset += e.delta; e.Use(); Repaint(); }

        // Right-click context menu on empty canvas
        if (e.type == EventType.ContextClick)
        {
            string hit = HitTest(e.mousePosition - new Vector2(area.x, area.y));
            if (hit == null)
            {
                var cp   = S2C(e.mousePosition - new Vector2(area.x, area.y));
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Text Node"),          false, () => SpawnAt(DialogueNodeType.Text,   false, cp));
                menu.AddItem(new GUIContent("Add Choice Node"),        false, () => SpawnAt(DialogueNodeType.Choice, false, cp));
                menu.AddItem(new GUIContent("Add Choice Router Node"), false, () => SpawnAt(DialogueNodeType.Choice, true,  cp));
                menu.AddItem(new GUIContent("Add End Node"),           false, () => SpawnAt(DialogueNodeType.End,    false, cp));
                menu.AddItem(new GUIContent("Add Random Node"),        false, () => SpawnAt(DialogueNodeType.Random, false, cp));
                menu.ShowAsContext();
                e.Use();
            }
        }

        // Left mouse down
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Vector2 lm = e.mousePosition - new Vector2(area.x, area.y);

            if (TryHitPort(lm, out string pid, out int poi))
            {
                _wiringActive = true; _wiringFromId = pid; _wiringFromOpt = poi; _wiringMouse = lm;
                e.Use(); return;
            }

            string hid = HitTest(lm);
            if (hid != null)
            {
                _selectedNodeId  = hid;
                var hn           = FindNode(hid);
                _draggingNodeId  = hid;
                _dragStartMouse  = e.mousePosition;
                _dragStartNodePos = hn != null ? hn.editorPosition : Vector2.zero;
            }
            else
            {
                _selectedNodeId = null;
            }
            e.Use();
        }

        // Drag
        if (e.type == EventType.MouseDrag && e.button == 0 && _draggingNodeId != null && !e.alt)
        {
            var dn = FindNode(_draggingNodeId);
            if (dn != null)
            {
                Vector2 delta = (e.mousePosition - _dragStartMouse) / _canvasZoom;
                Vector2 np    = _dragStartNodePos + delta;
                np.x = Mathf.Round(np.x / 10f) * 10f;
                np.y = Mathf.Round(np.y / 10f) * 10f;
                Undo.RecordObject(dn, "Move Node");
                dn.editorPosition = np;
                EditorUtility.SetDirty(dn);
            }
            e.Use(); Repaint();
        }

        // Mouse up
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_wiringActive)
            {
                Vector2 lm = e.mousePosition - new Vector2(area.x, area.y);
                string  t  = HitTest(lm);
                if (t != null && t != _wiringFromId) ConnectNodes(_wiringFromId, _wiringFromOpt, t);
                _wiringActive = false;
                Repaint();
            }
            _draggingNodeId = null;
            e.Use();
        }

        // Wire mouse tracking
        if (_wiringActive && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag))
        { _wiringMouse = e.mousePosition - new Vector2(area.x, area.y); Repaint(); }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        { _wiringActive = false; e.Use(); Repaint(); }

        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) &&
            _selectedNodeId != null)
        {
            _pendingDeleteNode = FindNode(_selectedNodeId);
            e.Use();
        }
    }

    // ── Ports ─────────────────────────────────────────────────────────────────

    private bool TryHitPort(Vector2 lm, out string nodeId, out int optIdx)
    {
        nodeId = null; optIdx = -1;
        foreach (var n in _graph.nodes)
        {
            if (n == null) continue;
            if (n.nodeType != DialogueNodeType.Choice || n.options == null || n.options.Count == 0)
            {
                if (Vector2.Distance(lm, C2S(OutPort(n, -1))) <= PORT_R + 4f)
                { nodeId = n.id; return true; }
            }
            if (n.options != null)
            {
                for (int i = 0; i < n.options.Count; i++)
                {
                    if (Vector2.Distance(lm, C2S(OutPort(n, i))) <= PORT_R + 4f)
                    { nodeId = n.id; optIdx = i; return true; }
                }
            }
        }
        return false;
    }

    private void ConnectNodes(string fromId, int optIdx, string toId)
    {
        var f = FindNode(fromId);
        if (f == null) return;
        Undo.RecordObject(f, "Connect");
        if (optIdx < 0)
        {
            f.nextNodeId = toId;
        }
        else if (f.options != null && optIdx < f.options.Count)
        {
            var o = f.options[optIdx];
            if (o != null) { Undo.RecordObject(o, "Connect Option"); o.nextNodeId = toId; EditorUtility.SetDirty(o); }
        }
        EditorUtility.SetDirty(f);
        AssetDatabase.SaveAssets();
    }

    // ── Node draw ─────────────────────────────────────────────────────────────

    private void DrawAllNodes()
    {
        if (_graph?.nodes == null) return;
        foreach (var n in _graph.nodes)
        {
            if (n != null) DrawGraphNode(n);
        }
    }

    private void DrawGraphNode(DialogueNode node)
    {
        bool isSel  = node.id == _selectedNodeId;
        bool isSt   = node.id == _graph.startNodeId;
        float nodeH = node.nodeType == DialogueNodeType.Choice && node.options?.Count > 0
            ? NODE_H_CHC + (node.options.Count - 1) * 14f : NODE_H;

        Vector2 sp = C2S(node.editorPosition);
        float   sw = NODE_W * _canvasZoom, sh = nodeH * _canvasZoom;
        Rect    nr = new Rect(sp.x, sp.y, sw, sh);

        // Shadow
        EditorGUI.DrawRect(new Rect(nr.x + 3, nr.y + 3, nr.width, nr.height), new Color(0,0,0,0.35f));

        Color accent = Accent(node, isSt);
        EditorGUI.DrawRect(nr, isSel ? Pal.BgNodeSel : Pal.BgNode);
        EditorGUI.DrawRect(new Rect(nr.x, nr.y, 4f * _canvasZoom, sh), accent);
        if (isSel) DrawOutline(nr, accent, 1.5f);

        int fs  = Mathf.RoundToInt(Mathf.Clamp(11f * _canvasZoom, 7f, 13f));
        var tit = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = fs, clipping = TextClipping.Clip, normal = { textColor = Pal.TxtMain } };
        var sub = new GUIStyle(EditorStyles.miniLabel)
            { fontSize = Mathf.Max(7, Mathf.RoundToInt(9f * _canvasZoom)), clipping = TextClipping.Clip, normal = { textColor = Pal.TxtMuted } };

        float tx = nr.x + 6f * _canvasZoom, tw = nr.width - 10f * _canvasZoom;
        string tag = node.isRouter ? "ROUTER" : node.nodeType == DialogueNodeType.Text ? "TEXT"
                   : node.nodeType == DialogueNodeType.Choice ? "CHOICE"
                   : node.nodeType == DialogueNodeType.Random ? "RANDOM" : "END";
        if (isSt) tag += " ★";
        GUI.Label(new Rect(tx, nr.y + 4f * _canvasZoom, tw, 14f * _canvasZoom), tag, sub);
        GUI.Label(new Rect(tx, nr.y + 16f * _canvasZoom, tw, 14f * _canvasZoom),
            string.IsNullOrWhiteSpace(node.name) ? "(Unnamed)" : node.name, tit);

        if (node.nodeType == DialogueNodeType.Choice && node.options != null)
        {
            float oy = nr.y + 34f * _canvasZoom;
            for (int i = 0; i < node.options.Count; i++)
            {
                EditorGUI.DrawRect(new Rect(nr.x + 4f * _canvasZoom, oy,
                    nr.width - 8f * _canvasZoom, 12f * _canvasZoom), new Color(0,0,0,0.2f));
                GUI.Label(new Rect(tx, oy, tw, 12f * _canvasZoom), $"  ▸ Opt {i}", sub);
                DrawPort(C2S(OutPort(node, i)), accent);
                oy += 14f * _canvasZoom;
            }
        }

        DrawPort(C2S(InPort(node)), Pal.TxtMuted);
        if (node.nodeType != DialogueNodeType.Choice || node.options == null || node.options.Count == 0)
            DrawPort(C2S(OutPort(node, -1)), accent);

        // Right-click context
        if (Event.current.type == EventType.ContextClick && nr.Contains(Event.current.mousePosition))
        {
            string cid = node.id;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Inspect"),      false, () => _selectedNodeId = cid);
            menu.AddItem(new GUIContent("Set as Start"), false, () => _graph.Editor_SetStartNode(FindNode(cid)));
            bool pinned = _graph.pinnedNodeIds != null && _graph.pinnedNodeIds.Contains(cid);
            menu.AddItem(new GUIContent(pinned ? "Unpin" : "Pin to Info"), false, () =>
            {
                if (_graph.pinnedNodeIds == null) _graph.pinnedNodeIds = new List<string>();
                Undo.RecordObject(_graph, "Toggle Pin");
                if (pinned) _graph.pinnedNodeIds.Remove(cid); else _graph.pinnedNodeIds.Add(cid);
                EditorUtility.SetDirty(_graph);
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => _pendingDeleteNode = FindNode(cid));
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    private void DrawPort(Vector2 sp, Color col)
    {
        if (Event.current.type != EventType.Repaint) return;
        float r = PORT_R * _canvasZoom;
        EditorGUI.DrawRect(new Rect(sp.x - r, sp.y - r, r * 2f, r * 2f), col);
    }

    // ── Wires ─────────────────────────────────────────────────────────────────

    private void DrawAllWires()
    {
        if (Event.current.type != EventType.Repaint || _graph?.nodes == null) return;
        foreach (var node in _graph.nodes)
        {
            if (node == null) continue;
            if (!string.IsNullOrEmpty(node.nextNodeId))
            {
                var t = FindNode(node.nextNodeId);
                if (t != null) DrawBezier(C2S(OutPort(node, -1)), C2S(InPort(t)), Pal.WireNormal, 1.5f);
            }
            if (node.options != null)
            {
                for (int i = 0; i < node.options.Count; i++)
                {
                    var o = node.options[i];
                    if (o == null || string.IsNullOrEmpty(o.nextNodeId)) continue;
                    var t = FindNode(o.nextNodeId);
                    if (t != null) DrawBezier(C2S(OutPort(node, i)), C2S(InPort(t)),
                        Accent(node, false) * 0.8f, 1.5f);
                }
            }
        }
    }

    private void DrawBezier(Vector2 from, Vector2 to, Color col, float w)
    {
        float d = Mathf.Abs(to.y - from.y) * 0.5f + 30f;
        Handles.BeginGUI();
        Handles.DrawBezier(from, to,
            new Vector3(from.x, from.y + d, 0), new Vector3(to.x, to.y - d, 0), col, null, w);
        Handles.EndGUI();
    }

    // ── Canvas toolbar — drawn OUTSIDE GUI.BeginClip ──────────────────────────
    // CRITICAL: toolbar must be drawn before GUI.BeginClip(canvas).
    // Inside BeginClip, GUI draws in clip-local space but input events still use
    // window space, so buttons visually appear correct but never receive clicks.

    private void DrawCanvasToolbar(Rect canvasArea)
    {
        // canvasArea is already the canvas rect (PANEL_W already excluded by caller).
        // Do NOT subtract PANEL_W again — that would shift the toolbar rect leftward,
        // making all buttons appear behind the sidebar and never receive clicks.
        float canvasW = canvasArea.width;
        float th      = TOOLBAR_H;
        var   tbR     = new Rect(canvasArea.x, canvasArea.yMax - th, canvasW, th);

        EditorGUI.DrawRect(tbR, Pal.BgHeader);

        float bx = tbR.x + 6f, by = tbR.y + 3f;

        if (GUI.Button(new Rect(bx, by, 62f, 22f), "+ Text"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Text);   PlaceNewNode(n); } bx += 66f;

        if (GUI.Button(new Rect(bx, by, 72f, 22f), "+ Choice"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Choice); PlaceNewNode(n); } bx += 76f;

        if (GUI.Button(new Rect(bx, by, 104f, 22f), "+ Choice Router"))
        {
            var n = _graph.Editor_CreateNode(DialogueNodeType.Choice);
            Undo.RecordObject(n, "Create Router");
            n.isRouter = true; EditorUtility.SetDirty(n);
            PlaceNewNode(n);
        }
        bx += 108f;

        if (GUI.Button(new Rect(bx, by, 56f, 22f), "+ End"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.End); PlaceNewNode(n); } bx += 64f;

        if (GUI.Button(new Rect(bx, by, 76f, 22f), "+ Random"))
        { var n = _graph.Editor_CreateNode(DialogueNodeType.Random); PlaceNewNode(n); } bx += 80f;

        EditorGUI.DrawRect(new Rect(bx, by + 2f, 1f, 18f), Pal.GridLine * 4f); bx += 8f;

        if (GUI.Button(new Rect(bx, by, 84f, 22f), "Auto Layout"))
        { AutoLayoutNewNodes(); Repaint(); } bx += 88f;

        if (GUI.Button(new Rect(bx, by, 74f, 22f), "Frame All"))
        {
            // Reserve toolbar height at the bottom so Frame All doesn't cover it
            var fr = new Rect(canvasArea.x, canvasArea.y, canvasArea.width, canvasArea.height - th);
            FrameAll(fr);
        }
        bx += 78f;

        GUI.Label(new Rect(tbR.xMax - 48f, by, 44f, 22f),
            $"{_canvasZoom * 100f:F0}%", EditorStyles.centeredGreyMiniLabel);

        string hint = _wiringActive
            ? "Click a node to connect  [ ESC = cancel ]"
            : "Drag · Scroll=zoom · Mid-drag=pan · RMB=menu · Click=select+inspect";
        GUI.Label(new Rect(bx, by, 360f, 22f), hint,
            new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleLeft });
    }

    // ── Canvas math ───────────────────────────────────────────────────────────

    private Vector2 C2S(Vector2 c) => c * _canvasZoom + _canvasOffset;
    private Vector2 S2C(Vector2 s) => (s - _canvasOffset) / _canvasZoom;

    private Vector2 InPort(DialogueNode n)
        => n.editorPosition + new Vector2(NODE_W * 0.5f, 0f);

    private Vector2 OutPort(DialogueNode n, int optIdx)
    {
        if (optIdx < 0)
        {
            float h = n.nodeType == DialogueNodeType.Choice && n.options?.Count > 0
                ? NODE_H_CHC + (n.options.Count - 1) * 14f : NODE_H;
            return n.editorPosition + new Vector2(NODE_W * 0.5f, h);
        }
        return n.editorPosition + new Vector2(NODE_W, NODE_H_CHC * 0.5f + optIdx * 14f + 7f);
    }

    private string HitTest(Vector2 lm)
    {
        if (_graph?.nodes == null) return null;
        for (int i = _graph.nodes.Count - 1; i >= 0; i--)
        {
            var n = _graph.nodes[i];
            if (n == null) continue;
            float nh = n.nodeType == DialogueNodeType.Choice && n.options?.Count > 0
                ? NODE_H_CHC + (n.options.Count - 1) * 14f : NODE_H;
            Vector2 sp = C2S(n.editorPosition);
            if (new Rect(sp.x, sp.y, NODE_W * _canvasZoom, nh * _canvasZoom).Contains(lm))
                return n.id;
        }
        return null;
    }

    private void SpawnAt(DialogueNodeType type, bool router, Vector2 cp)
    {
        var n = _graph.Editor_CreateNode(type);
        if (router) { Undo.RecordObject(n, "Router"); n.isRouter = true; EditorUtility.SetDirty(n); }
        Undo.RecordObject(n, "Place"); n.editorPosition = cp; EditorUtility.SetDirty(n);
        _selectedNodeId = n.id;
        Repaint();
    }

    private void PlaceNewNode(DialogueNode n)
    {
        Vector2 c = S2C(new Vector2(position.width * 0.5f, position.height * 0.5f));
        c += new Vector2((_graph.nodes.Count % 5) * (NODE_W + 20f), 0f);
        Undo.RecordObject(n, "Place"); n.editorPosition = c; EditorUtility.SetDirty(n);
        _selectedNodeId = n.id;
        Repaint();
    }

    private void CenterOn(DialogueNode n)
    {
        _canvasOffset = new Vector2(position.width * 0.5f, position.height * 0.5f)
                      - n.editorPosition * _canvasZoom;
        Repaint();
    }

    private void FrameAll(Rect area)
    {
        if (_graph?.nodes == null || _graph.nodes.Count == 0) return;
        float mnx = float.MaxValue, mny = float.MaxValue, mxx = float.MinValue, mxy = float.MinValue;
        foreach (var n in _graph.nodes)
        {
            if (n == null) continue;
            mnx = Mathf.Min(mnx, n.editorPosition.x);
            mny = Mathf.Min(mny, n.editorPosition.y);
            mxx = Mathf.Max(mxx, n.editorPosition.x + NODE_W);
            mxy = Mathf.Max(mxy, n.editorPosition.y + NODE_H);
        }
        float gw = mxx - mnx + 80f, gh = mxy - mny + 80f;
        _canvasZoom   = Mathf.Clamp(Mathf.Min(area.width / gw, area.height / gh), MIN_ZOOM, MAX_ZOOM);
        _canvasOffset = new Vector2(area.width, area.height) * 0.5f
                      - new Vector2(mnx + gw * 0.5f, mny + gh * 0.5f) * _canvasZoom;
        Repaint();
    }

    private void DrawOutline(Rect r, Color col, float t)
    {
        if (Event.current.type != EventType.Repaint) return;
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          r.width, t),    col);
        EditorGUI.DrawRect(new Rect(r.x,          r.yMax - t,   r.width, t),    col);
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          t, r.height),   col);
        EditorGUI.DrawRect(new Rect(r.xMax - t,   r.y,          t, r.height),   col);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared node body — used by Editor tab rows AND Graph inspector panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawNodeBody(DialogueNode node)
    {
        var so = GetNodeSO(node);

        // [8] Update once at the top.  Never call Update() again mid-draw.
        // Calling Update() a second time discards any SerializedProperty changes
        // that haven't been applied yet — that's why LocalizedString data was lost
        // when folding/unfolding: the fold toggle triggered a Repaint, DrawNodeBody
        // ran again, and a rogue so.Update() wiped pending localization table changes.
        so.Update();

        EditorGUI.BeginChangeCheck();

        // Name
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name", GUILayout.Width(70f));
        string nn = EditorGUILayout.TextField(node.name);
        if (nn != node.name)
        {
            Undo.RecordObject(node, "Rename Node");
            node.name = nn;
            EditorUtility.SetDirty(node);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"ID: {node.id}", EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);

        switch (node.nodeType)
        {
            case DialogueNodeType.Text:   DrawTextBody(node, so);   break;
            case DialogueNodeType.Choice: DrawChoiceBody(node, so); break;
            case DialogueNodeType.End:    DrawEndBody(node, so);    break;
            case DialogueNodeType.Random: DrawRandomBody(node, so); break;
        }

        EditorGUILayout.Space(8f);

        // Pin button
        bool pinned = _graph.pinnedNodeIds != null && _graph.pinnedNodeIds.Contains(node.id);
        if (GUILayout.Button(pinned ? "★ Unpin from Info" : "☆ Pin to Info", GUILayout.Width(140f)))
        {
            if (_graph.pinnedNodeIds == null) _graph.pinnedNodeIds = new List<string>();
            Undo.RecordObject(_graph, "Toggle Pin");
            if (pinned) _graph.pinnedNodeIds.Remove(node.id);
            else        _graph.pinnedNodeIds.Add(node.id);
            EditorUtility.SetDirty(_graph);
        }

        EditorGUILayout.Space(6f);

        // [8] Apply once at the bottom.
        bool changed = EditorGUI.EndChangeCheck();
        if (so.ApplyModifiedProperties() || changed)
        {
            EditorUtility.SetDirty(node);
            AssetDatabase.SaveAssets();
            _nodeHeights.Remove(node.id);
        }
    }

    // ── Text node ─────────────────────────────────────────────────────────────

    private void DrawTextBody(DialogueNode node, SerializedObject so)
    {
        EditorGUILayout.PropertyField(so.FindProperty("localizedText"), new GUIContent("Text"));
        EditorGUILayout.Space(4f);
        DrawNextNodePicker(node, so.FindProperty("nextNodeId"), "Next Node");
        EditorGUILayout.Space(4f);
        DrawActionList(so.FindProperty("onEnterActions"), "On Enter Actions");
    }

    // ── Choice node ───────────────────────────────────────────────────────────

    private void DrawChoiceBody(DialogueNode node, SerializedObject so)
    {
        // Purge null options — can appear after undo or external asset deletion
        if (node.options != null)
        {
            bool had = false;
            for (int i = node.options.Count - 1; i >= 0; i--)
            {
                if (node.options[i] != null) continue;
                if (!had) { Undo.RecordObject(node, "Remove Null Options"); had = true; }
                node.options.RemoveAt(i);
            }
            // Do NOT call so.Update() here — it would discard pending changes.
            // The single Update() at the top of DrawNodeBody is sufficient.
            if (had) EditorUtility.SetDirty(node);
        }

        // [4] isRouter drawn via SerializedProperty.
        // The toggle works because so.Update() was already called at the top of DrawNodeBody,
        // so the property reflects the current asset state.
        // ApplyModifiedProperties at the bottom will persist any toggle.
        EditorGUILayout.PropertyField(so.FindProperty("isRouter"),
            new GUIContent("Is Router", "Silently picks first option whose conditions pass."));

        if (node.isRouter)
            EditorGUILayout.HelpBox("Router: first passing option is selected. No UI shown.", MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

        for (int i = 0; i < node.options.Count; i++)
        {
            var opt = node.options[i];
            if (opt == null) continue;

            string ok = $"{node.id}_opt{i}";
            if (!_optFolds.ContainsKey(ok)) _optFolds[ok] = true;

            EditorGUILayout.BeginVertical(_styleInlineBox);
            EditorGUILayout.BeginHorizontal();

            // Foldout + editable name on same row
            bool wasOpen = _optFolds[ok];
            _optFolds[ok] = EditorGUILayout.Foldout(_optFolds[ok], $"Option {i}", true);
            if (_optFolds[ok] != wasOpen) _nodeHeights.Remove(node.id);

            // [5] Editable option name — was missing, so name was always the default
            EditorGUI.BeginChangeCheck();
            string oName = EditorGUILayout.TextField(opt.name, GUILayout.MinWidth(60f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(opt, "Rename Option");
                opt.name = oName;
                EditorUtility.SetDirty(opt);
            }

            GUILayout.FlexibleSpace();
            Color prev = GUI.color; GUI.color = Pal.BtnRed;
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                GUI.color = prev;
                _pendingDeleteOpt       = opt;
                _pendingDeleteOptParent = node;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.color = prev;
            EditorGUILayout.EndHorizontal();

            if (_optFolds[ok])
            {
                EditorGUI.indentLevel++;

                // Each option gets its own SerializedObject — safe to Update/Apply independently.
                var optSO = GetOptionSO(opt);
                optSO.Update();

                // Router options have no visible text — hide the text field.
                // The option's conditions determine which branch is taken silently.
                if (!node.isRouter)
                    EditorGUILayout.PropertyField(optSO.FindProperty("localizedOptionText"), new GUIContent("Text"));
                DrawNextNodePicker(node, optSO.FindProperty("nextNodeId"), "Next Node");
                EditorGUILayout.Space(4f);
                DrawConditionList(optSO.FindProperty("conditions"));
                EditorGUILayout.Space(4f);
                DrawActionList(optSO.FindProperty("actions"), "Actions");

                if (optSO.ApplyModifiedProperties())
                { EditorUtility.SetDirty(opt); AssetDatabase.SaveAssets(); }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        if (GUILayout.Button("+ Add Option")) AddOption(_graph, node, GetNodeSO(node));
    }

    // ── End node ──────────────────────────────────────────────────────────────

    private void DrawEndBody(DialogueNode node, SerializedObject so)
    {
        EditorGUILayout.PropertyField(so.FindProperty("localizedText"), new GUIContent("Closing Text (opt.)"));
        EditorGUILayout.Space(4f);
        DrawActionList(so.FindProperty("onEnterActions"), "On Enter Actions");
    }

    // ── Random node ───────────────────────────────────────────────────────────

    private void DrawRandomBody(DialogueNode node, SerializedObject so)
    {
        EditorGUILayout.HelpBox("One entry is chosen at random at runtime.", MessageType.None);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(so.FindProperty("randomTexts"), new GUIContent("Texts"), true);
        EditorGUILayout.Space(4f);
        DrawNextNodePicker(node, so.FindProperty("nextNodeId"), "Next Node");
        EditorGUILayout.Space(4f);
        DrawActionList(so.FindProperty("onEnterActions"), "On Enter Actions");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conditions / Actions lists
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawConditionList(SerializedProperty lp)
    {
        EditorGUILayout.LabelField("── Conditions", _styleSectionLbl);
        EditorGUI.indentLevel++;

        for (int i = 0; i < lp.arraySize; i++)
        {
            var ep  = lp.GetArrayElementAtIndex(i);
            var cnd = ep.objectReferenceValue as DialogueCondition;

            EditorGUILayout.BeginVertical(_styleInlineBox);
            EditorGUILayout.BeginHorizontal();

            int fk = cnd != null ? cnd.GetInstanceID() : i;
            if (!_subFolds.ContainsKey(fk)) _subFolds[fk] = true;
            _subFolds[fk] = EditorGUILayout.Foldout(_subFolds[fk],
                cnd != null ? cnd.GetType().Name : "None", true);

            EditorGUI.BeginChangeCheck();
            var nr = EditorGUILayout.ObjectField(ep.objectReferenceValue,
                typeof(DialogueCondition), false, GUILayout.Width(150)) as DialogueCondition;
            if (EditorGUI.EndChangeCheck())
            {
                if (cnd != null) InvalidateSub(cnd);
                ep.objectReferenceValue = nr;
                lp.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(lp.serializedObject.targetObject);
                AssetDatabase.SaveAssets();
            }

            Color prev = GUI.color; GUI.color = Pal.BtnRed;
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                GUI.color = prev;
                ep.objectReferenceValue = null;
                lp.serializedObject.ApplyModifiedProperties();
                lp.DeleteArrayElementAtIndex(i);
                lp.serializedObject.ApplyModifiedProperties();
                if (cnd != null) InvalidateSub(cnd);
                EditorUtility.SetDirty(lp.serializedObject.targetObject);
                AssetDatabase.SaveAssets();
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                break;
            }
            GUI.color = prev;
            EditorGUILayout.EndHorizontal();

            if (cnd != null && _subFolds[fk])
            { EditorGUI.indentLevel++; DrawInlineFields(cnd); EditorGUI.indentLevel--; }

            EditorGUILayout.EndVertical();
        }

        EditorGUI.indentLevel--;
        if (GUILayout.Button("+ Add Condition", GUILayout.Width(130)))
            ShowTypeDropdown<DialogueCondition>(_graph, lp);
    }

    private void DrawActionList(SerializedProperty lp, string label)
    {
        EditorGUILayout.LabelField($"── {label}", _styleSectionLbl);
        EditorGUI.indentLevel++;

        for (int i = 0; i < lp.arraySize; i++)
        {
            var ep  = lp.GetArrayElementAtIndex(i);
            var act = ep.objectReferenceValue as DialogueAction;

            EditorGUILayout.BeginVertical(_styleInlineBox);
            EditorGUILayout.BeginHorizontal();

            int fk = act != null ? act.GetInstanceID() : -(i + 1);
            if (!_subFolds.ContainsKey(fk)) _subFolds[fk] = true;
            _subFolds[fk] = EditorGUILayout.Foldout(_subFolds[fk],
                act != null ? act.GetType().Name : "None", true);

            EditorGUI.BeginChangeCheck();
            var nr = EditorGUILayout.ObjectField(ep.objectReferenceValue,
                typeof(DialogueAction), false, GUILayout.Width(150)) as DialogueAction;
            if (EditorGUI.EndChangeCheck())
            {
                if (act != null) InvalidateSub(act);
                ep.objectReferenceValue = nr;
                lp.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(lp.serializedObject.targetObject);
                AssetDatabase.SaveAssets();
            }

            Color prev = GUI.color; GUI.color = Pal.BtnRed;
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                GUI.color = prev;
                ep.objectReferenceValue = null;
                lp.serializedObject.ApplyModifiedProperties();
                lp.DeleteArrayElementAtIndex(i);
                lp.serializedObject.ApplyModifiedProperties();
                if (act != null) InvalidateSub(act);
                EditorUtility.SetDirty(lp.serializedObject.targetObject);
                AssetDatabase.SaveAssets();
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                break;
            }
            GUI.color = prev;
            EditorGUILayout.EndHorizontal();

            if (act != null && _subFolds[fk])
            { EditorGUI.indentLevel++; DrawInlineFields(act); EditorGUI.indentLevel--; }

            EditorGUILayout.EndVertical();
        }

        EditorGUI.indentLevel--;
        if (GUILayout.Button("+ Add Action", GUILayout.Width(120)))
            ShowTypeDropdown<DialogueAction>(_graph, lp);
    }

    private void DrawInlineFields(ScriptableObject sub)
    {
        var so = GetSubSO(sub);
        so.Update();
        var p = so.GetIterator(); bool ec = true;
        while (p.NextVisible(ec)) { ec = false; if (p.name == "m_Script") continue; EditorGUILayout.PropertyField(p, true); }
        if (so.ApplyModifiedProperties()) { EditorUtility.SetDirty(sub); AssetDatabase.SaveAssets(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Next node picker
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawNextNodePicker(DialogueNode excludeSelf, SerializedProperty prop, string label)
    {
        var ids = new List<string> { string.Empty };
        var lbs = new List<string> { "— (end dialogue) —" };
        foreach (var n in _graph.nodes)
        {
            if (n == null || n == excludeSelf) continue;
            ids.Add(n.id);
            lbs.Add($"[{n.nodeType}] {(string.IsNullOrWhiteSpace(n.name) ? n.id : n.name)}{(n.id == _graph.startNodeId ? " ★" : "")}");
        }
        int cur  = ids.IndexOf(prop.stringValue); if (cur < 0) cur = 0;
        int next = EditorGUILayout.Popup(label, cur, lbs.ToArray());
        prop.stringValue = next == 0 ? string.Empty : ids[next];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Height estimate for right inspector panel scroll
    // ─────────────────────────────────────────────────────────────────────────

    // [7] Per-option height respects _optFolds so collapsing an option
    //     doesn't leave a giant empty gap in the scroll view.
    private float EstimateHeight(DialogueNode node)
    {
        float h = 100f;

        var so = GetNodeSO(node);
        float locH = 22f;
        if (so != null)
        {
            var p = so.FindProperty("localizedText");
            if (p != null) locH = EditorGUI.GetPropertyHeight(p, true) + 4f;
        }

        switch (node.nodeType)
        {
            case DialogueNodeType.Text:
                h += locH + 30f + (node.onEnterActions?.Count ?? 0) * 50f + 50f;
                break;
            case DialogueNodeType.Choice:
                h += 60f;
                if (node.options != null)
                {
                    for (int i = 0; i < node.options.Count; i++)
                    {
                        string ok   = $"{node.id}_opt{i}";
                        bool   open = _optFolds.TryGetValue(ok, out bool v) && v;
                        h += open ? 200f : 26f;
                    }
                }
                h += 30f;
                break;
            case DialogueNodeType.End:
                h += locH + (node.onEnterActions?.Count ?? 0) * 50f + 50f;
                break;
            case DialogueNodeType.Random:
                h += (node.randomTexts?.Count ?? 0) * 30f + 80f + (node.onEnterActions?.Count ?? 0) * 50f;
                break;
        }
        return h + 30f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Option add / remove
    // ─────────────────────────────────────────────────────────────────────────

    private void AddOption(DialogueGraph graph, DialogueNode node, SerializedObject nodeSO)
    {
        string path = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrEmpty(path)) return;
        var opt = CreateInstance<DialogueOption>();
        opt.name = $"Option_{node.options.Count}";
        Undo.RecordObject(node, "Add Option");
        AssetDatabase.AddObjectToAsset(opt, path);
        Undo.RegisterCreatedObjectUndo(opt, "Add Option");
        node.options.Add(opt);
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        nodeSO.Update();
        _nodeHeights.Remove(node.id);
    }

    private void RemoveOption(DialogueGraph graph, DialogueNode node, SerializedObject nodeSO, DialogueOption opt)
    {
        InvalidateOpt(opt);
        Undo.RecordObject(node, "Remove Option");
        node.options.Remove(opt);
        Undo.DestroyObjectImmediate(opt);
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        nodeSO.Update();
        _nodeHeights.Remove(node.id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Asset creator (conditions / actions)
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowTypeDropdown<T>(DialogueGraph graph, SerializedProperty lp)
        where T : ScriptableObject
    {
        var menu = new GenericMenu();
        var types = TypeCache.GetTypesDerivedFrom<T>();

        foreach (Type t in types)
        {
            if (t.IsAbstract) continue;

            Type captured = t;
            menu.AddItem(new GUIContent(captured.Name), false, () =>
                CreateAndAppend(captured, graph, lp, captured.Name));
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No types found"));

        menu.ShowAsContext();
    }

    private void CreateAndAppend(Type type, DialogueGraph graph, SerializedProperty lp, string baseName)
    {
        string gp = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrEmpty(gp)) return;

        string fp = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(Path.GetDirectoryName(gp), $"{baseName}.asset"));

        var a = (ScriptableObject)CreateInstance(type);
        AssetDatabase.CreateAsset(a, fp);
        AssetDatabase.SaveAssets();

        var owner = lp.serializedObject.targetObject;
        Undo.RecordObject(owner, $"Add {type.Name}");
        lp.arraySize++;
        lp.GetArrayElementAtIndex(lp.arraySize - 1).objectReferenceValue = a;
        lp.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(a);
    }

    private void CreateAndAppend<T>(DialogueGraph graph, SerializedProperty lp, string baseName)
        where T : ScriptableObject
    {
        string gp = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrEmpty(gp)) return;

        string fp = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(Path.GetDirectoryName(gp), $"{baseName}.asset"));

        var a = CreateInstance<T>();
        AssetDatabase.CreateAsset(a, fp);
        AssetDatabase.SaveAssets();

        var owner = lp.serializedObject.targetObject;
        Undo.RecordObject(owner, $"Add {typeof(T).Name}");
        lp.arraySize++;
        lp.GetArrayElementAtIndex(lp.arraySize - 1).objectReferenceValue = a;
        lp.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(a);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SO cache
    // ─────────────────────────────────────────────────────────────────────────

    private SerializedObject GetNodeSO(DialogueNode n)
    {
        if (_nodeSOs.TryGetValue(n.id, out var c) && c?.targetObject != null) return c;
        return _nodeSOs[n.id] = new SerializedObject(n);
    }

    private void InvalidateNodeSO(string id) => _nodeSOs.Remove(id);

    private SerializedObject GetOptionSO(DialogueOption o)
    {
        int k = o.GetInstanceID();
        if (_optionSOs.TryGetValue(k, out var c) && c?.targetObject != null) return c;
        return _optionSOs[k] = new SerializedObject(o);
    }

    private void InvalidateOpt(DialogueOption o) { if (o != null) _optionSOs.Remove(o.GetInstanceID()); }

    private SerializedObject GetSubSO(ScriptableObject s)
    {
        int k = s.GetInstanceID();
        if (_subSOs.TryGetValue(k, out var c) && c?.targetObject != null) return c;
        return _subSOs[k] = new SerializedObject(s);
    }

    private void InvalidateSub(ScriptableObject s) { if (s != null) _subSOs.Remove(s.GetInstanceID()); }

    // ─────────────────────────────────────────────────────────────────────────
    // Sidebar
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshGraphList()
    {
        if (_allGraphs != null && EditorApplication.timeSinceStartup - _lastScanTime < 3.0) return;
        _lastScanTime = EditorApplication.timeSinceStartup;
        _allGraphs    = new List<DialogueGraph>();
        foreach (var guid in AssetDatabase.FindAssets("t:DialogueGraph"))
        {
            var g = AssetDatabase.LoadAssetAtPath<DialogueGraph>(AssetDatabase.GUIDToAssetPath(guid));
            if (g != null) _allGraphs.Add(g);
        }
        _allGraphs.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
    }

    private void DrawSidebar(Rect area)
    {
        EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.14f));
        EditorGUI.DrawRect(new Rect(area.xMax - 1f, area.y, 1f, area.height), Pal.GridLine * 3f);

        float y = area.y, x = area.x, w = area.width;

        // Header
        EditorGUI.DrawRect(new Rect(x, y, w, 36f), Pal.BgHeader);
        GUI.Label(new Rect(x + 8f, y + 4f, w - 16f, 14f), "DIALOGUES",
            new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = Pal.TxtMuted }, fontStyle = FontStyle.Bold, fontSize = 9 });
        _sidebarSearch = EditorGUI.TextField(new Rect(x + 4f, y + 18f, w - 8f, 16f),
            _sidebarSearch, EditorStyles.toolbarSearchField);
        y += 38f;

        if (GUI.Button(new Rect(x + 4f, y, w - 8f, 18f), "↻  Refresh", EditorStyles.miniButton))
        { _lastScanTime = -1; RefreshGraphList(); }
        y += 22f;

        var listArea = new Rect(x, y, w, area.height - (y - area.y));
        float itemH  = 38f;
        float ch     = Mathf.Max((_allGraphs?.Count ?? 0) * (itemH + 2f), listArea.height);

        _sidebarScroll = GUI.BeginScrollView(listArea, _sidebarScroll,
            new Rect(0, 0, w - 14f, ch));

        float iy = 2f;
        if (_allGraphs == null || _allGraphs.Count == 0)
        {
            GUI.Label(new Rect(4f, iy, w - 8f, 20f), "No graphs found.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            string filter = _sidebarSearch?.Trim().ToLowerInvariant() ?? string.Empty;
            foreach (var g in _allGraphs)
            {
                if (g == null) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    !g.name.ToLowerInvariant().Contains(filter) &&
                    !(g.characterName?.ToLowerInvariant().Contains(filter) ?? false))
                    continue;

                bool isAct = _graph == g;
                var  ir    = new Rect(0f, iy, w - 14f, itemH);

                EditorGUI.DrawRect(ir, isAct ? Pal.AccentText * 0.25f : new Color(0,0,0,0));
                if (isAct) EditorGUI.DrawRect(new Rect(0f, iy, 3f, itemH), Pal.AccentText);

                float ts = 28f, tx = 6f, ty = iy + (itemH - ts) * 0.5f;
                if (g.characterIllustration != null)
                    GUI.DrawTexture(new Rect(tx, ty, ts, ts), g.characterIllustration.texture, ScaleMode.ScaleToFit);
                else
                    EditorGUI.DrawRect(new Rect(tx, ty, ts, ts), new Color(0.25f, 0.25f, 0.30f));

                float lx = tx + ts + 6f, lw = w - lx - 14f - 4f;
                GUI.Label(new Rect(lx, iy + 4f, lw, 14f), g.name,
                    new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 10, clipping = TextClipping.Clip, normal = { textColor = isAct ? Pal.TxtMain : Pal.TxtMuted } });
                GUI.Label(new Rect(lx, iy + 20f, lw, 12f),
                    !string.IsNullOrWhiteSpace(g.characterName) ? g.characterName : "—",
                    new GUIStyle(EditorStyles.miniLabel)
                    { clipping = TextClipping.Clip,
                      normal   = { textColor = isAct ? Pal.TxtAccent : new Color(0.4f, 0.4f, 0.5f) } });

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && ir.Contains(Event.current.mousePosition))
                { Load(g); Event.current.Use(); Repaint(); }

                EditorGUI.DrawRect(new Rect(0f, iy + itemH, w - 14f, 1f), Pal.GridLine * 2f);
                iy += itemH + 2f;
            }
        }

        GUI.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────────

    private Dictionary<string, DialogueNode> BuildNodeMap()
    {
        var m = new Dictionary<string, DialogueNode>();
        if (_graph?.nodes == null) return m;
        foreach (var n in _graph.nodes)
        {
            if (n != null && !m.ContainsKey(n.id)) m[n.id] = n;
        }
        return m;
    }

    private DialogueNode FindNode(string id)
    {
        if (string.IsNullOrEmpty(id) || _graph?.nodes == null) return null;
        foreach (var n in _graph.nodes)
        {
            if (n != null && n.id == id) return n;
        }
        return null;
    }

    private Color Accent(DialogueNode n, bool isStart)
    {
        if (isStart)    return Pal.AccentStart;
        if (n.isRouter) return Pal.AccentRouter;
        return n.nodeType == DialogueNodeType.Text   ? Pal.AccentText
             : n.nodeType == DialogueNodeType.Choice ? Pal.AccentChoice
             : n.nodeType == DialogueNodeType.Random ? Pal.AccentRandom
                                                     : Pal.AccentEnd;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Styles
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureStyles()
    {
        if (_stylesBuilt) return;

        _styleTab = new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 12,
            normal    = { textColor = Pal.TxtMuted }
        };
        _styleTabSel = new GUIStyle(_styleTab)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Pal.TxtMain }
        };
        _styleSectionLbl = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.9f) }
        };
        _styleInlineBox = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(6, 6, 4, 4),
            margin  = new RectOffset(0, 0, 2, 2)
        };
        _stylePinnedBtn = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Pal.TxtAccent },
            hover     = { textColor = Color.white }
        };

        _stylesBuilt = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Empty state
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawEmptyState(Rect area)
    {
        EditorGUI.DrawRect(area, Pal.BgDark);
        GUI.Label(new Rect(area.x, area.y + area.height * 0.5f - 20f, area.width, 40f),
            "Select a dialogue from the sidebar to open it.",
            new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Pal.TxtMuted }
            });
    }
}
#endif