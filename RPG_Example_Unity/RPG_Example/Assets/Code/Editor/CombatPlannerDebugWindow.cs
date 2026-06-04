#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CombatPlannerDebugWindow : EditorWindow
{
    #region Fields

    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    private readonly List<EnemyBase> _enemies = new();
    private EnemyBase _selected;
    private EnemyCombatPlanner _selectedPlanner;

    private double _lastRefresh;
    private const double REFRESH_INTERVAL = 0.1;

    private const float LEFT_PANEL_WIDTH = 200f;
    private const float SCORE_BAR_WIDTH = 120f;
    private const float SCORE_BAR_HEIGHT = 14f;
    private const float ROW_HEIGHT = 20f;

    private GUIStyle _headerStyle;
    private GUIStyle _selectedRowStyle;
    private GUIStyle _normalRowStyle;
    private GUIStyle _activeActionStyle;
    private bool _stylesInitialized;

    private static readonly Color COLOR_HIGH = new Color(0.20f, 0.80f, 0.30f, 1f);
    private static readonly Color COLOR_MID = new Color(0.90f, 0.75f, 0.10f, 1f);
    private static readonly Color COLOR_LOW = new Color(0.85f, 0.25f, 0.20f, 1f);
    private static readonly Color COLOR_ACTIVE = new Color(0.20f, 0.55f, 1.00f, 0.25f);
    private static readonly Color COLOR_BAR_BG = new Color(0.15f, 0.15f, 0.15f, 1f);

    #endregion

    #region Menu

    [MenuItem("Tools/Combat Planner Debugger")]
    public static void Open()
    {
        var w = GetWindow<CombatPlannerDebugWindow>("Combat Planner");
        w.minSize = new Vector2(700f, 400f);
    }

    #endregion

    #region Unity Editor Callbacks

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!Application.isPlaying) return;
        if (EditorApplication.timeSinceStartup - _lastRefresh < REFRESH_INTERVAL) return;

        _lastRefresh = EditorApplication.timeSinceStartup;
        RefreshEnemyList();
        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the Combat Planner Debugger.", MessageType.Info);
            return;
        }

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawDivider();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Toolbar

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            RefreshEnemyList();
        }

        GUILayout.FlexibleSpace();

        if (_selected != null)
        {
            GUILayout.Label($"Watching: {_selected.name}", EditorStyles.miniLabel);

            if (_selectedPlanner != null)
            {
                string actionName = _selectedPlanner.Debug_CurrentActionName;
                string category   = _selectedPlanner.CurrentActionCategory.ToString();
                GUILayout.Label($"  |  Action: {actionName}  [{category}]", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Left Panel — Enemy List

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH));
        EditorGUILayout.LabelField("Enemies", _headerStyle, GUILayout.Height(22));

        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;

            bool isSelected = enemy == _selected;
            var style = isSelected ? _selectedRowStyle : _normalRowStyle;

            var ctx = enemy.Context;
            var alertLevel = ctx?.Perception?.AlertLevel ?? Enums.AlertLevel.Unaware;
            string label = $"{enemy.name}\n<size=9><color=#888>{alertLevel}</color></size>";

            if (GUILayout.Button(new GUIContent(label), style, GUILayout.Height(36)))
            {
                SelectEnemy(enemy);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Divider

    private void DrawDivider()
    {
        var r = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.35f));
    }

    #endregion

    #region Right Panel — Planner Detail

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        if (_selected == null || _selectedPlanner == null)
        {
            EditorGUILayout.HelpBox("Select an enemy from the list.", MessageType.None);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawCurrentAction();
        GUILayout.Space(8);
        DrawScoreContext();
        GUILayout.Space(8);
        DrawTokenStatus();
        GUILayout.Space(8);
        DrawTemperament();
        GUILayout.Space(8);
        DrawActionList();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCurrentAction()
    {
        EditorGUILayout.LabelField("Current Action", _headerStyle);

        var action   = _selectedPlanner.CurrentAction;
        string name  = _selectedPlanner.Debug_CurrentActionName;
        string cat   = _selectedPlanner.CurrentActionCategory.ToString();

        var bgRect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(bgRect, action != null ? COLOR_ACTIVE : new Color(0.1f, 0.1f, 0.1f, 0.3f));

        EditorGUI.indentLevel++;
        DrawContextRow("Name",     name);
        DrawContextRow("Category", cat);
        EditorGUI.indentLevel--;

        EditorGUILayout.EndVertical();
    }

    private void DrawScoreContext()
    {
        var sc = _selectedPlanner.Debug_GetLastContext();

        EditorGUILayout.LabelField("Score Context", _headerStyle);

        EditorGUI.indentLevel++;

        DrawContextRow("Distance", $"{sc.distance:F2} m");
        DrawContextRow("Angle", $"{sc.angleDegree:F1}°");
        DrawContextRow("Self HP", $"{sc.selfHP * 100f:F0}%");
        DrawContextRow("Target HP", $"{sc.targetHP * 100f:F0}%");
        DrawContextRow("Has LOS", sc.hasLOS ? "✓" : "✗");
        DrawContextRow("Space Free", sc.spaceFree ? "✓" : "✗");
        DrawContextRow("Target Windup", $"{sc.targetWindup:F2}");
        DrawContextRow("Target Recovery", $"{sc.targetRecovery:F2}");
        DrawContextRow("Attack Intent", $"{sc.attackIntent01:F2}");
        DrawContextRow("Advantage", $"{sc.advantage:F2}");
        DrawContextRow("Token Available", sc.attackTokenAvailable ? "✓" : "✗");

        EditorGUI.indentLevel--;
    }

    private void DrawTokenStatus()
    {
        if (!GameServices.TryGet<AttackTokenService>(out var tokenService)) return;

        var faction = _selected.Faction;
        int active = tokenService.ActiveTokens(faction);
        int max = tokenService.MaxTokens(faction);

        EditorGUILayout.LabelField("Attack Tokens", _headerStyle);
        EditorGUI.indentLevel++;

        Color prev = GUI.color;
        GUI.color = active >= max ? COLOR_LOW : COLOR_HIGH;
        DrawContextRow($"{faction}", $"{active} / {max} active");
        GUI.color = prev;

        EditorGUI.indentLevel--;
    }

    private void DrawActionList()
    {
        var sc = _selectedPlanner.Debug_GetLastContext();
        var currentAction = _selectedPlanner.CurrentAction;
        var actions = _selectedPlanner.Debug_Actions;

        EditorGUILayout.LabelField("Actions", _headerStyle);
        GUILayout.Space(2);

        foreach (var action in actions)
        {
            if (action == null) continue;

            bool isActive = action == currentAction;
            float score = action.Evaluate(sc);

            DrawActionRow(action, sc, score, isActive);
            GUILayout.Space(2);
        }
    }

    private void DrawActionRow(EnemyAction action, ScoreContext sc, float score, bool isActive)
    {
        float normalizedScore = action.baseScore > 0f
            ? Mathf.Clamp01(score / action.baseScore)
            : 0f;

        var rowRect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(ROW_HEIGHT));

        if (isActive)
        {
            EditorGUI.DrawRect(rowRect, COLOR_ACTIVE);
        }

        EditorGUILayout.BeginHorizontal();

        var style = isActive ? _activeActionStyle : EditorStyles.foldout;

        string prefix = isActive ? "▶ " : "   ";
        string label = $"{prefix}{action.name}  [{action.category}]";
        bool foldout = SessionState.GetBool($"CPD_fold_{action.GetInstanceID()}", false);
        bool newFold = EditorGUILayout.Foldout(foldout, label, true, style);

        if (newFold != foldout)
        {
            SessionState.SetBool($"CPD_fold_{action.GetInstanceID()}", newFold);
        }

        GUILayout.FlexibleSpace();

        DrawScoreBar(normalizedScore, score);

        EditorGUILayout.EndHorizontal();

        if (newFold)
        {
            EditorGUI.indentLevel += 2;

            DrawContextRow("Base Score", $"{action.baseScore:F1}");
            DrawContextRow("Final Score", $"{score:F1}");
            DrawContextRow("Category", action.category.ToString());

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Considerations", EditorStyles.miniBoldLabel);

            var considerations = action.considerations;
            if (considerations == null || considerations.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                for (int i = 0; i < considerations.Count; i++)
                {
                    var c = considerations[i];
                    if (c == null) continue;

                    float cv = Mathf.Clamp01(c.Evaluate(sc));
                    DrawConsiderationRow(c, cv);
                }
            }

            GUILayout.Space(4);

            if (action != null)
            {
                var so = new SerializedObject(action);
                so.Update();

                EditorGUILayout.LabelField("Properties", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;

                var prop = so.GetIterator();
                prop.NextVisible(true);

                while (prop.NextVisible(false))
                {
                    if (prop.name == "m_Script") continue;
                    if (prop.name == "considerations") continue;

                    EditorGUILayout.PropertyField(prop, true);
                }

                if (so.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(action);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel -= 2;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawConsiderationRow(UtilityConsideration c, float value)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(c.name, EditorStyles.miniLabel, GUILayout.MinWidth(120));

        GUILayout.FlexibleSpace();
        DrawScoreBar(value, value, width: 80f);

        EditorGUILayout.EndHorizontal();

        var so = new SerializedObject(c);
        so.Update();

        EditorGUI.indentLevel++;

        var prop = so.GetIterator();
        prop.NextVisible(true);

        while (prop.NextVisible(false))
        {
            if (prop.name == "m_Script") continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(c);
        }

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Temperament

    private static readonly Enums.CombatTemperament[] ALL_TEMPERAMENTS =
    {
        Enums.CombatTemperament.SuperDefensive,
        Enums.CombatTemperament.Defensive,
        Enums.CombatTemperament.Normal,
        Enums.CombatTemperament.Aggressive,
        Enums.CombatTemperament.SuperAgressive
    };

    private static readonly string[] TEMPERAMENT_LABELS =
    {
        "SuperDef", "Defensive", "Normal", "Aggressive", "SuperAgg"
    };

    private static readonly string[] WEIGHT_FIELDS =
    {
        "Attack", "Pressure", "Disengage", "Defend", "Evade", "Heal", "Flank", "Special", "Bait"
    };

    private bool _temperamentFoldout = true;

    private void DrawTemperament()
    {
        _temperamentFoldout = EditorGUILayout.Foldout(_temperamentFoldout, "Temperament", true, _headerStyle);
        if (!_temperamentFoldout) return;

        var ctx = _selected.Context;
        var active = ctx != null ? ctx.Temperament : Enums.CombatTemperament.Normal;

        float colW = 72f;
        float labelW = 72f;
        float barH = 12f;
        float maxWeight = 2.0f;

        var centeredMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        var boldMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        // ── Header row ──────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(labelW + 4f);

        for (int t = 0; t < ALL_TEMPERAMENTS.Length; t++)
        {
            bool isActive = ALL_TEMPERAMENTS[t] == active;
            var style = isActive ? boldMini : centeredMini;
            Color prev = GUI.color;
            if (isActive) GUI.color = new Color(0.4f, 0.85f, 1f, 1f);
            GUILayout.Label(TEMPERAMENT_LABELS[t], style, GUILayout.Width(colW));
            GUI.color = prev;
        }

        EditorGUILayout.EndHorizontal();

        // ── One row per weight field ─────────────────────────────────────────
        for (int w = 0; w < WEIGHT_FIELDS.Length; w++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(WEIGHT_FIELDS[w], EditorStyles.miniLabel, GUILayout.Width(labelW));

            for (int t = 0; t < ALL_TEMPERAMENTS.Length; t++)
            {
                var weights = CombatTemperamentDB.Get(ALL_TEMPERAMENTS[t]);
                float weight = GetWeight(weights, w);
                float norm = Mathf.Clamp01(weight / maxWeight);
                bool isActive = ALL_TEMPERAMENTS[t] == active;

                var barRect = GUILayoutUtility.GetRect(colW, barH,
                    GUILayout.Width(colW), GUILayout.Height(barH));

                Color bg = isActive ? new Color(0.18f, 0.25f, 0.35f, 1f) : COLOR_BAR_BG;
                EditorGUI.DrawRect(barRect, bg);

                Color fill = weight >= 1.1f ? COLOR_HIGH : weight <= 0.85f ? COLOR_LOW : COLOR_MID;
                if (isActive) fill = new Color(fill.r * 1.2f, fill.g * 1.2f, fill.b * 1.2f, 1f);

                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * norm, barRect.height);
                EditorGUI.DrawRect(fillRect, fill);

                var ls = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                ls.normal.textColor = Color.white;
                EditorGUI.LabelField(barRect, $"{weight:F2}", ls);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Impact on current actions ─────────────────────────────────────────
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Score multiplier on current actions", EditorStyles.miniBoldLabel);

        var sc = _selectedPlanner.Debug_GetLastContext();
        var actions = _selectedPlanner.Debug_Actions;

        foreach (var action in actions)
        {
            if (action == null) continue;

            float baseActionScore = action.Evaluate(sc);
            if (baseActionScore <= 0f) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(action.name, EditorStyles.miniLabel, GUILayout.Width(labelW + colW));

            for (int t = 0; t < ALL_TEMPERAMENTS.Length; t++)
            {
                var weights = CombatTemperamentDB.Get(ALL_TEMPERAMENTS[t]);
                float mult = GetTemperamentWeightForCategory(action.category, weights);
                float effective = baseActionScore * mult;
                float norm = Mathf.Clamp01(effective / (action.baseScore * maxWeight));
                bool isActive = ALL_TEMPERAMENTS[t] == active;

                var barRect = GUILayoutUtility.GetRect(colW, barH,
                    GUILayout.Width(colW), GUILayout.Height(barH));

                Color bg = isActive ? new Color(0.18f, 0.25f, 0.35f, 1f) : COLOR_BAR_BG;
                EditorGUI.DrawRect(barRect, bg);

                Color fill = norm > 0.6f ? COLOR_HIGH : norm > 0.3f ? COLOR_MID : COLOR_LOW;
                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * norm, barRect.height);
                EditorGUI.DrawRect(fillRect, fill);

                var ls = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                ls.normal.textColor = Color.white;
                EditorGUI.LabelField(barRect, $"{effective:F0}", ls);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private static float GetWeight(TemperamentWeights w, int index)
    {
        switch (index)
        {
            case 0: return w.attack;
            case 1: return w.pressure;
            case 2: return w.disengage;
            case 3: return w.defend;
            case 4: return w.evade;
            case 5: return w.heal;
            case 6: return w.flank;
            case 7: return w.special;
            case 8: return w.bait;
            default: return 1f;
        }
    }

    private static float GetTemperamentWeightForCategory(Enums.ActionCategory cat, TemperamentWeights w)
    {
        switch (cat)
        {
            case Enums.ActionCategory.Attack: return w.attack;
            case Enums.ActionCategory.Defense: return w.defend;
            case Enums.ActionCategory.Disengage: return w.disengage;
            case Enums.ActionCategory.Movement: return w.pressure;
            case Enums.ActionCategory.Flank: return w.flank;
            case Enums.ActionCategory.Bait: return w.bait;
            case Enums.ActionCategory.Special: return w.special;
            default: return 1f;
        }
    }

    #endregion

    #region Helpers

    private void DrawContextRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(130));
        EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawScoreBar(float normalized, float rawValue, float width = SCORE_BAR_WIDTH)
    {
        var barRect = GUILayoutUtility.GetRect(width, SCORE_BAR_HEIGHT,
            GUILayout.Width(width), GUILayout.Height(SCORE_BAR_HEIGHT));

        EditorGUI.DrawRect(barRect, COLOR_BAR_BG);

        var fillRect = new Rect(barRect.x, barRect.y, barRect.width * normalized, barRect.height);
        var barColor = normalized > 0.6f ? COLOR_HIGH : normalized > 0.25f ? COLOR_MID : COLOR_LOW;
        EditorGUI.DrawRect(fillRect, barColor);

        var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        labelStyle.normal.textColor = Color.white;
        EditorGUI.LabelField(barRect, $"{rawValue:F1}", labelStyle);
    }

    private void RefreshEnemyList()
    {
        _enemies.Clear();
        var found = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        System.Array.Sort(found, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        _enemies.AddRange(found);

        if (_selected != null && !_enemies.Contains(_selected))
        {
            SelectEnemy(null);
        }
    }

    private void SelectEnemy(EnemyBase enemy)
    {
        _selected = enemy;
        _selectedPlanner = enemy != null ? enemy.GetComponent<EnemyCombatPlanner>() : null;
    }

    private void EnsureStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 0, 4, 4)
        };

        _selectedRowStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            richText = true,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 4, 4, 4)
        };
        _selectedRowStyle.normal.textColor = Color.white;
        _selectedRowStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.75f, 0.9f));

        _normalRowStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            richText = true,
            fontSize = 11,
            padding = new RectOffset(8, 4, 4, 4)
        };

        _activeActionStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };
        _activeActionStyle.normal.textColor = new Color(0.4f, 0.8f, 1f, 1f);
        _activeActionStyle.onNormal.textColor = new Color(0.4f, 0.8f, 1f, 1f);
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
#endif