#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow : EditorWindow
{
    private const string EmptyDetailsText = "Selecciona una loot table para ver / editar sus datos.";

    private const string PrefSearch = "BS.LootTableBrowser.Search";
    private const string PrefSortMode = "BS.LootTableBrowser.SortMode";
    private const string PrefLastCreateFolder = "BS.LootTableBrowser.LastCreateFolder";

    private const float DetailsResponsiveBreak = 620f;
    private const int SectionGap = 10;
    private const double DebounceDelaySeconds = 0.15;

    private const int InventoryPreviewWidth = 6;
    private const int InventoryPreviewHeight = 3;
    private const int InventoryCellSize = 36;

    private const bool DefaultGuaranteedOpen = true;
    private const bool DefaultWeightedOpen = true;
    private const bool DefaultSimulationOpen = true;

    private static readonly Vector2 MinWindowSize = new(1000f, 650f);

    private static readonly Color WarnColor = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color ErrorColor = new(0.9f, 0.25f, 0.25f, 1f);

    internal readonly List<LootTable> AllLootTables = new();
    internal readonly List<LootTable> FilteredLootTables = new();

    internal readonly Dictionary<LootTable, ValidationSeverity> SeverityCache = new();
    internal readonly Dictionary<LootTable, List<ValidationMessage>> ValidationCache = new();

    private bool _debounceActive;
    private RefreshFlags _debouncedFlags = RefreshFlags.None;
    private double _debounceUntil;

    private Domain _domain;
    private UI _ui;

    internal bool IsUIReady;
    internal SortMode CurrentSortMode = SortMode.NameAz;

    internal LootTable Selected;
    internal SerializedObject SelectedSo;
    internal bool SeverityCacheDirty = true;

    internal EntrySortDirection GuaranteedSortDirection = EntrySortDirection.Desc;
    internal EntrySortDirection WeightedSortDirection = EntrySortDirection.Desc;

    internal ToolbarButton NewBtn;
    internal ToolbarButton RefreshBtn;
    internal ToolbarButton ResetBtn;
    internal ToolbarButton SortBtn;
    internal ToolbarSearchField SearchField;

    internal VisualElement ToolbarLeft;
    internal VisualElement ToolbarRight;

    internal ListView ListView;
    internal ScrollView DetailsScroll;
    internal VisualElement DetailsPanel;

    internal VisualElement DetailsBodyRow;
    internal VisualElement DetailsLeftColumn;
    internal VisualElement DetailsRightColumn;

    internal Label HeaderName;
    internal Label HeaderType;
    internal Button HeaderSelectButton;
    internal Button HeaderCopyGuidButton;
    internal Label HeaderGuidValue;

    internal TextField RenameField;
    internal Button RenameApplyButton;

    internal VisualElement ValidationContainer;
    internal HelpBox ValidationBox;

    internal IntegerField SimIterationsField;
    internal Button SimRollOnceBtn;
    internal Button SimRunBtn;
    internal Label SimSummaryLabel;
    internal VisualElement SimResultsRoot;
    internal VisualElement SimInventoryGrid;
    internal VisualElement SimInventoryGridOuter;
    internal Label SimInventoryInfoLabel;
    internal Label SimInventoryOverflowLabel;

    internal SimulationSnapshot LastSimulation;

    [MenuItem("Tools/Content Editors/Loot Table Browser")]
    public static void Open()
    {
        var w = GetWindow<LootTableBrowserWindow>("Loot Tables");
        w.minSize = MinWindowSize;
    }

    private void OnEnable()
    {
        minSize = MinWindowSize;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        _domain?.SaveToolbarPrefs();
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
        _domain.LoadToolbarPrefsEarly();

        Refresh(RefreshFlags.Hard);
        _ui.ShowEmptyDetails();

        root.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (!IsUIReady) return;
            _ui.ScaleInventoryGridToFit();
        });

        IsUIReady = true;
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

        if (flags.HasFlag(RefreshFlags.Assets))
            _domain.RefreshAssetsInternal();

        if (flags.HasFlag(RefreshFlags.Filter))
            _domain.RefilterInternal();

        if (flags.HasFlag(RefreshFlags.Validations))
            _domain.UpdateValidationsInternal();

        if (flags.HasFlag(RefreshFlags.List))
            _ui.RefreshList();

        if (flags.HasFlag(RefreshFlags.Details) && Selected != null)
            _ui.RefreshHeader(Selected);

        if (flags.HasFlag(RefreshFlags.Simulation))
            _ui.RefreshSimulationView();
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

        if (flags.HasFlag(RefreshFlags.Assets))
        {
            Refresh(_debouncedFlags);
            _debouncedFlags = RefreshFlags.None;
            StopDebounce();
            return;
        }

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
        StopDebounce();

        if (flags != RefreshFlags.None)
            Refresh(flags);
    }

    private void StopDebounce()
    {
        if (!_debounceActive) return;
        _debounceActive = false;
        EditorApplication.update -= DebounceUpdate;
    }

    internal void MarkSeverityDirty()
    {
        SeverityCacheDirty = true;
    }

    internal void ClearSimulation()
    {
        LastSimulation = null;
        _ui.RefreshSimulationView();
    }
}
#endif