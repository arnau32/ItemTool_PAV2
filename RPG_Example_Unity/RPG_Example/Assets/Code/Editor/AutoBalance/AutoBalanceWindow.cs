using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AutoBalanceWindow : EditorWindow
{
    #region Enums & Constants

    private enum Tab     { Dashboard, Player, Enemies, Weapons, Armors, Consumables, Curves, Simulation, Report, Diff }
    private enum SimMode { Single, Batch, Matrix, TierSummary, SurvivalCheck }

    private static readonly string[] TAB_LABELS =
        { "Dashboard", "Player", "Enemigos", "Armas", "Armaduras", "Consumibles", "Curvas", "Simulación", "Reporte", "Diff JSON" };

    private static readonly Enums.EquipSlot[] ARMOR_SLOTS =
    {
        Enums.EquipSlot.Helmet,
        Enums.EquipSlot.ChestArmor,
        Enums.EquipSlot.LowerArmor,
        Enums.EquipSlot.Backpack,
    };
    private static readonly string[] ARMOR_SLOT_LABELS = { "Casco", "Pecho", "Piernas", "Mochila" };

    private static readonly string[] WEAPON_FAMILY_NAMES = System.Enum.GetNames(typeof(Enums.WeaponFamily));

    private const float SIDEBAR_W = 148f;
    private const float TOOLBAR_H = 38f;
    private const float LIST_W    = 210f;
    private const float KPI_H     = 64f;
    private const float ROW_H     = 22f;

    private static readonly Color COL_BG      = new Color(0.14f, 0.14f, 0.17f);
    private static readonly Color COL_SIDEBAR = new Color(0.11f, 0.11f, 0.14f);
    private static readonly Color COL_TOOLBAR = new Color(0.10f, 0.10f, 0.13f);
    private static readonly Color COL_PANEL   = new Color(0.18f, 0.18f, 0.22f);
    private static readonly Color COL_ROWALT  = new Color(0.16f, 0.16f, 0.20f);
    private static readonly Color COL_TABSEL  = new Color(0.22f, 0.42f, 0.80f);
    private static readonly Color COL_GREEN   = new Color(0.22f, 0.70f, 0.32f);
    private static readonly Color COL_BLUE    = new Color(0.22f, 0.52f, 0.90f);
    private static readonly Color COL_ORANGE  = new Color(0.90f, 0.52f, 0.10f);
    private static readonly Color COL_PURPLE  = new Color(0.62f, 0.32f, 0.90f);
    private static readonly Color COL_RED     = new Color(0.85f, 0.22f, 0.22f);
    private static readonly Color COL_YELLOW  = new Color(0.90f, 0.80f, 0.12f);

    private static readonly Color COL_HP  = new Color(0.85f, 0.25f, 0.25f);
    private static readonly Color COL_ATK = new Color(0.90f, 0.55f, 0.15f);
    private static readonly Color COL_DEF = new Color(0.25f, 0.55f, 0.85f);
    private static readonly Color COL_STA = new Color(0.25f, 0.75f, 0.40f);

    #endregion

    #region Fields — Data

    private List<WeaponData>           _weapons     = new();
    private List<EnemyDefinition>      _enemies     = new();
    private List<CharacterBaseStatsSO> _baseStats   = new();
    private List<UpgradeNodeSO>        _nodes       = new();
    private List<EquipableItemData>    _armors      = new();
    private List<ConsumableItemData>   _consumables = new();
    private BalanceTargetConfigSO      _targets;
    private bool _dataLoaded;

    #endregion

    #region Fields — Navigation

    private Tab _tab = Tab.Simulation;
    private readonly Vector2[] _tabScrolls = new Vector2[10];

    #endregion

    #region Fields — Selection

    private int _selWeapon     = 0;
    private int _selEnemy      = 0;
    private int _selArmor      = 0;
    private int _selConsumable = 0;
    private int _selPlayerSO   = 0;

    private readonly Vector2[] _listScrolls = new Vector2[10];

    private SerializedObject _balanceSO;
    private Object           _balanceSOTarget;

    private SerializedObject _enemyCtxSO;
    private Object           _enemyCtxSOTarget;

    #endregion

    #region Fields — Combo Analysis

    private bool[]   _comboFoldouts           = System.Array.Empty<bool>();
    private int      _lastSelWeaponForCombos   = -1;
    private readonly Dictionary<AttackData, SerializedObject> _attackSOs = new();

    #endregion

    #region Fields — Player (Base Stats & Node editing)

    private SerializedObject _playerBaseStatsSO;
    private Object           _playerBaseSOTarget;

    private readonly Dictionary<UpgradeNodeSO, SerializedObject> _nodeEditSOs = new();
    private bool[] _nodeEditExpanded;

    #endregion

    #region Fields — Skill Tree (Player tab)

    private int[] _upgradeLvls;
    private bool  _showPerLevelBreakdown;

    #endregion

    #region Fields — Simulation

    private int   _simPlayerSO = 0;
    private int   _simWeapon   = 0;
    private int   _simEnemy    = 0;
    private int[] _simUpgradeLvls;

    // Enemy defense formula (K diminishing returns — only for enemies).
    private float _defK   = 100f;
    private float _defCap = 0.50f;

    // Player defense redesign — values in percentage points (e.g. 25 = 25%).
    private float _maxArmorDefPct   = 25f;
    private float _maxBonfireDefPct = 25f;

    // Player armor selection per slot (0 = none, 1+ = armor index + 1).
    private int[] _simArmorPerSlot;

    // Enemy weapon override (-1 = auto from prefab).
    private int _simEnemyWeaponIdx = -1;

    // Rarity override for player weapon (-1 = base/no rarity).
    private int _simWeaponRarity = -1;

    private AutoBalanceSimulator.SimResult _simResult;
    private AutoBalanceSimulator.SimResult _simResultRarityMin;
    private AutoBalanceSimulator.SimResult _simResultRarityMax;
    private Vector2 _comparatorScroll;

    private SimMode _simMode;

    // Batch
    private readonly List<(EnemyDefinition enemy, AutoBalanceSimulator.SimResult result)> _batchResults = new();
    private Vector2 _batchScroll;

    // Matrix
    private int[]   _enemyFamilyIdx; // -1 = unassigned, else (int)WeaponFamily
    private readonly List<(EnemyDefinition enemy, WeaponData pWeapon, WeaponData eWeapon, AutoBalanceSimulator.SimResult result)>
        _matrixResults = new();
    private Vector2 _matrixScroll;

    // Tier Summary
    private int  _tierFilter      = 1;
    private bool _primaryOnly     = false;
    private int  _tierSelWeapon   = 0;  // index into _tierWeaponList
    private readonly List<WeaponData> _tierWeaponList = new();
    private readonly List<(WeaponData weapon, EnemyDefinition enemy,
        AutoBalanceSimulator.SimResult result,
        BalanceTargetConfigSO.ZoneTargets targets, BalanceTargetConfigSO.MatchupLabel matchup)>
        _tierSummaryResults = new();
    private Vector2 _tierSummaryScroll;

    // Survival Check
    private int   _survArmorTier     = 1;
    private float _survBonfireDef    = 0f;
    private float _survBonfireHealth = 0f;
    private int[] _survEnemyFamilyIdx;  // -1 = auto (prefab mult), else (int)WeaponFamily
    private readonly List<(EnemyDefinition enemy, int weaponTier, string tierLabel, float enemyMult, AutoBalanceSimulator.SimResult result)>
        _survResults = new();
    private Vector2 _survScroll;

    #endregion

    #region Fields — Diff

    private struct DiffEntry
    {
        public string category;
        public string entityName;
        public string field;
        public float  currentValue;
        public float  jsonValue;
    }

    private readonly List<DiffEntry> _diffEntries = new();
    private string  _diffJsonPath = "";
    private bool    _diffLoaded;
    private Vector2 _diffScroll;
    private bool    _diffShowAll;

    #endregion

    #region Fields — Styles

    private bool      _stylesReady;
    private GUIStyle  _sVal, _sLabel, _sTabSel, _sTabNorm;

    #endregion

    #region Menu / Lifecycle

    [MenuItem("Tools/Auto Balance Tool")]
    static void Open() => GetWindow<AutoBalanceWindow>("Auto Balance Tool");

    private void OnEnable() { _stylesReady = false; LoadData(); }

    private void OnGUI()
    {
        EnsureStyles();
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), COL_BG);
        DrawToolbar(new Rect(0, 0, position.width, TOOLBAR_H));

        float cy = TOOLBAR_H;
        float ch = position.height - TOOLBAR_H;
        DrawSidebar(new Rect(0, cy, SIDEBAR_W, ch));

        var mainRect = new Rect(SIDEBAR_W, cy, position.width - SIDEBAR_W, ch);
        GUILayout.BeginArea(mainRect);
        _tabScrolls[(int)_tab] = GUILayout.BeginScrollView(_tabScrolls[(int)_tab]);
        GUILayout.Space(6);
        DrawActiveTab();
        GUILayout.Space(16);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    #endregion

    #region Toolbar

    private void DrawToolbar(Rect r)
    {
        EditorGUI.DrawRect(r, COL_TOOLBAR);
        GUILayout.BeginArea(r);
        GUILayout.BeginHorizontal();
        GUILayout.Space(8);
        GUILayout.Label("Auto Balance Tool  |  Between Shadows",
            EditorStyles.boldLabel, GUILayout.Width(270), GUILayout.Height(TOOLBAR_H));
        GUILayout.FlexibleSpace();
        if (ColorButton("⟳  Cargar",  COL_BLUE,   80)) { LoadData(); }
        GUILayout.Space(4);
        if (ColorButton("▶  Simular", COL_GREEN,  80)) { RunSimulation(); _tab = Tab.Simulation; }
        GUILayout.Space(4);
        if (ColorButton("⚖  Balance", COL_ORANGE, 80)) { RunAutoBalance(); }
        GUILayout.Space(4);
        if (ColorButton("↓  Exportar",new Color(0.4f,0.4f,0.4f), 80)) ExportCSV();
        GUILayout.Space(8);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        GUI.color = Color.white;
    }

    #endregion

    #region Sidebar

    private void DrawSidebar(Rect r)
    {
        EditorGUI.DrawRect(r, COL_SIDEBAR);
        GUILayout.BeginArea(r);
        GUILayout.Space(8);
        for (int i = 0; i < TAB_LABELS.Length; i++)
        {
            var  t   = (Tab)i;
            bool sel = _tab == t;
            var  rect = GUILayoutUtility.GetRect(SIDEBAR_W, 28f);
            if (sel) EditorGUI.DrawRect(rect, COL_TABSEL);
            GUI.color = sel ? Color.white : new Color(0.70f, 0.70f, 0.70f);
            if (GUI.Button(rect, TAB_LABELS[i], sel ? _sTabSel : _sTabNorm))
            {
                if (_tab != t) { _balanceSO = null; _balanceSOTarget = null; _enemyCtxSO = null; _enemyCtxSOTarget = null; }
                _tab = t;
            }
        }
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();
        GUI.color = new Color(0.45f, 0.45f, 0.45f);
        GUILayout.Label("BetweenShadows Alpha", EditorStyles.miniLabel, GUILayout.Width(SIDEBAR_W));
        GUI.color = Color.white;
        GUILayout.Space(4);
        GUILayout.EndArea();
    }

    #endregion

    #region Tab Routing

    private void DrawActiveTab()
    {
        switch (_tab)
        {
            case Tab.Dashboard:   DrawDashboard();   break;
            case Tab.Player:      DrawPlayer();      break;
            case Tab.Enemies:     DrawEnemies();     break;
            case Tab.Weapons:     DrawWeapons();     break;
            case Tab.Armors:      DrawArmors();      break;
            case Tab.Consumables: DrawConsumables(); break;
            case Tab.Curves:      DrawCurves();      break;
            case Tab.Simulation:  DrawSimulation();  break;
            case Tab.Report:      DrawReport();      break;
            case Tab.Diff:        DrawDiff();        break;
        }
    }

    #endregion

    #region Tab — Dashboard

    private void DrawDashboard()
    {
        SectionHeader("Dashboard — Estado general");
        if (!_dataLoaded) { EditorGUILayout.HelpBox("Carga los datos con el botón Cargar.", MessageType.Info); return; }

        GUILayout.BeginHorizontal();
        BeginPanel("Assets cargados", 260f);
        StatRow("CharacterBaseStatsSO", _baseStats.Count.ToString());
        StatRow("WeaponData",           _weapons.Count.ToString());
        StatRow("EnemyDefinition",      _enemies.Count.ToString());
        StatRow("UpgradeNodeSO",        _nodes.Count.ToString());
        StatRow("EquipableItemData",    _armors.Count.ToString());
        StatRow("ConsumableItemData",   _consumables.Count.ToString());
        StatRow("BalanceTargets SO",    _targets != null ? "✓" : "✗ Sin asignar");
        EndPanel();

        GUILayout.Space(8);

        BeginPanel("Multiplicadores de rareza (runtime)", 260f);
        (float minC,  float maxC)  = RarityUtility.GetMultiplierRange(Enums.ItemRarity.Common);
        (float minNC, float maxNC) = RarityUtility.GetMultiplierRange(Enums.ItemRarity.Rare);
        (float minE,  float maxE)  = RarityUtility.GetMultiplierRange(Enums.ItemRarity.Epic);
        (float minL,  float maxL)  = RarityUtility.GetMultiplierRange(Enums.ItemRarity.Legendary);
        StatRow("Common",    $"{minC:F1} – {maxC:F1}");
        StatRow("Rare", $"{minNC:F1} – {maxNC:F1}");
        StatRow("Epic",      $"{minE:F1} – {maxE:F1}");
        StatRow("Legendary", $"{minL:F1} – {maxL:F1}");
        EndPanel();

        GUILayout.Space(8);

        BeginPanel("Alertas rápidas", 0f);
        if (_targets == null)
        {
            EditorGUILayout.HelpBox(
                "Crea un BalanceTargetConfigSO para ver alertas:\n" +
                "Create → BetweenShadows/Balance/Balance Targets", MessageType.Info);
        }
        else
        {
            bool any = false;
            foreach (var e in _enemies)
            {
                if (e?.baseStats == null || !_targets.TryGetZoneTargets(e.zone, e.category, out var zt)) continue;
                float hp  = AutoBalanceDataLoader.GetEnemyHealth(e);
                float atk = AutoBalanceDataLoader.GetEnemyAttack(e) * AutoBalanceDataLoader.GetEnemyWeaponMult(e);
                if (hp <= 0f || atk <= 0f) continue;
                if (zt.hitsToKill.y > 0 && hp / Mathf.Max(atk, 1f) > zt.hitsToKill.y * 4f)
                { AlertRow($"{e.displayName} — posible enemigo muy tanque", COL_YELLOW); any = true; }
            }
            if (!any) GreenRow("✓ Sin alertas críticas detectadas.");
        }
        EndPanel();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        DrawDefenseCapsPanel();
    }

    private void DrawDefenseCapsPanel()
    {
        BeginPanel("Caps de defensa — Advertencias", 0f);

        GUILayout.BeginHorizontal();

        // ── Caps editables ────────────────────────────────────────────────────
        GUILayout.BeginVertical(GUILayout.Width(280f));
        SectionSubHeader("Configuración de caps");
        _maxArmorDefPct   = EditorGUILayout.Slider("Cap armadura (%)",  _maxArmorDefPct,   0f, 50f);
        _maxBonfireDefPct = EditorGUILayout.Slider("Cap hoguera (%)",   _maxBonfireDefPct,  0f, 50f);
        GUILayout.Space(2);
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        GUILayout.Label($"Cap total: {_maxArmorDefPct + _maxBonfireDefPct:F0}%  (suma de caps)", EditorStyles.miniLabel);
        GUI.color = Color.white;
        GUILayout.EndVertical();

        GUILayout.Space(12);

        // ── Top 3 armaduras por defensa ───────────────────────────────────────
        GUILayout.BeginVertical();
        SectionSubHeader("Top 3 armaduras por defensa (peor caso equipado)");

        if (_armors.Count == 0)
        {
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label("Sin armaduras cargadas.", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        else
        {
            // Sort descending by defense value, take top 3
            var sorted = new List<EquipableItemData>(_armors);
            sorted.Sort((a, b) =>
            {
                float da = AutoBalanceDataLoader.GetArmorStatFlat(new List<EquipableItemData> { a }, Enums.StatType.Defense);
                float db = AutoBalanceDataLoader.GetArmorStatFlat(new List<EquipableItemData> { b }, Enums.StatType.Defense);
                return db.CompareTo(da);
            });

            int   showCount = Mathf.Min(3, sorted.Count);
            float top3Sum   = 0f;

            TableHeader(new[] { "Armadura", "Slot", "Defensa %" }, new[] { 160f, 90f, 80f });
            for (int i = 0; i < showCount; i++)
            {
                var   a   = sorted[i];
                float def = AutoBalanceDataLoader.GetArmorStatFlat(new List<EquipableItemData> { a }, Enums.StatType.Defense);
                top3Sum  += def;
                TableRow(i % 2 == 1, new[] { a.itemNameID ?? a.name, a.equipSlot.ToString(), $"{def:F1}%" },
                    new[] { 160f, 90f, 80f });
            }

            GUILayout.Space(4);

            bool overCap = top3Sum > _maxArmorDefPct;
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label("Suma top 3:", EditorStyles.miniLabel, GUILayout.Width(70f));
            GUI.color = overCap ? COL_RED : COL_GREEN;
            GUILayout.Label($"{top3Sum:F1}%", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label($"/ cap {_maxArmorDefPct:F0}%", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            if (overCap)
                AlertRow($"Las 3 mejores armaduras suman {top3Sum:F1}% — supera el cap de {_maxArmorDefPct:F0}%. Sube el cap o baja los valores de defensa.", COL_RED);
            else
                GreenRow($"✓ Top 3 armaduras ({top3Sum:F1}%) dentro del cap de {_maxArmorDefPct:F0}%.");
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        EndPanel();
    }

    #endregion

    #region Tab — Player

    private void DrawPlayer()
    {
        SectionHeader("Player — Stats base y Skill Tree");

        if (_baseStats.Count == 0)
        {
            EditorGUILayout.HelpBox("No se encontraron CharacterBaseStatsSO assets.", MessageType.Warning);
            return;
        }

        var names = new string[_baseStats.Count];
        for (int i = 0; i < _baseStats.Count; i++) names[i] = _baseStats[i].name;
        EditorGUI.BeginChangeCheck();
        _selPlayerSO = EditorGUILayout.Popup("Player Base Stats SO", _selPlayerSO, names);
        if (EditorGUI.EndChangeCheck()) { _playerBaseStatsSO = null; _playerBaseSOTarget = null; }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();

        // ── Left: Skill Tree ─────────────────────────────────────────────────
        GUILayout.BeginVertical(GUILayout.Width(380f));
        BeginPanel("Skill Tree — Niveles y configuración", 380f);
        EnsureUpgradeLevels();
        EnsureNodeEditExpanded();

        _showPerLevelBreakdown = EditorGUILayout.Toggle("Mostrar desglose por nivel", _showPerLevelBreakdown);
        GUILayout.Space(4);

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node == null) continue;

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1f, GUILayout.ExpandWidth(true)),
                new Color(0.28f, 0.28f, 0.33f));
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            GUILayout.Label($"{node.upgradeType}  [{node.progressionType}]", EditorStyles.boldLabel, GUILayout.Width(240f));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Nivel", GUILayout.Width(40f));
            _upgradeLvls[i] = EditorGUILayout.IntSlider(_upgradeLvls[i], 0, node.maxLevel);
            GUILayout.EndHorizontal();

            float curVal  = node.GetTotalValueAtLevel(_upgradeLvls[i]);
            int   curCost = node.GetCostForLevel(_upgradeLvls[i]);

            GUILayout.BeginHorizontal();
            GUI.color = COL_GREEN;
            GUILayout.Label($"+{curVal:F1}  acumulado", EditorStyles.miniLabel, GUILayout.Width(130f));
            GUI.color = _upgradeLvls[i] < node.maxLevel ? new Color(0.85f, 0.75f, 0.30f) : new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label(_upgradeLvls[i] < node.maxLevel ? $"Siguiente: {curCost} AuraDust" : "MAX", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (_showPerLevelBreakdown && node.maxLevel > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                for (int lv = 1; lv <= node.maxLevel; lv++)
                {
                    bool active = lv == _upgradeLvls[i];
                    GUILayout.BeginHorizontal();
                    GUI.color = active ? COL_GREEN : new Color(0.60f, 0.60f, 0.60f);
                    GUILayout.Label($"  Lv {lv}", EditorStyles.miniLabel, GUILayout.Width(40f));
                    float delta = node.GetTotalValueAtLevel(lv) - node.GetTotalValueAtLevel(lv - 1);
                    GUILayout.Label($"+{delta:F1}  (acum {node.GetTotalValueAtLevel(lv):F1})",
                        EditorStyles.miniLabel, GUILayout.Width(130f));
                    GUILayout.Label($"{node.GetCostForLevel(lv - 1)} AD", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            // Node SO editing (mode, maxLevel, progression params)
            _nodeEditExpanded[i] = EditorGUILayout.Foldout(_nodeEditExpanded[i], "  Configurar nodo");
            if (_nodeEditExpanded[i])
            {
                var nso = GetNodeEditSO(node);
                nso.Update();
                EditorGUI.indentLevel++;
                DrawProp(nso, "maxLevel",        "Max niveles");
                DrawProp(nso, "progressionType", "Tipo de progresión");
                GUILayout.Space(2);
                switch (node.progressionType)
                {
                    case Enums.ProgressionType.Linear:
                        DrawProp(nso, "linearValueBase",      "Valor base");
                        DrawProp(nso, "linearValueIncrement", "Incremento por nivel");
                        DrawProp(nso, "linearCostBase",       "Coste base");
                        DrawProp(nso, "linearCostStep",       "Coste por nivel");
                        break;
                    case Enums.ProgressionType.Multiplicative:
                        DrawProp(nso, "multiValueBase",       "Valor base");
                        DrawProp(nso, "multiValueMultiplier", "Multiplicador valor");
                        DrawProp(nso, "multiCostBase",        "Coste base");
                        DrawProp(nso, "multiCostMultiplier",  "Multiplicador coste");
                        break;
                    case Enums.ProgressionType.Power:
                        DrawProp(nso, "powerA",              "A (factor)");
                        DrawProp(nso, "powerB",              "B (exponente)");
                        DrawProp(nso, "powerC",              "C (offset)");
                        DrawProp(nso, "powerCostBase",       "Coste base");
                        DrawProp(nso, "powerCostMultiplier", "Multiplicador coste");
                        break;
                    case Enums.ProgressionType.Exponential:
                        DrawProp(nso, "expValueBase",        "Valor base");
                        DrawProp(nso, "expValueGrowth",      "Crecimiento");
                        DrawProp(nso, "expCostBase",         "Coste base");
                        DrawProp(nso, "expCostGrowth",       "Crecimiento coste");
                        break;
                    case Enums.ProgressionType.Logarithmic:
                        DrawProp(nso, "logA",                "A");
                        DrawProp(nso, "logK",                "K");
                        DrawProp(nso, "logC",                "C (offset)");
                        DrawProp(nso, "logCostBase",         "Coste base");
                        DrawProp(nso, "logCostMultiplier",   "Multiplicador coste");
                        break;
                    case Enums.ProgressionType.DiminishingReturns:
                        DrawProp(nso, "drMaxValue",          "Valor máximo");
                        DrawProp(nso, "drK",                 "K (velocidad)");
                        DrawProp(nso, "drUseHyperbolic",     "Hiperbólico");
                        DrawProp(nso, "drCostBase",          "Coste base");
                        DrawProp(nso, "drCostMultiplier",    "Multiplicador coste");
                        break;
                    case Enums.ProgressionType.Manual:
                        DrawProp(nso, "manualLevels", "Niveles manuales", true);
                        break;
                }
                EditorGUI.indentLevel--;
                if (nso.ApplyModifiedProperties()) Repaint();
            }

            GUILayout.Space(2);
        }
        EndPanel();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // ── Right: editable base stats + final stats + EHP ───────────────────
        GUILayout.BeginVertical();
        var so = SafeGet(_baseStats, _selPlayerSO);

        // Editable base stats SO
        if (so != null)
        {
            var pso = GetPlayerBaseStatsSO(so);
            pso.Update();
            BeginPanel("Stats base (editable)", 0f);
            var statsProp = pso.FindProperty("_stats");
            if (statsProp != null && statsProp.isArray)
            {
                for (int s = 0; s < statsProp.arraySize; s++)
                {
                    var entry     = statsProp.GetArrayElementAtIndex(s);
                    var typeProp  = entry.FindPropertyRelative("type");
                    var valueProp = entry.FindPropertyRelative("baseValue");
                    if (typeProp == null || valueProp == null) continue;
                    string statName = ((Enums.StatType)typeProp.enumValueIndex).ToString();
                    EditorGUILayout.PropertyField(valueProp, new GUIContent(statName));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No hay stats configuradas en el SO.", MessageType.Info);
            }
            if (pso.ApplyModifiedProperties()) { Repaint(); }
            GUILayout.Space(4);
            if (GUILayout.Button("Ping Stats SO", GUILayout.Width(110))) EditorGUIUtility.PingObject(so);
            EndPanel();
            GUILayout.Space(8);
        }

        // Final stats table (base + upgrade bonuses)
        BeginPanel("Resumen de stats finales", 0f);
        TableHeader(new[] { "Stat", "Base (SO)", "+Mejoras", "= Final" },
                    new[] { 80f,    80f,          80f,        80f });
        bool alt = false;
        foreach (Enums.StatType st in System.Enum.GetValues(typeof(Enums.StatType)))
        {
            float baseVal  = AutoBalanceDataLoader.GetStatBase(so, st);
            float bonus    = AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _upgradeLvls, st);
            float final    = baseVal + bonus;
            TableRow(alt, new[]
            {
                st.ToString(),
                baseVal.ToString("F1"),
                bonus > 0 ? $"+{bonus:F1}" : "—",
                final.ToString("F1"),
            }, new[] { 80f, 80f, 80f, 80f });
            alt = !alt;
        }
        EndPanel();

        GUILayout.Space(8);

        // Vida efectiva (formerly "EHP preview")
        BeginPanel("Vida efectiva (HP con defensa)", 0f);
        float health      = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                          + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _upgradeLvls, Enums.StatType.Health);
        float bonfireRaw  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Defense)
                          + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _upgradeLvls, Enums.StatType.Defense);
        float bonfirePct  = Mathf.Clamp(bonfireRaw, 0f, _maxBonfireDefPct);
        float totalPct    = bonfirePct; // no armor in Player tab
        float reduction   = totalPct / 100f;
        float ehp         = reduction < 1f ? health / (1f - reduction) : 99999f;
        StatRow("Health total",       health.ToString("F0"));
        StatRow("Def. hoguera",       $"{bonfirePct:F1}%  (cap {_maxBonfireDefPct:F0}%)");
        StatRow("Def. armadura",      "0%  (configura en Simulación)");
        StatRow("Reducción total",    $"{totalPct:F1}%  (máx {_maxArmorDefPct + _maxBonfireDefPct:F0}%)");
        StatRow("Vida efectiva",      ehp < 99998f ? ehp.ToString("F0") : "∞");
        GUI.color = new Color(0.5f, 0.5f, 0.5f);
        GUILayout.Label("Vida efectiva = HP / (1 − reducción def.)", EditorStyles.miniLabel);
        GUI.color = Color.white;
        EndPanel();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    #endregion

    #region Tab — Enemies

    private void DrawEnemies()
    {
        SectionHeader("Enemigos — Stats por arquetipo");

        if (_enemies.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No se encontraron EnemyDefinition assets.\n\n" +
                "Crea uno por tipo de enemigo:\n" +
                "  Create → Expedition → Enemy Definition\n\n" +
                "Asigna: displayName, prefab, baseStats (CharacterBaseStatsSO), zone y category.\n" +
                "Un EnemyDefinition por arquetipo (Normal, Tank, Fast…), no por instancia.",
                MessageType.Warning);
            return;
        }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(LIST_W));
        _listScrolls[(int)Tab.Enemies] = GUILayout.BeginScrollView(_listScrolls[(int)Tab.Enemies]);
        for (int i = 0; i < _enemies.Count; i++)
        {
            var  e     = _enemies[i];
            bool hasSO = e.baseStats != null;
            string lbl = $"[Z{e.zone}·{e.category}] {e.displayName ?? e.name}";
            if (!hasSO) lbl += " ⚠";
            DrawListItem(lbl, i == _selEnemy, () =>
            {
                if (_selEnemy != i) { _balanceSO = null; _balanceSOTarget = null; _enemyCtxSO = null; _enemyCtxSOTarget = null; }
                _selEnemy = i;
            });
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(4);

        var enemy = SafeGet(_enemies, _selEnemy);
        if (enemy != null)
        {
            GUILayout.BeginVertical();
            SectionSubHeader($"Editando: {enemy.displayName ?? enemy.name}");

            var ctxSO = GetEnemyCtxSO(enemy);
            ctxSO.Update();
            BeginPanel("Definición del arquetipo", 0f);
            DrawProp(ctxSO, "displayName", "Nombre display");
            DrawProp(ctxSO, "id",          "ID interno");
            DrawProp(ctxSO, "zone",        "Zona");
            DrawProp(ctxSO, "category",    "Categoría");
            float weaponMult = AutoBalanceDataLoader.GetEnemyWeaponMult(enemy);
            StatRow("Enemy Dmg Mult (prefab)", $"x{weaponMult:F2}");
            if (ctxSO.ApplyModifiedProperties()) Repaint();
            EndPanel();

            GUILayout.Space(4);

            if (enemy.baseStats == null)
            {
                EditorGUILayout.HelpBox(
                    "Sin CharacterBaseStatsSO asignado.\n" +
                    "1. Crea uno: Create → BetweenShadows/Characters/Base Stats\n" +
                    "2. Asígnalo al campo 'Base Stats' de la EnemyDefinition.",
                    MessageType.Warning);
                if (GUILayout.Button("Abrir EnemyDefinition", GUILayout.Width(180f)))
                    Selection.activeObject = enemy;
            }
            else
            {
                var statsSO = GetBalanceSO(enemy.baseStats);
                statsSO.Update();

                BeginPanel($"Stats base — {enemy.baseStats.name}", 0f);
                var statsProp = statsSO.FindProperty("_stats");
                if (statsProp != null && statsProp.isArray)
                {
                    for (int s = 0; s < statsProp.arraySize; s++)
                    {
                        var entry     = statsProp.GetArrayElementAtIndex(s);
                        var typeProp  = entry.FindPropertyRelative("type");
                        var valueProp = entry.FindPropertyRelative("baseValue");
                        if (typeProp == null || valueProp == null) continue;
                        var statName = ((Enums.StatType)typeProp.enumValueIndex).ToString();
                        EditorGUILayout.PropertyField(valueProp, new GUIContent(statName));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("El CharacterBaseStatsSO no tiene stats configurados.", MessageType.Info);
                }
                if (statsSO.ApplyModifiedProperties()) Repaint();
                EndPanel();

                GUILayout.Space(4);

                BeginPanel("Preview de combat (fórmula actual)", 0f);
                float hp  = AutoBalanceDataLoader.GetEnemyHealth(enemy);
                float atk = AutoBalanceDataLoader.GetEnemyAttack(enemy) * weaponMult;
                float def = AutoBalanceDataLoader.GetEnemyDefense(enemy);
                float red = Mathf.Min(def / (def + _defK), _defCap);
                StatRow("Health",              hp.ToString("F0"));
                StatRow("Ataque efectivo",     atk.ToString("F1")  + $"  (base × x{weaponMult:F2})");
                StatRow("Defense",             def.ToString("F0"));
                StatRow("Reducción def.",      $"{red * 100f:F1}%  (K={_defK:F0}, cap={_defCap*100f:F0}%)");
                StatRow("Daño real al player", Mathf.Max(atk * (1f - red), 1f).ToString("F1"));
                EndPanel();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ping Stats SO",     GUILayout.Width(110))) EditorGUIUtility.PingObject(enemy.baseStats);
                if (GUILayout.Button("Ping Definition",   GUILayout.Width(110))) EditorGUIUtility.PingObject(enemy);
                if (GUILayout.Button("Open Inspector",    GUILayout.Width(110))) Selection.activeObject = enemy;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        GUILayout.EndHorizontal();
    }

    private SerializedObject GetEnemyCtxSO(Object target)
    {
        if (target == null) return null;
        if (_enemyCtxSOTarget != target) { _enemyCtxSOTarget = target; _enemyCtxSO = new SerializedObject(target); }
        else _enemyCtxSO?.Update();
        return _enemyCtxSO;
    }

    #endregion

    #region Tab — Weapons

    private void DrawWeapons()
    {
        SectionHeader("Armas — Parámetros de balance");
        if (_weapons.Count == 0) { EditorGUILayout.HelpBox("Sin WeaponData assets.", MessageType.Info); return; }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(LIST_W));
        _listScrolls[(int)Tab.Weapons] = GUILayout.BeginScrollView(_listScrolls[(int)Tab.Weapons]);
        for (int i = 0; i < _weapons.Count; i++)
        {
            var w = _weapons[i];
            DrawListItem($"[T{w.weaponTier}] {w.itemNameID ?? w.name} ({w.itemRarity})", i == _selWeapon, () =>
            {
                if (_selWeapon != i) { _balanceSO = null; _balanceSOTarget = null; }
                _selWeapon = i;
            });
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(4);

        var w2 = SafeGet(_weapons, _selWeapon);
        if (w2 != null)
        {
            GUILayout.BeginVertical();
            SectionSubHeader($"Editando: {w2.itemNameID ?? w2.name}");

            BeginPanel("Métricas calculadas", 0f);
            float flat = AutoBalanceDataLoader.GetWeaponFlatAttack(w2);
            StatRow("Flat Attack (modifiers)", flat.ToString("F1"));
            if (w2.combos != null && w2.combos.Count > 0)
            {
                var bestM = AutoBalanceDataLoader.GetComboMetrics(w2.combos[0], flat);
                for (int ci = 1; ci < w2.combos.Count; ci++)
                {
                    var m = AutoBalanceDataLoader.GetComboMetrics(w2.combos[ci], flat);
                    if (m.comboDps > bestM.comboDps) bestM = m;
                }
                StatRow("DPS estimado (mejor combo)", bestM.comboDps.ToString("F2"));
                StatRow("Stamina/combo",              bestM.totalStamina.ToString("F1"));
                StatRow("Pasos del combo",            bestM.steps.Length.ToString());
            }
            else
            {
                StatRow("DPS estimado (mejor combo)", "—");
                StatRow("Stamina/combo",              "—");
                StatRow("Pasos del combo",            "0");
            }
            EndPanel();

            GUILayout.Space(4);

            var bso = GetBalanceSO(w2);
            bso.Update();

            BeginPanel("Identidad", 0f);
            DrawProp(bso, "uniqueID",   "Unique ID");
            DrawProp(bso, "itemNameID", "Name ID");
            DrawProp(bso, "itemRarity", "Rarity");
            DrawProp(bso, "itemType",   "Item Type");
            DrawProp(bso, "equipSlot",  "Equip Slot");
            EndPanel();

            GUILayout.Space(4);

            BeginPanel("Balance", 0f);
            DrawProp(bso, "familyType",            "Familia");
            DrawProp(bso, "weaponTier",             "Weapon Tier");
            DrawProp(bso, "enemyDamageMultiplier",  "Enemy Dmg Mult");
            DrawProp(bso, "skillScoreNeeded",       "Skill Score Needed");
            EndPanel();

            GUILayout.Space(4);

            BeginPanel("Modificadores de stats (Player)", 0f);
            DrawProp(bso, "modifiers", "Modifiers", true);
            EndPanel();

            if (bso.ApplyModifiedProperties()) Repaint();

            GUILayout.Space(6);
            DrawWeaponCombos(w2);
            GUILayout.Space(6);
            PingButton(w2);
            GUILayout.EndVertical();
        }

        GUILayout.EndHorizontal();
    }

    #endregion

    #region Tab — Weapon Combo Analysis

    private static readonly float[] COMBO_COL_W = { 20f, 42f, 118f, 50f, 52f, 58f, 50f, 62f, 52f, 55f };
    private static readonly string[] COMBO_COL_H = { "#", "Input", "Ataque", "Dur(s)", "Vent(s)", "Daño", "Mult", "Stam", "Poise", "DPS" };

    private void DrawWeaponCombos(WeaponData w)
    {
        if (w.combos == null || w.combos.Count == 0)
        {
            BeginPanel("Análisis de Combos", 0f);
            GUILayout.Label("Sin combos configurados.", EditorStyles.miniLabel);
            EndPanel();
            return;
        }

        if (_lastSelWeaponForCombos != _selWeapon)
        {
            _lastSelWeaponForCombos = _selWeapon;
            _comboFoldouts = new bool[w.combos.Count];
            _attackSOs.Clear();
        }
        if (_comboFoldouts.Length != w.combos.Count)
            System.Array.Resize(ref _comboFoldouts, w.combos.Count);

        float weaponFlat = AutoBalanceDataLoader.GetWeaponFlatAttack(w);
        var allMetrics = new AutoBalanceDataLoader.ComboMetrics[w.combos.Count];
        for (int c = 0; c < w.combos.Count; c++)
            allMetrics[c] = AutoBalanceDataLoader.GetComboMetrics(w.combos[c], weaponFlat);

        BeginPanel("Análisis de Combos", 0f);

        int bestDps = 0;
        for (int c = 1; c < allMetrics.Length; c++)
            if (allMetrics[c].comboDps > allMetrics[bestDps].comboDps) bestDps = c;

        GUILayout.BeginHorizontal();
        GUI.color = COL_ATK;
        GUILayout.Label(
            $"↑ DPS: Combo {bestDps} ({allMetrics[bestDps].inputSequence}) → {allMetrics[bestDps].comboDps:F1} dmg/s",
            EditorStyles.miniLabel);
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();
        GUI.color = COL_STA;
        GUILayout.Label(
            $"Eficiencia: {allMetrics[bestDps].staminaEfficiency:F2} dmg/sta",
            EditorStyles.miniLabel);
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        for (int c = 0; c < w.combos.Count; c++)
        {
            var combo = w.combos[c];
            var m = allMetrics[c];

            string foldoutLabel =
                $"Combo {c}  —  {m.inputSequence}   |  DPS {m.comboDps:F1}  Daño {m.totalDamage:F0}  Stam {m.totalStamina:F0}";
            _comboFoldouts[c] = EditorGUILayout.Foldout(_comboFoldouts[c], foldoutLabel, true);

            if (!_comboFoldouts[c])
            {
                GUILayout.Space(2);
                continue;
            }

            GUILayout.Space(2);
            TableHeader(COMBO_COL_H, COMBO_COL_W);

            for (int s = 0; s < m.steps.Length; s++)
            {
                var step = m.steps[s];
                var attackData = combo.steps[s]?.attack;

                if (s % 2 == 1)
                    EditorGUI.DrawRect(
                        GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);

                GUILayout.BeginHorizontal();

                ColLabel((s + 1).ToString(), COMBO_COL_W[0], false);

                Color inputCol = step.inputLabel switch
                {
                    "L" => COL_BLUE,
                    "H" => COL_ORANGE,
                    "S" => COL_PURPLE,
                    _   => COL_GREEN,
                };
                GUI.color = inputCol;
                GUILayout.Label(step.inputLabel, EditorStyles.miniBoldLabel, GUILayout.Width(COMBO_COL_W[1]));
                GUI.color = Color.white;

                ColLabel(step.attackName, COMBO_COL_W[2], false);
                ColLabel(step.animLength.ToString("F2"), COMBO_COL_W[3], false);
                ColLabel(step.activeWindowTime > 0.001f ? step.activeWindowTime.ToString("F2") : "—",
                    COMBO_COL_W[4], false);

                ColLabel(step.damage.ToString("F0"), COMBO_COL_W[5], false);

                if (attackData != null)
                {
                    var so     = GetAttackDataSO(attackData);
                    var pMult  = so.FindProperty("damageMultiplier");
                    var pStam  = so.FindProperty("staminaCost");
                    var pPoise = so.FindProperty("poiseDamage");

                    if (pMult  != null) EditorGUILayout.PropertyField(pMult,  GUIContent.none, GUILayout.Width(COMBO_COL_W[6]));
                    else ColLabel(step.damageMultiplier.ToString("F2"), COMBO_COL_W[6], false);

                    if (pStam  != null) EditorGUILayout.PropertyField(pStam,  GUIContent.none, GUILayout.Width(COMBO_COL_W[7]));
                    else ColLabel(step.staminaCost.ToString("F0"), COMBO_COL_W[7], false);

                    if (pPoise != null) EditorGUILayout.PropertyField(pPoise, GUIContent.none, GUILayout.Width(COMBO_COL_W[8]));
                    else ColLabel("—", COMBO_COL_W[8], false);

                    if (so.ApplyModifiedProperties()) Repaint();
                }
                else
                {
                    ColLabel(step.damageMultiplier.ToString("F2"), COMBO_COL_W[6], false);
                    ColLabel(step.staminaCost.ToString("F0"),      COMBO_COL_W[7], false);
                    ColLabel("—",                                   COMBO_COL_W[8], false);
                }

                GUI.color = COL_ATK;
                GUILayout.Label(step.stepDps.ToString("F1"), EditorStyles.miniLabel, GUILayout.Width(COMBO_COL_W[9]));
                GUI.color = Color.white;

                GUILayout.EndHorizontal();
            }

            SectionDivider();
            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.70f, 0.70f, 0.70f);
            GUILayout.Label("Total", EditorStyles.miniBoldLabel,
                GUILayout.Width(COMBO_COL_W[0] + COMBO_COL_W[1] + COMBO_COL_W[2]));
            GUI.color = Color.white;
            ColLabel(m.totalTime.ToString("F2") + "s", COMBO_COL_W[3], true);
            GUILayout.Space(COMBO_COL_W[4]);
            ColLabel(m.totalDamage.ToString("F0"), COMBO_COL_W[5], true);
            GUILayout.Space(COMBO_COL_W[6]);
            ColLabel(m.totalStamina.ToString("F0"), COMBO_COL_W[7], true);
            GUILayout.Space(COMBO_COL_W[8]);
            GUI.color = COL_ATK;
            GUILayout.Label(m.comboDps.ToString("F1"), EditorStyles.miniBoldLabel, GUILayout.Width(COMBO_COL_W[9]));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = COL_STA;
            GUILayout.Label($"  Eficiencia: {m.staminaEfficiency:F2} dmg/sta", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            for (int s = 0; s < combo.steps.Count; s++)
            {
                var comboStep = combo.steps[s];
                if (comboStep?.attack == null) continue;
                string btnLabel = string.IsNullOrEmpty(comboStep.attack.attackName)
                    ? comboStep.attack.name
                    : comboStep.attack.attackName;
                if (GUILayout.Button(btnLabel, EditorStyles.miniButton, GUILayout.Width(80f)))
                    EditorGUIUtility.PingObject(comboStep.attack);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        EndPanel();
    }

    private SerializedObject GetAttackDataSO(AttackData atk)
    {
        if (!_attackSOs.TryGetValue(atk, out var so) || so == null || so.targetObject == null)
        {
            so = new SerializedObject(atk);
            _attackSOs[atk] = so;
        }
        else so.Update();
        return so;
    }

    #endregion

    #region Tab — Armors

    private void DrawArmors()
    {
        SectionHeader("Armaduras — Parámetros de balance");
        if (_armors.Count == 0) { EditorGUILayout.HelpBox("Sin armaduras.", MessageType.Info); return; }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(LIST_W));
        _listScrolls[(int)Tab.Armors] = GUILayout.BeginScrollView(_listScrolls[(int)Tab.Armors]);
        for (int i = 0; i < _armors.Count; i++)
        {
            var a = _armors[i];
            DrawListItem($"[{a.equipSlot}] {a.itemNameID ?? a.name} ({a.itemRarity})", i == _selArmor, () =>
            {
                if (_selArmor != i) { _balanceSO = null; _balanceSOTarget = null; }
                _selArmor = i;
            });
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(4);

        var a2 = SafeGet(_armors, _selArmor);
        if (a2 != null)
        {
            GUILayout.BeginVertical();
            SectionSubHeader($"Editando: {a2.itemNameID ?? a2.name}");

            BeginPanel("Métricas calculadas", 0f);
            var single = new List<EquipableItemData> { a2 };
            float defVal = AutoBalanceDataLoader.GetArmorStatFlat(single, Enums.StatType.Defense);
            StatRow("+Health",    AutoBalanceDataLoader.GetArmorStatFlat(single, Enums.StatType.Health).ToString("F0"));
            StatRow("+Defense",   $"{defVal:F0}  ({defVal:F1}% reducción)");
            StatRow("+Stamina",   AutoBalanceDataLoader.GetArmorStatFlat(single, Enums.StatType.Stamina).ToString("F0"));
            EndPanel();

            GUILayout.Space(4);

            var bso = GetBalanceSO(a2);
            bso.Update();

            BeginPanel("Identidad", 0f);
            DrawProp(bso, "uniqueID",   "Unique ID");
            DrawProp(bso, "itemNameID", "Name ID");
            DrawProp(bso, "itemRarity", "Rarity");
            DrawProp(bso, "equipSlot",  "Equip Slot");
            EndPanel();

            GUILayout.Space(4);

            BeginPanel("Modificadores de stats", 0f);
            DrawProp(bso, "modifiers", "Modifiers", true);
            EndPanel();

            if (bso.ApplyModifiedProperties()) Repaint();
            GUILayout.Space(6);
            PingButton(a2);
            GUILayout.EndVertical();
        }

        GUILayout.EndHorizontal();
    }

    #endregion

    #region Tab — Consumables

    private void DrawConsumables()
    {
        SectionHeader("Consumibles — Parámetros de balance");
        if (_consumables.Count == 0) { EditorGUILayout.HelpBox("Sin consumibles.", MessageType.Info); return; }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(LIST_W));
        _listScrolls[(int)Tab.Consumables] = GUILayout.BeginScrollView(_listScrolls[(int)Tab.Consumables]);
        for (int i = 0; i < _consumables.Count; i++)
        {
            var c = _consumables[i];
            int n = c.buffs?.Count ?? 0;
            DrawListItem($"{c.itemNameID ?? c.name} ({n} buff{(n != 1 ? "s" : "")})", i == _selConsumable, () =>
            {
                if (_selConsumable != i) { _balanceSO = null; _balanceSOTarget = null; }
                _selConsumable = i;
            });
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(4);

        var c2 = SafeGet(_consumables, _selConsumable);
        if (c2 != null)
        {
            GUILayout.BeginVertical();
            SectionSubHeader($"Editando: {c2.itemNameID ?? c2.name}");

            if (_simResult.valid)
            {
                BeginPanel("Impacto simulado (vs último enemigo)", 0f);
                if (c2.buffs != null)
                {
                    foreach (var b in c2.buffs)
                    {
                        if (b.applicationMode != Enums.BuffApplicationMode.ModifyStat) continue;
                        if (b.statType == Enums.StatType.Attack)
                        {
                            float newDPS = _simResult.playerDPS * (1f + b.baseValue);
                            StatRow("DPS con buff",  newDPS.ToString("F2"));
                            float so2 = SafeGet(_baseStats, _simPlayerSO) != null
                                ? AutoBalanceDataLoader.GetEnemyHealth(SafeGet(_enemies, _simEnemy)) / newDPS : 0f;
                            StatRow("TTK con buff",  so2 > 0 ? $"{so2:F1}s" : "-");
                        }
                    }
                }
                EndPanel();
                GUILayout.Space(4);
            }

            var bso = GetBalanceSO(c2);
            bso.Update();

            BeginPanel("Identidad", 0f);
            DrawProp(bso, "uniqueID",   "Unique ID");
            DrawProp(bso, "itemNameID", "Name ID");
            DrawProp(bso, "itemRarity", "Rarity");
            EndPanel();

            GUILayout.Space(4);

            BeginPanel("Efectos (buffs)", 0f);
            DrawProp(bso, "buffs", "Buffs", true);
            EndPanel();

            if (bso.ApplyModifiedProperties()) Repaint();
            GUILayout.Space(6);
            PingButton(c2);
            GUILayout.EndVertical();
        }

        GUILayout.EndHorizontal();
    }

    #endregion

    #region Tab — Curves

    private void DrawCurves()
    {
        SectionHeader("Curvas — Progresión simulada");
        EnsureUpgradeLevels();

        if (_nodes.Count == 0 || _baseStats.Count == 0)
        {
            EditorGUILayout.HelpBox("Se necesitan CharacterBaseStatsSO y UpgradeNodeSO assets.", MessageType.Warning);
            return;
        }

        var so = SafeGet(_baseStats, _selPlayerSO);

        SectionSubHeader("Progresión de stats del player por nivel de mejora");

        int maxLv = 0;
        foreach (var n in _nodes) if (n != null) maxLv = Mathf.Max(maxLv, n.maxLevel);
        if (maxLv == 0) maxLv = 5;

        var statTypes = new[] { Enums.StatType.Health, Enums.StatType.Attack, Enums.StatType.Defense, Enums.StatType.Stamina };
        var statCols  = new[] { COL_HP, COL_ATK, COL_DEF, COL_STA };

        GUILayout.BeginHorizontal();
        for (int si = 0; si < statTypes.Length; si++)
        {
            var  st    = statTypes[si];
            var  col   = statCols[si];
            float baseV = AutoBalanceDataLoader.GetStatBase(so, st);

            UpgradeNodeSO node = null;
            foreach (var n in _nodes)
                if (n != null && n.upgradeType == StatTypeToUpgradeType(st)) { node = n; break; }

            int pts  = maxLv + 1;
            var vals = new float[pts];
            for (int lv = 0; lv <= maxLv; lv++)
                vals[lv] = baseV + (node != null ? node.GetTotalValueAtLevel(lv) : 0f);

            GUILayout.BeginVertical();
            GUI.color = col;
            GUILayout.Label(st.ToString(), EditorStyles.boldLabel);
            GUI.color = Color.white;

            var curveData = new AnimationCurve();
            for (int lv = 0; lv <= maxLv; lv++)
                curveData.AddKey(new Keyframe(lv, vals[lv]));

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.CurveField(curveData, col,
                    new Rect(0, vals[0] * 0.9f, maxLv, vals[maxLv] * 1.1f - vals[0] * 0.9f),
                    GUILayout.Height(80f), GUILayout.ExpandWidth(true));

            StatRow($"Lv 0",     vals[0].ToString("F0"));
            StatRow($"Lv {maxLv}", vals[maxLv].ToString("F0"));
            StatRow($"Ganancia", $"+{vals[maxLv] - vals[0]:F0}");
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(12);

        SectionSubHeader("Vida efectiva del player por nivel de Health upgrade");
        float baseHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health);
        float baseDef = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Defense);

        UpgradeNodeSO hpNode = null, defNode = null;
        foreach (var n in _nodes)
        {
            if (n?.upgradeType == Enums.UpgradeType.Health)  hpNode  = n;
            if (n?.upgradeType == Enums.UpgradeType.Defense) defNode = n;
        }

        var ehpCurve = new AnimationCurve();
        for (int lv = 0; lv <= maxLv; lv++)
        {
            float hp      = baseHp  + (hpNode  != null ? hpNode.GetTotalValueAtLevel(lv)  : 0f);
            float defRaw  = baseDef + (defNode  != null ? defNode.GetTotalValueAtLevel(lv) : 0f);
            float defPct  = Mathf.Clamp(defRaw, 0f, _maxBonfireDefPct) / 100f;
            float ehp     = defPct < 1f ? hp / (1f - defPct) : 99999f;
            ehpCurve.AddKey(new Keyframe(lv, Mathf.Min(ehp, 9999f)));
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.CurveField(ehpCurve, COL_HP,
                new Rect(0, baseHp * 0.9f, maxLv, 9999f),
                GUILayout.Height(90f), GUILayout.ExpandWidth(true));

        GUILayout.Space(12);

        if (_enemies.Count > 0)
        {
            SectionSubHeader("Health de enemigos por zona y categoría");
            DrawEnemyBarChart();
        }

        if (_targets != null)
        {
            GUILayout.Space(12);
            SectionSubHeader("Rangos objetivo (BalanceTargetConfigSO)");
            TableHeader(new[] { "Zona", "Categoría", "TTK min", "TTK max", "Kills", "Dies" },
                        new[] { 50f,    90f,          65f,       65f,       60f,    60f });
            for (int i = 0; i < _targets.zoneTargets.Length; i++)
            {
                var zt = _targets.zoneTargets[i];
                TableRow(i % 2 == 1, new[]
                {
                    zt.zone.ToString(), zt.category.ToString(),
                    zt.ttk.x.ToString("F1"), zt.ttk.y.ToString("F1"),
                    $"{zt.hitsToKill.x}–{zt.hitsToKill.y}", $"{zt.hitsToDie.x}–{zt.hitsToDie.y}"
                }, new[] { 50f, 90f, 65f, 65f, 60f, 60f });
            }
        }
    }

    private void DrawEnemyBarChart()
    {
        if (_enemies.Count == 0) return;

        float maxH = 1f;
        foreach (var e in _enemies) maxH = Mathf.Max(maxH, AutoBalanceDataLoader.GetEnemyHealth(e));

        Rect area = GUILayoutUtility.GetRect(0, 100f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.16f));

        float barW = area.width / Mathf.Max(_enemies.Count, 1);
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e   = _enemies[i];
            float h = AutoBalanceDataLoader.GetEnemyHealth(e);
            float barH = (h / maxH) * (area.height - 20f);
            float x  = area.x + i * barW + barW * 0.1f;
            float bw = barW * 0.80f;
            float y  = area.y + (area.height - 20f) - barH;

            Color col = e.category switch
            {
                Enums.EnemyCategory.Normal => COL_BLUE,
                Enums.EnemyCategory.Fast   => COL_GREEN,
                Enums.EnemyCategory.Tank   => COL_RED,
                Enums.EnemyCategory.Elite  => COL_ORANGE,
                Enums.EnemyCategory.Boss   => COL_PURPLE,
                _                          => COL_BLUE,
            };

            EditorGUI.DrawRect(new Rect(x, y, bw, barH), col);

            string lbl = e.displayName?.Length > 8
                ? e.displayName.Substring(0, 8) + "…"
                : (e.displayName ?? e.name);
            GUI.Label(new Rect(x, area.y + area.height - 18f, barW, 16f), lbl,
                EditorStyles.centeredGreyMiniLabel);
        }

        GUI.color = new Color(0.6f, 0.6f, 0.6f);
        GUI.Label(new Rect(area.x + 2, area.y + 2, 80f, 14f), $"Max HP: {maxH:F0}", EditorStyles.miniLabel);
        GUI.color = Color.white;
    }

    private static Enums.UpgradeType StatTypeToUpgradeType(Enums.StatType s) => s switch
    {
        Enums.StatType.Health  => Enums.UpgradeType.Health,
        Enums.StatType.Defense => Enums.UpgradeType.Defense,
        Enums.StatType.Stamina => Enums.UpgradeType.Stamina,
        Enums.StatType.Speed   => Enums.UpgradeType.Speed,
        Enums.StatType.Attack  => Enums.UpgradeType.Attack,
        Enums.StatType.Weight  => Enums.UpgradeType.Weight,
        _                      => Enums.UpgradeType.Health,
    };

    #endregion

    #region Tab — Simulation

    private void DrawSimulation()
    {
        if (!_dataLoaded) { EditorGUILayout.HelpBox("Carga datos primero.", MessageType.Info); return; }

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_simMode == SimMode.Single,      "Individual",  EditorStyles.miniButton, GUILayout.Width(80f)))  _simMode = SimMode.Single;
        if (GUILayout.Toggle(_simMode == SimMode.Batch,       "Vs todos",   EditorStyles.miniButton, GUILayout.Width(80f)))  _simMode = SimMode.Batch;
        if (GUILayout.Toggle(_simMode == SimMode.Matrix,      "Matrix",     EditorStyles.miniButton, GUILayout.Width(80f)))  _simMode = SimMode.Matrix;
        if (GUILayout.Toggle(_simMode == SimMode.TierSummary,   "Por Tier",      EditorStyles.miniButton, GUILayout.Width(80f)))  _simMode = SimMode.TierSummary;
        if (GUILayout.Toggle(_simMode == SimMode.SurvivalCheck, "Supervivencia", EditorStyles.miniButton, GUILayout.Width(90f)))  _simMode = SimMode.SurvivalCheck;
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        if (_simMode == SimMode.Matrix)
        {
            DrawMatrixMode();
            return;
        }

        if (_simMode == SimMode.TierSummary)
        {
            DrawTierSummary();
            return;
        }

        if (_simMode == SimMode.SurvivalCheck)
        {
            DrawSurvivalCheck();
            return;
        }

        if (_simMode == SimMode.Batch)
        {
            DrawBatchSimulation();
            return;
        }

        // Single
        GUILayout.BeginHorizontal();
        DrawSimConfig();
        GUILayout.Space(8);
        DrawSimResults();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        DrawKPIStrip();
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        DrawAlerts();
        GUILayout.Space(8);
        DrawWeaponComparator();
        GUILayout.EndHorizontal();
    }

    private void DrawSimConfig()
    {
        BeginPanel("Configuración", 320f);
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();

        SectionSubHeader("Player");
        if (_baseStats.Count > 0)
        {
            var names = new string[_baseStats.Count];
            for (int i = 0; i < _baseStats.Count; i++) names[i] = _baseStats[i].name;
            _simPlayerSO = EditorGUILayout.Popup("Base Stats SO", Mathf.Clamp(_simPlayerSO, 0, names.Length - 1), names);
        }

        SectionSubHeader("Mejoras (hoguera)");
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i] == null) continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label(_nodes[i].upgradeType.ToString(), GUILayout.Width(70));
            _simUpgradeLvls[i] = EditorGUILayout.IntSlider(_simUpgradeLvls[i], 0, _nodes[i].maxLevel);
            GUI.color = COL_GREEN;
            GUILayout.Label($"+{_nodes[i].GetTotalValueAtLevel(_simUpgradeLvls[i]):F0}", GUILayout.Width(40));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        SectionSubHeader("Arma (player)");
        if (_weapons.Count > 0)
        {
            var wn = new string[_weapons.Count];
            for (int i = 0; i < _weapons.Count; i++)
                wn[i] = $"[T{_weapons[i].weaponTier}] {_weapons[i].itemNameID ?? _weapons[i].name}";
            _simWeapon = EditorGUILayout.Popup("Arma", Mathf.Clamp(_simWeapon, 0, wn.Length - 1), wn);
        }

        // Rarity selector — applies multiplier to the weapon's flat Attack modifiers.
        var rarityOptions = new string[5];
        rarityOptions[0] = "— Sin rareza (valor base) —";
        var rarityEnumVals = (Enums.ItemRarity[])System.Enum.GetValues(typeof(Enums.ItemRarity));
        for (int i = 0; i < rarityEnumVals.Length; i++) rarityOptions[i + 1] = rarityEnumVals[i].ToString();
        _simWeaponRarity = EditorGUILayout.Popup("Rareza arma", _simWeaponRarity + 1, rarityOptions) - 1;

        if (_simWeaponRarity >= 0)
        {
            (float rMin, float rMax) = RarityUtility.GetMultiplierRange(rarityEnumVals[_simWeaponRarity]);
            GUI.color = COL_PURPLE;
            GUILayout.Label($"Multiplicador: ×{rMin:F2} – ×{rMax:F2}  (mid ×{(rMin+rMax)*0.5f:F2})",
                EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        SectionSubHeader("Armaduras (player)");
        for (int s = 0; s < ARMOR_SLOTS.Length; s++)
        {
            var slot     = ARMOR_SLOTS[s];
            var filtered = GetArmorsBySlot(slot);
            if (filtered.Count == 0) continue;

            var popupNames = new string[filtered.Count + 1];
            popupNames[0] = "— Ninguna —";
            for (int a = 0; a < filtered.Count; a++)
                popupNames[a + 1] = filtered[a].itemNameID ?? filtered[a].name;

            _simArmorPerSlot[s] = EditorGUILayout.Popup(ARMOR_SLOT_LABELS[s],
                Mathf.Clamp(_simArmorPerSlot[s], 0, popupNames.Length - 1), popupNames);
        }

        // Defense preview
        float armorDef   = ComputeArmorDefRaw();
        float bonfireDef = AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense);
        float aClamp     = Mathf.Clamp(armorDef,   0f, _maxArmorDefPct);
        float bClamp     = Mathf.Clamp(bonfireDef, 0f, _maxBonfireDefPct);
        float totalDef   = Mathf.Clamp(aClamp + bClamp, 0f, _maxArmorDefPct + _maxBonfireDefPct);
        GUILayout.Space(4);
        GUI.color = COL_DEF;
        GUILayout.Label($"Def. armadura: {aClamp:F1}%  |  Hoguera: {bClamp:F1}%  |  Total: {totalDef:F1}%", EditorStyles.miniLabel);
        GUI.color = Color.white;

        SectionSubHeader("Defensa máxima (diseño)");
        _maxArmorDefPct   = EditorGUILayout.Slider("Cap armadura (%)",  _maxArmorDefPct,  0f, 50f);
        _maxBonfireDefPct = EditorGUILayout.Slider("Cap hoguera (%)",   _maxBonfireDefPct, 0f, 50f);
        GUI.color = new Color(0.5f, 0.5f, 0.5f);
        GUILayout.Label($"Máx total: {_maxArmorDefPct + _maxBonfireDefPct:F0}%  (suma de caps)", EditorStyles.miniLabel);
        GUI.color = Color.white;

        SectionSubHeader("Enemigo");
        if (_enemies.Count > 0)
        {
            var en = new string[_enemies.Count];
            for (int i = 0; i < _enemies.Count; i++)
                en[i] = $"[Z{_enemies[i].zone}·{_enemies[i].category}] {_enemies[i].displayName ?? _enemies[i].name}";
            _simEnemy = EditorGUILayout.Popup("Enemigo", Mathf.Clamp(_simEnemy, 0, en.Length - 1), en);
        }

        // Enemy weapon override
        if (_weapons.Count > 0)
        {
            var ewn = new string[_weapons.Count + 1];
            ewn[0] = "Automático (prefab)";
            for (int i = 0; i < _weapons.Count; i++)
                ewn[i + 1] = $"[{_weapons[i].familyType}] {_weapons[i].itemNameID ?? _weapons[i].name}";
            int prev = _simEnemyWeaponIdx + 1; // shift: -1→0, 0→1, etc.
            int next = EditorGUILayout.Popup("Arma enemigo", Mathf.Clamp(prev, 0, ewn.Length - 1), ewn);
            _simEnemyWeaponIdx = next - 1;
        }

        SectionSubHeader("Fórmula defensa (enemigos)");
        _defK   = EditorGUILayout.FloatField("Resistencia base K", _defK);
        _defCap = EditorGUILayout.Slider("Cap reducción (%)",      _defCap * 100f, 0f, 95f) / 100f;
        GUI.color = new Color(0.5f, 0.5f, 0.5f);
        GUILayout.Label("Fórmula: def/(def+K). Mayor K → menos reducción.", EditorStyles.miniLabel);
        GUI.color = Color.white;

        GUILayout.Space(6);
        if (GUILayout.Button("▶  Simular combate", GUILayout.Height(26))) RunSimulation();
        EndPanel();
    }

    private void DrawSimResults()
    {
        BeginPanel("Resultados", 0f);
        if (!_simResult.valid)
        { GUILayout.Label("Presiona Simular.", EditorStyles.centeredGreyMiniLabel); EndPanel(); return; }

        var r = _simResult;
        TableHeader(new[] { "Métrica", "Valor", "Objetivo" }, new[] { 160f, 90f, 100f });
        ResultRow("TTK (s)",                r.ttk < 9999f ? $"{r.ttk:F1}s" : "∞",                 GetTargetStr(0), false);
        ResultRow("Golpes para matar",      r.hitsToKill.ToString(),                                GetTargetStr(1), false);
        ResultRow("Golpes para morir",      r.hitsToDie.ToString(),                                 GetTargetStr(2), true);
        ResultRow("DPS estimado",           r.playerDPS.ToString("F2"),                             "-",             false);
        ResultRow("Daño/golpe player",      r.playerDamagePerHit.ToString("F1"),                    "-",             false);
        ResultRow("Daño/golpe enemigo",     r.enemyDamagePerHit.ToString("F1"),                     "-",             true);
        ResultRow("Vida efectiva player",   r.playerEHP < 99998f ? r.playerEHP.ToString("F0") : "∞","-",            false);
        ResultRow("Def. armadura player",   $"{r.playerArmorDefPct * 100f:F1}%",                    $"≤{_maxArmorDefPct:F0}%",   false);
        ResultRow("Def. hoguera player",    $"{r.playerBonfireDefPct * 100f:F1}%",                  $"≤{_maxBonfireDefPct:F0}%", false);
        ResultRow("Reducción total player", $"{r.playerDefReduction * 100f:F1}%",                   $"≤{_maxArmorDefPct+_maxBonfireDefPct:F0}%", false);
        ResultRow("Red. def. enemigo",      $"{r.enemyDefReduction  * 100f:F1}%",                   $"≤{_defCap*100f:F0}%",      false);
        ResultRow("Stam/combo",             r.staminaPerCombo.ToString("F1"),                        "-",             false);

        string poiseDisplay = r.hitsToStagger >= 9999 ? "—" : r.hitsToStagger.ToString();
        ResultRow("Golpes stagger",         poiseDisplay, "-", false);

        if (_simWeaponRarity >= 0 && _simResultRarityMin.valid && _simResultRarityMax.valid)
        {
            GUILayout.Space(4);
            SectionDivider();
            GUILayout.Space(2);
            GUI.color = COL_PURPLE;
            GUILayout.Label("Rango por rareza  (peor caso → mejor caso)", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;
            var rLo = _simResultRarityMin; // mult mínimo → menos daño → TTK alto
            var rHi = _simResultRarityMax; // mult máximo → más daño  → TTK bajo
            ResultRow("TTK rango",        $"{(rHi.ttk < 9999f ? rHi.ttk.ToString("F1") : "∞")}s – {(rLo.ttk < 9999f ? rLo.ttk.ToString("F1") : "∞")}s", "-", false);
            ResultRow("Kills rango",      $"{rHi.hitsToKill} – {rLo.hitsToKill}",                                                                           "-", false);
            ResultRow("DPS rango",        $"{rHi.playerDPS:F1} – {rLo.playerDPS:F1}",                                                                       "-", false);
            ResultRow("Dmg/golpe rango",  $"{rHi.playerDamagePerHit:F1} – {rLo.playerDamagePerHit:F1}",                                                     "-", false);
        }
        EndPanel();
    }

    private void DrawBatchSimulation()
    {
        EnsureEnemyFamilyIdx();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("▶  Simular vs todos los enemigos", GUILayout.Height(26), GUILayout.Width(240f)))
            RunBatchSimulation();
        GUILayout.Space(8);
        _primaryOnly = GUILayout.Toggle(_primaryOnly, "Solo Primary", EditorStyles.miniButton, GUILayout.Width(90f));
        GUILayout.EndHorizontal();

        if (_batchResults.Count == 0)
        { GUILayout.Label("Presiona el botón para simular.", EditorStyles.centeredGreyMiniLabel); return; }

        var weapon = SafeGet(_weapons, _simWeapon);
        int wTier  = weapon != null ? weapon.weaponTier : 0;

        GUILayout.Space(6);
        SectionSubHeader("Resultados — build actual vs todos los enemigos");
        TableHeader(new[] { "Enemigo", "Zona", "Cat.", "Matchup", "TTK", "Kills", "Dies", "Poise", "Estado" },
                    new[] { 140f,      45f,    65f,    90f,       55f,   40f,     40f,    55f,     90f });

        _batchScroll = GUILayout.BeginScrollView(_batchScroll, GUILayout.Height(300f));
        for (int i = 0; i < _batchResults.Count; i++)
        {
            var (enemy, res) = _batchResults[i];

            BalanceTargetConfigSO.MatchupLabel matchup = BalanceTargetConfigSO.MatchupLabel.Primary;
            BalanceTargetConfigSO.ZoneTargets  adjZT   = default;
            bool hasTargets = false;

            if (_targets != null)
            {
                hasTargets = _targets.TryGetMatchupTargets(wTier, enemy.zone, enemy.category, out adjZT, out matchup);
                if (_primaryOnly && matchup != BalanceTargetConfigSO.MatchupLabel.Primary) continue;
            }

            var (status, statusCol) = hasTargets
                ? EvaluateMatchup(res, adjZT, matchup)
                : ("Sin target", new Color(0.5f, 0.5f, 0.5f));

            string poiseStr = res.hitsToStagger >= 9999 ? "—" :
                (res.staggerBeforeKill ? $"★{res.hitsToStagger}" : $"{res.hitsToStagger}");
            Color poiseCol = res.staggerBeforeKill ? COL_GREEN : new Color(0.6f, 0.6f, 0.6f);

            if (i % 2 == 1) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);
            GUILayout.BeginHorizontal();
            ColLabel(enemy.displayName ?? enemy.name, 140f, false);
            ColLabel(enemy.zone.ToString(),            45f,  false);
            ColLabel(enemy.category.ToString(),         65f,  false);
            GUI.color = MatchupColor(matchup);
            GUILayout.Label(matchup.ToString(), EditorStyles.miniLabel, GUILayout.Width(90f));
            GUI.color = Color.white;
            ColLabel(res.ttk < 9999f ? $"{res.ttk:F1}s" : "∞", 55f, false);
            ColLabel(res.hitsToKill.ToString(),  40f, false);
            ColLabel(res.hitsToDie.ToString(),   40f, false);
            GUI.color = poiseCol;
            GUILayout.Label(poiseStr, EditorStyles.miniLabel, GUILayout.Width(55f));
            GUI.color = statusCol;
            GUILayout.Label(status, EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawTierSummary()
    {
        // ── Top bar: tier filter + options + simulate ─────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Label("Tier:", EditorStyles.miniBoldLabel, GUILayout.Width(32f));
        for (int t = 1; t <= 4; t++)
        {
            bool sel = _tierFilter == t;
            GUI.color = sel ? COL_TABSEL : Color.white;
            if (GUILayout.Toggle(sel, $"T{t}", EditorStyles.miniButton, GUILayout.Width(36f))) _tierFilter = t;
            GUI.color = Color.white;
        }
        GUILayout.Space(12);
        _primaryOnly = GUILayout.Toggle(_primaryOnly, "Solo Primary", EditorStyles.miniButton, GUILayout.Width(90f));
        GUILayout.Space(8);
        if (GUILayout.Button("▶  Simular Tier", GUILayout.Height(22), GUILayout.Width(110f)))
        {
            RunTierSummarySimulation();
            _tierSelWeapon = 0;
        }
        GUILayout.EndHorizontal();

        if (_tierSummaryResults.Count == 0 || _tierWeaponList.Count == 0)
        { GUILayout.Space(8); GUILayout.Label("Selecciona un tier y simula.", EditorStyles.centeredGreyMiniLabel); return; }

        // ── Weapon selector buttons ───────────────────────────────────────────
        GUILayout.Space(8);
        SectionSubHeader("Arma");
        GUILayout.BeginHorizontal();
        for (int wi = 0; wi < _tierWeaponList.Count; wi++)
        {
            var w   = _tierWeaponList[wi];
            bool sel = _tierSelWeapon == wi;
            string lbl = w.itemNameID ?? w.name;

            // Count issues for this weapon to show a badge
            int issues = 0;
            foreach (var (rw, _, rres, rzt, rmatch) in _tierSummaryResults)
            {
                if (rw != w) continue;
                var (_, sc) = EvaluateMatchup(rres, rzt, rmatch);
                if (sc == COL_RED || sc == COL_ORANGE) issues++;
            }

            GUI.color = sel ? COL_TABSEL : (issues > 0 ? new Color(0.9f, 0.5f, 0.5f) : new Color(0.75f, 0.75f, 0.75f));
            float btnW = Mathf.Max(80f, lbl.Length * 7.5f);
            if (GUILayout.Button(issues > 0 ? $"{lbl} ⚠{issues}" : lbl,
                    EditorStyles.miniButton, GUILayout.Width(btnW), GUILayout.Height(24f)))
                _tierSelWeapon = wi;
            GUI.color = Color.white;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // ── Results table for selected weapon ─────────────────────────────────
        _tierSelWeapon = Mathf.Clamp(_tierSelWeapon, 0, _tierWeaponList.Count - 1);
        var selWeapon = _tierWeaponList[_tierSelWeapon];

        GUILayout.Space(6);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 28f, GUILayout.ExpandWidth(true)), COL_PANEL);
        GUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUI.color = new Color(0.85f, 0.85f, 1f);
        GUILayout.Label($"[T{selWeapon.weaponTier}] {selWeapon.itemNameID ?? selWeapon.name}", EditorStyles.miniBoldLabel, GUILayout.Height(28f));
        GUI.color = new Color(0.6f, 0.6f, 0.6f);
        GUILayout.Label($"flatAtk: {AutoBalanceDataLoader.GetWeaponFlatAttack(selWeapon):F0}   avgPoise: {AutoBalanceDataLoader.GetAvgPoiseDamage(selWeapon):F1}", EditorStyles.miniLabel, GUILayout.Height(28f));
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        TableHeader(
            new[] { "Enemigo", "Z",  "Cat.", "Matchup", "HTK", "Obj.HTK", "TTK",  "Obj.TTK", "Dies", "Poise", "Estado" },
            new[] {  160f,      30f,  65f,    85f,       40f,   70f,       52f,    80f,        40f,    50f,     90f });

        _tierSummaryScroll = GUILayout.BeginScrollView(_tierSummaryScroll, GUILayout.Height(420f));
        int row = 0;
        foreach (var (weapon, enemy, res, adjZT, matchup) in _tierSummaryResults)
        {
            if (weapon != selWeapon) continue;

            var (status, statusCol) = EvaluateMatchup(res, adjZT, matchup);
            string poiseStr = res.hitsToStagger >= 9999 ? "—" :
                (res.staggerBeforeKill ? $"★{res.hitsToStagger}" : $"{res.hitsToStagger}");
            Color poiseCol = res.staggerBeforeKill ? COL_GREEN : new Color(0.5f, 0.5f, 0.5f);

            if (row % 2 == 1) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);
            GUILayout.BeginHorizontal();
            ColLabel(enemy.displayName ?? enemy.name,                            160f, false);
            ColLabel(enemy.zone.ToString(),                                        30f, false);
            ColLabel(enemy.category.ToString(),                                    65f, false);
            GUI.color = MatchupColor(matchup);
            GUILayout.Label(matchup.ToString(), EditorStyles.miniLabel,            GUILayout.Width(85f));
            GUI.color = Color.white;
            ColLabel(res.hitsToKill.ToString(),                                    40f, false);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label($"[{adjZT.hitsToKill.x}–{adjZT.hitsToKill.y}]", EditorStyles.miniLabel, GUILayout.Width(70f));
            GUI.color = Color.white;
            ColLabel(res.ttk < 9999f ? $"{res.ttk:F1}s" : "∞",                   52f, false);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label($"[{adjZT.ttk.x:F0}–{adjZT.ttk.y:F0}s]",        EditorStyles.miniLabel, GUILayout.Width(80f));
            GUI.color = Color.white;
            ColLabel(res.hitsToDie.ToString(),                                     40f, false);
            GUI.color = poiseCol;
            GUILayout.Label(poiseStr, EditorStyles.miniLabel,                      GUILayout.Width(50f));
            GUI.color = statusCol;
            GUILayout.Label(status, EditorStyles.miniBoldLabel,                    GUILayout.Width(90f));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            row++;
        }
        GUILayout.EndScrollView();
    }

    private void DrawSurvivalCheck()
    {
        SectionSubHeader("Configuración — Supervivencia del player");

        GUILayout.BeginHorizontal();

        // ── Left: player defense setup ────────────────────────────────────────
        BeginPanel("Setup defensa", 260f);

        SectionSubHeader("Tier de armadura");
        GUILayout.BeginHorizontal();
        bool noArmor = _survArmorTier == 0;
        GUI.color = noArmor ? COL_RED : Color.white;
        if (GUILayout.Toggle(noArmor, "Sin", EditorStyles.miniButton, GUILayout.Width(36f))) _survArmorTier = 0;
        GUI.color = Color.white;
        for (int t = 1; t <= 3; t++)
        {
            bool sel = _survArmorTier == t;
            GUI.color = sel ? COL_TABSEL : Color.white;
            if (GUILayout.Toggle(sel, $"T{t}", EditorStyles.miniButton, GUILayout.Width(36f))) _survArmorTier = t;
            GUI.color = Color.white;
        }
        GUILayout.EndHorizontal();

        // Armor defense preview
        float survArmorRaw = _survArmorTier == 0 ? 0f : ComputeArmorDefForTier(_survArmorTier);
        float survArmorFrac = Mathf.Clamp(survArmorRaw, 0f, _maxArmorDefPct) / 100f;
        GUILayout.Space(2);
        GUI.color = COL_DEF;
        GUILayout.Label($"Def. armadura: {survArmorRaw:F1}%  →  cap {Mathf.Min(survArmorRaw, _maxArmorDefPct):F1}%", EditorStyles.miniLabel);
        GUI.color = Color.white;

        GUILayout.Space(6);
        SectionSubHeader("Hoguera — Defensa");
        _survBonfireDef = EditorGUILayout.Slider("Def. hoguera (%)", _survBonfireDef, 0f, _maxBonfireDefPct);
        float survBonfireFrac = Mathf.Clamp(_survBonfireDef, 0f, _maxBonfireDefPct) / 100f;

        GUILayout.Space(4);
        float survTotalDef = survArmorFrac + survBonfireFrac;
        GUI.color = COL_DEF;
        GUILayout.Label($"Reducción total: {survTotalDef * 100f:F1}%  (cap {(_maxArmorDefPct + _maxBonfireDefPct):F0}%)", EditorStyles.miniLabel);
        GUI.color = Color.white;

        GUILayout.Space(6);
        SectionSubHeader("Hoguera — Vida");
        _survBonfireHealth = EditorGUILayout.FloatField("Vida hoguera (+HP)", _survBonfireHealth);
        _survBonfireHealth = Mathf.Max(0f, _survBonfireHealth);

        GUILayout.Space(6);
        SectionSubHeader("Player base stats");
        if (_baseStats.Count > 0)
        {
            var names = new string[_baseStats.Count];
            for (int i = 0; i < _baseStats.Count; i++) names[i] = _baseStats[i].name;
            _simPlayerSO = EditorGUILayout.Popup("Base Stats SO", Mathf.Clamp(_simPlayerSO, 0, names.Length - 1), names);
        }
        var survSO = SafeGet(_baseStats, _simPlayerSO);
        float survHp = AutoBalanceDataLoader.GetStatBase(survSO, Enums.StatType.Health)
                     + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        GUI.color = COL_HP;
        if (_survBonfireHealth > 0f)
            GUILayout.Label($"HP: {survHp:F0}  +  {_survBonfireHealth:F0} hoguera  =  {survHp + _survBonfireHealth:F0}", EditorStyles.miniLabel);
        else
            GUILayout.Label($"HP: {survHp:F0}", EditorStyles.miniLabel);
        GUI.color = Color.white;

        GUILayout.Space(8);
        if (GUILayout.Button("▶  Simular supervivencia", GUILayout.Height(26))) RunSurvivalSimulation();
        EndPanel();

        GUILayout.Space(8);

        // ── Right: info panel ─────────────────────────────────────────────────
        BeginPanel("Información", 0f);
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        GUILayout.Label(
            "Simula cuántos golpes aguanta el player ante\n" +
            "cada enemigo con el tier de armadura y\n" +
            "defensa de hoguera configurados.\n\n" +
            "Compara contra el objetivo hitsToDie del\n" +
            "BalanceTargetConfigSO por zona y categoría.",
            EditorStyles.miniLabel);
        GUI.color = Color.white;
        EndPanel();

        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        EnsureSurvEnemyFamilyIdx();
        SectionSubHeader("Familia de arma por enemigo (simula todos los tiers disponibles de esa familia)");
        TableHeader(new[] { "Enemigo", "Zona", "Familia de arma (todos sus tiers se simulan)" }, new[] { 180f, 40f, 200f });
        for (int ei = 0; ei < _enemies.Count; ei++)
        {
            var e = _enemies[ei];
            GUILayout.BeginHorizontal();
            ColLabel(e.displayName ?? e.name, 180f, false);
            ColLabel(e.zone.ToString(), 40f, false);

            var familyOptions = new string[WEAPON_FAMILY_NAMES.Length + 1];
            familyOptions[0] = "— Auto (prefab) —";
            for (int f = 0; f < WEAPON_FAMILY_NAMES.Length; f++) familyOptions[f + 1] = WEAPON_FAMILY_NAMES[f];

            int prevF = _survEnemyFamilyIdx[ei] + 1;
            int nextF = EditorGUILayout.Popup(Mathf.Clamp(prevF, 0, familyOptions.Length - 1), familyOptions, GUILayout.Width(200f));
            _survEnemyFamilyIdx[ei] = nextF - 1;

            GUILayout.EndHorizontal();
        }

        if (_survResults.Count == 0) return;

        GUILayout.Space(8);
        float survHpTotal = survHp + _survBonfireHealth;
        SectionSubHeader($"Resultados — Arm. T{_survArmorTier}  |  Hoguera def {_survBonfireDef:F0}%  |  HP {survHpTotal:F0}");
        TableHeader(
            new[] { "Enemigo",  "Z",   "Cat.",  "Arma",  "Mult×",  "ATK final", "Dmg/hit", "HitsToDie", "Objetivo", "Estado" },
            new[] {  150f,       25f,   60f,     40f,     45f,      60f,         55f,       65f,         75f,        80f });

        _survScroll = GUILayout.BeginScrollView(_survScroll, GUILayout.Height(400f));
        int lastZone = -1;
        EnemyDefinition lastEnemy = null;
        for (int i = 0; i < _survResults.Count; i++)
        {
            var (enemy, weapTier, tierLabel, enemyMult, res) = _survResults[i];

            if (enemy.zone != lastZone)
            {
                lastZone = enemy.zone;
                lastEnemy = null;
                GUILayout.Space(4);
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), new Color(0.12f, 0.12f, 0.20f));
                GUILayout.BeginHorizontal();
                GUI.color = new Color(0.85f, 0.85f, 1f);
                GUILayout.Label($"Zona {enemy.zone}", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            bool isFirstRowOfEnemy = !ReferenceEquals(enemy, lastEnemy);
            lastEnemy = enemy;

            string status    = "—";
            Color  statusCol = Color.white;
            string targetStr = "—";

            if (_targets != null && _targets.TryGetZoneTargets(enemy.zone, enemy.category, out var zt))
            {
                targetStr = $"{zt.hitsToDie.x}–{zt.hitsToDie.y}";
                bool tooFew  = res.hitsToDie < zt.hitsToDie.x;
                bool tooMany = res.hitsToDie > zt.hitsToDie.y;
                if (tooFew)        { status = "☠ Frágil";   statusCol = COL_RED;    }
                else if (tooMany)  { status = "⛨ Tanque";   statusCol = COL_YELLOW; }
                else               { status = "✓ OK";        statusCol = COL_GREEN;  }
            }

            float eAtkBase  = AutoBalanceDataLoader.GetEnemyAttack(enemy);
            float eAtkFinal = eAtkBase * Mathf.Max(enemyMult, 0.01f);

            Color rowBg = isFirstRowOfEnemy
                ? new Color(0.20f, 0.20f, 0.25f)
                : (i % 2 == 1 ? COL_ROWALT : new Color(0f, 0f, 0f, 0f));
            if (rowBg.a > 0f)
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), rowBg);

            GUILayout.BeginHorizontal();
            // Enemy name: only on first row of that enemy group.
            string nameStr = isFirstRowOfEnemy ? (enemy.displayName ?? enemy.name) : "";
            ColLabel(nameStr,                               150f, isFirstRowOfEnemy);
            ColLabel(isFirstRowOfEnemy ? enemy.zone.ToString() : "",       25f, false);
            ColLabel(isFirstRowOfEnemy ? enemy.category.ToString() : "",   60f, false);
            GUI.color = COL_ATK;
            GUILayout.Label(tierLabel, EditorStyles.miniBoldLabel, GUILayout.Width(40f));
            GUI.color = Color.white;
            ColLabel($"×{enemyMult:F2}",                   45f, false);
            ColLabel(eAtkFinal.ToString("F1"),              60f, false);
            ColLabel(res.enemyDamagePerHit.ToString("F1"), 55f, false);
            GUI.color = statusCol;
            GUILayout.Label(res.hitsToDie.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(65f));
            GUI.color = Color.white;
            ColLabel(targetStr,                             75f, false);
            GUI.color = statusCol;
            GUILayout.Label(status, EditorStyles.miniBoldLabel, GUILayout.Width(80f));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawMatrixMode()
    {
        EnsureEnemyFamilyIdx();

        SectionSubHeader("Asigna familia de arma a cada enemigo");
        EditorGUILayout.HelpBox(
            "Cada enemigo simulará con todas las armas de la familia asignada.\n" +
            "El player simulará con cada arma del juego.\n" +
            "Debes asignar familia a todos los enemigos para simular.",
            MessageType.Info);

        GUILayout.Space(4);

        // Enemy family assignment list
        bool allAssigned = true;
        TableHeader(new[] { "Enemigo", "Zona", "Familia" }, new[] { 180f, 50f, 130f });
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            GUILayout.BeginHorizontal();
            ColLabel(e.displayName ?? e.name, 180f, false);
            ColLabel(e.zone.ToString(),        50f,  false);

            // Popup: "Sin asignar" + all WeaponFamily values
            var familyOptions = new string[WEAPON_FAMILY_NAMES.Length + 1];
            familyOptions[0] = "— Sin asignar —";
            for (int f = 0; f < WEAPON_FAMILY_NAMES.Length; f++) familyOptions[f + 1] = WEAPON_FAMILY_NAMES[f];

            int prev = _enemyFamilyIdx[i] + 1; // -1→0, 0→1, etc.
            int next = EditorGUILayout.Popup(Mathf.Clamp(prev, 0, familyOptions.Length - 1), familyOptions, GUILayout.Width(130f));
            _enemyFamilyIdx[i] = next - 1;

            if (_enemyFamilyIdx[i] < 0)
            {
                allAssigned = false;
                GUI.color = COL_RED;
                GUILayout.Label("⚠", EditorStyles.miniBoldLabel, GUILayout.Width(20f));
                GUI.color = Color.white;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        if (!allAssigned)
        {
            EditorGUILayout.HelpBox("Asigna familia a todos los enemigos para habilitar la simulación.", MessageType.Warning);
        }

        GUI.enabled = allAssigned;
        if (GUILayout.Button("▶  Simular matrix (todas las builds vs todos los enemigos)", GUILayout.Height(28)))
            RunMatrixSimulation();
        GUI.enabled = true;

        if (_matrixResults.Count == 0) return;

        GUILayout.Space(8);
        SectionSubHeader("Resultados — cada enemigo × cada arma player × cada arma enemigo");

        // Build sorted weapon list for header
        var wNames = new List<string>();
        for (int i = 0; i < _weapons.Count; i++)
            wNames.Add($"[T{_weapons[i].weaponTier}] {(_weapons[i].itemNameID ?? _weapons[i].name).Substring(0, Mathf.Min((_weapons[i].itemNameID ?? _weapons[i].name).Length, 10))}");

        // Compact scrollable table
        _matrixScroll = GUILayout.BeginScrollView(_matrixScroll, GUILayout.Height(400f));

        EnemyDefinition lastEnemy   = null;
        WeaponData      lastEWeapon = null;

        for (int i = 0; i < _matrixResults.Count; i++)
        {
            var (enemy, pWeap, eWeap, res) = _matrixResults[i];

            // Enemy + enemy weapon header
            if (enemy != lastEnemy || eWeap != lastEWeapon)
            {
                if (i > 0) GUILayout.Space(4);
                lastEnemy   = enemy;
                lastEWeapon = eWeap;

                EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), new Color(0.12f, 0.12f, 0.18f));
                GUILayout.BeginHorizontal();
                GUI.color = new Color(0.80f, 0.80f, 0.90f);
                GUILayout.Label($"{enemy.displayName ?? enemy.name}  [Z{enemy.zone}·{enemy.category}]",
                    EditorStyles.miniBoldLabel, GUILayout.Width(250f));
                GUILayout.Label($"Arma enemigo: {eWeap.itemNameID ?? eWeap.name}  (×{eWeap.enemyDamageMultiplier:F2})",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            // Result row
            bool inRange = false;
            Color rowCol = COL_GREEN;
            if (_targets != null && _targets.TryGetZoneTargets(enemy.zone, enemy.category, out var zt))
            {
                bool crit = res.ttk < zt.ttk.x || res.hitsToDie <= 2;
                bool warn = res.ttk > zt.ttk.y  || res.hitsToKill > zt.hitsToKill.y;
                if (crit)      rowCol = COL_RED;
                else if (warn) rowCol = COL_YELLOW;
                else           { rowCol = COL_GREEN; inRange = true; }
            }
            else rowCol = new Color(0.5f, 0.5f, 0.5f);

            if (i % 2 == 0) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);
            GUILayout.BeginHorizontal();
            GUILayout.Label("  Player:", EditorStyles.miniLabel, GUILayout.Width(55f));
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            GUILayout.Label($"{pWeap.itemNameID ?? pWeap.name}", EditorStyles.miniLabel, GUILayout.Width(120f));
            GUI.color = Color.white;
            GUILayout.Label($"TTK: ", EditorStyles.miniLabel, GUILayout.Width(30f));
            GUI.color = rowCol;
            GUILayout.Label(res.ttk < 9999f ? $"{res.ttk:F1}s" : "∞", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label($"Kills: {res.hitsToKill}  Dies: {res.hitsToDie}  DPS: {res.playerDPS:F1}", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    private void DrawKPIStrip()
    {
        var r = _simResult;
        Rect strip = GUILayoutUtility.GetRect(0, KPI_H + 8f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(strip, COL_PANEL);
        float cw = strip.width / 5f;
        KPICard(new Rect(strip.x,      strip.y+4, cw-4, KPI_H), "TTK Enemigo",     r.valid ? (r.ttk < 9999f ? $"{r.ttk:F1}s" : "∞") : "-", COL_BLUE);
        KPICard(new Rect(strip.x+cw,   strip.y+4, cw-4, KPI_H), "Golpes p. matar", r.valid ? r.hitsToKill.ToString() : "-",    COL_GREEN);
        KPICard(new Rect(strip.x+cw*2, strip.y+4, cw-4, KPI_H), "Golpes p. morir", r.valid ? r.hitsToDie.ToString()  : "-",    COL_ORANGE);
        KPICard(new Rect(strip.x+cw*3, strip.y+4, cw-4, KPI_H), "DPS estimado",    r.valid ? r.playerDPS.ToString("F1") : "-", COL_PURPLE);
        KPICard(new Rect(strip.x+cw*4, strip.y+4, cw-4, KPI_H), "Vida efectiva",
            r.valid ? (r.playerEHP < 99998f ? r.playerEHP.ToString("F0") : "∞") : "-", COL_BLUE);
    }

    private void DrawAlerts()
    {
        BeginPanel("Alertas", 320f);
        if (!_simResult.valid) { GUILayout.Label("Sin datos.", EditorStyles.miniLabel); EndPanel(); return; }

        bool any    = false;
        var  r      = _simResult;
        var  enemy  = SafeGet(_enemies, _simEnemy);
        var  weapon = SafeGet(_weapons, _simWeapon);
        int  wTier  = weapon != null ? weapon.weaponTier : 0;

        if (_targets != null && enemy != null && wTier > 0)
        {
            bool gotTargets = _targets.TryGetMatchupTargets(wTier, enemy.zone, enemy.category, out var adjZT, out var matchup);
            if (gotTargets)
            {
                GUI.color = MatchupColor(matchup);
                GUILayout.Label($"Matchup: {matchup}  [T{wTier} vs Z{enemy.zone}]", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
                GUILayout.Space(2);

                if (matchup == BalanceTargetConfigSO.MatchupLabel.Trivial ||
                    matchup == BalanceTargetConfigSO.MatchupLabel.Extreme)
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    GUILayout.Label("Matchup extremo — diferencia de tier esperada.", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    bool isHard = matchup == BalanceTargetConfigSO.MatchupLabel.Hard;

                    // For Hard matchup: being slow is expected. Only alert if beyond the tolerance-adjusted max.
                    if (!isHard && r.ttk < adjZT.ttk.x)               { AlertRow($"TTK {r.ttk:F1}s demasiado bajo (mín {adjZT.ttk.x:F1}s)",                COL_ORANGE); any = true; }
                    if (r.ttk > adjZT.ttk.y)                           { AlertRow($"TTK {r.ttk:F1}s fuera de rango (máx {adjZT.ttk.y:F1}s)",               isHard ? COL_RED : COL_YELLOW); any = true; }
                    if (!isHard && r.hitsToKill < adjZT.hitsToKill.x)  { AlertRow($"Matas en {r.hitsToKill} golpes — muy rápido (mín {adjZT.hitsToKill.x})", COL_ORANGE); any = true; }
                    if (r.hitsToKill > adjZT.hitsToKill.y)             { AlertRow($"Necesitas {r.hitsToKill} golpes — fuera de rango (máx {adjZT.hitsToKill.y})", isHard ? COL_RED : COL_YELLOW); any = true; }
                    if (r.hitsToDie  < adjZT.hitsToDie.x)              { AlertRow($"Mueres en {r.hitsToDie} golpes — muy frágil (mín {adjZT.hitsToDie.x})",  COL_RED);    any = true; }
                    if (r.hitsToDie  > adjZT.hitsToDie.y)              { AlertRow($"Aguantas {r.hitsToDie} golpes — muy tanque (máx {adjZT.hitsToDie.y})",   COL_YELLOW); any = true; }

                    if (isHard && r.hitsToKill <= adjZT.hitsToKill.y && r.hitsToKill >= adjZT.hitsToKill.x)
                    {
                        GUI.color = COL_YELLOW;
                        GUILayout.Label($"Matchup duro como esperado — {r.hitsToKill} golpes dentro del rango ajustado [{adjZT.hitsToKill.x}–{adjZT.hitsToKill.y}]", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }

                    // FlatAttack solver suggestion when too slow
                    if (r.hitsToKill > adjZT.hitsToKill.y && enemy != null)
                    {
                        float eHp    = AutoBalanceDataLoader.GetEnemyHealth(enemy);
                        float eDef   = AutoBalanceDataLoader.GetEnemyDefense(enemy);
                        float pAtk   = r.playerDamagePerHit > 0 ? r.playerDamagePerHit / (1f - r.enemyDefReduction) : 0f;
                        int   midHTK = (adjZT.hitsToKill.x + adjZT.hitsToKill.y) / 2;
                        float needed = midHTK > 0 ? eHp / midHTK * (_defK + eDef) / Mathf.Max(_defK, 0.001f) : 0f;
                        float suggestFlat = needed - pAtk;
                        if (suggestFlat > 0f)
                        {
                            GUI.color = COL_BLUE;
                            GUILayout.Label($"→ flatAttack sugerido: ~{suggestFlat:F0}  (para {midHTK} golpes)", EditorStyles.miniLabel);
                            GUI.color = Color.white;
                        }
                    }
                }
            }
        }

        if (r.hitsToDie <= 2)
        {
            AlertRow("Enemigo mata en ≤2 golpes. Crítico.", COL_RED);
            any = true;
        }
        if (r.playerDefReduction >= ((_maxArmorDefPct + _maxBonfireDefPct) / 100f) - 0.01f)
        {
            AlertRow("Defensa máxima alcanzada.", COL_YELLOW);
            any = true;
        }
        if (r.staminaPerCombo > 80f)
        {
            AlertRow("Stamina muy alta por combo.", COL_YELLOW);
            any = true;
        }

        // Poise
        if (r.hitsToStagger < 9999)
        {
            GUI.color = r.staggerBeforeKill ? COL_GREEN : new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Label(r.staggerBeforeKill
                ? $"★ Stagger en {r.hitsToStagger} golpes (antes de matar)"
                : $"Poise: stagger en {r.hitsToStagger} golpes (después de matar en {r.hitsToKill})",
                EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        if (!any && (_targets == null || enemy == null || wTier == 0))
            GreenRow("✓ Balance dentro de rangos objetivo.");
        else if (!any)
            GreenRow("✓ Dentro del rango para este matchup.");

        EndPanel();
    }

    private void DrawWeaponComparator()
    {
        BeginPanel("Comparador de armas", 0f);
        if (!_simResult.valid || _enemies.Count == 0 || _baseStats.Count == 0)
        { GUILayout.Label("Sin datos.", EditorStyles.miniLabel); EndPanel(); return; }

        var enemy = SafeGet(_enemies, _simEnemy);
        var so    = SafeGet(_baseStats, _simPlayerSO);
        if (enemy == null || so == null) { EndPanel(); return; }

        float eDefRed = Mathf.Min(AutoBalanceDataLoader.GetEnemyDefense(enemy) /
            (AutoBalanceDataLoader.GetEnemyDefense(enemy) + _defK), _defCap);

        TableHeader(new[] { "Arma", "Tier", "DPS", "Dmg/golpe", "Kills" },
                    new[] { 150f,   40f,    60f,   80f,          50f });

        _comparatorScroll = GUILayout.BeginScrollView(_comparatorScroll, GUILayout.Height(130f));
        for (int i = 0; i < _weapons.Count; i++)
        {
            var  w   = _weapons[i];
            bool sel = i == _simWeapon;
            var (comboDmg, comboTime, _, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(w);
            float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                       + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack)
                       + AutoBalanceDataLoader.GetWeaponFlatAttack(w);
            float avgDmg  = comboSteps > 0 ? comboDmg / comboSteps : 0f;
            float avgTime = comboSteps > 0 ? comboTime / comboSteps : 1f;
            float dph     = Mathf.Max((pAtk + avgDmg) * (1f - eDefRed), 1f);
            float dps     = avgTime > 0f ? dph / avgTime : 0f;
            int   kills   = Mathf.CeilToInt(AutoBalanceDataLoader.GetEnemyHealth(enemy) / Mathf.Max(dph, 0.001f));

            if (sel) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_TABSEL);
            GUILayout.BeginHorizontal();
            ColLabel(w.itemNameID ?? w.name, 150f, sel);
            ColLabel(w.weaponTier.ToString(), 40f,  false);
            ColLabel(dps.ToString("F1"),      60f,  false);
            ColLabel(dph.ToString("F1"),      80f,  false);
            ColLabel(kills.ToString(),         50f,  false);
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.MouseDown
                && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            { _simWeapon = i; Event.current.Use(); }
        }
        GUILayout.EndScrollView();
        EndPanel();
    }

    #endregion

    #region Tab — Report

    private void DrawReport()
    {
        SectionHeader("Reporte — Exportación");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Exportar Armas CSV",    GUILayout.Height(28), GUILayout.Width(160))) ExportWeaponsCSV();
        if (GUILayout.Button("Exportar Enemigos CSV", GUILayout.Height(28), GUILayout.Width(160))) ExportEnemiesCSV();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        if (GUILayout.Button("Exportar JSON Completo (IA)", GUILayout.Height(32), GUILayout.Width(220))) ExportFullJSON();

        GUILayout.Space(8);
        BeginPanel("Resumen", 300f);
        StatRow("WeaponData",         _weapons.Count.ToString());
        StatRow("EnemyDefinition",    _enemies.Count.ToString());
        StatRow("ConsumableItemData", _consumables.Count.ToString());
        StatRow("UpgradeNodeSO",      _nodes.Count.ToString());
        if (_simResult.valid)
        {
            SectionDivider();
            StatRow("TTK",          _simResult.ttk < 9999f ? $"{_simResult.ttk:F1}s" : "∞");
            StatRow("Hits to kill", _simResult.hitsToKill.ToString());
            StatRow("Hits to die",  _simResult.hitsToDie.ToString());
        }
        EndPanel();
    }

    #endregion

    #region Tab — Diff

    private void DrawDiff()
    {
        SectionHeader("Diff JSON — Comparador de Balance IA");

        GUILayout.BeginHorizontal();
        _diffJsonPath = GUILayout.TextField(_diffJsonPath, GUILayout.ExpandWidth(true), GUILayout.Height(22));
        if (GUILayout.Button("…", GUILayout.Width(26), GUILayout.Height(22)))
        {
            string p = EditorUtility.OpenFilePanel("Seleccionar JSON de balance", "", "json");
            if (!string.IsNullOrEmpty(p)) _diffJsonPath = p;
        }
        if (ColorButton("Cargar", COL_BLUE, 70))
        {
            if (System.IO.File.Exists(_diffJsonPath))
                ParseDiffJson(System.IO.File.ReadAllText(_diffJsonPath));
            else
                EditorUtility.DisplayDialog("Error", $"Archivo no encontrado:\n{_diffJsonPath}", "OK");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (!_diffLoaded)
        {
            EditorGUILayout.HelpBox("Carga el JSON exportado (modificado por la IA) para ver qué valores cambiaron respecto al juego actual.", MessageType.Info);
            return;
        }

        if (_diffEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("No se detectaron diferencias entre el JSON y el estado actual del proyecto.", MessageType.Warning);
            return;
        }

        int buffs = 0, nerfs = 0;
        foreach (var d in _diffEntries)
        {
            if (d.jsonValue > d.currentValue) buffs++;
            else nerfs++;
        }

        BeginPanel($"{_diffEntries.Count} cambios  —  ↑ {buffs} buffs  ·  ↓ {nerfs} nerfs", 0f);

        TableHeader(
            new[] { "Categoría", "Nombre", "Campo", "Actual", "JSON", "Delta", "%" },
            new[] { 80f, 150f, 180f, 70f, 70f, 65f, 60f });

        _diffScroll = GUILayout.BeginScrollView(_diffScroll);

        string lastCat = "";
        bool alt = false;
        foreach (var d in _diffEntries)
        {
            if (d.category != lastCat) { lastCat = d.category; SectionDivider(); alt = false; }

            float delta   = d.jsonValue - d.currentValue;
            float pct     = !Mathf.Approximately(d.currentValue, 0f) ? (delta / d.currentValue) * 100f : 0f;
            Color rowBg   = alt ? COL_ROWALT : new Color(0f, 0f, 0f, 0f);
            Color valCol  = delta > 0f ? COL_GREEN : COL_RED;
            string dStr   = delta > 0f ? $"+{delta:F2}" : $"{delta:F2}";
            string pStr   = delta > 0f ? $"+{pct:F1}%" : $"{pct:F1}%";
            alt = !alt;

            if (alt) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);
            GUILayout.BeginHorizontal();
            ColLabel(d.category,                    80f,  false);
            ColLabel(d.entityName,                  150f, false);
            ColLabel(d.field,                       180f, false);
            ColLabel($"{d.currentValue:F2}",        70f,  false);
            GUI.color = valCol;
            GUILayout.Label($"{d.jsonValue:F2}", EditorStyles.miniLabel, GUILayout.Width(70f));
            GUILayout.Label(dStr,                EditorStyles.miniLabel, GUILayout.Width(65f));
            GUILayout.Label(pStr,                EditorStyles.miniLabel, GUILayout.Width(60f));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        EndPanel();
    }

    #endregion

    #region Simulation Logic

    private void RunSimulation()
    {
        if (_baseStats.Count == 0 || _weapons.Count == 0 || _enemies.Count == 0) return;
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();

        var so     = SafeGet(_baseStats, _simPlayerSO);
        var weapon = SafeGet(_weapons,   _simWeapon);
        var enemy  = SafeGet(_enemies,   _simEnemy);

        var (comboDmg, comboTime, comboStam, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(weapon);

        float armorDefRaw   = ComputeArmorDefRaw();
        float bonfireDefRaw = AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense);
        float armorFrac     = Mathf.Clamp(armorDefRaw,   0f, _maxArmorDefPct)   / 100f;
        float bonfireFrac   = Mathf.Clamp(bonfireDefRaw, 0f, _maxBonfireDefPct) / 100f;

        float eWeaponMult = _simEnemyWeaponIdx >= 0 && _simEnemyWeaponIdx < _weapons.Count
            ? _weapons[_simEnemyWeaponIdx].enemyDamageMultiplier
            : AutoBalanceDataLoader.GetEnemyWeaponMult(enemy);

        float baseFlatAtk = AutoBalanceDataLoader.GetWeaponFlatAttack(weapon);

        // Rarity multiplier ranges — applied only to the weapon's flat Attack modifiers.
        float multMid = 1f, multMin = 1f, multMax = 1f;
        if (_simWeaponRarity >= 0)
        {
            var rarityEnumVals = (Enums.ItemRarity[])System.Enum.GetValues(typeof(Enums.ItemRarity));
            (multMin, multMax) = RarityUtility.GetMultiplierRange(rarityEnumVals[_simWeaponRarity]);
            multMid = (multMin + multMax) * 0.5f;
        }

        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);
        float pSta = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina);
        float eHp  = AutoBalanceDataLoader.GetEnemyHealth(enemy);
        float eAtk = AutoBalanceDataLoader.GetEnemyAttack(enemy);
        float eDef = AutoBalanceDataLoader.GetEnemyDefense(enemy);

        float avgPoisePerHit = AutoBalanceDataLoader.GetAvgPoiseDamage(weapon);

        _simResult = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
        {
            playerHealth = pHp, playerAttack = pAtk, playerDefense = 0f, playerStamina = pSta,
            useDirectPlayerDef = true, playerArmorDefPct = armorFrac, playerBonfireDefPct = bonfireFrac,
            weaponFlatAttack  = baseFlatAtk * multMid,
            comboTotalDamage  = comboDmg,  comboTotalTime  = comboTime,
            comboTotalStamina = comboStam, comboStepCount  = comboSteps,
            enemyHealth = eHp, enemyAttackBase = eAtk, enemyWeaponMult = eWeaponMult, enemyDefense = eDef,
            defenseK = _defK, reductionCap = _defCap,
            enemyMaxPoise = enemy.maxPoise, weaponPoiseDamagePerHit = avgPoisePerHit,
        });

        if (_simWeaponRarity >= 0)
        {
            _simResultRarityMin = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
            {
                playerHealth = pHp, playerAttack = pAtk, playerDefense = 0f, playerStamina = pSta,
                useDirectPlayerDef = true, playerArmorDefPct = armorFrac, playerBonfireDefPct = bonfireFrac,
                weaponFlatAttack  = baseFlatAtk * multMin,
                comboTotalDamage  = comboDmg,  comboTotalTime  = comboTime,
                comboTotalStamina = comboStam, comboStepCount  = comboSteps,
                enemyHealth = eHp, enemyAttackBase = eAtk, enemyWeaponMult = eWeaponMult, enemyDefense = eDef,
                defenseK = _defK, reductionCap = _defCap,
                enemyMaxPoise = enemy.maxPoise, weaponPoiseDamagePerHit = avgPoisePerHit,
            });
            _simResultRarityMax = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
            {
                playerHealth = pHp, playerAttack = pAtk, playerDefense = 0f, playerStamina = pSta,
                useDirectPlayerDef = true, playerArmorDefPct = armorFrac, playerBonfireDefPct = bonfireFrac,
                weaponFlatAttack  = baseFlatAtk * multMax,
                comboTotalDamage  = comboDmg,  comboTotalTime  = comboTime,
                comboTotalStamina = comboStam, comboStepCount  = comboSteps,
                enemyHealth = eHp, enemyAttackBase = eAtk, enemyWeaponMult = eWeaponMult, enemyDefense = eDef,
                defenseK = _defK, reductionCap = _defCap,
                enemyMaxPoise = enemy.maxPoise, weaponPoiseDamagePerHit = avgPoisePerHit,
            });
        }
        else
        {
            _simResultRarityMin = default;
            _simResultRarityMax = default;
        }

        Repaint();
    }

    private void RunBatchSimulation()
    {
        if (_baseStats.Count == 0 || _weapons.Count == 0 || _enemies.Count == 0) return;
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();

        var so     = SafeGet(_baseStats, _simPlayerSO);
        var weapon = SafeGet(_weapons,   _simWeapon);
        var (comboDmg, comboTime, comboStam, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(weapon);

        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);

        float armorFrac   = Mathf.Clamp(ComputeArmorDefRaw(), 0f, _maxArmorDefPct)   / 100f;
        float bonfireFrac = Mathf.Clamp(
            AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense),
            0f, _maxBonfireDefPct) / 100f;

        float batchFlatAtk  = AutoBalanceDataLoader.GetWeaponFlatAttack(weapon);
        float batchAvgPoise = AutoBalanceDataLoader.GetAvgPoiseDamage(weapon);

        _batchResults.Clear();
        foreach (var enemy in _enemies)
        {
            var result = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
            {
                playerHealth         = pHp, playerAttack = pAtk, playerDefense = 0f,
                playerStamina        = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina),
                useDirectPlayerDef   = true,
                playerArmorDefPct    = armorFrac,
                playerBonfireDefPct  = bonfireFrac,
                weaponFlatAttack     = batchFlatAtk,
                comboTotalDamage     = comboDmg, comboTotalTime = comboTime,
                comboTotalStamina    = comboStam, comboStepCount = comboSteps,
                enemyHealth          = AutoBalanceDataLoader.GetEnemyHealth(enemy),
                enemyAttackBase      = AutoBalanceDataLoader.GetEnemyAttack(enemy),
                enemyWeaponMult      = AutoBalanceDataLoader.GetEnemyWeaponMult(enemy),
                enemyDefense         = AutoBalanceDataLoader.GetEnemyDefense(enemy),
                defenseK             = _defK, reductionCap = _defCap,
                enemyMaxPoise        = enemy.maxPoise,
                weaponPoiseDamagePerHit = batchAvgPoise,
            });
            _batchResults.Add((enemy, result));
        }
        Repaint();
    }

    private void RunMatrixSimulation()
    {
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();
        EnsureEnemyFamilyIdx();

        _matrixResults.Clear();

        var so = SafeGet(_baseStats, _simPlayerSO);

        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);
        float pSta = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina);

        float armorFrac   = Mathf.Clamp(ComputeArmorDefRaw(), 0f, _maxArmorDefPct)   / 100f;
        float bonfireFrac = Mathf.Clamp(
            AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense),
            0f, _maxBonfireDefPct) / 100f;

        for (int ei = 0; ei < _enemies.Count; ei++)
        {
            var enemy     = _enemies[ei];
            int familyIdx = _enemyFamilyIdx[ei];
            if (familyIdx < 0) continue;

            var family = (Enums.WeaponFamily)familyIdx;

            // Collect enemy weapons of that family
            var enemyWeapons = new List<WeaponData>();
            foreach (var w in _weapons)
                if (w.familyType == family) enemyWeapons.Add(w);

            if (enemyWeapons.Count == 0)
            {
                Debug.LogWarning($"[AutoBalance] Sin armas de familia {family} para {enemy.displayName}.");
                continue;
            }

            foreach (var pWeap in _weapons)
            {
                var (comboDmg, comboTime, comboStam, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(pWeap);

                foreach (var eWeap in enemyWeapons)
                {
                    var result = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
                    {
                        playerHealth         = pHp, playerAttack = pAtk, playerDefense = 0f,
                        playerStamina        = pSta,
                        useDirectPlayerDef   = true,
                        playerArmorDefPct    = armorFrac,
                        playerBonfireDefPct  = bonfireFrac,
                        weaponFlatAttack     = AutoBalanceDataLoader.GetWeaponFlatAttack(pWeap),
                        comboTotalDamage     = comboDmg, comboTotalTime = comboTime,
                        comboTotalStamina    = comboStam, comboStepCount = comboSteps,
                        enemyHealth          = AutoBalanceDataLoader.GetEnemyHealth(enemy),
                        enemyAttackBase      = AutoBalanceDataLoader.GetEnemyAttack(enemy),
                        enemyWeaponMult      = eWeap.enemyDamageMultiplier,
                        enemyDefense         = AutoBalanceDataLoader.GetEnemyDefense(enemy),
                        defenseK             = _defK, reductionCap = _defCap,
                    });
                    _matrixResults.Add((enemy, pWeap, eWeap, result));
                }
            }
        }

        Repaint();
    }

    private void RunTierSummarySimulation()
    {
        if (_baseStats.Count == 0 || _weapons.Count == 0 || _enemies.Count == 0 || _targets == null) return;
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();

        var so = SafeGet(_baseStats, _simPlayerSO);
        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);
        float pSta = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina);

        // Use the armor configured in the player panel — defense doesn't matter for weapon attack balance
        // but hitsToDie needs a baseline so we use the configured setup.
        float armorFrac   = Mathf.Clamp(ComputeArmorDefRaw(), 0f, _maxArmorDefPct) / 100f;
        float bonfireFrac = Mathf.Clamp(
            AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense),
            0f, _maxBonfireDefPct) / 100f;

        _tierSummaryResults.Clear();
        _tierWeaponList.Clear();

        foreach (var weapon in _weapons)
        {
            if (weapon.weaponTier != _tierFilter) continue;

            var (comboDmg, comboTime, comboStam, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(weapon);
            float flatAtk  = AutoBalanceDataLoader.GetWeaponFlatAttack(weapon);
            float avgPoise = AutoBalanceDataLoader.GetAvgPoiseDamage(weapon);
            bool addedToList = false;

            foreach (var enemy in _enemies)
            {
                bool gotTargets = _targets.TryGetMatchupTargets(weapon.weaponTier, enemy.zone, enemy.category,
                    out var adjZT, out var matchup);
                if (!gotTargets) continue;
                if (_primaryOnly && matchup != BalanceTargetConfigSO.MatchupLabel.Primary) continue;

                if (!addedToList) { _tierWeaponList.Add(weapon); addedToList = true; }

                var result = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
                {
                    playerHealth            = pHp, playerAttack = pAtk, playerDefense = 0f,
                    playerStamina           = pSta,
                    useDirectPlayerDef      = true,
                    playerArmorDefPct       = armorFrac,
                    playerBonfireDefPct     = bonfireFrac,
                    weaponFlatAttack        = flatAtk,
                    comboTotalDamage        = comboDmg, comboTotalTime = comboTime,
                    comboTotalStamina       = comboStam, comboStepCount = comboSteps,
                    enemyHealth             = AutoBalanceDataLoader.GetEnemyHealth(enemy),
                    enemyAttackBase         = AutoBalanceDataLoader.GetEnemyAttack(enemy),
                    enemyWeaponMult         = AutoBalanceDataLoader.GetEnemyWeaponMult(enemy),
                    enemyDefense            = AutoBalanceDataLoader.GetEnemyDefense(enemy),
                    defenseK                = _defK, reductionCap = _defCap,
                    enemyMaxPoise           = enemy.maxPoise,
                    weaponPoiseDamagePerHit = avgPoise,
                });

                _tierSummaryResults.Add((weapon, enemy, result, adjZT, matchup));
            }
        }

        _tierSelWeapon = 0;
        Repaint();
    }

    private void RunSurvivalSimulation()
    {
        if (_baseStats.Count == 0 || _enemies.Count == 0) return;
        EnsureSimUpgradeLevels();
        EnsureSurvEnemyFamilyIdx();

        var so = SafeGet(_baseStats, _simPlayerSO);
        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health)
                   + _survBonfireHealth;
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);
        float pSta = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina);

        float armorRaw    = _survArmorTier == 0 ? 0f : ComputeArmorDefForTier(_survArmorTier);
        float armorFrac   = Mathf.Clamp(armorRaw, 0f, _maxArmorDefPct) / 100f;
        float bonfireFrac = Mathf.Clamp(_survBonfireDef, 0f, _maxBonfireDefPct) / 100f;

        var sorted = new List<EnemyDefinition>(_enemies);
        sorted.Sort((a, b) => a.zone != b.zone ? a.zone.CompareTo(b.zone) : a.category.CompareTo(b.category));

        _survResults.Clear();
        foreach (var enemy in sorted)
        {
            int origIdx   = _enemies.IndexOf(enemy);
            int familyIdx = origIdx >= 0 && _survEnemyFamilyIdx != null && origIdx < _survEnemyFamilyIdx.Length
                ? _survEnemyFamilyIdx[origIdx] : -1;

            // Build the list of (tier, label, mult) rows to simulate for this enemy.
            var tierRows = BuildTierRows(familyIdx, enemy);

            foreach (var (tier, label, mult) in tierRows)
            {
                var result = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
                {
                    playerHealth         = pHp, playerAttack = pAtk, playerDefense = 0f,
                    playerStamina        = pSta,
                    useDirectPlayerDef   = true,
                    playerArmorDefPct    = armorFrac,
                    playerBonfireDefPct  = bonfireFrac,
                    weaponFlatAttack     = 0f,
                    comboTotalDamage     = 0f, comboTotalTime = 1f,
                    comboTotalStamina    = 0f, comboStepCount = 0,
                    enemyHealth          = AutoBalanceDataLoader.GetEnemyHealth(enemy),
                    enemyAttackBase      = AutoBalanceDataLoader.GetEnemyAttack(enemy),
                    enemyWeaponMult      = mult,
                    enemyDefense         = AutoBalanceDataLoader.GetEnemyDefense(enemy),
                    defenseK             = _defK, reductionCap = _defCap,
                });
                _survResults.Add((enemy, tier, label, mult, result));
            }
        }

        Repaint();
    }

    // Returns one entry per weapon tier present in the given family.
    // When familyIdx < 0 (auto), returns a single row using the prefab's mult.
    private List<(int tier, string label, float mult)> BuildTierRows(int familyIdx, EnemyDefinition fallback)
    {
        if (familyIdx < 0)
        {
            float m = AutoBalanceDataLoader.GetEnemyWeaponMult(fallback);
            return new List<(int, string, float)> { (0, "Auto", m) };
        }

        var family  = (Enums.WeaponFamily)familyIdx;
        var tierSum = new Dictionary<int, (float sum, int count)>();
        foreach (var w in _weapons)
        {
            if (w.familyType != family) continue;
            if (!tierSum.ContainsKey(w.weaponTier)) tierSum[w.weaponTier] = (0f, 0);
            var cur = tierSum[w.weaponTier];
            tierSum[w.weaponTier] = (cur.sum + w.enemyDamageMultiplier, cur.count + 1);
        }

        var rows = new List<(int, string, float)>();
        foreach (var kv in tierSum)
            rows.Add((kv.Key, $"T{kv.Key}", kv.Value.sum / kv.Value.count));
        rows.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return rows;
    }

    private static (string status, Color color) EvaluateMatchup(
        AutoBalanceSimulator.SimResult res,
        BalanceTargetConfigSO.ZoneTargets targets,
        BalanceTargetConfigSO.MatchupLabel matchup)
    {
        bool htkOk   = res.hitsToKill >= targets.hitsToKill.x && res.hitsToKill <= targets.hitsToKill.y;
        bool ttkOk   = res.ttk >= targets.ttk.x && res.ttk <= targets.ttk.y;
        bool inRange  = htkOk && ttkOk;
        bool tooFast  = res.hitsToKill < targets.hitsToKill.x || res.ttk < targets.ttk.x;
        bool tooSlow  = res.hitsToKill > targets.hitsToKill.y || res.ttk > targets.ttk.y;
        bool dieFrag  = res.hitsToDie < targets.hitsToDie.x;

        switch (matchup)
        {
            // Primary: tight evaluation — any deviation matters.
            case BalanceTargetConfigSO.MatchupLabel.Primary:
                if (tooFast) return ("⚡ Rápido",   COL_ORANGE);
                if (tooSlow) return ("⚠ Lento",     COL_RED);
                if (dieFrag) return ("☠ Frágil",    COL_RED);
                return ("✓ OK",                      COL_GREEN);

            // Easy: weapon stronger than zone — expected to be fast.
            case BalanceTargetConfigSO.MatchupLabel.Easy:
                if (tooFast) return ("~ Fácil",     COL_BLUE);
                if (inRange) return ("✓ OK",         COL_GREEN);
                return ("⚠ Lento (raro)",            COL_YELLOW);

            // Hard: weapon weaker than zone — expected to struggle.
            case BalanceTargetConfigSO.MatchupLabel.Hard:
                if (tooSlow) return ("✗ Muy duro",  COL_RED);
                if (tooFast) return ("✓ Mejor esp.", COL_GREEN);
                return ("~ Duro (esp.)",             COL_YELLOW);

            // Extreme: very mismatched — wide tolerance, nearly no concern.
            case BalanceTargetConfigSO.MatchupLabel.Extreme:
                if (inRange) return ("★ Viable",    COL_GREEN);
                return ("· Extremo",                 new Color(0.5f, 0.5f, 0.5f));

            // Trivial: weapon completely dominates — no balance concern.
            case BalanceTargetConfigSO.MatchupLabel.Trivial:
                return ("· Trivial",                 new Color(0.5f, 0.5f, 0.5f));

            default:
                return ("?",                         Color.white);
        }
    }

    private static Color MatchupColor(BalanceTargetConfigSO.MatchupLabel matchup) => matchup switch
    {
        BalanceTargetConfigSO.MatchupLabel.Primary => COL_GREEN,
        BalanceTargetConfigSO.MatchupLabel.Easy    => COL_BLUE,
        BalanceTargetConfigSO.MatchupLabel.Hard    => COL_YELLOW,
        BalanceTargetConfigSO.MatchupLabel.Extreme => COL_RED,
        BalanceTargetConfigSO.MatchupLabel.Trivial => new Color(0.5f, 0.5f, 0.5f),
        _                                          => Color.white,
    };

    private void RunAutoBalance()
    {
        RunSimulation();
        if (!_simResult.valid) return;
        var enemy = SafeGet(_enemies, _simEnemy);
        var sb    = new System.Text.StringBuilder("=== Auto Balance Proposals ===\n");

        if (_targets != null && enemy != null && _targets.TryGetZoneTargets(enemy.zone, enemy.category, out var zt))
        {
            float target = (zt.ttk.x + zt.ttk.y) * 0.5f;
            if (Mathf.Abs(_simResult.ttk - target) > 0.5f)
                sb.AppendLine($"• Ajustar Health de {enemy.displayName} x{target / _simResult.ttk:F2} (TTK {_simResult.ttk:F1}s → {target:F1}s)");
            if (_simResult.hitsToKill < zt.hitsToKill.x)
                sb.AppendLine($"• Subir Health de {enemy.displayName} — matas en {_simResult.hitsToKill} (mín {zt.hitsToKill.x})");
            if (_simResult.hitsToKill > zt.hitsToKill.y)
                sb.AppendLine($"• Bajar Health de {enemy.displayName} — necesitas {_simResult.hitsToKill} (máx {zt.hitsToKill.y})");
        }
        else sb.AppendLine("Asigna un BalanceTargetConfigSO para propuestas precisas.");

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Auto Balance", sb.ToString(), "OK");
    }

    #endregion

    #region Data Loading

    private void LoadData()
    {
        _weapons     = AutoBalanceDataLoader.LoadWeapons();
        _enemies     = AutoBalanceDataLoader.LoadEnemies();
        _baseStats   = AutoBalanceDataLoader.LoadBaseStats();
        _nodes       = AutoBalanceDataLoader.LoadUpgradeNodes();
        _armors      = AutoBalanceDataLoader.LoadArmors();
        _consumables = AutoBalanceDataLoader.LoadConsumables();
        _targets     = AutoBalanceDataLoader.LoadBalanceTargets();
        _dataLoaded  = true;

        _selPlayerSO = Mathf.Clamp(_selPlayerSO, 0, Mathf.Max(_baseStats.Count - 1, 0));
        _simPlayerSO = Mathf.Clamp(_simPlayerSO, 0, Mathf.Max(_baseStats.Count - 1, 0));
        _simWeapon   = Mathf.Clamp(_simWeapon,   0, Mathf.Max(_weapons.Count    - 1, 0));
        _simEnemy    = Mathf.Clamp(_simEnemy,     0, Mathf.Max(_enemies.Count   - 1, 0));

        _balanceSO = null; _balanceSOTarget = null;
        _enemyCtxSO = null; _enemyCtxSOTarget = null;
        _playerBaseStatsSO = null; _playerBaseSOTarget = null;
        _nodeEditSOs.Clear();
        _attackSOs.Clear();
        _lastSelWeaponForCombos = -1;

        _batchResults.Clear();
        _matrixResults.Clear();

        EnsureUpgradeLevels();
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();
        EnsureEnemyFamilyIdx();
        EnsureSurvEnemyFamilyIdx();
    }

    private void EnsureUpgradeLevels()
    {
        if (_upgradeLvls == null || _upgradeLvls.Length != _nodes.Count)
            _upgradeLvls = new int[_nodes.Count];
        EnsureNodeEditExpanded();
    }

    private void EnsureNodeEditExpanded()
    {
        if (_nodeEditExpanded == null || _nodeEditExpanded.Length != _nodes.Count)
            _nodeEditExpanded = new bool[_nodes.Count];
    }

    private void EnsureSimUpgradeLevels()
    {
        if (_simUpgradeLvls == null || _simUpgradeLvls.Length != _nodes.Count)
            _simUpgradeLvls = new int[_nodes.Count];
    }

    private void EnsureSimArmorSlots()
    {
        if (_simArmorPerSlot == null || _simArmorPerSlot.Length != ARMOR_SLOTS.Length)
            _simArmorPerSlot = new int[ARMOR_SLOTS.Length];
    }

    private void EnsureEnemyFamilyIdx()
    {
        if (_enemyFamilyIdx == null || _enemyFamilyIdx.Length != _enemies.Count)
        {
            int[] old      = _enemyFamilyIdx;
            _enemyFamilyIdx = new int[_enemies.Count];
            for (int i = 0; i < _enemyFamilyIdx.Length; i++)
                _enemyFamilyIdx[i] = old != null && i < old.Length ? old[i] : -1;
        }
    }

    private void EnsureSurvEnemyFamilyIdx()
    {
        if (_survEnemyFamilyIdx != null && _survEnemyFamilyIdx.Length == _enemies.Count) return;

        int[] old = _survEnemyFamilyIdx;
        _survEnemyFamilyIdx = new int[_enemies.Count];
        for (int i = 0; i < _enemies.Count; i++)
        {
            // Preserve manual override if list just grew; auto-assign for new entries.
            if (old != null && i < old.Length && old[i] != -1)
                _survEnemyFamilyIdx[i] = old[i];
            else
                _survEnemyFamilyIdx[i] = GetDefaultSurvFamily(_enemies[i]);
        }
    }

    // T1 enemies → Warrior; T2/T3 enemies → GreatSword. Falls back to -1 (auto/prefab).
    private static int GetDefaultSurvFamily(EnemyDefinition e)
    {
        string n = ((e.displayName ?? e.name) + e.name).ToUpperInvariant();
        bool hasT2 = n.Contains("T2") || n.Contains("_2");
        bool hasT3 = n.Contains("T3") || n.Contains("_3");
        bool hasT1 = (n.Contains("T1") || n.Contains("_1")) && !hasT2 && !hasT3;
        if (hasT2 || hasT3) return (int)Enums.WeaponFamily.GreatSword;
        if (hasT1)           return (int)Enums.WeaponFamily.Warrior;
        return -1;
    }

    #endregion

    #region Defense Helpers

    private float ComputeArmorDefRaw()
    {
        EnsureSimArmorSlots();
        float sum = 0f;
        for (int s = 0; s < ARMOR_SLOTS.Length; s++)
        {
            int idx = _simArmorPerSlot[s] - 1; // popup 0 = "Ninguna"
            if (idx < 0) continue;
            var filtered = GetArmorsBySlot(ARMOR_SLOTS[s]);
            if (idx >= filtered.Count) continue;
            sum += AutoBalanceDataLoader.GetArmorStatFlat(new List<EquipableItemData> { filtered[idx] }, Enums.StatType.Defense);
        }
        return sum;
    }

    private List<EquipableItemData> GetArmorsBySlot(Enums.EquipSlot slot)
    {
        var result = new List<EquipableItemData>();
        foreach (var a in _armors)
            if (a.equipSlot == slot) result.Add(a);
        return result;
    }

    // Extracts armor tier from name (e.g. "Helmet Tier 2" → 2). Returns 0 if not found.
    private static int GetArmorTierFromName(EquipableItemData armor)
    {
        string n = (armor.itemNameID ?? armor.name ?? "").ToLowerInvariant();
        int idx = n.IndexOf("tier", System.StringComparison.Ordinal);
        if (idx < 0) return 0;
        int start = idx + 4;
        while (start < n.Length && n[start] == ' ') start++;
        if (start < n.Length && char.IsDigit(n[start]))
            return n[start] - '0';
        return 0;
    }

    // Returns total Defense flat stat for all armors of the given tier (as percentage points, same units as ComputeArmorDefRaw).
    private float ComputeArmorDefForTier(int tier)
    {
        float sum = 0f;
        foreach (var a in _armors)
        {
            if (GetArmorTierFromName(a) != tier) continue;
            if (a.modifiers == null) continue;
            foreach (var m in a.modifiers)
                if (m.statTypeAffected == Enums.StatType.Defense && m.type == StatModifierType.Flat)
                    sum += m.value;
        }
        return sum;
    }

    #endregion

    #region Export

    private void ExportCSV() { ExportWeaponsCSV(); ExportEnemiesCSV(); }

    private void ExportWeaponsCSV()
    {
        var sb = new System.Text.StringBuilder("Name,Tier,Rarity,Family,FlatAtk,EnemyMult\n");
        foreach (var w in _weapons)
            sb.AppendLine($"{w.itemNameID},{w.weaponTier},{w.itemRarity},{w.familyType}," +
                          $"{AutoBalanceDataLoader.GetWeaponFlatAttack(w):F1},{w.enemyDamageMultiplier:F2}");
        System.IO.File.WriteAllText("AutoBalance_Weapons.csv", sb.ToString());
        Debug.Log("[AutoBalance] Weapons.csv exportado.");
    }

    private void ExportEnemiesCSV()
    {
        var sb = new System.Text.StringBuilder("Name,Zone,Category,Health,Attack,Defense,WeaponMult\n");
        foreach (var e in _enemies)
            sb.AppendLine($"{e.displayName},{e.zone},{e.category}," +
                          $"{AutoBalanceDataLoader.GetEnemyHealth(e):F0},{AutoBalanceDataLoader.GetEnemyAttack(e):F0}," +
                          $"{AutoBalanceDataLoader.GetEnemyDefense(e):F0},{AutoBalanceDataLoader.GetEnemyWeaponMult(e):F2}");
        System.IO.File.WriteAllText("AutoBalance_Enemies.csv", sb.ToString());
        Debug.Log("[AutoBalance] Enemies.csv exportado.");
    }

    private void ExportFullJSON()
    {
        EnsureSimUpgradeLevels();
        EnsureSimArmorSlots();

        var so           = SafeGet(_baseStats, _simPlayerSO);
        float armorRaw   = ComputeArmorDefRaw();
        float bonfireRaw = AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Defense);
        float armorFrac  = Mathf.Clamp(armorRaw,   0f, _maxArmorDefPct)   / 100f;
        float bonFrac    = Mathf.Clamp(bonfireRaw,  0f, _maxBonfireDefPct) / 100f;
        float pHp  = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Health)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Health);
        float pAtk = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Attack)
                   + AutoBalanceDataLoader.GetUpgradeBonus(_nodes, _simUpgradeLvls, Enums.StatType.Attack);
        float pSta = AutoBalanceDataLoader.GetStatBase(so, Enums.StatType.Stamina);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");

        // ── meta ──────────────────────────────────────────────────────────────
        sb.AppendLine("  \"meta\": {");
        sb.AppendLine($"    \"exportDate\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm}\",");
        sb.AppendLine($"    \"weapons\": {_weapons.Count},");
        sb.AppendLine($"    \"enemies\": {_enemies.Count},");
        sb.AppendLine($"    \"armors\": {_armors.Count},");
        sb.AppendLine($"    \"consumables\": {_consumables.Count},");
        sb.AppendLine($"    \"upgradeNodes\": {_nodes.Count}");
        sb.AppendLine("  },");

        // ── simulationConfig ──────────────────────────────────────────────────
        sb.AppendLine("  \"simulationConfig\": {");
        sb.AppendLine($"    \"armorDefRaw\": {armorRaw:F2},");
        sb.AppendLine($"    \"bonfireDefRaw\": {bonfireRaw:F2},");
        sb.AppendLine($"    \"armorDefPct\": {armorFrac * 100f:F1},");
        sb.AppendLine($"    \"bonfireDefPct\": {bonFrac * 100f:F1},");
        sb.AppendLine($"    \"totalPlayerDefReductionPct\": {(armorFrac + bonFrac) * 100f:F1},");
        sb.AppendLine($"    \"maxArmorDefPct\": {_maxArmorDefPct:F1},");
        sb.AppendLine($"    \"maxBonfireDefPct\": {_maxBonfireDefPct:F1},");
        sb.AppendLine($"    \"enemyDefenseK\": {_defK:F1},");
        sb.AppendLine($"    \"enemyReductionCap\": {_defCap:F2},");
        sb.AppendLine("    \"playerStats\": {");
        sb.AppendLine($"      \"health\": {pHp:F1},");
        sb.AppendLine($"      \"attack\": {pAtk:F1},");
        sb.AppendLine($"      \"stamina\": {pSta:F1}");
        sb.AppendLine("    },");
        sb.AppendLine("    \"upgradeLevels\": [");
        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node == null) continue;
            int lvl = (_simUpgradeLvls != null && i < _simUpgradeLvls.Length) ? _simUpgradeLvls[i] : 0;
            string c = i < _nodes.Count - 1 ? "," : "";
            sb.AppendLine($"      {{ \"node\": \"{JStr(node.name)}\", \"type\": \"{node.upgradeType}\", \"level\": {lvl} }}{c}");
        }
        sb.AppendLine("    ]");
        sb.AppendLine("  },");

        // ── balanceTargets ────────────────────────────────────────────────────
        sb.AppendLine("  \"balanceTargets\": [");
        if (_targets?.zoneTargets != null)
        {
            for (int i = 0; i < _targets.zoneTargets.Length; i++)
            {
                var zt = _targets.zoneTargets[i];
                string c = i < _targets.zoneTargets.Length - 1 ? "," : "";
                sb.AppendLine("    {");
                sb.AppendLine($"      \"zone\": {zt.zone},");
                sb.AppendLine($"      \"category\": \"{zt.category}\",");
                sb.AppendLine($"      \"ttk\": {{ \"min\": {zt.ttk.x:F1}, \"max\": {zt.ttk.y:F1} }},");
                sb.AppendLine($"      \"hitsToKill\": {{ \"min\": {zt.hitsToKill.x}, \"max\": {zt.hitsToKill.y} }},");
                sb.AppendLine($"      \"hitsToDie\": {{ \"min\": {zt.hitsToDie.x}, \"max\": {zt.hitsToDie.y} }},");
                sb.AppendLine($"      \"dpsRange\": {{ \"min\": {zt.dpsRange.x:F1}, \"max\": {zt.dpsRange.y:F1} }}");
                sb.AppendLine($"    }}{c}");
            }
        }
        sb.AppendLine("  ],");

        // ── rarityRanges ──────────────────────────────────────────────────────
        sb.AppendLine("  \"rarityRanges\": [");
        if (_targets?.rarityRanges != null)
        {
            for (int i = 0; i < _targets.rarityRanges.Length; i++)
            {
                var r = _targets.rarityRanges[i];
                string c = i < _targets.rarityRanges.Length - 1 ? "," : "";
                sb.AppendLine($"    {{ \"rarity\": \"{r.rarity}\", \"min\": {r.MinMultiplier:F2}, \"max\": {r.MaxMultiplier:F2} }}{c}");
            }
        }
        sb.AppendLine("  ],");

        // ── player baseStats ──────────────────────────────────────────────────
        sb.AppendLine("  \"player\": [");
        for (int i = 0; i < _baseStats.Count; i++)
        {
            var bso = _baseStats[i];
            string c = i < _baseStats.Count - 1 ? "," : "";
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{JStr(bso.name)}\",");
            sb.AppendLine($"      \"health\": {AutoBalanceDataLoader.GetStatBase(bso, Enums.StatType.Health):F1},");
            sb.AppendLine($"      \"attack\": {AutoBalanceDataLoader.GetStatBase(bso, Enums.StatType.Attack):F1},");
            sb.AppendLine($"      \"defense\": {AutoBalanceDataLoader.GetStatBase(bso, Enums.StatType.Defense):F1},");
            sb.AppendLine($"      \"stamina\": {AutoBalanceDataLoader.GetStatBase(bso, Enums.StatType.Stamina):F1},");
            sb.AppendLine($"      \"speed\": {AutoBalanceDataLoader.GetStatBase(bso, Enums.StatType.Speed):F1}");
            sb.AppendLine($"    }}{c}");
        }
        sb.AppendLine("  ],");

        // ── upgradeNodes ──────────────────────────────────────────────────────
        sb.AppendLine("  \"upgradeNodes\": [");
        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node == null) continue;
            string c = i < _nodes.Count - 1 ? "," : "";
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{JStr(node.name)}\",");
            sb.AppendLine($"      \"type\": \"{node.upgradeType}\",");
            sb.AppendLine($"      \"progressionType\": \"{node.progressionType}\",");
            sb.AppendLine($"      \"maxLevel\": {node.maxLevel},");
            sb.Append("      \"valuePerLevel\": [");
            for (int lvl = 1; lvl <= node.maxLevel; lvl++)
            {
                sb.Append($"{node.GetTotalValueAtLevel(lvl):F1}");
                if (lvl < node.maxLevel) sb.Append(", ");
            }
            sb.AppendLine("]");
            sb.AppendLine($"    }}{c}");
        }
        sb.AppendLine("  ],");

        // ── weapons ───────────────────────────────────────────────────────────
        sb.AppendLine("  \"weapons\": [");
        for (int wi = 0; wi < _weapons.Count; wi++)
        {
            var w       = _weapons[wi];
            string wc   = wi < _weapons.Count - 1 ? "," : "";
            float flatAtk = AutoBalanceDataLoader.GetWeaponFlatAttack(w);
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": \"{JStr(w.uniqueID)}\",");
            sb.AppendLine($"      \"name\": \"{JStr(w.itemNameID)}\",");
            sb.AppendLine($"      \"tier\": \"{w.weaponTier}\",");
            sb.AppendLine($"      \"rarity\": \"{w.itemRarity}\",");
            sb.AppendLine($"      \"family\": \"{w.familyType}\",");
            sb.AppendLine($"      \"flatAttack\": {flatAtk:F1},");
            sb.AppendLine($"      \"enemyMult\": {w.enemyDamageMultiplier:F2},");
            sb.Append("      \"combos\": [");
            if (w.combos != null && w.combos.Count > 0)
            {
                sb.AppendLine();
                for (int ci = 0; ci < w.combos.Count; ci++)
                {
                    var m  = AutoBalanceDataLoader.GetComboMetrics(w.combos[ci], flatAtk);
                    string cc = ci < w.combos.Count - 1 ? "," : "";
                    sb.AppendLine("        {");
                    sb.AppendLine($"          \"inputSequence\": \"{JStr(m.inputSequence)}\",");
                    sb.AppendLine($"          \"stepCount\": {m.steps.Length},");
                    sb.AppendLine($"          \"totalDamage\": {m.totalDamage:F1},");
                    sb.AppendLine($"          \"totalTime\": {m.totalTime:F2},");
                    sb.AppendLine($"          \"totalStamina\": {m.totalStamina:F1},");
                    sb.AppendLine($"          \"dps\": {m.comboDps:F1},");
                    sb.AppendLine($"          \"staminaEfficiency\": {m.staminaEfficiency:F2},");
                    sb.Append("          \"steps\": [");
                    if (m.steps.Length > 0)
                    {
                        sb.AppendLine();
                        for (int si = 0; si < m.steps.Length; si++)
                        {
                            var s   = m.steps[si];
                            string sc = si < m.steps.Length - 1 ? "," : "";
                            sb.AppendLine($"            {{ \"input\": \"{JStr(s.inputLabel)}\", \"name\": \"{JStr(s.attackName)}\", \"damage\": {s.damage:F1}, \"mult\": {s.damageMultiplier:F2}, \"time\": {s.timeConsumed:F2}, \"activeWindow\": {s.activeWindowTime:F2}, \"stamina\": {s.staminaCost:F1}, \"poiseDamage\": {s.poiseDamage:F0}, \"dps\": {s.stepDps:F1} }}{sc}");
                        }
                        sb.Append("          ]");
                    }
                    else sb.Append("]");
                    sb.AppendLine();
                    sb.AppendLine($"        }}{cc}");
                }
                sb.Append("      ]");
            }
            else sb.Append("]");
            sb.AppendLine();
            sb.AppendLine($"    }}{wc}");
        }
        sb.AppendLine("  ],");

        // ── enemies ───────────────────────────────────────────────────────────
        sb.AppendLine("  \"enemies\": [");
        for (int ei = 0; ei < _enemies.Count; ei++)
        {
            var e     = _enemies[ei];
            string ec = ei < _enemies.Count - 1 ? "," : "";
            float eDef = AutoBalanceDataLoader.GetEnemyDefense(e);
            float eRed = Mathf.Min(eDef / (eDef + Mathf.Max(_defK, 0.001f)), _defCap);
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": \"{JStr(e.id)}\",");
            sb.AppendLine($"      \"name\": \"{JStr(e.displayName)}\",");
            sb.AppendLine($"      \"zone\": {e.zone},");
            sb.AppendLine($"      \"category\": \"{e.category}\",");
            sb.AppendLine($"      \"health\": {AutoBalanceDataLoader.GetEnemyHealth(e):F0},");
            sb.AppendLine($"      \"attack\": {AutoBalanceDataLoader.GetEnemyAttack(e):F0},");
            sb.AppendLine($"      \"defense\": {eDef:F0},");
            sb.AppendLine($"      \"defenseReductionPct\": {eRed * 100f:F1},");
            sb.AppendLine($"      \"weaponMult\": {AutoBalanceDataLoader.GetEnemyWeaponMult(e):F2},");
            sb.AppendLine($"      \"maxPoise\": {e.maxPoise:F0},");
            sb.AppendLine($"      \"poiseRegenDelay\": {e.poiseRegenDelay:F1},");
            sb.AppendLine($"      \"poiseRegenRate\": {e.poiseRegenRate:F1},");
            sb.AppendLine($"      \"stunDuration\": {e.stunDuration:F1},");
            sb.AppendLine($"      \"parryPoiseDamage\": {e.parryPoiseDamage:F0},");
            sb.AppendLine($"      \"perfectParryPoiseDamage\": {e.perfectParryPoiseDamage:F0}");
            sb.AppendLine($"    }}{ec}");
        }
        sb.AppendLine("  ],");

        // ── armors ────────────────────────────────────────────────────────────
        sb.AppendLine("  \"armors\": [");
        for (int ai = 0; ai < _armors.Count; ai++)
        {
            var a     = _armors[ai];
            string ac = ai < _armors.Count - 1 ? "," : "";
            var aList = new List<EquipableItemData> { a };
            float aDef = AutoBalanceDataLoader.GetArmorStatFlat(aList, Enums.StatType.Defense);
            float aHp  = AutoBalanceDataLoader.GetArmorStatFlat(aList, Enums.StatType.Health);
            float aSta = AutoBalanceDataLoader.GetArmorStatFlat(aList, Enums.StatType.Stamina);
            sb.AppendLine($"    {{ \"id\": \"{JStr(a.uniqueID)}\", \"name\": \"{JStr(a.itemNameID)}\", \"slot\": \"{a.equipSlot}\", \"rarity\": \"{a.itemRarity}\", \"flatDefense\": {aDef:F1}, \"flatHealth\": {aHp:F1}, \"flatStamina\": {aSta:F1} }}{ac}");
        }
        sb.AppendLine("  ],");

        // ── consumables ───────────────────────────────────────────────────────
        sb.AppendLine("  \"consumables\": [");
        for (int ci = 0; ci < _consumables.Count; ci++)
        {
            var con   = _consumables[ci];
            string cc = ci < _consumables.Count - 1 ? "," : "";
            sb.AppendLine($"    {{ \"id\": \"{JStr(con.uniqueID)}\", \"name\": \"{JStr(con.itemNameID)}\", \"rarity\": \"{con.itemRarity}\", \"buffCount\": {con.buffs?.Count ?? 0} }}{cc}");
        }
        sb.AppendLine("  ],");

        // ── simulationBatch: all weapons × all enemies ────────────────────────
        sb.AppendLine("  \"simulationBatch\": [");
        bool first = true;
        for (int wi = 0; wi < _weapons.Count; wi++)
        {
            var w = _weapons[wi];
            var (comboDmg, comboTime, comboStam, comboSteps) = AutoBalanceDataLoader.GetLongestCombo(w);
            float flatAtk = AutoBalanceDataLoader.GetWeaponFlatAttack(w);

            for (int ei = 0; ei < _enemies.Count; ei++)
            {
                var e   = _enemies[ei];
                var res = AutoBalanceSimulator.Run(new AutoBalanceSimulator.SimConfig
                {
                    playerHealth        = pHp,   playerAttack   = pAtk, playerDefense = 0f,
                    playerStamina       = pSta,
                    useDirectPlayerDef  = true,
                    playerArmorDefPct   = armorFrac,
                    playerBonfireDefPct = bonFrac,
                    weaponFlatAttack    = flatAtk,
                    comboTotalDamage    = comboDmg,  comboTotalTime    = comboTime,
                    comboTotalStamina   = comboStam, comboStepCount    = comboSteps,
                    enemyHealth         = AutoBalanceDataLoader.GetEnemyHealth(e),
                    enemyAttackBase     = AutoBalanceDataLoader.GetEnemyAttack(e),
                    enemyWeaponMult     = AutoBalanceDataLoader.GetEnemyWeaponMult(e),
                    enemyDefense        = AutoBalanceDataLoader.GetEnemyDefense(e),
                    defenseK            = _defK, reductionCap = _defCap,
                });

                string ttkSt = "n/a", htkSt = "n/a", htdSt = "n/a";
                if (_targets != null && _targets.TryGetZoneTargets(e.zone, e.category, out var zt))
                {
                    ttkSt = res.ttk >= zt.ttk.x && res.ttk <= zt.ttk.y       ? "OK" : res.ttk < zt.ttk.x ? "TooFast" : "TooSlow";
                    htkSt = res.hitsToKill >= zt.hitsToKill.x && res.hitsToKill <= zt.hitsToKill.y ? "OK" : res.hitsToKill < zt.hitsToKill.x ? "TooFew" : "TooMany";
                    htdSt = res.hitsToDie  >= zt.hitsToDie.x  && res.hitsToDie  <= zt.hitsToDie.y  ? "OK" : res.hitsToDie  < zt.hitsToDie.x  ? "TooFew" : "TooMany";
                }

                if (!first) sb.AppendLine(",");
                first = false;
                sb.AppendLine("    {");
                sb.AppendLine($"      \"weapon\": \"{JStr(w.itemNameID)}\",");
                sb.AppendLine($"      \"weaponFamily\": \"{w.familyType}\",");
                sb.AppendLine($"      \"weaponTier\": \"{w.weaponTier}\",");
                sb.AppendLine($"      \"enemy\": \"{JStr(e.displayName)}\",");
                sb.AppendLine($"      \"enemyZone\": {e.zone},");
                sb.AppendLine($"      \"enemyCategory\": \"{e.category}\",");
                sb.AppendLine($"      \"ttk\": {(res.ttk < 9999f ? $"{res.ttk:F1}" : "null")},");
                sb.AppendLine($"      \"hitsToKill\": {res.hitsToKill},");
                sb.AppendLine($"      \"hitsToDie\": {res.hitsToDie},");
                sb.AppendLine($"      \"playerDPS\": {res.playerDPS:F1},");
                sb.AppendLine($"      \"playerDamagePerHit\": {res.playerDamagePerHit:F1},");
                sb.AppendLine($"      \"enemyDamagePerHit\": {res.enemyDamagePerHit:F1},");
                sb.AppendLine($"      \"playerEHP\": {res.playerEHP:F1},");
                sb.AppendLine($"      \"playerDefReductionPct\": {res.playerDefReduction * 100f:F1},");
                sb.AppendLine($"      \"enemyDefReductionPct\": {res.enemyDefReduction * 100f:F1},");
                sb.AppendLine($"      \"staminaPerCombo\": {res.staminaPerCombo:F1},");
                sb.AppendLine($"      \"ttkStatus\": \"{ttkSt}\",");
                sb.AppendLine($"      \"hitsToKillStatus\": \"{htkSt}\",");
                sb.AppendLine($"      \"hitsToDieStatus\": \"{htdSt}\"");
                sb.Append("    }");
            }
        }
        sb.AppendLine();
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        string path = "AutoBalance_Full.json";
        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log($"[AutoBalance] JSON completo exportado → {path}");
        EditorUtility.RevealInFinder(path);
    }

    private static string JStr(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    #endregion

    #region JSON Diff Parser

    private void ParseDiffJson(string json)
    {
        _diffEntries.Clear();

        // Enemies
        foreach (var obj in ExtractObjects(ExtractArrayText(json, "enemies")))
        {
            string id   = GetJsonStr(obj, "id");
            string name = GetJsonStr(obj, "name");
            var e = _enemies.Find(x => (!string.IsNullOrEmpty(id) && x.id == id) || x.displayName == name);
            if (e == null) continue;

            string lbl = e.displayName;
            TryAddDiff("Enemigo", lbl, "health",                  AutoBalanceDataLoader.GetEnemyHealth(e),     GetJsonFloat(obj, "health",                  AutoBalanceDataLoader.GetEnemyHealth(e)));
            TryAddDiff("Enemigo", lbl, "attack",                  AutoBalanceDataLoader.GetEnemyAttack(e),     GetJsonFloat(obj, "attack",                  AutoBalanceDataLoader.GetEnemyAttack(e)));
            TryAddDiff("Enemigo", lbl, "defense",                 AutoBalanceDataLoader.GetEnemyDefense(e),    GetJsonFloat(obj, "defense",                 AutoBalanceDataLoader.GetEnemyDefense(e)));
            TryAddDiff("Enemigo", lbl, "weaponMult",              AutoBalanceDataLoader.GetEnemyWeaponMult(e), GetJsonFloat(obj, "weaponMult",              AutoBalanceDataLoader.GetEnemyWeaponMult(e)));
            TryAddDiff("Enemigo", lbl, "maxPoise",                e.maxPoise,                GetJsonFloat(obj, "maxPoise",                e.maxPoise));
            TryAddDiff("Enemigo", lbl, "poiseRegenDelay",         e.poiseRegenDelay,         GetJsonFloat(obj, "poiseRegenDelay",         e.poiseRegenDelay));
            TryAddDiff("Enemigo", lbl, "poiseRegenRate",          e.poiseRegenRate,          GetJsonFloat(obj, "poiseRegenRate",          e.poiseRegenRate));
            TryAddDiff("Enemigo", lbl, "stunDuration",            e.stunDuration,            GetJsonFloat(obj, "stunDuration",            e.stunDuration));
            TryAddDiff("Enemigo", lbl, "parryPoiseDamage",        e.parryPoiseDamage,        GetJsonFloat(obj, "parryPoiseDamage",        e.parryPoiseDamage));
            TryAddDiff("Enemigo", lbl, "perfectParryPoiseDamage", e.perfectParryPoiseDamage, GetJsonFloat(obj, "perfectParryPoiseDamage", e.perfectParryPoiseDamage));
        }

        // Weapons
        foreach (var obj in ExtractObjects(ExtractArrayText(json, "weapons")))
        {
            string id   = GetJsonStr(obj, "id");
            string name = GetJsonStr(obj, "name");
            var w = _weapons.Find(x => (!string.IsNullOrEmpty(id) && x.uniqueID == id) || x.itemNameID == name);
            if (w == null) continue;

            string lbl    = w.itemNameID;
            float curFlat = AutoBalanceDataLoader.GetWeaponFlatAttack(w);
            TryAddDiff("Arma", lbl, "flatAttack", curFlat,                     GetJsonFloat(obj, "flatAttack", curFlat));
            TryAddDiff("Arma", lbl, "enemyMult",  w.enemyDamageMultiplier,     GetJsonFloat(obj, "enemyMult",  w.enemyDamageMultiplier));
        }

        // Upgrade nodes
        foreach (var obj in ExtractObjects(ExtractArrayText(json, "upgradeNodes")))
        {
            string name = GetJsonStr(obj, "name");
            var node = _nodes.Find(x => x.name == name);
            if (node == null) continue;

            int jsonMax = (int)GetJsonFloat(obj, "maxLevel", node.maxLevel);
            TryAddDiff("Nodo", node.name, "maxLevel", node.maxLevel, jsonMax);

            var perLevel = GetJsonFloatArray(obj, "valuePerLevel");
            if (perLevel.Count > 0)
                TryAddDiff("Nodo", node.name, "valorMaxNivel", node.GetTotalValueAtLevel(node.maxLevel), perLevel[perLevel.Count - 1]);
        }

        _diffLoaded = true;
    }

    private void TryAddDiff(string cat, string entity, string field, float current, float json)
    {
        if (Mathf.Approximately(current, json)) return;
        _diffEntries.Add(new DiffEntry { category = cat, entityName = entity, field = field, currentValue = current, jsonValue = json });
    }

    private static string ExtractArrayText(string json, string key)
    {
        string marker = $"\"{key}\":";
        int pos = json.IndexOf(marker);
        if (pos < 0) return "";
        int start = json.IndexOf('[', pos + marker.Length);
        if (start < 0) return "";
        int depth = 0, end = start;
        for (int i = start; i < json.Length; i++)
        {
            if      (json[i] == '[') depth++;
            else if (json[i] == ']') { if (--depth == 0) { end = i; break; } }
        }
        return json.Substring(start + 1, end - start - 1);
    }

    private static List<string> ExtractObjects(string arrayText)
    {
        var result = new List<string>();
        int i = 0;
        while (i < arrayText.Length)
        {
            while (i < arrayText.Length && arrayText[i] != '{') i++;
            if (i >= arrayText.Length) break;
            int start = i, depth = 0;
            for (; i < arrayText.Length; i++)
            {
                if      (arrayText[i] == '{') depth++;
                else if (arrayText[i] == '}') { if (--depth == 0) { result.Add(arrayText.Substring(start, i - start + 1)); i++; break; } }
            }
        }
        return result;
    }

    private static float GetJsonFloat(string obj, string key, float def = 0f)
    {
        var m = System.Text.RegularExpressions.Regex.Match(obj, $@"""{key}"":\s*([\d.\-]+)");
        return m.Success && float.TryParse(m.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : def;
    }

    private static string GetJsonStr(string obj, string key)
    {
        var m = System.Text.RegularExpressions.Regex.Match(obj, $@"""{key}"":\s*""([^""]*)""");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static List<float> GetJsonFloatArray(string obj, string key)
    {
        var result = new List<float>();
        var m = System.Text.RegularExpressions.Regex.Match(obj, $@"""{key}"":\s*\[([^\]]+)\]");
        if (!m.Success) return result;
        foreach (var part in m.Groups[1].Value.Split(','))
        {
            if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
                result.Add(v);
        }
        return result;
    }

    #endregion

    #region Target Helpers

    private string GetTargetStr(int type)
    {
        var e = SafeGet(_enemies, _simEnemy);
        if (_targets == null || e == null || !_targets.TryGetZoneTargets(e.zone, e.category, out var zt)) return "-";
        return type switch
        {
            0 => $"{zt.ttk.x:F1}–{zt.ttk.y:F1}s",
            1 => $"{zt.hitsToKill.x}–{zt.hitsToKill.y}",
            2 => $"{zt.hitsToDie.x}–{zt.hitsToDie.y}",
            _ => "-"
        };
    }

    #endregion

    #region UI Primitives

    private SerializedObject GetBalanceSO(Object target)
    {
        if (target == null) return null;
        if (_balanceSOTarget != target) { _balanceSOTarget = target; _balanceSO = new SerializedObject(target); }
        else _balanceSO?.Update();
        return _balanceSO;
    }

    private SerializedObject GetPlayerBaseStatsSO(Object target)
    {
        if (target == null) return null;
        if (_playerBaseSOTarget != target) { _playerBaseSOTarget = target; _playerBaseStatsSO = new SerializedObject(target); }
        else _playerBaseStatsSO?.Update();
        return _playerBaseStatsSO;
    }

    private SerializedObject GetNodeEditSO(UpgradeNodeSO node)
    {
        if (!_nodeEditSOs.TryGetValue(node, out var so) || so == null)
        {
            so = new SerializedObject(node);
            _nodeEditSOs[node] = so;
        }
        else so.Update();
        return so;
    }

    private static void DrawProp(SerializedObject so, string propName, string label = null, bool children = false)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        var gc = label != null ? new GUIContent(label) : null;
        if (gc != null) EditorGUILayout.PropertyField(prop, gc, children);
        else            EditorGUILayout.PropertyField(prop, children);
    }

    private void DrawListItem(string label, bool selected, System.Action onClick)
    {
        var rect = GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true));
        if (selected) EditorGUI.DrawRect(rect, COL_TABSEL);
        else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, new Color(0.25f, 0.25f, 0.30f));
        GUI.color = selected ? Color.white : new Color(0.80f, 0.80f, 0.80f);
        GUI.Label(new Rect(rect.x + 8, rect.y + 3, rect.width - 10, rect.height), label, EditorStyles.miniLabel);
        GUI.color = Color.white;
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        { onClick?.Invoke(); Event.current.Use(); Repaint(); }
    }

    private void PingButton(Object target)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset",     GUILayout.Width(90)))  EditorGUIUtility.PingObject(target);
        if (GUILayout.Button("Open Inspector", GUILayout.Width(110))) Selection.activeObject = target;
        GUILayout.EndHorizontal();
    }

    private void KPICard(Rect r, string label, string value, Color accent)
    {
        EditorGUI.DrawRect(r, COL_PANEL);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), accent);
        GUI.Label(new Rect(r.x + 8, r.y + 6,  r.width - 12, 28), value, _sVal);
        GUI.Label(new Rect(r.x + 8, r.y + 36, r.width - 12, 18), label, _sLabel);
    }

    private bool ColorButton(string label, Color col, float w)
    {
        GUI.color = col;
        bool p = GUILayout.Button(label, GUILayout.Width(w), GUILayout.Height(26));
        GUI.color = Color.white;
        return p;
    }

    private void BeginPanel(string title, float w)
    {
        if (w > 0) GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(w));
        else       GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        GUILayout.Label(title, EditorStyles.boldLabel);
        GUI.color = Color.white;
        SectionDivider();
        GUILayout.Space(2);
    }

    private void EndPanel() { GUILayout.Space(4); GUILayout.EndVertical(); }

    private void SectionHeader(string text)
    {
        GUI.color = new Color(0.9f, 0.9f, 0.9f);
        GUILayout.Label(text, EditorStyles.boldLabel);
        GUI.color = Color.white;
        SectionDivider();
        GUILayout.Space(4);
    }

    private void SectionSubHeader(string text)
    {
        GUILayout.Space(4);
        GUI.color = new Color(0.70f, 0.70f, 0.70f);
        GUILayout.Label(text, EditorStyles.boldLabel);
        GUI.color = Color.white;
    }

    private void SectionDivider()
        => EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1f, GUILayout.ExpandWidth(true)), new Color(0.30f, 0.30f, 0.35f));

    private void StatRow(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUI.color = new Color(0.70f, 0.70f, 0.70f);
        GUILayout.Label(label, GUILayout.Width(160));
        GUI.color = Color.white;
        GUILayout.Label(value);
        GUILayout.EndHorizontal();
    }

    private void AlertRow(string msg, Color col)
    { GUI.color = col; GUILayout.Label($"▲ {msg}", EditorStyles.miniLabel); GUI.color = Color.white; }

    private void GreenRow(string msg)
    { GUI.color = COL_GREEN; GUILayout.Label(msg, EditorStyles.miniLabel); GUI.color = Color.white; }

    private void TableHeader(string[] labels, float[] widths)
    {
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), new Color(0.12f, 0.12f, 0.18f));
        GUILayout.BeginHorizontal();
        for (int i = 0; i < labels.Length; i++)
        {
            GUI.color = new Color(0.80f, 0.80f, 0.90f);
            GUILayout.Label(labels[i], EditorStyles.miniBoldLabel, GUILayout.Width(widths[i]));
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    private void TableRow(bool alt, string[] values, float[] widths)
    {
        if (alt) EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, ROW_H, GUILayout.ExpandWidth(true)), COL_ROWALT);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < values.Length; i++)
        {
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            GUILayout.Label(values[i], EditorStyles.miniLabel, GUILayout.Width(widths[i]));
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    private void ResultRow(string metric, string value, string target, bool highlight)
    {
        GUILayout.BeginHorizontal();
        GUI.color = new Color(0.70f, 0.70f, 0.70f);
        GUILayout.Label(metric, EditorStyles.miniLabel,    GUILayout.Width(160f));
        GUI.color = highlight ? new Color(0.9f, 0.65f, 0.3f) : Color.white;
        GUILayout.Label(value,  EditorStyles.miniLabel,    GUILayout.Width(90f));
        GUI.color = new Color(0.55f, 0.55f, 0.60f);
        GUILayout.Label(target, EditorStyles.miniLabel,    GUILayout.Width(100f));
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    private void ColLabel(string text, float w, bool bold)
    {
        GUI.color = bold ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        GUILayout.Label(text, bold ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel, GUILayout.Width(w));
        GUI.color = Color.white;
    }

    private static T SafeGet<T>(List<T> list, int idx) where T : class
        => list != null && list.Count > 0 ? list[Mathf.Clamp(idx, 0, list.Count - 1)] : null;

    #endregion

    #region Styles

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;
        _sVal     = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
        _sLabel   = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        _sTabSel  = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, padding = new RectOffset(12, 4, 0, 0), normal = { textColor = Color.white } };
        _sTabNorm = new GUIStyle(EditorStyles.label) { padding = new RectOffset(12, 4, 0, 0) };
    }

    #endregion
}
