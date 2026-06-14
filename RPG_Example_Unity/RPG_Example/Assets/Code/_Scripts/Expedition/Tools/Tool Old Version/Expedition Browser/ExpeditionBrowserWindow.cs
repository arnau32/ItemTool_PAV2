#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ExpeditionBrowserWindow : EditorWindow
{
    private const string EmptyDetailsText = "Selecciona una entidad de escena para ver / editar sus datos.";
    private const string EmptyMapText = "Map tab vacío por ahora.";

    private const string PrefSearch = "BS.ExpeditionBrowser.Search";
    private const string PrefActiveTab = "BS.ExpeditionBrowser.ActiveTab";
    private const string PrefIncludeInactive = "BS.ExpeditionBrowser.IncludeInactive";

    private const double DebounceDelaySeconds = 0.12;
    private const float FixedListWidth = 320f;

    private static readonly Vector2 MinWindowSize = new(1200f, 700f);

    private static readonly Color WarnColor = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color ErrorColor = new(0.9f, 0.25f, 0.25f, 1f);

    internal readonly List<SceneEntityRecord> AllRecords = new();
    internal readonly List<SceneEntityRecord> FilteredRecords = new();

    internal readonly Dictionary<SceneEntityRecord, ValidationSeverity> SeverityCache = new();
    internal readonly Dictionary<SceneEntityRecord, List<ValidationMessage>> ValidationCache = new();

    internal readonly Dictionary<string, bool> LootZoneFoldoutStates = new();
    internal readonly Dictionary<string, bool> EnemyZoneFoldoutStates = new();
    private bool _debounceActive;
    private RefreshFlags _debouncedFlags = RefreshFlags.None;
    private double _debounceUntil;

    private Domain _domain;
    private UI _ui;

    internal bool IsUIReady;
    internal ExpeditionListTab CurrentTab = ExpeditionListTab.Enemies;
    internal bool IncludeInactive = true;

    internal SceneEntityRecord SelectedRecord;

    internal ToolbarButton RefreshBtn;
    internal ToolbarSearchField SearchField;

    internal ToolbarToggle EnemiesTabToggle;
    internal ToolbarToggle LootPointsTabToggle;
    internal ToolbarToggle MapTabToggle;
    internal ToolbarToggle IncludeInactiveToggle;

    internal ScrollView ListScroll;
    internal VisualElement ListRoot;

    internal ScrollView DetailsScroll;
    internal VisualElement DetailsPanel;

    internal Label HeaderName;
    internal Label HeaderType;
    internal Label HeaderPath;
    internal Button HeaderSelectButton;
    internal Button HeaderPingButton;
    internal Button HeaderFrameButton;

    internal VisualElement ValidationContainer;
    internal HelpBox ValidationBox;

    [MenuItem("Tools/Content Editors/Expedition Browser")]
    public static void Open()
    {
        var w = GetWindow<ExpeditionBrowserWindow>("Expedition Browser");
        w.minSize = MinWindowSize;
    }

    private void OnEnable()
    {
        minSize = MinWindowSize;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        _domain?.SavePrefs();
    }

    public void CreateGUI()
    {
        IsUIReady = false;

        _ui = new UI(this);
        _domain = new Domain(this);

        var root = rootVisualElement;
        root.style.flexDirection = FlexDirection.Column;
        root.Clear();

        _ui.BuildRoot(root);
        _domain.LoadPrefs();

        Refresh(RefreshFlags.Hard);
        _ui.ShowEmptyDetails();

        IsUIReady = true;
    }

    private void OnHierarchyChanged()
    {
        if (!IsUIReady) return;
        RequestRefreshDebounced(RefreshFlags.Hard);
    }

    private void OnUndoRedo()
    {
        if (!IsUIReady) return;
        Refresh(RefreshFlags.Soft);
    }

    private void Refresh(RefreshFlags flags)
    {
        if (!IsUIReady && flags != RefreshFlags.Hard)
            return;

        if (flags.HasFlag(RefreshFlags.Scan))
            _domain.ScanScene();

        if (flags.HasFlag(RefreshFlags.Filter))
            _domain.Refilter();

        if (flags.HasFlag(RefreshFlags.Validations))
            _domain.RebuildValidations();

        if (flags.HasFlag(RefreshFlags.List))
            _ui.RefreshList();

        if (flags.HasFlag(RefreshFlags.Details))
            _ui.RefreshDetails();
    }

    private void RequestRefreshDebounced(RefreshFlags flags)
    {
        if (!IsUIReady) return;

        if (flags == RefreshFlags.Hard)
        {
            Refresh(flags);
            return;
        }

        _debouncedFlags |= flags;
        _debounceUntil = EditorApplication.timeSinceStartup + DebounceDelaySeconds;

        if (_debounceActive) return;

        _debounceActive = true;
        EditorApplication.update += DebounceUpdate;
    }

    private void DebounceUpdate()
    {
        if (EditorApplication.timeSinceStartup < _debounceUntil)
            return;

        var flags = _debouncedFlags;
        _debouncedFlags = RefreshFlags.None;

        _debounceActive = false;
        EditorApplication.update -= DebounceUpdate;

        if (flags != RefreshFlags.None)
            Refresh(flags);
    }
}
#endif