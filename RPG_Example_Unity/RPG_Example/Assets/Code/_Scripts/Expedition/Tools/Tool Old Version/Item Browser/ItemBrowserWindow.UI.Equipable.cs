#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ItemBrowserWindow
{
    private sealed partial class UI
    {
        private VisualElement BuildEquipableSection(SerializedObject equipSo)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var slotProp = equipSo.FindProperty("equipSlot");
            var prefabProp = equipSo.FindProperty("prefab");

            var rollModeProp = equipSo.FindProperty("rollMode");
            var rarityProp   = equipSo.FindProperty("itemRarity");

            if (rarityProp   != null) AddRow(root, new PropertyField(rarityProp,   "Item Rarity"));
            if (rollModeProp != null) AddRow(root, new PropertyField(rollModeProp, "Roll Mode"));

            root.Add(new HelpBox(
                "FixedRarityFixedStats → stats base del SO sin variación.\n" +
                "FixedRarityRandomStats → rarity fija, stats rolados dentro del rango de esa rarity.\n" +
                "RandomRarityRandomStats → rarity y stats aleatorios.",
                HelpBoxMessageType.Info));

            root.Add(BuildSectionSpacer());

            if (slotProp != null) AddRow(root, new PropertyField(slotProp, "Equip Slot"));
            if (prefabProp != null) AddRow(root, new PropertyField(prefabProp, "Prefab"));

            if (slotProp == null || prefabProp == null)
            {
                root.Add(new HelpBox("EquipableItemData debería tener equipSlot, modifiers y prefab.",
                    HelpBoxMessageType.Warning));
            }

            var modifiersProp = FindFirstArrayElementTypeContains(equipSo, "StatModifier");
            if (modifiersProp == null)
            {
                root.Add(new HelpBox(
                    "No encontré ninguna lista/array de StatModifier en este Equipable.\n" +
                    "Asegúrate de que modifiers sea List<StatModifier> y StatModifier sea [Serializable].",
                    HelpBoxMessageType.Warning));
                return root;
            }

            root.Add(BuildStatModifiersEditor(modifiersProp));
            return root;
        }

        private VisualElement BuildStatModifiersEditor(SerializedProperty listProp)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginTop = 8;

            var title = new Label("Stat Modifiers");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            container.Add(title);

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            var addBtn = new Button(() => AddStatModifier(listProp)) { text = "+ Add" };
            addBtn.style.height = 20;

            var sortBtn = new Button(() => SortStatModifiersByStatTypeAffected(listProp)) { text = "Sort" };
            sortBtn.style.height = 20;
            sortBtn.style.marginLeft = 6;

            var clearBtn = new Button(() => ClearStatModifiers(listProp)) { text = "Clear" };
            clearBtn.style.height = 20;
            clearBtn.style.marginLeft = 6;

            bar.Add(addBtn);
            bar.Add(sortBtn);
            bar.Add(clearBtn);
            container.Add(bar);

            var listView = new ListView
            {
                reorderable = true,
                selectionType = SelectionType.None
            };

            var indices = new List<int>();

            void RebuildIndexSource()
            {
                indices.Clear();
                for (var i = 0; i < listProp.arraySize; i++) indices.Add(i);
                listView.itemsSource = indices;
                listView.RefreshItems();
            }

            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 26;

                var stat = new EnumField { name = "statTypeAffected" };
                stat.style.flexGrow = 1;
                stat.style.flexShrink = 1;
                stat.style.marginRight = 6;
                row.Add(stat);

                var type = new EnumField { name = "type" };
                type.style.minWidth = 110;
                type.style.flexShrink = 1;
                type.style.marginRight = 6;
                row.Add(type);

                var value = new FloatField { name = "value" };
                value.style.minWidth = 90;
                value.style.flexShrink = 1;
                value.style.marginRight = 6;
                row.Add(value);

                var remove = new Button { name = "remove", text = "X" };
                remove.style.width = 24;
                remove.style.height = 18;
                row.Add(remove);

                row.userData = new StatRowBinding();
                return row;
            };

            listView.bindItem = (ve, idx) =>
            {
                var arrayIndex = indices[idx];
                var elementProp = listProp.GetArrayElementAtIndex(arrayIndex);

                var statProp = elementProp.FindPropertyRelative("statTypeAffected");
                var typeProp = elementProp.FindPropertyRelative("type");
                var valueProp = elementProp.FindPropertyRelative("value");

                var statField = ve.Q<EnumField>("statTypeAffected");
                var typeField = ve.Q<EnumField>("type");
                var valueField = ve.Q<FloatField>("value");
                var removeBtn = ve.Q<Button>("remove");

                statField.Unbind();
                typeField.Unbind();
                valueField.Unbind();

                if (statProp != null)
                    statField.BindProperty(statProp);
                else
                {
                    statField.SetEnabled(false);
                    statField.tooltip = "No se encontró statTypeAffected en StatModifier.";
                }

                if (typeProp != null)
                    typeField.BindProperty(typeProp);
                else
                {
                    typeField.SetEnabled(false);
                    typeField.tooltip = "No se encontró type en StatModifier.";
                }

                if (valueProp != null)
                    valueField.BindProperty(valueProp);
                else
                {
                    valueField.SetEnabled(false);
                    valueField.tooltip = "No se encontró value en StatModifier.";
                }

                var binding = (StatRowBinding)ve.userData;

                if (binding.ClickHandler != null)
                    removeBtn.clicked -= binding.ClickHandler;

                binding.ArrayIndex = arrayIndex;
                binding.ClickHandler = () =>
                {
                    RemoveStatModifierAt(listProp, binding.ArrayIndex);
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

        private void AddStatModifier(SerializedProperty listProp)
        {
            _w.SelectedSo.Update();
            var index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);

            var el = listProp.GetArrayElementAtIndex(index);
            ResetStatModifierElement(el);

            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void RemoveStatModifierAt(SerializedProperty listProp, int index)
        {
            if (index < 0 || index >= listProp.arraySize) return;

            _w.SelectedSo.Update();
            listProp.DeleteArrayElementAtIndex(index);
            _w.SelectedSo.ApplyModifiedProperties();
        }

        private void ClearStatModifiers(SerializedProperty listProp)
        {
            if (!EditorUtility.DisplayDialog("Clear Stat Modifiers",
                    "¿Seguro que quieres borrar TODOS los Stat Modifiers?", "Clear", "Cancel"))
                return;

            _w.SelectedSo.Update();
            listProp.ClearArray();
            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private static void ResetStatModifierElement(SerializedProperty elementProp)
        {
            if (elementProp == null) return;

            var statProp = elementProp.FindPropertyRelative("statTypeAffected");
            if (statProp != null && statProp.propertyType == SerializedPropertyType.Enum)
                statProp.enumValueIndex = 0;

            var typeProp = elementProp.FindPropertyRelative("type");
            if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
                typeProp.enumValueIndex = 0;

            var valueProp = elementProp.FindPropertyRelative("value");
            if (valueProp != null && valueProp.propertyType == SerializedPropertyType.Float)
                valueProp.floatValue = 0f;
        }

        private void SortStatModifiersByStatTypeAffected(SerializedProperty listProp)
        {
            _w.SelectedSo.Update();

            var n = listProp.arraySize;
            if (n <= 1) return;

            var keys = new List<(int idx, string key)>(n);
            for (var i = 0; i < n; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var statProp = el.FindPropertyRelative("statTypeAffected");

                var k = statProp != null && statProp.propertyType == SerializedPropertyType.Enum
                    ? statProp.enumNames != null && statProp.enumValueIndex >= 0 &&
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