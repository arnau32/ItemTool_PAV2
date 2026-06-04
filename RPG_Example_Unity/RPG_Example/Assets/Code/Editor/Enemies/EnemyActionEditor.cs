using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyAction), true)]
public class EnemyActionEditor : Editor
{
    private SerializedProperty _considerationsProp;
    private ReorderableList _list;

    private int _selectedIndex = -1;

    private Editor _selectedEditor;
    private UtilityConsideration _selectedObj;

    private static Type[] _cachedConsiderationTypes;

    private const int ExistingPickerID = 912341;
    private bool _waitingForExistingPick;

    protected virtual void OnEnable()
    {
        _considerationsProp = serializedObject.FindProperty("considerations");

        _list = new ReorderableList(serializedObject, _considerationsProp, true, true, true, true);

        _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Considerations");

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = _considerationsProp.GetArrayElementAtIndex(index);
            rect.height = EditorGUIUtility.singleLineHeight;

            // Mostramos el object field
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width - 110f, rect.height),
                element,
                GUIContent.none
            );

            // Boton Localize (solo si es un asset externo)
            var c = element.objectReferenceValue as UtilityConsideration;
            using (new EditorGUI.DisabledScope(c == null || IsLocalSubAssetOfAction(c)))
            {
                if (GUI.Button(new Rect(rect.x + rect.width - 105f, rect.y, 105f, rect.height), "Make Local"))
                {
                    MakeLocalCopy(index);
                }
            }
        };

        _list.onSelectCallback = list =>
        {
            _selectedIndex = list.index;
            RebuildSelectedEditor();
        };

        _list.onAddDropdownCallback = (rect, list) => ShowAddMenu();

        _list.onRemoveCallback = list => RemoveAt(list.index);

        CacheTypes();
        RebuildSelectedEditor();
    }

    protected virtual void OnDisable()
    {
        DestroySelectedEditor();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Dibuja todo menos la lista de considerations
        DrawPropertiesExcluding(serializedObject, "m_Script", "considerations");

        EditorGUILayout.Space(8);
        DrawConsiderationsList();

        serializedObject.ApplyModifiedProperties();
    }

    protected void DrawConsiderationsList()
    {
        _list.DoLayoutList();
        HandleExistingPicker();
        DrawSelectedConsiderationInspector();
    }

    private void ShowAddMenu()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Add Existing (Preset)"), false, StartPickExisting);

        menu.AddSeparator("");

        foreach (var t in _cachedConsiderationTypes)
        {
            menu.AddItem(new GUIContent("Create Local (SubAsset)/" + t.Name), false, () => AddLocalConsideration(t));
        }

        menu.ShowAsContext();
    }

    private void StartPickExisting()
    {
        _waitingForExistingPick = true;
        EditorGUIUtility.ShowObjectPicker<UtilityConsideration>(null, false, "", ExistingPickerID);
    }

    private void HandleExistingPicker()
    {
        if (!_waitingForExistingPick) return;

        var e = Event.current;
        if (e == null) return;

        if (e.commandName != "ObjectSelectorClosed") return;
        if (EditorGUIUtility.GetObjectPickerControlID() != ExistingPickerID) return;

        _waitingForExistingPick = false;

        var picked = EditorGUIUtility.GetObjectPickerObject() as UtilityConsideration;
        if (picked == null) return;

        AddExistingReference(picked);
    }

    private void AddExistingReference(UtilityConsideration picked)
    {
        var action = (EnemyAction)target;
        if (action.considerations == null)
            action.considerations = new System.Collections.Generic.List<UtilityConsideration>();

        Undo.RecordObject(action, "Add Existing Consideration");
        action.considerations.Add(picked);
        EditorUtility.SetDirty(action);

        serializedObject.Update();
        _selectedIndex = action.considerations.Count - 1;
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void AddLocalConsideration(Type type)
    {
        var action = (EnemyAction)target;

        var path = AssetDatabase.GetAssetPath(action);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("EnemyAction must be an asset on disk to add sub-assets.");
            return;
        }

        if (action.considerations == null)
            action.considerations = new System.Collections.Generic.List<UtilityConsideration>();

        Undo.RecordObject(action, "Create Local Consideration");

        var c = ScriptableObject.CreateInstance(type) as UtilityConsideration;
        if (c == null)
        {
            Debug.LogError($"Type {type.Name} is not a UtilityConsideration.");
            return;
        }

        c.name = $"{type.Name}_{action.considerations.Count:000}";
        Undo.RegisterCreatedObjectUndo(c, "Create Consideration");

        AssetDatabase.AddObjectToAsset(c, action);
        AssetDatabase.ImportAsset(path);

        action.considerations.Add(c);
        EditorUtility.SetDirty(action);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        serializedObject.Update();
        _selectedIndex = action.considerations.Count - 1;
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void MakeLocalCopy(int index)
    {
        var action = (EnemyAction)target;
        if (action.considerations == null) return;
        if (index < 0 || index >= action.considerations.Count) return;

        var original = action.considerations[index];
        if (original == null) return;

        // Si ya es local, no hacemos nada
        if (IsLocalSubAssetOfAction(original)) return;

        var path = AssetDatabase.GetAssetPath(action);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("EnemyAction must be an asset on disk to add sub-assets.");
            return;
        }

        Undo.RecordObject(action, "Make Local Copy");

        // Clonar y convertir en subasset
        var clone = Instantiate(original);
        clone.name = original.name + "_Local";
        Undo.RegisterCreatedObjectUndo(clone, "Clone Consideration");

        AssetDatabase.AddObjectToAsset(clone, action);
        AssetDatabase.ImportAsset(path);

        action.considerations[index] = clone;
        EditorUtility.SetDirty(action);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        serializedObject.Update();
        _selectedIndex = index;
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void RemoveAt(int index)
    {
        var action = (EnemyAction)target;
        if (action.considerations == null) return;
        if (index < 0 || index >= action.considerations.Count) return;

        Undo.RecordObject(action, "Remove Consideration");

        var c = action.considerations[index];
        action.considerations.RemoveAt(index);

        // Solo destruimos si es subasset local del action.
        // Si es un preset externo, NO se borra, solo se quita la referencia.
        if (c != null && IsLocalSubAssetOfAction(c))
        {
            Undo.DestroyObjectImmediate(c);
        }

        EditorUtility.SetDirty(action);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        serializedObject.Update();

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, action.considerations.Count - 1);
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void DrawSelectedConsiderationInspector()
    {
        var action = (EnemyAction)target;
        if (action.considerations == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= action.considerations.Count) return;

        var c = action.considerations[_selectedIndex];
        if (c == null) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Selected Consideration", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            EnsureSelectedEditor(c);
            _selectedEditor?.OnInspectorGUI();
        }
    }

    private bool IsLocalSubAssetOfAction(UtilityConsideration c)
    {
        if (c == null) return false;

        var action = (EnemyAction)target;
        var actionPath = AssetDatabase.GetAssetPath(action);
        var cPath = AssetDatabase.GetAssetPath(c);

        if (string.IsNullOrEmpty(actionPath) || string.IsNullOrEmpty(cPath)) return false;

        // Misma ruta y ademas es subasset
        return actionPath == cPath && AssetDatabase.IsSubAsset(c);
    }

    private void CacheTypes()
    {
        if (_cachedConsiderationTypes != null) return;

        _cachedConsiderationTypes =
            TypeCache.GetTypesDerivedFrom<UtilityConsideration>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && typeof(UtilityConsideration).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();
    }

    private void RebuildSelectedEditor()
    {
        var action = (EnemyAction)target;
        UtilityConsideration c = null;

        if (action.considerations != null && _selectedIndex >= 0 && _selectedIndex < action.considerations.Count)
            c = action.considerations[_selectedIndex];

        EnsureSelectedEditor(c);
    }

    private void EnsureSelectedEditor(UtilityConsideration c)
    {
        if (_selectedObj == c && _selectedEditor != null) return;

        DestroySelectedEditor();

        _selectedObj = c;
        if (_selectedObj != null)
            _selectedEditor = CreateEditor(_selectedObj);
    }

    private void DestroySelectedEditor()
    {
        if (_selectedEditor != null)
        {
            DestroyImmediate(_selectedEditor);
            _selectedEditor = null;
        }

        _selectedObj = null;
    }
}