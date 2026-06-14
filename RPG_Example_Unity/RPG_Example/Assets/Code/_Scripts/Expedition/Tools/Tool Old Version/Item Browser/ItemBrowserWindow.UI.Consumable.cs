#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ItemBrowserWindow
{
    private sealed partial class UI
    {
        private VisualElement BuildConsumableSection(SerializedObject consumableSo)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var buffsProp = consumableSo.FindProperty("buffs");

            var title = new Label("Consumable Effects");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            root.Add(title);

            if (buffsProp == null)
            {
                root.Add(new HelpBox("No se encontró la propiedad buffs en ConsumableItemData.", HelpBoxMessageType.Warning));
                return root;
            }

            root.Add(BuildBuffEffectsEditor(buffsProp));
            return root;
        }

        private VisualElement BuildBuffEffectsEditor(SerializedProperty listProp)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginTop = 8;

            var title = new Label("Buff Effects");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            container.Add(title);

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            var addBtn = new Button(() => AddBuffEffect(listProp)) { text = "+ Add" };
            addBtn.style.height = 20;

            var sortBtn = new Button(() => SortBuffEffectsByStat(listProp)) { text = "Sort" };
            sortBtn.style.height = 20;
            sortBtn.style.marginLeft = 6;

            var clearBtn = new Button(() => ClearBuffEffects(listProp)) { text = "Clear" };
            clearBtn.style.height = 20;
            clearBtn.style.marginLeft = 6;

            bar.Add(addBtn);
            bar.Add(sortBtn);
            bar.Add(clearBtn);
            container.Add(bar);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            header.Add(MakeFlexibleHeaderLabel("Mode", 6));
            header.Add(MakeFlexibleHeaderLabel("Stat", 6));
            header.Add(MakeFlexibleHeaderLabel("Modifier", 6));
            header.Add(MakeFixedHeaderLabel("Value", 90, 6));
            header.Add(MakeFixedHeaderLabel("Duration", 90, 6));
            header.Add(MakeFixedHeaderLabel("", 24, 6));

            container.Add(header);

            var listView = new ListView
            {
                reorderable = true,
                selectionType = SelectionType.None
            };

            var indices = new System.Collections.Generic.List<int>();

            void RebuildIndexSource()
            {
                indices.Clear();
                for (var i = 0; i < listProp.arraySize; i++)
                    indices.Add(i);

                listView.itemsSource = indices;
                listView.RefreshItems();
            }

            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 26;

                var modeField = new EnumField { name = "applicationMode" };
                modeField.style.flexGrow = 1;
                modeField.style.flexShrink = 1;
                modeField.style.marginRight = 6;
                row.Add(modeField);

                var statField = new EnumField { name = "statType" };
                statField.style.flexGrow = 1;
                statField.style.flexShrink = 1;
                statField.style.marginRight = 6;
                row.Add(statField);

                var modifierField = new EnumField { name = "modifierType" };
                modifierField.style.flexGrow = 1;
                modifierField.style.flexShrink = 1;
                modifierField.style.marginRight = 6;
                row.Add(modifierField);

                var valueField = new FloatField { name = "baseValue" };
                valueField.style.width = 90;
                valueField.style.minWidth = 90;
                valueField.style.marginRight = 6;
                row.Add(valueField);

                var durationField = new FloatField { name = "duration" };
                durationField.style.width = 90;
                durationField.style.minWidth = 90;
                durationField.style.marginRight = 6;
                row.Add(durationField);

                var removeBtn = new Button { name = "remove", text = "X" };
                removeBtn.style.width = 24;
                removeBtn.style.height = 18;
                row.Add(removeBtn);

                row.userData = new BuffRowBinding();
                return row;
            };

            listView.bindItem = (ve, idx) =>
            {
                var arrayIndex = indices[idx];
                var elementProp = listProp.GetArrayElementAtIndex(arrayIndex);

                var modeProp = elementProp.FindPropertyRelative("applicationMode");
                var statProp = elementProp.FindPropertyRelative("statType");
                var modifierProp = elementProp.FindPropertyRelative("modifierType");
                var valueProp = elementProp.FindPropertyRelative("baseValue");
                var durationProp = elementProp.FindPropertyRelative("duration");

                var modeField = ve.Q<EnumField>("applicationMode");
                var statField = ve.Q<EnumField>("statType");
                var modifierField = ve.Q<EnumField>("modifierType");
                var valueField = ve.Q<FloatField>("baseValue");
                var durationField = ve.Q<FloatField>("duration");
                var removeBtn = ve.Q<Button>("remove");

                modeField.Unbind();
                statField.Unbind();
                modifierField.Unbind();
                valueField.Unbind();
                durationField.Unbind();

                BindOrDisable(modeField, modeProp, "No se encontró applicationMode en BuffEffect.");
                BindOrDisable(statField, statProp, "No se encontró statType en BuffEffect.");
                BindOrDisable(modifierField, modifierProp, "No se encontró modifierType en BuffEffect.");
                BindOrDisable(valueField, valueProp, "No se encontró baseValue en BuffEffect.");
                BindOrDisable(durationField, durationProp, "No se encontró duration en BuffEffect.");

                var binding = (BuffRowBinding)ve.userData;

                if (binding.ClickHandler != null)
                    removeBtn.clicked -= binding.ClickHandler;

                binding.ArrayIndex = arrayIndex;
                binding.ClickHandler = () =>
                {
                    RemoveBuffEffectAt(listProp, binding.ArrayIndex);
                    RebuildIndexSource();
                    _w.MarkSeverityDirty();
                    _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
                };

                removeBtn.clicked += binding.ClickHandler;
            };

            listView.itemIndexChanged += (_, __) =>
            {
                _w.SelectedSo.Update();
                _w.SelectedSo.ApplyModifiedProperties();
                RebuildIndexSource();
                _w.MarkSeverityDirty();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
            };

            container.Add(listView);
            RebuildIndexSource();

            container.TrackPropertyValue(listProp, _ =>
            {
                RebuildIndexSource();
                _w.MarkSeverityDirty();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
            });

            return container;
        }

        private void AddBuffEffect(SerializedProperty listProp)
        {
            _w.SelectedSo.Update();

            var index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);

            var el = listProp.GetArrayElementAtIndex(index);
            ResetBuffEffectElement(el);

            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void RemoveBuffEffectAt(SerializedProperty listProp, int index)
        {
            if (index < 0 || index >= listProp.arraySize) return;

            _w.SelectedSo.Update();
            listProp.DeleteArrayElementAtIndex(index);
            _w.SelectedSo.ApplyModifiedProperties();
        }

        private void ClearBuffEffects(SerializedProperty listProp)
        {
            if (!EditorUtility.DisplayDialog("Clear Buff Effects",
                    "¿Seguro que quieres borrar TODOS los Buff Effects?", "Clear", "Cancel"))
                return;

            _w.SelectedSo.Update();
            listProp.ClearArray();
            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private static void ResetBuffEffectElement(SerializedProperty elementProp)
        {
            if (elementProp == null) return;

            var modeProp = elementProp.FindPropertyRelative("applicationMode");
            if (modeProp != null && modeProp.propertyType == SerializedPropertyType.Enum)
                modeProp.enumValueIndex = 0;

            var statProp = elementProp.FindPropertyRelative("statType");
            if (statProp != null && statProp.propertyType == SerializedPropertyType.Enum)
                statProp.enumValueIndex = 0;

            var modifierProp = elementProp.FindPropertyRelative("modifierType");
            if (modifierProp != null && modifierProp.propertyType == SerializedPropertyType.Enum)
                modifierProp.enumValueIndex = 0;

            var valueProp = elementProp.FindPropertyRelative("baseValue");
            if (valueProp != null && valueProp.propertyType == SerializedPropertyType.Float)
                valueProp.floatValue = 0f;

            var durationProp = elementProp.FindPropertyRelative("duration");
            if (durationProp != null && durationProp.propertyType == SerializedPropertyType.Float)
                durationProp.floatValue = 0f;
        }

        private void SortBuffEffectsByStat(SerializedProperty listProp)
        {
            _w.SelectedSo.Update();

            var n = listProp.arraySize;
            if (n <= 1) return;

            var keys = new System.Collections.Generic.List<(int idx, string key)>(n);

            for (var i = 0; i < n; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var statProp = el.FindPropertyRelative("statType");

                var k = statProp != null && statProp.propertyType == SerializedPropertyType.Enum
                    ? statProp.enumNames != null &&
                      statProp.enumValueIndex >= 0 &&
                      statProp.enumValueIndex < statProp.enumNames.Length
                        ? statProp.enumNames[statProp.enumValueIndex]
                        : statProp.enumValueIndex.ToString()
                    : i.ToString();

                keys.Add((i, k));
            }

            var order = keys.OrderBy(x => x.key, StringComparer.OrdinalIgnoreCase).Select(x => x.idx).ToList();

            for (var target = 0; target < n; target++)
            {
                var currentIndex = order[target];
                if (currentIndex == target) continue;

                listProp.MoveArrayElement(currentIndex, target);

                for (var j = target + 1; j < n; j++)
                {
                    if (order[j] == target) order[j] = currentIndex;
                    else if (order[j] > currentIndex && order[j] <= target) order[j]++;
                    else if (order[j] < currentIndex && order[j] >= target) order[j]--;
                }

                order[target] = target;
            }

            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }
    }
}
#endif