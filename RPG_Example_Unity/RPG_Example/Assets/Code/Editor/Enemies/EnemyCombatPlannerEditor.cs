using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyCombatPlanner))]
public class EnemyCombatPlannerEditor : Editor
{ 
    private SerializedProperty _configProp;
    private SerializedProperty _combatRangeProp;
    private SerializedProperty _actionsProp;

    private ReorderableList _list;

    private int _selectedIndex = -1;

    private Editor _selectedEditor;
    private EnemyAction _selectedObj;

    private static Type[] _cachedActionTypes;

    private const int ExistingPickerID = 248913;
    private bool _waitingForExistingPick;

    private void OnEnable()
    {
        _configProp = serializedObject.FindProperty("_config");
        _combatRangeProp = serializedObject.FindProperty("_combatRange");
        _actionsProp = serializedObject.FindProperty("_actions");

        _list = new ReorderableList(serializedObject, _actionsProp, true, true, true, true);

        _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Actions");

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = _actionsProp.GetArrayElementAtIndex(index);
            rect.height = EditorGUIUtility.singleLineHeight;

            float gap = 4f;
            float btnW = 64f;

            var fieldRect = new Rect(rect.x, rect.y, rect.width - (btnW + gap), rect.height);
            var cloneRect = new Rect(fieldRect.xMax + gap, rect.y, btnW, rect.height);

            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

            var a = element.objectReferenceValue as EnemyAction;
            using (new EditorGUI.DisabledScope(a == null))
            {
                if (GUI.Button(cloneRect, "Clone"))
                    CloneAsset(index);
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

    private void OnDisable()
    {
        DestroySelectedEditor();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_configProp);
        EditorGUILayout.PropertyField(_combatRangeProp);

        EditorGUILayout.Space(8);
        _list.DoLayoutList();

        HandleExistingPicker();
        DrawSelectedActionInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddMenu()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Add Existing"), false, StartPickExisting);
        menu.AddSeparator("");

        foreach (var t in _cachedActionTypes)
        {
            menu.AddItem(new GUIContent("Create New/" + t.Name), false, () => CreateNewActionAsset(t));
        }

        menu.ShowAsContext();
    }

    private void StartPickExisting()
    {
        _waitingForExistingPick = true;
        EditorGUIUtility.ShowObjectPicker<EnemyAction>(null, false, "", ExistingPickerID);
    }

    private void HandleExistingPicker()
    {
        if (!_waitingForExistingPick) return;

        var e = Event.current;
        if (e == null) return;

        if (e.commandName != "ObjectSelectorClosed") return;
        if (EditorGUIUtility.GetObjectPickerControlID() != ExistingPickerID) return;

        _waitingForExistingPick = false;

        var picked = EditorGUIUtility.GetObjectPickerObject() as EnemyAction;
        if (picked == null) return;

        AddExistingReference(picked);
    }

    private void AddExistingReference(EnemyAction picked)
    {
        Undo.RecordObject(target, "Add Enemy Action");

        serializedObject.Update();
        _actionsProp.arraySize++;
        _actionsProp.GetArrayElementAtIndex(_actionsProp.arraySize - 1).objectReferenceValue = picked;
        serializedObject.ApplyModifiedProperties();

        _selectedIndex = _actionsProp.arraySize - 1;
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void CreateNewActionAsset(Type type)
    {
        var instance = ScriptableObject.CreateInstance(type) as EnemyAction;
        if (instance == null)
        {
            Debug.LogError("Type no es EnemyAction: " + type.Name);
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Create EnemyAction",
            type.Name + ".asset",
            "asset",
            "Elige donde guardar el nuevo EnemyAction."
        );

        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(instance);
            return;
        }

        instance.name = type.Name;

        AssetDatabase.CreateAsset(instance, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddExistingReference(instance);
    }

    private void CloneAsset(int index)
    {
        if (index < 0 || index >= _actionsProp.arraySize) return;

        serializedObject.Update();
        var element = _actionsProp.GetArrayElementAtIndex(index);
        var original = element.objectReferenceValue as EnemyAction;
        serializedObject.ApplyModifiedProperties();

        if (original == null) return;

        var originalPath = AssetDatabase.GetAssetPath(original);
        if (string.IsNullOrEmpty(originalPath))
        {
            Debug.LogError("No puedo clonar: la accion no es un asset en disco.");
            return;
        }

        string newPath = EditorUtility.SaveFilePanelInProject(
            "Clone EnemyAction",
            original.name + "_Local.asset",
            "asset",
            "Elige donde guardar el clon."
        );

        if (string.IsNullOrEmpty(newPath)) return;

        if (!AssetDatabase.CopyAsset(originalPath, newPath))
        {
            Debug.LogError("CopyAsset fallo al clonar la accion.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var clone = AssetDatabase.LoadAssetAtPath<EnemyAction>(newPath);
        if (clone == null)
        {
            Debug.LogError("Se creo el clon pero no se pudo cargar desde: " + newPath);
            return;
        }

        Undo.RecordObject(target, "Clone Enemy Action");

        serializedObject.Update();
        element = _actionsProp.GetArrayElementAtIndex(index);
        element.objectReferenceValue = clone;
        serializedObject.ApplyModifiedProperties();

        _selectedIndex = index;
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= _actionsProp.arraySize) return;

        Undo.RecordObject(target, "Remove Enemy Action");

        serializedObject.Update();
        _actionsProp.DeleteArrayElementAtIndex(index);

        // Para object references, Unity a veces primero pone null y no reduce size
        if (index < _actionsProp.arraySize)
        {
            var el = _actionsProp.GetArrayElementAtIndex(index);
            if (el.objectReferenceValue == null)
                _actionsProp.DeleteArrayElementAtIndex(index);
        }

        serializedObject.ApplyModifiedProperties();

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _actionsProp.arraySize - 1);
        _list.index = _selectedIndex;
        RebuildSelectedEditor();
    }

    private void DrawSelectedActionInspector()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _actionsProp.arraySize) return;

        serializedObject.Update();
        var element = _actionsProp.GetArrayElementAtIndex(_selectedIndex);
        var a = element.objectReferenceValue as EnemyAction;
        serializedObject.ApplyModifiedProperties();

        if (a == null) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Selected Action", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            EnsureSelectedEditor(a);
            _selectedEditor?.OnInspectorGUI();
        }
    }

    private void CacheTypes()
    {
        if (_cachedActionTypes != null) return;

        _cachedActionTypes =
            TypeCache.GetTypesDerivedFrom<EnemyAction>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && typeof(EnemyAction).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();
    }

    private void RebuildSelectedEditor()
    {
        EnemyAction a = null;

        if (_selectedIndex >= 0 && _selectedIndex < _actionsProp.arraySize)
            a = _actionsProp.GetArrayElementAtIndex(_selectedIndex).objectReferenceValue as EnemyAction;

        EnsureSelectedEditor(a);
    }

    private void EnsureSelectedEditor(EnemyAction a)
    {
        if (_selectedObj == a && _selectedEditor != null) return;

        DestroySelectedEditor();

        _selectedObj = a;
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
