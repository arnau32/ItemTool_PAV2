using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UpgradeNodeSO))]
public class UpgradeNodeSOEditor : Editor
{
    #region Fields

    // Identity
    private SerializedProperty _displayName;
    private SerializedProperty _icon;

    // Stat
    private SerializedProperty _upgradeType;
    private SerializedProperty _maxLevel;

    // Progression
    private SerializedProperty _progressionType;

    private SerializedProperty _manualLevels;

    private SerializedProperty _linearValueBase;
    private SerializedProperty _linearValueIncrement;
    private SerializedProperty _linearCostBase;
    private SerializedProperty _linearCostStep;

    private SerializedProperty _multiValueBase;
    private SerializedProperty _multiValueMultiplier;
    private SerializedProperty _multiCostBase;
    private SerializedProperty _multiCostMultiplier;

    private SerializedProperty _powerA;
    private SerializedProperty _powerB;
    private SerializedProperty _powerC;
    private SerializedProperty _powerCostBase;
    private SerializedProperty _powerCostMultiplier;

    private SerializedProperty _expValueBase;
    private SerializedProperty _expValueGrowth;
    private SerializedProperty _expCostBase;
    private SerializedProperty _expCostGrowth;

    private SerializedProperty _logA;
    private SerializedProperty _logK;
    private SerializedProperty _logC;
    private SerializedProperty _logCostBase;
    private SerializedProperty _logCostMultiplier;

    private SerializedProperty _drMaxValue;
    private SerializedProperty _drK;
    private SerializedProperty _drUseHyperbolic;
    private SerializedProperty _drCostBase;
    private SerializedProperty _drCostMultiplier;

    private bool _previewFoldout = true;

    #endregion

    #region Info Strings

    private static readonly (string info, string pros, string cons, MessageType msgType)[] _typeInfo =
    {
        // Manual
        (
            "Control total por nivel. Ideal para progresiones muy específicas o experimentales.",
            "Pros: Control absoluto.",
            "Cons: Tedioso para muchos niveles.",
            MessageType.None
        ),
        // Linear
        (
            "Incremento constante por nivel. La progresión más predecible y fácil de balancear.\n" +
            "value(n) = base + increment × (n-1)     cost(n) = baseCost + costStep × (n-1)",
            "Pros: Intuitiva para el jugador, fácil de ajustar.",
            "Cons: Power creep si no hay un techo bien diseñado.",
            MessageType.Info
        ),
        // Multiplicative
        (
            "Cada nivel multiplica al anterior. Crecimiento que se acelera suavemente.\n" +
            "value(n) = base × multiplier^(n-1)     cost(n) = baseCost × costMultiplier^(n-1)",
            "Pros: Sensación natural de progresión. Muy usada en RPGs.",
            "Cons: Puede escalar rápido en niveles altos si multiplier > 1.5.",
            MessageType.Info
        ),
        // Power
        (
            "Curva de potencia. b > 1 acelera, 0 < b < 1 desacelera.\n" +
            "value(n) = a × n^b + c",
            "Pros: Forma de curva flexible con pocos parámetros.",
            "Cons: Menos intuitiva de tunear sin ver la preview.",
            MessageType.Info
        ),
        // Exponential
        (
            "Crecimiento muy agresivo. Recomendada sobre todo para coste, no para stat directo.\n" +
            "value(n) = base × growth^(n-1)     cost(n) = baseCost × costGrowth^(n-1)",
            "Pros: Frena eficazmente la progresión late-game con el coste.",
            "Cons: Como stat directo puede romper el balance fácilmente.",
            MessageType.Warning
        ),
        // Logarithmic
        (
            "Mucho beneficio al inicio, cada vez menos. Perfecta para resistencias y probabilidades.\n" +
            "value(n) = a × log(n + k) + c",
            "Pros: Evita power creep, early game satisfactorio.",
            "Cons: Los últimos niveles se pueden sentir vacíos.",
            MessageType.Info
        ),
        // DiminishingReturns
        (
            "Se aproxima a un máximo sin alcanzarlo fácilmente. Ideal para armadura, evasión, crit.\n" +
            "Exponencial: value(n) = maxValue × (1 - e^(-k×n))\n" +
            "Hiperbólica:  value(n) = (n / (n + k)) × maxValue",
            "Pros: Imposible romper el juego, combina bien con caps.",
            "Cons: Menos intuitiva sin preview visible al jugador.",
            MessageType.Info
        ),
    };

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        _displayName = serializedObject.FindProperty("displayName");
        _icon = serializedObject.FindProperty("icon");
        _upgradeType = serializedObject.FindProperty("upgradeType");
        _maxLevel = serializedObject.FindProperty("maxLevel");

        _progressionType = serializedObject.FindProperty("progressionType");
        _manualLevels = serializedObject.FindProperty("manualLevels");

