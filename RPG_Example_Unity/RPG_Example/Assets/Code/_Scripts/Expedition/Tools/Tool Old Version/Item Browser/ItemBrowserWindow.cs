#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public partial class ItemBrowserWindow : EditorWindow
{
    private const string EmptyDetailsText = "Selecciona un item para Ver / Editar sus datos.";

    private const string PrefSearch = "BS.ItemBrowser.Search";
    private const string PrefSortMode = "BS.ItemBrowser.SortMode";
    private const string PrefDupNameAsError = "BS.ItemBrowser.DupNameAsError";
    private const string PrefTypeFilterMask = "BS.ItemBrowser.TypeFilterMask";
    private const string PrefLastCreateFolder = "BS.ItemBrowser.LastCreateFolder";

    private const int PreviewMaxCellsPerAxis = 12;
    private const int GridCellSize = 18;

    private const int SectionGap = 10;
    private const int RowGap = 6;
    private const float DetailsResponsiveBreak = 620f;

    private const bool DefaultBasicOpen = true;
    private const bool DefaultStackOpen = true;
    private const double DebounceDelaySeconds = 0.15;

    private static readonly Vector2 MinWindowSize = new(1000f, 650f);

    private static readonly Color DupWarnColor = new(1f, 0.78f, 0.2f, 0.95f);
    private static readonly Color DupErrorColor = new(0.9f, 0.25f, 0.25f, 0.95f);
    private static readonly Color WarnColor = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color ErrorColor = new(0.9f, 0.25f, 0.25f, 1f);

    internal readonly List<ItemData> AllItems = new();
    internal readonly List<Enum> AllTypeEnumValues = new();
    internal readonly HashSet<int> EnabledTypeIndices = new();
    internal readonly List<ItemData> FilteredItems = new();

    internal readonly Dictionary<string, List<ItemData>> NameIndex = new(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<ItemData, ValidationSeverity> SeverityCache = new();
    internal readonly Dictionary<int, int> TypeValueToIndex = new();

    private bool _debounceActive;
    private RefreshFlags _debouncedFlags = RefreshFlags.None;
    private double _debounceUntil;
    private Domain _domain;
    private string _selectedNameLast = string.Empty;
    private bool _selectedSnapshotInit;
    private int _selectedTypeLast = int.MinValue;

    private UI _ui;

    internal Image BasicIconPreview;
    internal SortMode CurrentSortMode = SortMode.NameAz;
    internal VisualElement DetailsPanel;

    internal ScrollView DetailsScroll;
    internal VisualElement DimsGrid;
    internal VisualElement DimsGridOuter;
    internal Label DimsOverflowLabel;

    internal bool DupNameAsError;

    internal ToolbarToggle DupStrictToggle;
    internal Button FixTypeButton;
    internal Button HeaderCopyGuidButton;
    internal Label HeaderGuidValue;

    internal Image HeaderIcon;
    internal Label HeaderName;
    internal Button HeaderSelectButton;
    internal Label HeaderType;

    internal bool IsUIReady;
    internal Type ItemTypeEnumSystemType;
    internal ListView ListView;

    internal bool NameIndexDirty = true;

    internal ToolbarButton NewBtn;
    internal ToolbarButton RefreshBtn;
    internal ToolbarButton ResetBtn;
    internal ToolbarSearchField SearchField;
    internal ItemData Selected;
    internal SerializedObject SelectedSo;
    internal bool SeverityCacheDirty = true;
    internal ToolbarButton SortBtn;

    internal VisualElement ToolbarLeft;
    internal VisualElement ToolbarRight;

    internal ToolbarButton TypeFilterBtn;
    internal HelpBox ValidationBox;
    internal VisualElement ValidationContainer;

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
            _ui.UpdateDupToggleVisual();
            _ui.ScaleDimsGridToFit();
        });

        IsUIReady = true;
    }

    [MenuItem("Tools/Content Editors/Item Browser")]
    public static void Open()
    {
        var w = GetWindow<ItemBrowserWindow>("Item Browser");
        w.minSize = MinWindowSize;
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

        if (flags.HasFlag(RefreshFlags.Details))
        {
            if (Selected != null)
                _ui.RefreshHeader(Selected);
        }
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

    private void MarkNameIndexDirty()
    {
        NameIndexDirty = true;
        SeverityCacheDirty = true;
    }

    private void MarkSeverityDirty()
    {
        SeverityCacheDirty = true;
    }

    private void SelectItem(ItemData item)
    {
        Selected = item;

        if (Selected == null)
        {
            SelectedSo = null;
            _ui.ShowEmptyDetails();
            return;
        }

        SelectedSo = new SerializedObject(Selected);
        _ui.ShowItemHeaderAndForm(Selected);

        Refresh(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
    }

    private static string GetGuid(Object asset)
    {
        if (asset == null) return "";
        var path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return asset.GetInstanceID().ToString();
        return AssetDatabase.AssetPathToGUID(path);
    }

    private static void SelectAndPing(Object asset)
    {
        if (asset == null) return;
        Selection.activeObject = asset;
        EditorUtility.FocusProjectWindow();
        EditorGUIUtility.PingObject(asset);
    }

    private static SerializedProperty FindFirstArrayElementTypeContains(SerializedObject so, string elementTypeContains)
    {
        if (so == null || string.IsNullOrEmpty(elementTypeContains)) return null;

        var it = so.GetIterator();
        var enterChildren = true;

        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (!it.isArray) continue;
            if (it.propertyType != SerializedPropertyType.Generic) continue;

            var aet = it.arrayElementType ?? string.Empty;
            if (aet.IndexOf(elementTypeContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return it.Copy();
        }

        return null;
    }
}
#endif