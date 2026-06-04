using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for QuestInfoSO.
///
/// Steps are reutilizable SOs — same design as EnemyAction on EnemyCombatPlanner.
/// The list supports two workflows simultaneously:
///
///   EXTERNAL (drag & drop):
///     Drag any existing QuestStepSO asset from the Project into a slot.
///     The step lives in its own file and can be shared by multiple quests.
///     Example: "Kill5Goblins" referenced by "MainQuest" and "DailyBounty".
///
///   INLINE (create as subasset):
///     Click "+ Kill", "+ Visit", etc. to create a step embedded inside
///     this QuestInfoSO asset — convenient for steps that are quest-specific
///     and will never be reused elsewhere.
///     The step appears as a child asset in the Project window.
///
/// Both modes coexist in the same list freely.
///
/// Subasset steps are destroyed with the quest when removed from the list.
/// External steps are only dereferenced — the SO file is not touched.
///
/// Add a new step type:
///   1. Create the class (inherits QuestStepSO, has [CreateAssetMenu]).
///   2. Add a button in DrawCreateStepButtons().
///   3. Add a case in DrawInlineStepFields() to expose its unique fields.
/// </summary>
[CustomEditor(typeof(QuestInfoSO))]
public class QuestInfoSOEditor : Editor
{
    #region State

    private readonly Dictionary<int, bool>           _stepFoldouts = new();
    private readonly Dictionary<int, SerializedObject> _stepSOs    = new();

    private GUIStyle _titleStyle;
    private GUIStyle _stepBoxStyle;
    private GUIStyle _subassetLabelStyle;
    private GUIStyle _externalLabelStyle;
    private bool     _stylesReady;

    #endregion

    #region Styles

    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