        _linearValueBase = serializedObject.FindProperty("linearValueBase");
        _linearValueIncrement = serializedObject.FindProperty("linearValueIncrement");
        _linearCostBase = serializedObject.FindProperty("linearCostBase");
        _linearCostStep = serializedObject.FindProperty("linearCostStep");

        _multiValueBase = serializedObject.FindProperty("multiValueBase");
        _multiValueMultiplier = serializedObject.FindProperty("multiValueMultiplier");
        _multiCostBase = serializedObject.FindProperty("multiCostBase");
        _multiCostMultiplier = serializedObject.FindProperty("multiCostMultiplier");

        _powerA = serializedObject.FindProperty("powerA");
        _powerB = serializedObject.FindProperty("powerB");
        _powerC = serializedObject.FindProperty("powerC");
        _powerCostBase = serializedObject.FindProperty("powerCostBase");
        _powerCostMultiplier = serializedObject.FindProperty("powerCostMultiplier");

        _expValueBase = serializedObject.FindProperty("expValueBase");
        _expValueGrowth = serializedObject.FindProperty("expValueGrowth");
        _expCostBase = serializedObject.FindProperty("expCostBase");
        _expCostGrowth = serializedObject.FindProperty("expCostGrowth");

        _logA = serializedObject.FindProperty("logA");
        _logK = serializedObject.FindProperty("logK");
        _logC = serializedObject.FindProperty("logC");
        _logCostBase = serializedObject.FindProperty("logCostBase");
        _logCostMultiplier = serializedObject.FindProperty("logCostMultiplier");

