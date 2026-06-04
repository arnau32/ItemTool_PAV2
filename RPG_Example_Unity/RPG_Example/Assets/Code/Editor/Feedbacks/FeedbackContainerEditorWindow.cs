using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeedbacksNagu
{
    public class FeedbackContainerEditorWindow : EditorWindow
    {
        private struct FeedbackTypeEntry
        {
            public Type Type;
            public string DisplayName;
            public string Category;
        }

        private class CategoryGroup
        {
            public string Name;
            public List<FeedbackTypeEntry> Entries = new();
            public bool Expanded = true;
        }

        private UnityEngine.Object _targetObject;
        private string _containerPath;
        private string _containerDisplayName;

        private SerializedObject _so;
        private SerializedProperty _containerProp;
        private SerializedProperty _feedbacksProp;

        private readonly List<CategoryGroup> _categories = new();
        private readonly List<bool> _foldouts = new();

        private Vector2 _paletteScroll;
        private Vector2 _listScroll;
        private string _search = string.Empty;

        private bool _isDragging;
        private FeedbackTypeEntry _dragEntry;

        private const float PaletteW = 220f;
        private const float Padding = 6f;
        private const float HeaderH = 22f;
        private const float ItemSpacing = 3f;
        private const float RowPad = 4f;
        private const float MoveButtonW = 20f;
        private const float DeleteW = 20f;

        private static readonly Color PanelBg = new(0.18f, 0.18f, 0.18f);
        private static readonly Color HeaderBg = new(0.13f, 0.13f, 0.13f);
        private static readonly Color ItemBg = new(0.22f, 0.22f, 0.22f);
        private static readonly Color ItemBgAlt = new(0.20f, 0.20f, 0.20f);
        private static readonly Color ItemBgHover = new(0.28f, 0.28f, 0.28f);
        private static readonly Color CategoryBg = new(0.15f, 0.15f, 0.15f);
        private static readonly Color DropZoneBg = new(0.25f, 0.40f, 0.25f, 0.5f);
        private static readonly Color DropZoneActive = new(0.30f, 0.65f, 0.30f, 0.8f);
        private static readonly Color AccentGreen = new(0.35f, 0.80f, 0.35f);
        private static readonly Color AccentRed = new(0.85f, 0.30f, 0.30f);
        private static readonly Color Separator = new(0.10f, 0.10f, 0.10f);

        public static void Open(SerializedProperty containerProperty)
        {
            var window = GetWindow<FeedbackContainerEditorWindow>("Feedback Editor");
            window.minSize = new Vector2(660f, 440f);
            window.BindFromProperty(containerProperty);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            BuildCategoryGroups();
            if (_targetObject != null && !string.IsNullOrEmpty(_containerPath))
                RebuildSerializedState();
        }

        private void OnGUI()
        {
            if (!EnsureBinding())
            {
                DrawUnbound();
                return;
            }

            DrawToolbar();

            float cy = HeaderH;
            float ch = position.height - HeaderH;

            DrawPalette(new Rect(0f, cy, PaletteW, ch));
            DrawDivider(new Rect(PaletteW, cy, 1f, ch));
            DrawList(new Rect(PaletteW + 1f, cy, position.width - PaletteW - 1f, ch));

            HandleDragEvents();
        }

        private void BindFromProperty(SerializedProperty prop)
        {
            _targetObject = prop.serializedObject.targetObject;
            _containerPath = prop.propertyPath;
            _containerDisplayName = ObjectNames.NicifyVariableName(prop.displayName);
            RebuildSerializedState();
        }

        private bool EnsureBinding()
        {
            if (_targetObject == null || string.IsNullOrEmpty(_containerPath))
                return false;

            if (_so == null || _so.targetObject != _targetObject)
            {
                RebuildSerializedState();
                if (_so == null) return false;
            }

            _so.UpdateIfRequiredOrScript();
            _containerProp = _so.FindProperty(_containerPath);
            _feedbacksProp = _containerProp?.FindPropertyRelative("feedbacks");

            if (_containerProp == null || _feedbacksProp == null)
                return false;

            SyncFoldouts();
            return true;
        }

        private void RebuildSerializedState()
        {
            if (_targetObject == null)
            {
                _so = null;
                _containerProp = null;
                _feedbacksProp = null;
                return;
            }

            _so = new SerializedObject(_targetObject);
            _containerProp = _so.FindProperty(_containerPath);
            _feedbacksProp = _containerProp?.FindPropertyRelative("feedbacks");
            SyncFoldouts();
        }

        private void SyncFoldouts()
        {
            int count = _feedbacksProp?.arraySize ?? 0;
            while (_foldouts.Count < count) _foldouts.Add(true);
            while (_foldouts.Count > count) _foldouts.RemoveAt(_foldouts.Count - 1);
        }

        private void ApplyAndMarkDirty(string undoLabel)
        {
            if (_so == null || _targetObject == null) return;

            Undo.RecordObject(_targetObject, undoLabel);
            _so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_targetObject);

            if (_targetObject is Component component)
            {
                var scene = component.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
            else if (_targetObject is GameObject go)
            {
                var scene = go.scene;
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void AddFeedback(Type type)
        {
            if (!EnsureBinding() || _feedbacksProp == null) return;

            int index = _feedbacksProp.arraySize;
            _feedbacksProp.arraySize++;
            var newElement = _feedbacksProp.GetArrayElementAtIndex(index);
            newElement.managedReferenceValue = Activator.CreateInstance(type);
            SyncFoldouts();
            if (index < _foldouts.Count) _foldouts[index] = true;
            ApplyAndMarkDirty($"Add {type.Name}");
            Repaint();
        }

        private void RemoveFeedback(int index)
        {
            if (!EnsureBinding() || _feedbacksProp == null) return;
            if (index < 0 || index >= _feedbacksProp.arraySize) return;

            _feedbacksProp.DeleteArrayElementAtIndex(index);
            SyncFoldouts();
            ApplyAndMarkDirty("Remove Feedback");
            Repaint();
        }

        private void MoveFeedback(int from, int to)
        {
            if (!EnsureBinding() || _feedbacksProp == null) return;
            if (from < 0 || from >= _feedbacksProp.arraySize) return;
            if (to < 0 || to >= _feedbacksProp.arraySize || to == from) return;

            _feedbacksProp.MoveArrayElement(from, to);

            if (from < _foldouts.Count && to < _foldouts.Count)
            {
                bool movedFoldout = _foldouts[from];
                _foldouts.RemoveAt(from);
                _foldouts.Insert(to, movedFoldout);
            }

            ApplyAndMarkDirty("Reorder Feedback");
            Repaint();
        }

        private void BuildCategoryGroups()
        {
            var entries = new List<FeedbackTypeEntry>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic) continue;
                    foreach (var type in asm.GetTypes())
                    {
                        if (type == null || type.IsAbstract || !typeof(FeedbackBase).IsAssignableFrom(type))
                            continue;

                        var categoryAttribute = type.GetCustomAttribute<FeedbackCategoryAttribute>();
                        entries.Add(new FeedbackTypeEntry
                        {
                            Type = type,
                            DisplayName = ObjectNames.NicifyVariableName(type.Name),
                            Category = categoryAttribute?.Category ?? "Other"
                        });
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (var type in ex.Types)
                    {
                        if (type == null || type.IsAbstract || !typeof(FeedbackBase).IsAssignableFrom(type))
                            continue;

                        var categoryAttribute = type.GetCustomAttribute<FeedbackCategoryAttribute>();
                        entries.Add(new FeedbackTypeEntry
                        {
                            Type = type,
                            DisplayName = ObjectNames.NicifyVariableName(type.Name),
                            Category = categoryAttribute?.Category ?? "Other"
                        });
                    }
                }
            }

            var expandedState = _categories.ToDictionary(c => c.Name, c => c.Expanded);
            _categories.Clear();

            foreach (var group in entries.GroupBy(e => e.Category).OrderBy(g => g.Key == "Other" ? "ZZZZ" : g.Key))
            {
                _categories.Add(new CategoryGroup
                {
                    Name = group.Key,
                    Entries = group.OrderBy(e => e.DisplayName).ToList(),
                    Expanded = !expandedState.TryGetValue(group.Key, out bool expanded) || expanded
                });
            }
        }

        private void DrawToolbar()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, HeaderH), HeaderBg);
            EditorGUI.LabelField(
                new Rect(Padding, 0f, position.width - 120f, HeaderH),
                $"{_targetObject?.name}  /  {_containerDisplayName}",
                EditorStyles.boldLabel);

            GUI.enabled = Application.isPlaying;
            if (GUI.Button(new Rect(position.width - 110f, 2f, 100f, HeaderH - 4f), "▶  Play All", EditorStyles.miniButton))
            {
                var gameObject = (_targetObject as Component)?.gameObject ?? _targetObject as GameObject;
                if (gameObject != null)
                {
                    object owner = _targetObject;
                    foreach (string part in _containerPath.Split('.'))
                    {
                        if (owner == null) break;
                        var field = owner.GetType().GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        owner = field?.GetValue(owner);
                    }

                    if (owner is FeedbackContainer container)
                        container.PlayFeedbacks(gameObject);
                }
            }

            GUI.enabled = true;
        }

        private void DrawPalette(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, HeaderH), HeaderBg);
            EditorGUI.LabelField(new Rect(rect.x + Padding, rect.y, rect.width - Padding, HeaderH), "Feedback Types", EditorStyles.boldLabel);

            var searchRect = new Rect(rect.x + Padding, rect.y + HeaderH + 2f, rect.width - Padding * 2f, EditorGUIUtility.singleLineHeight);
            _search = EditorGUI.TextField(searchRect, _search, EditorStyles.toolbarSearchField);

            bool searching = !string.IsNullOrEmpty(_search);
            float lineH = EditorGUIUtility.singleLineHeight;
            float top = searchRect.yMax + 4f;
            float scrollH = rect.height - (top - rect.y);
            float viewH = ComputePaletteViewHeight(searching, lineH);

            _paletteScroll = GUI.BeginScrollView(
                new Rect(rect.x, top, rect.width, scrollH),
                _paletteScroll,
                new Rect(0f, 0f, rect.width - 14f, viewH));

            float y = 2f;
            float width = rect.width - 14f;

            if (searching)
            {
                foreach (var group in _categories)
                {
                    foreach (var entry in group.Entries)
                    {
                        if (MatchesSearch(entry.DisplayName))
                            y = DrawPaletteEntry(entry, y, width, lineH, 0f);
                    }
                }
            }
            else
            {
                foreach (var group in _categories)
                {
                    y = DrawCategoryHeader(group, y, width, lineH);
                    if (!group.Expanded) continue;

                    foreach (var entry in group.Entries)
                        y = DrawPaletteEntry(entry, y, width, lineH, 10f);
                }
            }

            GUI.EndScrollView();
        }

        private float DrawCategoryHeader(CategoryGroup group, float y, float width, float lineH)
        {
            EditorGUI.DrawRect(new Rect(0f, y, width, lineH), CategoryBg);
            group.Expanded = EditorGUI.Foldout(
                new Rect(Padding, y, width - Padding, lineH),
                group.Expanded,
                $"  {group.Name}  ({group.Entries.Count})",
                true,
                EditorStyles.boldLabel);
            return y + lineH + 1f;
        }

        private float DrawPaletteEntry(FeedbackTypeEntry entry, float y, float width, float lineH, float indent)
        {
            float addWidth = 22f;
            var itemRect = new Rect(0f, y, width, lineH);
            bool hover = itemRect.Contains(Event.current.mousePosition) && !_isDragging;
            EditorGUI.DrawRect(itemRect, hover ? ItemBgHover : ItemBg);

            float nameWidth = width - indent - addWidth - Padding * 2f;
            var nameRect = new Rect(indent + Padding, y + 1f, nameWidth, lineH - 2f);
            var nameStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                fontSize = ComputeFontSizeForLabel(entry.DisplayName, nameWidth, 11, 8)
            };
            EditorGUI.LabelField(nameRect, new GUIContent(entry.DisplayName, entry.DisplayName), nameStyle);

            Color previousColor = GUI.color;
            GUI.color = AccentGreen;
            if (GUI.Button(new Rect(width - addWidth, y + 1f, addWidth - 2f, lineH - 2f), "+", EditorStyles.miniButton))
                AddFeedback(entry.Type);
            GUI.color = previousColor;

            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                _isDragging = true;
                _dragEntry = entry;
                Event.current.Use();
            }

            return y + lineH + 2f;
        }

        private static int ComputeFontSizeForLabel(string text, float availableWidth, int baseSize, int minSize)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            for (int size = baseSize; size >= minSize; size--)
            {
                style.fontSize = size;
                if (style.CalcSize(new GUIContent(text)).x <= availableWidth)
                    return size;
            }

            return minSize;
        }

        private float ComputePaletteViewHeight(bool searching, float lineH)
        {
            float height = 2f;
            if (searching)
            {
                foreach (var group in _categories)
                {
                    foreach (var entry in group.Entries)
                    {
                        if (MatchesSearch(entry.DisplayName))
                            height += lineH + 2f;
                    }
                }

                return height;
            }

            foreach (var group in _categories)
            {
                height += lineH + 1f;
                if (group.Expanded)
                    height += group.Entries.Count * (lineH + 2f);
            }

            return height;
        }

        private bool MatchesSearch(string name)
            => name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void DrawList(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);

            int count = _feedbacksProp?.arraySize ?? 0;
            float lineH = EditorGUIUtility.singleLineHeight;

            if (count == 0 && !_isDragging)
            {
                EditorGUI.LabelField(
                    new Rect(rect.x + Padding, rect.y + rect.height * 0.4f, rect.width - Padding * 2f, 40f),
                    "No feedbacks. Click [+] or drag a type from the left panel.",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
            }

            float totalHeight = ComputeListHeight(count, lineH);
            var viewRect = new Rect(0f, 0f, rect.width - 14f, totalHeight);

            _listScroll = GUI.BeginScrollView(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                _listScroll,
                viewRect);

            float y = Padding;

            for (int i = 0; i < count; i++)
            {
                SerializedProperty elementProp = _feedbacksProp.GetArrayElementAtIndex(i);
                bool unfolded = i < _foldouts.Count && _foldouts[i];
                float propertiesHeight = unfolded ? GetPropertiesHeight(elementProp) : 0f;
                float itemHeight = lineH + (unfolded ? propertiesHeight + 2f : 0f);
                float fullHeight = itemHeight + RowPad * 2f + ItemSpacing;
                var itemRect = new Rect(Padding, y, viewRect.width - Padding * 2f, itemHeight + RowPad * 2f);

                EditorGUI.DrawRect(itemRect, i % 2 == 0 ? ItemBg : ItemBgAlt);
                EditorGUI.DrawRect(new Rect(Padding, y + itemRect.height, viewRect.width - Padding * 2f, 1f), Separator);

                float headerY = itemRect.y + RowPad;

                SerializedProperty activeProp = elementProp.FindPropertyRelative("active");
                if (activeProp != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool newActive = EditorGUI.Toggle(new Rect(itemRect.x + RowPad, headerY, 14f, lineH), activeProp.boolValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        activeProp.boolValue = newActive;
                        ApplyAndMarkDirty("Toggle Feedback Active");
                    }
                }

                string typeName = GetShortTypeName(elementProp);
                float foldX = itemRect.x + RowPad + 18f;
                float foldW = itemRect.width - 18f - MoveButtonW * 2f - DeleteW - RowPad * 4f;
                bool wasOpen = unfolded;
                bool nowOpen = EditorGUI.Foldout(new Rect(foldX, headerY, foldW, lineH), wasOpen, typeName, true, EditorStyles.boldLabel);
                if (i < _foldouts.Count)
                    _foldouts[i] = nowOpen;

                GUI.enabled = i > 0;
                if (GUI.Button(new Rect(itemRect.xMax - DeleteW - MoveButtonW * 2f - 4f, headerY, MoveButtonW, lineH), "↑", EditorStyles.miniButton))
                {
                    GUI.enabled = true;
                    MoveFeedback(i, i - 1);
                    break;
                }

                GUI.enabled = i < count - 1;
                if (GUI.Button(new Rect(itemRect.xMax - DeleteW - MoveButtonW - 2f, headerY, MoveButtonW, lineH), "↓", EditorStyles.miniButton))
                {
                    GUI.enabled = true;
                    MoveFeedback(i, i + 1);
                    break;
                }

                GUI.enabled = true;

                Color oldColor = GUI.color;
                GUI.color = AccentRed;
                if (GUI.Button(new Rect(itemRect.xMax - DeleteW - RowPad, headerY, DeleteW, lineH), "✕", EditorStyles.miniButton))
                {
                    GUI.color = oldColor;
                    RemoveFeedback(i);
                    break;
                }

                GUI.color = oldColor;

                if (nowOpen)
                {
                    var fieldsRect = new Rect(itemRect.x + RowPad, headerY + lineH + 2f, itemRect.width - RowPad * 2f, propertiesHeight);
                    DrawFeedbackFields(elementProp, fieldsRect);
                }

                y += fullHeight;
            }

            float dropHeight = 28f;
            float dropY = Mathf.Max(y + 4f, viewRect.height - dropHeight - Padding);
            var dropRect = new Rect(Padding, dropY, viewRect.width - Padding * 2f, dropHeight);
            bool activeDrop = _isDragging && dropRect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(dropRect, activeDrop ? DropZoneActive : DropZoneBg);
            EditorGUI.LabelField(
                dropRect,
                _isDragging ? $"Drop  \"{_dragEntry.DisplayName}\"  here" : "← Drop feedback here",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    normal = { textColor = activeDrop ? Color.white : new Color(0.55f, 0.55f, 0.55f) }
                });

            GUI.EndScrollView();
        }

        private float ComputeListHeight(int count, float lineH)
        {
            if (_feedbacksProp == null) return 60f;

            float height = Padding;
            for (int i = 0; i < count; i++)
            {
                bool unfolded = i < _foldouts.Count && _foldouts[i];
                SerializedProperty elementProp = _feedbacksProp.GetArrayElementAtIndex(i);
                float propertiesHeight = unfolded ? GetPropertiesHeight(elementProp) : 0f;
                height += lineH + (unfolded ? propertiesHeight + 2f : 0f) + RowPad * 2f + ItemSpacing;
            }

            return height + 40f;
        }

        private float GetPropertiesHeight(SerializedProperty elementProp)
        {
            if (elementProp == null) return 2f;

            float height = 0f;
            var iterator = elementProp.Copy();
            var endProperty = elementProp.GetEndProperty();
            bool enterChildren = iterator.NextVisible(true);

            while (enterChildren && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.name != "active")
                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;

                enterChildren = iterator.NextVisible(false);
            }

            return Mathf.Max(height, 2f);
        }

        private void DrawFeedbackFields(SerializedProperty elementProp, Rect rect)
        {
            var iterator = elementProp.Copy();
            var endProperty = elementProp.GetEndProperty();
            float y = rect.y;
            bool enterChildren = iterator.NextVisible(true);

            while (enterChildren && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.name != "active")
                {
                    float height = EditorGUI.GetPropertyHeight(iterator, true);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, height), iterator, true);
                    if (EditorGUI.EndChangeCheck())
                        ApplyAndMarkDirty($"Modify {GetShortTypeName(elementProp)}");

                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }

                enterChildren = iterator.NextVisible(false);
            }
        }

        private static string GetShortTypeName(SerializedProperty elementProp)
        {
            if (elementProp != null)
            {
                string fullTypeName = elementProp.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(fullTypeName))
                {
                    int lastDot = fullTypeName.LastIndexOf('.');
                    int firstSpace = fullTypeName.IndexOf(' ');
                    string raw = lastDot >= 0
                        ? fullTypeName[(lastDot + 1)..]
                        : firstSpace >= 0
                            ? fullTypeName[(firstSpace + 1)..]
                            : fullTypeName;
                    return ObjectNames.NicifyVariableName(raw);
                }
            }

            return "Unknown";
        }

        private void HandleDragEvents()
        {
            Event ev = Event.current;
            if (!_isDragging) return;

            if (ev.type == EventType.Repaint || ev.type == EventType.MouseDrag)
            {
                var floatingRect = new Rect(ev.mousePosition.x + 14f, ev.mousePosition.y - 9f, 180f, 18f);
                EditorGUI.DrawRect(floatingRect, new Color(0.12f, 0.12f, 0.12f, 0.92f));
                EditorGUI.LabelField(floatingRect, _dragEntry.DisplayName, EditorStyles.miniLabel);
                Repaint();
            }

            if (ev.type == EventType.MouseUp)
            {
                if (ev.mousePosition.x > PaletteW && ev.mousePosition.y > HeaderH)
                    AddFeedback(_dragEntry.Type);

                _isDragging = false;
                ev.Use();
                Repaint();
            }

            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                _isDragging = false;
                ev.Use();
                Repaint();
            }
        }

        private void DrawUnbound()
        {
            EditorGUI.LabelField(
                new Rect(Padding, position.height * 0.45f, position.width - Padding * 2f, 40f),
                "No FeedbackContainer bound.\nSelect a component in the Inspector to open a container.",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
        }

        private static void DrawDivider(Rect rect)
            => EditorGUI.DrawRect(rect, Separator);
    }
}