        _stepBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin  = new RectOffset(0, 0, 3, 3)
        };

        // Blue tint label for steps embedded as subassets.
        _subassetLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.4f, 0.6f, 1f) }
        };

        // Green tint label for externally referenced steps.
        _externalLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.4f, 0.85f, 0.4f) }
        };

        _stylesReady = true;
    }

    #endregion

    #region Inspector Root

    public override void OnInspectorGUI()
    {
        EnsureStyles();

        var quest = (QuestInfoSO)target;
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Quest Info", _titleStyle);
        EditorGUILayout.Space(6);

        // ── General info ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("qName"),       new GUIContent("Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"),        new GUIContent("Icon"));
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"ID (asset name): {quest.id}", EditorStyles.miniLabel);

        EditorGUILayout.Space(10);

        // ── Requirements ──────────────────────────────────────────────────────
        EditorGUILayout.PropertyField(serializedObject.FindProperty("questPreequisits"),
            new GUIContent("Prerequisites"), true);

        EditorGUILayout.Space(10);

        // ── Steps ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        DrawStepList(quest);

        EditorGUILayout.Space(10);

        // ── Can Finish text ───────────────────────────────────────────────────
        EditorGUILayout.LabelField("Completion", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("canFinishText"),
            new GUIContent("Talk to NPC Text", "Shown in the HUD while the quest is waiting to be turned in."));

        EditorGUILayout.Space(10);

        // ── Rewards ───────────────────────────────────────────────────────────
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reward"),
            new GUIContent("Reward"), true);

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed) EditorUtility.SetDirty(quest);
    }

    #endregion

    #region Step List

    private void DrawStepList(QuestInfoSO quest)
    {
        var stepsProp = serializedObject.FindProperty("questSteps");

        if (stepsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "No steps yet.\nCreate an inline step with the buttons below, or drag an existing QuestStepSO here.",
                MessageType.Info);
        }
        else
        {
            int toRemove = -1;
            int toMoveUp = -1;

            for (int i = 0; i < stepsProp.arraySize; i++)
            {
                var elemProp = stepsProp.GetArrayElementAtIndex(i);
                var step     = elemProp.objectReferenceValue as QuestStepSO;

                bool isSubasset = step != null && AssetDatabase.IsSubAsset(step);

                if (!_stepFoldouts.ContainsKey(i))
                    _stepFoldouts[i] = true;

                // Tint subassets slightly blue to differentiate from external steps.
                Color prevBg = GUI.backgroundColor;
                if (isSubasset) GUI.backgroundColor = new Color(0.82f, 0.9f, 1f);

                EditorGUILayout.BeginVertical(_stepBoxStyle);
                GUI.backgroundColor = prevBg;

                // ── Step header ───────────────────────────────────────────────
                EditorGUILayout.BeginHorizontal();

                string stepLabel = step != null
                    ? $"[{i}]  {step.GetType().Name}  —  {step.name}"
                    : $"[{i}]  (empty slot)";

                _stepFoldouts[i] = EditorGUILayout.Foldout(_stepFoldouts[i], stepLabel, true);

                GUILayout.FlexibleSpace();

                // Source badge
                if (step != null)
                {
                    string badge = isSubasset ? "● inline" : "○ external";
                    GUIStyle badgeStyle = isSubasset ? _subassetLabelStyle : _externalLabelStyle;
                    EditorGUILayout.LabelField(badge, badgeStyle, GUILayout.Width(70));
                }

                // Move up button (skips index 0)
                GUI.enabled = i > 0;
                if (GUILayout.Button("↑", GUILayout.Width(24)))
                    toMoveUp = i;
                GUI.enabled = true;

                // Remove button
                Color prevC = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                    toRemove = i;
                GUI.color = prevC;

                EditorGUILayout.EndHorizontal();

                // ── Step body ─────────────────────────────────────────────────
                if (_stepFoldouts[i])
                {
                    EditorGUI.indentLevel++;

                    if (step != null)
                    {
                        // Show a Ping button for external steps so you can find the asset.
                        if (!isSubasset)
                        {
                            if (GUILayout.Button("Ping in Project", GUILayout.Width(120)))
                                EditorGUIUtility.PingObject(step);
                        }

                        EditorGUILayout.Space(2);
                        DrawInlineStepFields(step);
                    }
                    else
                    {
                        // Empty slot — show object picker so the designer can assign any step.
                        EditorGUILayout.PropertyField(elemProp, new GUIContent("Step SO"));
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            // Deferred mutations — never modify array mid-iteration.
            if (toMoveUp >= 1)
            {
                stepsProp.MoveArrayElement(toMoveUp, toMoveUp - 1);
                serializedObject.ApplyModifiedProperties();
            }

            if (toRemove >= 0)
                RemoveStep(quest, stepsProp, toRemove);
        }

        EditorGUILayout.Space(4);

        // ── External reference slot ───────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Add existing:", GUILayout.Width(80));

        var dropped = EditorGUILayout.ObjectField(
            null, typeof(QuestStepSO), false, GUILayout.ExpandWidth(true)) as QuestStepSO;

        if (dropped != null)
        {
            stepsProp.arraySize++;
            stepsProp.GetArrayElementAtIndex(stepsProp.arraySize - 1).objectReferenceValue = dropped;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(quest);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        DrawCreateStepButtons(quest, stepsProp);
    }

    #endregion

    #region Inline Field Rendering

    /// <summary>
    /// Renders the key fields of each step type inline.
    /// Add a new case here for every new QuestStepSO subclass.
    /// objectiveText is always shown as it belongs to the base class.
    /// </summary>
    private void DrawInlineStepFields(QuestStepSO step)
    {
        var so = GetOrCreateStepSO(step);
        so.Update();

        EditorGUI.BeginChangeCheck();

        // Base field — always shown.
        EditorGUILayout.PropertyField(so.FindProperty("objectiveText"), new GUIContent("Objective Text"));

        EditorGUILayout.Space(2);

        switch (step)
        {
            case KillEnemyStepSO kill:
                EditorGUILayout.PropertyField(so.FindProperty("targetEnemy"),  new GUIContent("Enemy"));
                EditorGUILayout.PropertyField(so.FindProperty("requiredKills"), new GUIContent("Required Kills"));
                break;

            case VisitZoneStepSO visit:
                EditorGUILayout.PropertyField(so.FindProperty("zoneId"), new GUIContent("Zone ID"));
                break;

            case CollectItemStepSO collect:
                EditorGUILayout.PropertyField(so.FindProperty("targetItem"),       new GUIContent("Item"));
                EditorGUILayout.PropertyField(so.FindProperty("requiredQuantity"), new GUIContent("Quantity"));
                break;

            case DeliverItemStepSO deliver:
                EditorGUILayout.PropertyField(so.FindProperty("requiredItem"),      new GUIContent("Item"));
                EditorGUILayout.PropertyField(so.FindProperty("requiredQuantity"),  new GUIContent("Quantity"));
                EditorGUILayout.PropertyField(so.FindProperty("consumeOnDelivery"), new GUIContent("Consume on Delivery"));
                EditorGUILayout.PropertyField(so.FindProperty("deliveryZoneId"),    new GUIContent("Delivery Zone ID"));
                break;

            default:
                // Unknown step type — fall back to full default inspector for that SO.
                DrawDefaultInspectorForObject(step);
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(step);
        }
    }

    private SerializedObject GetOrCreateStepSO(QuestStepSO step)
    {
        int id = step.GetInstanceID();
        if (!_stepSOs.TryGetValue(id, out var so) || so == null)
        {
            so = new SerializedObject(step);
            _stepSOs[id] = so;
        }
        return so;
    }

    /// <summary>
    /// Fallback for unknown step types: renders all serialized fields.
    /// Avoids losing data when a new step type is added before DrawInlineStepFields is updated.
    /// </summary>
    private void DrawDefaultInspectorForObject(Object obj)
    {
        var so = obj is QuestStepSO step ? GetOrCreateStepSO(step) : new SerializedObject(obj);
        so.Update();

        var prop = so.GetIterator();
        prop.NextVisible(true); // skip "m_Script"

        EditorGUI.BeginChangeCheck();

        while (prop.NextVisible(false))
            EditorGUILayout.PropertyField(prop, true);

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(obj);
        }
    }

    #endregion

    #region Create Step Buttons

    private void DrawCreateStepButtons(QuestInfoSO quest, SerializedProperty stepsProp)
    {
        EditorGUILayout.LabelField("Create inline step:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Kill",    GUILayout.Width(70)))  CreateInlineStep<KillEnemyStepSO>(quest,  stepsProp);
        if (GUILayout.Button("+ Visit",   GUILayout.Width(70)))  CreateInlineStep<VisitZoneStepSO>(quest,  stepsProp);
        if (GUILayout.Button("+ Collect", GUILayout.Width(70)))  CreateInlineStep<CollectItemStepSO>(quest, stepsProp);
        if (GUILayout.Button("+ Deliver", GUILayout.Width(70)))  CreateInlineStep<DeliverItemStepSO>(quest, stepsProp);

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Creates a QuestStepSO as a subasset of this QuestInfoSO and appends it to the list.
    /// The step is named QuestId_StepType_N so it is identifiable in the Project window.
    /// </summary>
    private void CreateInlineStep<T>(QuestInfoSO quest, SerializedProperty stepsProp)
        where T : QuestStepSO
    {
        string assetPath = AssetDatabase.GetAssetPath(quest);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("[QuestInfoSOEditor] Cannot create inline step — quest asset path is empty.");
            return;
        }

        var step  = CreateInstance<T>();
        step.name = $"{quest.id}_{typeof(T).Name}_{stepsProp.arraySize}";

        Undo.RecordObject(quest, $"Add {typeof(T).Name}");
        AssetDatabase.AddObjectToAsset(step, assetPath);
        Undo.RegisterCreatedObjectUndo(step, $"Add {typeof(T).Name}");

        stepsProp.arraySize++;
        stepsProp.GetArrayElementAtIndex(stepsProp.arraySize - 1).objectReferenceValue = step;
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(quest);
        // Defer SaveAssets outside the current GUI pass — calling it mid-frame
        // invalidates the serializedObject and breaks the inspector layout.
        EditorApplication.delayCall += AssetDatabase.SaveAssets;
        GUIUtility.ExitGUI();
    }

    #endregion

    #region Remove Step

    /// <summary>
    /// Removes a step from the list.
    /// If the step is a subasset of this quest, it is also destroyed.
    /// If it is an external SO, only the reference is cleared — the file is untouched.
    /// </summary>
    private void RemoveStep(QuestInfoSO quest, SerializedProperty stepsProp, int index)
    {
        var elemProp = stepsProp.GetArrayElementAtIndex(index);
        var step     = elemProp.objectReferenceValue as QuestStepSO;

        if (step != null && AssetDatabase.IsSubAsset(step))
        {
            // Inline step — destroy the subasset.
            Undo.RecordObject(quest, "Remove Inline Step");
            Undo.DestroyObjectImmediate(step);
        }

        // Clear reference before deleting the array slot (Unity requirement for object refs).
        elemProp.objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();

        stepsProp.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(quest);
        EditorApplication.delayCall += AssetDatabase.SaveAssets;
        GUIUtility.ExitGUI();
    }

    #endregion
}