        _drMaxValue = serializedObject.FindProperty("drMaxValue");
        _drK = serializedObject.FindProperty("drK");
        _drUseHyperbolic = serializedObject.FindProperty("drUseHyperbolic");
        _drCostBase = serializedObject.FindProperty("drCostBase");
        _drCostMultiplier = serializedObject.FindProperty("drCostMultiplier");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentity();
        EditorGUILayout.Space(4);
        DrawStat();
        EditorGUILayout.Space(4);
        DrawProgression();
        EditorGUILayout.Space(4);
        DrawPreview();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Draw Sections

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_displayName);
        EditorGUILayout.PropertyField(_icon);
    }

    private void DrawStat()
    {
        EditorGUILayout.LabelField("Stat", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_upgradeType);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_maxLevel);
        bool maxLevelChanged = EditorGUI.EndChangeCheck();

        if (maxLevelChanged && _progressionType.enumValueIndex == (int)Enums.ProgressionType.Manual)
            SyncManualLevels();
    }

    private void DrawProgression()
    {
        EditorGUILayout.LabelField("Progression", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_progressionType);

        int typeIndex = _progressionType.enumValueIndex;
        if (typeIndex >= 0 && typeIndex < _typeInfo.Length)
        {
            var info = _typeInfo[typeIndex];

            if (info.msgType != MessageType.None)
                EditorGUILayout.HelpBox(info.info, info.msgType);
            else
                EditorGUILayout.HelpBox(info.info, MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(info.pros, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(info.cons, EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.Space(4);

        switch ((Enums.ProgressionType)typeIndex)
        {
            case Enums.ProgressionType.Manual: DrawManual(); break;
            case Enums.ProgressionType.Linear: DrawLinear(); break;
            case Enums.ProgressionType.Multiplicative: DrawMultiplicative(); break;
            case Enums.ProgressionType.Power: DrawPower(); break;
            case Enums.ProgressionType.Exponential: DrawExponential(); break;
            case Enums.ProgressionType.Logarithmic: DrawLogarithmic(); break;
            case Enums.ProgressionType.DiminishingReturns: DrawDiminishing(); break;
        }
    }

    private void DrawManual()
    {
        SyncManualLevels();

        EditorGUILayout.LabelField("Levels (value = stat increment, cost = AuraDust)", EditorStyles.miniLabel);

        for (int i = 0; i < _manualLevels.arraySize; i++)
        {
            SerializedProperty entry = _manualLevels.GetArrayElementAtIndex(i);
            SerializedProperty value = entry.FindPropertyRelative("value");
            SerializedProperty cost = entry.FindPropertyRelative("cost");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Lv {i + 1}", GUILayout.Width(38));
                EditorGUILayout.PropertyField(value, GUIContent.none, GUILayout.Width(70));
                EditorGUILayout.LabelField("val", EditorStyles.miniLabel, GUILayout.Width(22));
                EditorGUILayout.PropertyField(cost, GUIContent.none, GUILayout.Width(70));
                EditorGUILayout.LabelField("cost", EditorStyles.miniLabel, GUILayout.Width(30));
            }
        }
    }

    private void DrawLinear()
    {
        EditorGUILayout.LabelField("Value", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_linearValueBase, new GUIContent("Base Value"));
        EditorGUILayout.PropertyField(_linearValueIncrement, new GUIContent("Increment/Level"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_linearCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_linearCostStep, new GUIContent("Cost Step/Level"));
    }

    private void DrawMultiplicative()
    {
        EditorGUILayout.LabelField("Value", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_multiValueBase, new GUIContent("Base Value"));
        EditorGUILayout.PropertyField(_multiValueMultiplier, new GUIContent("Value Multiplier"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_multiCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_multiCostMultiplier, new GUIContent("Cost Multiplier"));
    }

    private void DrawPower()
    {
        EditorGUILayout.LabelField("Value  —  value(n) = a × n^b + c", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_powerA, new GUIContent("a"));
        EditorGUILayout.PropertyField(_powerB, new GUIContent("b  (>1 accel, <1 desacel)"));
        EditorGUILayout.PropertyField(_powerC, new GUIContent("c  (offset)"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost  —  multiplicativa", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_powerCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_powerCostMultiplier, new GUIContent("Cost Multiplier"));
    }

    private void DrawExponential()
    {
        EditorGUILayout.LabelField("Value  —  value(n) = base × growth^(n-1)", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_expValueBase, new GUIContent("Base Value"));
        EditorGUILayout.PropertyField(_expValueGrowth, new GUIContent("Value Growth"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost  —  cost(n) = baseCost × costGrowth^(n-1)", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_expCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_expCostGrowth, new GUIContent("Cost Growth"));
    }

    private void DrawLogarithmic()
    {
        EditorGUILayout.LabelField("Value  —  value(n) = a × log(n + k) + c", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_logA, new GUIContent("a  (amplitud)"));
        EditorGUILayout.PropertyField(_logK, new GUIContent("k  (offset inicio, > 0)"));
        EditorGUILayout.PropertyField(_logC, new GUIContent("c  (offset base)"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost  —  multiplicativa", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_logCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_logCostMultiplier, new GUIContent("Cost Multiplier"));
    }

    private void DrawDiminishing()
    {
        EditorGUILayout.PropertyField(_drMaxValue, new GUIContent("Max Value (asíntota)"));
        EditorGUILayout.PropertyField(_drK, new GUIContent("k  (velocidad de caída)"));
        EditorGUILayout.PropertyField(_drUseHyperbolic, new GUIContent("Usar fórmula hiperbólica"));

        string formula = _drUseHyperbolic.boolValue
            ? "value(n) = (n / (n + k)) × maxValue"
            : "value(n) = maxValue × (1 - e^(-k×n))";
        EditorGUILayout.LabelField(formula, EditorStyles.miniLabel);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Cost  —  multiplicativa", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(_drCostBase, new GUIContent("Base Cost"));
        EditorGUILayout.PropertyField(_drCostMultiplier, new GUIContent("Cost Multiplier"));
    }

    #endregion

    #region Preview Table

    private void DrawPreview()
    {
        serializedObject.ApplyModifiedProperties();
        var node = (UpgradeNodeSO)target;

        _previewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_previewFoldout, "Preview por nivel");

        if (_previewFoldout)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawPreviewHeader();

            float prevTotal = 0f;

            for (int lvl = 1; lvl <= node.maxLevel; lvl++)
            {
                float total = node.GetTotalValueAtLevel(lvl);
                float increment = total - prevTotal;
                int cost = node.GetCostForLevel(lvl - 1);
                prevTotal = total;

                DrawPreviewRow(lvl, increment, total, cost);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPreviewHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Nivel", EditorStyles.boldLabel, GUILayout.Width(46));
            EditorGUILayout.LabelField("Incremento", EditorStyles.boldLabel, GUILayout.Width(82));
            EditorGUILayout.LabelField("Total", EditorStyles.boldLabel, GUILayout.Width(72));
            EditorGUILayout.LabelField("Coste (Dust)", EditorStyles.boldLabel, GUILayout.MinWidth(80));
        }

        var rect = GUILayoutUtility.GetLastRect();
        rect.y += EditorGUIUtility.singleLineHeight;
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        GUILayout.Space(2);
    }

    private void DrawPreviewRow(int level, float increment, float total, int cost)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"{level}", GUILayout.Width(46));
            EditorGUILayout.LabelField($"+{increment:F2}", GUILayout.Width(82));
            EditorGUILayout.LabelField($"{total:F2}", GUILayout.Width(72));
            EditorGUILayout.LabelField($"{cost}", GUILayout.MinWidth(80));
        }
    }

    #endregion

    #region Helpers

    private void SyncManualLevels()
    {
        int target = Mathf.Max(1, _maxLevel.intValue);
        if (_manualLevels.arraySize == target) return;

        _manualLevels.arraySize = target;
        serializedObject.ApplyModifiedProperties();
    }

    #endregion
}