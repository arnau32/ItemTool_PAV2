#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace ToolUI
{
    /// <summary>
    /// Editor reutilizable para listas de LootTable.Entry.
    /// Replica el comportamiento de la LootTable tool original:
    /// rows manuales, chance chips, override de rarity para equipables,
    /// preview inline de nested loot tables y refresco dinámico.
    /// </summary>
    public class ToolLootTableEntriesEditor : ToolUIElement
    {
        private sealed class EntryRowData
        {
            public Action CleanupEntryTypeCallback;
            public Action CleanupItemCallback;
            public Action CleanupOverrideCallback;
            public Action CleanupWeightCallback;
            public Action CleanupDoubleClickCallback;
            public Action CleanupRemoveCallback;
        }
        
        private sealed class EntrySortData
        {
            public int EntryType;
            public UnityEngine.Object Item;
            public UnityEngine.Object NestedTable;
            public int Weight;
            public int MinQuantity;
            public int MaxQuantity;
            public int EquipableOverrideMode;
            public int OverrideFixedRarity;
        }

        // Navigation
        private readonly Action<UnityEngine.Object> onOpenTarget;
        
        private readonly SerializedObject serializedObject;
        private readonly SerializedProperty listProperty;
        private readonly bool weightedMode;
        private readonly string addButtonText;
        private readonly string clearTitle;
        private readonly string clearQuestion;
        private readonly Action onChanged;

        private VisualElement rowsContainer;
        private int lastCount = -1;
        
        private readonly bool isWeighted;

        public ToolLootTableEntriesEditor(
            SerializedObject serializedObject,
            SerializedProperty listProperty,
            bool weightedMode,
            string addButtonText,
            string clearTitle,
            string clearQuestion,
            Action onChanged = null,
            Action<UnityEngine.Object> onOpenTarget = null)
        {
            this.serializedObject = serializedObject;
            this.listProperty = listProperty;
            this.weightedMode = weightedMode;
            this.addButtonText = addButtonText;
            this.clearTitle = clearTitle;
            this.clearQuestion = clearQuestion;
            this.onChanged = onChanged;
            this.onOpenTarget = onOpenTarget;

            Initialize();
        }

        protected override void Build()
        {
            style.flexDirection = FlexDirection.Column;
            style.marginTop = ToolUISizes.LargeGap;

            if (serializedObject == null || listProperty == null || !listProperty.isArray)
            {
                Add(new HelpBox("Loot entries inválidas o no encontradas.", HelpBoxMessageType.Warning));
                return;
            }

            BuildToolbar();
            BuildHeader();
            BuildRowsContainer();

            this.TrackPropertyValue(listProperty, _ =>
            {
                if (listProperty.arraySize != lastCount)
                    RebuildRows();
                else
                    RefreshAllChanceChips();

                NotifyChanged();
            });

            RebuildRows();
        }

        private void BuildToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = ToolUISizes.Gap;

            var addBtn = new Button(AddEntry) { text = addButtonText };
            addBtn.style.height = ToolUISizes.SmallButtonHeight;

            var sortBtn = new Button(SortEntries)
            {
                text = weightedMode ? "Sort Weight" : "Sort Name"
            };

            sortBtn.style.height = ToolUISizes.SmallButtonHeight;
            sortBtn.style.marginLeft = ToolUISizes.Gap;

            var clearBtn = new Button(ClearEntries) { text = "Clear" };
            clearBtn.style.height = ToolUISizes.SmallButtonHeight;
            clearBtn.style.marginLeft = ToolUISizes.Gap;

            bar.Add(addBtn);
            bar.Add(sortBtn);
            bar.Add(clearBtn);

            Add(bar);
        }
        

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = ToolUISizes.SmallGap;

            header.Add(MakeFixedHeaderLabel("", 24, 6));
            header.Add(MakeFixedHeaderLabel("Type", 90, 6));
            header.Add(MakeFlexibleHeaderLabel("Item / Table", 6));

            if (weightedMode)
                header.Add(MakeFixedHeaderLabel("Weight", 72, 6));

            header.Add(MakeFixedHeaderLabel("Min", 64, 6));
            header.Add(MakeFixedHeaderLabel("Max", 64, 6));
            header.Add(MakeFixedHeaderLabel("Chance", 72, 6));
            header.Add(MakeFixedHeaderLabel("", 24, 6));

            Add(header);
        }

        private void BuildRowsContainer()
        {
            var borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.13f, 0.13f)
                : new Color(0.60f, 0.60f, 0.60f);

            rowsContainer = new VisualElement();
            rowsContainer.style.flexDirection = FlexDirection.Column;

            rowsContainer.style.borderLeftWidth = 1f;
            rowsContainer.style.borderRightWidth = 1f;
            rowsContainer.style.borderTopWidth = 1f;
            rowsContainer.style.borderBottomWidth = 1f;

            rowsContainer.style.borderLeftColor = borderColor;
            rowsContainer.style.borderRightColor = borderColor;
            rowsContainer.style.borderTopColor = borderColor;
            rowsContainer.style.borderBottomColor = borderColor;

            Add(rowsContainer);
        }

        private VisualElement MakeRow()
        {
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.userData = new EntryRowData();

            var mainRow = new VisualElement();
            mainRow.name = "main-row";
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.minHeight = 26;

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 20;
            icon.style.height = 20;
            icon.style.minWidth = 20;
            icon.style.marginRight = 6;
            mainRow.Add(icon);

            var entryTypeField = new EnumField(Enums.LootEntryType.Item);
            entryTypeField.name = "entryType";
            entryTypeField.style.width = 90;
            entryTypeField.style.minWidth = 90;
            entryTypeField.style.marginRight = 6;
            mainRow.Add(entryTypeField);

            var itemField = new ObjectField
            {
                name = "item",
                objectType = typeof(ItemData),
                allowSceneObjects = false
            };
            itemField.style.flexGrow = 1;
            itemField.style.flexShrink = 1;
            itemField.style.marginRight = 6;
            mainRow.Add(itemField);

            var nestedTableField = new ObjectField
            {
                name = "nestedTable",
                objectType = typeof(LootTable),
                allowSceneObjects = false
            };
            nestedTableField.style.flexGrow = 1;
            nestedTableField.style.flexShrink = 1;
            nestedTableField.style.marginRight = 6;
            nestedTableField.style.display = DisplayStyle.None;
            mainRow.Add(nestedTableField);

            if (weightedMode)
            {
                var weightField = new IntegerField { name = "weight" };
                weightField.style.width = 72;
                weightField.style.minWidth = 72;
                weightField.style.marginRight = 6;
                mainRow.Add(weightField);
            }

            var minField = new IntegerField { name = "minQuantity" };
            minField.style.width = 64;
            minField.style.minWidth = 64;
            minField.style.marginRight = 6;
            mainRow.Add(minField);

            var maxField = new IntegerField { name = "maxQuantity" };
            maxField.style.width = 64;
            maxField.style.minWidth = 64;
            maxField.style.marginRight = 6;
            mainRow.Add(maxField);

            var chanceChip = new Label { name = "chance" };
            chanceChip.style.width = 72;
            chanceChip.style.minWidth = 72;
            chanceChip.style.marginRight = 6;
            ApplyChanceChipStyle(chanceChip);
            mainRow.Add(chanceChip);

            var removeBtn = new Button { name = "remove", text = "X" };
            removeBtn.style.width = 24;
            removeBtn.style.height = 18;
            mainRow.Add(removeBtn);

            wrapper.Add(mainRow);

            var overrideRow = new VisualElement();
            overrideRow.name = "override-row";
            overrideRow.style.flexDirection = FlexDirection.Row;
            overrideRow.style.alignItems = Align.Center;
            overrideRow.style.minHeight = 22;
            overrideRow.style.marginLeft = 26;
            overrideRow.style.marginBottom = 2;
            overrideRow.style.display = DisplayStyle.None;

            var overrideLabel = new Label("Rarity Override:");
            overrideLabel.style.minWidth = 100;
            overrideLabel.style.marginRight = 4;
            overrideRow.Add(overrideLabel);

            var overrideModeField = new EnumField(Enums.LootEntryEquipableOverrideMode.None);
            overrideModeField.name = "equipableOverrideMode";
            overrideModeField.style.width = 200;
            overrideModeField.style.marginRight = 6;
            overrideRow.Add(overrideModeField);

            var fixedRarityLabel = new Label("Fixed:");
            fixedRarityLabel.name = "fixedRarityLabel";
            fixedRarityLabel.style.minWidth = 36;
            fixedRarityLabel.style.marginRight = 4;
            fixedRarityLabel.style.display = DisplayStyle.None;
            overrideRow.Add(fixedRarityLabel);

            var fixedRarityField = new EnumField(Enums.ItemRarity.Common);
            fixedRarityField.name = "overrideFixedRarity";
            fixedRarityField.style.width = 100;
            fixedRarityField.style.display = DisplayStyle.None;
            overrideRow.Add(fixedRarityField);

            wrapper.Add(overrideRow);

            var nestedDetails = new Foldout
            {
                name = "nested-details",
                text = "Entries",
                value = false
            };

            nestedDetails.style.marginLeft = 26;
            nestedDetails.style.marginTop = 4;
            nestedDetails.style.marginBottom = 6;
            nestedDetails.style.display = DisplayStyle.None;

            wrapper.Add(nestedDetails);

            return wrapper;
        }

        private void BindRow(VisualElement ve, int arrayIndex)
        {
            var rowData = (EntryRowData)ve.userData;
            var elementProp = listProperty.GetArrayElementAtIndex(arrayIndex);

            var entryTypeProp = elementProp.FindPropertyRelative("entryType");
            var itemProp = elementProp.FindPropertyRelative("item");
            var nestedTableProp = elementProp.FindPropertyRelative("nestedTable");
            var minProp = elementProp.FindPropertyRelative("minQuantity");
            var maxProp = elementProp.FindPropertyRelative("maxQuantity");
            var weightProp = weightedMode ? elementProp.FindPropertyRelative("weight") : null;
            var overrideModeProp = elementProp.FindPropertyRelative("equipableOverrideMode");
            var fixedRarityProp = elementProp.FindPropertyRelative("overrideFixedRarity");

            var icon = ve.Q<Image>("icon");
            var entryTypeField = ve.Q<EnumField>("entryType");
            var itemField = ve.Q<ObjectField>("item");
            
            var mainRow = ve.Q<VisualElement>("main-row");
            
            var nestedTableField = ve.Q<ObjectField>("nestedTable");
            var minField = ve.Q<IntegerField>("minQuantity");
            var maxField = ve.Q<IntegerField>("maxQuantity");
            var weightField = weightedMode ? ve.Q<IntegerField>("weight") : null;
            var removeBtn = ve.Q<Button>("remove");
            var overrideRow = ve.Q<VisualElement>("override-row");
            var overrideModeField = ve.Q<EnumField>("equipableOverrideMode");
            var fixedRarityField = ve.Q<EnumField>("overrideFixedRarity");
            var fixedRarityLabel = ve.Q<Label>("fixedRarityLabel");
            var nestedDetails = ve.Q<Foldout>("nested-details");
            var chanceChip = ve.Q<Label>("chance");

            void SyncFieldsFromSerializedProperties()
            {
                if (entryTypeProp != null)
                    entryTypeField.SetValueWithoutNotify((Enums.LootEntryType)entryTypeProp.enumValueIndex);

                if (itemProp != null)
                    itemField.SetValueWithoutNotify(itemProp.objectReferenceValue);

                if (nestedTableProp != null)
                    nestedTableField.SetValueWithoutNotify(nestedTableProp.objectReferenceValue);

                if (minProp != null)
                    minField.SetValueWithoutNotify(minProp.intValue);

                if (maxProp != null)
                    maxField.SetValueWithoutNotify(maxProp.intValue);

                if (weightProp != null && weightField != null)
                    weightField.SetValueWithoutNotify(weightProp.intValue);

                if (overrideModeProp != null)
                    overrideModeField.SetValueWithoutNotify((Enums.LootEntryEquipableOverrideMode)overrideModeProp.enumValueIndex);

                if (fixedRarityProp != null)
                    fixedRarityField.SetValueWithoutNotify((Enums.ItemRarity)fixedRarityProp.enumValueIndex);
            }

            void UpdateIcon()
            {
                if (entryTypeProp == null || icon == null)
                    return;

                bool isItem = entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;

                if (isItem)
                {
                    var item = itemProp?.objectReferenceValue as ItemData;
                    icon.image = GetItemIcon(item)?.texture;
                }
                else
                {
                    var table = nestedTableProp?.objectReferenceValue as LootTable;
                    icon.image = table != null
                        ? EditorGUIUtility.IconContent("ScriptableObject Icon").image
                        : null;
                }

                icon.style.opacity = icon.image != null ? 1f : 0.15f;
            }

            void UpdateOverrideVisibility()
            {
                if (entryTypeProp == null)
                    return;

                bool isItem = entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;
                var currentItem = isItem ? itemProp?.objectReferenceValue as ItemData : null;
                bool isEquipable = currentItem is EquipableItemData;

                overrideRow.style.display = isEquipable ? DisplayStyle.Flex : DisplayStyle.None;

                if (!isEquipable || overrideModeProp == null)
                    return;

                var mode = (Enums.LootEntryEquipableOverrideMode)overrideModeProp.enumValueIndex;
                bool showFixed = mode.ToString().Contains("Fixed");
                
                fixedRarityLabel.style.display = showFixed ? DisplayStyle.Flex : DisplayStyle.None;
                fixedRarityField.style.display = showFixed ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void UpdateEntryTypeVisibility()
            {
                if (entryTypeProp == null)
                    return;

                bool isItem = entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;

                itemField.style.display = isItem ? DisplayStyle.Flex : DisplayStyle.None;
                nestedTableField.style.display = isItem ? DisplayStyle.None : DisplayStyle.Flex;

                nestedDetails.style.display = isItem ? DisplayStyle.None : DisplayStyle.Flex;
                nestedDetails.Clear();

                if (!isItem && nestedTableProp?.objectReferenceValue is LootTable nestedTable)
                    nestedDetails.Add(BuildNestedLootTableInlineEditor(nestedTable));

                UpdateIcon();
                UpdateOverrideVisibility();
            }

            void UpdateChanceChip()
            {
                if (chanceChip == null)
                    return;

                if (!weightedMode || weightProp == null)
                {
                    chanceChip.text = "—";
                    chanceChip.tooltip = "Guaranteed entry";
                    return;
                }

                int totalWeight = GetSerializedWeightTotal(listProperty);
                int weight = Mathf.Max(0, weightProp.intValue);

                if (totalWeight <= 0 || weight <= 0)
                {
                    chanceChip.text = "—";
                    chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
                    return;
                }

                float pct = 100f * weight / totalWeight;
                chanceChip.text = $"{pct:F1}%";
                chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
            }

            SyncFieldsFromSerializedProperties();

            entryTypeField.Unbind();
            itemField.Unbind();
            nestedTableField.Unbind();
            minField.Unbind();
            maxField.Unbind();
            weightField?.Unbind();
            overrideModeField.Unbind();
            fixedRarityField.Unbind();

            if (entryTypeProp != null) entryTypeField.BindProperty(entryTypeProp);
            if (itemProp != null) itemField.BindProperty(itemProp);
            if (nestedTableProp != null) nestedTableField.BindProperty(nestedTableProp);
            if (minProp != null) minField.BindProperty(minProp);
            if (maxProp != null) maxField.BindProperty(maxProp);
            if (weightProp != null && weightField != null) weightField.BindProperty(weightProp);
            if (overrideModeProp != null) overrideModeField.BindProperty(overrideModeProp);
            if (fixedRarityProp != null) fixedRarityField.BindProperty(fixedRarityProp);

            EventCallback<ChangeEvent<Enum>> entryTypeCb = _ =>
            {
                UpdateEntryTypeVisibility();
                NotifyChanged();
            };

            entryTypeField.RegisterValueChangedCallback(entryTypeCb);
            rowData.CleanupEntryTypeCallback = () => entryTypeField.UnregisterValueChangedCallback(entryTypeCb);

            EventCallback<ChangeEvent<UnityEngine.Object>> itemCb = _ =>
            {
                UpdateIcon();
                UpdateOverrideVisibility();
                NotifyChanged();
            };

            itemField.RegisterValueChangedCallback(itemCb);
            nestedTableField.RegisterValueChangedCallback(itemCb);
            rowData.CleanupItemCallback = () =>
            {
                itemField.UnregisterValueChangedCallback(itemCb);
                nestedTableField.UnregisterValueChangedCallback(itemCb);
            };

            EventCallback<ChangeEvent<Enum>> overrideCb = _ =>
            {
                UpdateOverrideVisibility();
                NotifyChanged();
            };

            overrideModeField.RegisterValueChangedCallback(overrideCb);
            rowData.CleanupOverrideCallback = () => overrideModeField.UnregisterValueChangedCallback(overrideCb);

            if (weightedMode && weightField != null)
            {
                EventCallback<ChangeEvent<int>> weightCb = _ =>
                {
                    RefreshAllChanceChips();
                    NotifyChanged();
                };

                weightField.RegisterValueChangedCallback(weightCb);
                rowData.CleanupWeightCallback = () => weightField.UnregisterValueChangedCallback(weightCb);
            }

            EventCallback<MouseDownEvent> doubleClickCb = evt =>
            {
                if (evt.button != 0 || evt.clickCount != 2)
                    return;

                bool isItem = entryTypeProp == null ||
                              entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;

                UnityEngine.Object target = isItem
                    ? itemProp?.objectReferenceValue
                    : nestedTableProp?.objectReferenceValue;

                if (target == null)
                    return;

                onOpenTarget?.Invoke(target);

                evt.StopPropagation();
            };
            
            if (mainRow != null)
            {
                mainRow.RegisterCallback(doubleClickCb);

                rowData.CleanupDoubleClickCallback = () =>
                {
                    mainRow.UnregisterCallback(doubleClickCb);
                };
            }

            Action removeHandler = () => RemoveEntryAt(arrayIndex);

            removeBtn.clicked += removeHandler;

            rowData.CleanupRemoveCallback = () =>
            {
                removeBtn.clicked -= removeHandler;
            };
            UpdateEntryTypeVisibility();
            UpdateChanceChip();
        }

        private void RebuildRows()
        {
            foreach (var child in rowsContainer.Children().ToList())
                UnbindRow(child);

            rowsContainer.Clear();

            for (int i = 0; i < listProperty.arraySize; i++)
            {
                var row = MakeRow();
                BindRow(row, i);
                rowsContainer.Add(row);
            }

            lastCount = listProperty.arraySize;
        }

        private void UnbindRow(VisualElement ve)
        {
            var rowData = ve.userData as EntryRowData;
            if (rowData == null)
                return;

            rowData.CleanupEntryTypeCallback?.Invoke();
            rowData.CleanupItemCallback?.Invoke();
            rowData.CleanupOverrideCallback?.Invoke();
            rowData.CleanupWeightCallback?.Invoke();
            rowData.CleanupDoubleClickCallback?.Invoke();

            rowData.CleanupDoubleClickCallback = null;
            rowData.CleanupEntryTypeCallback = null;
            rowData.CleanupItemCallback = null;
            rowData.CleanupOverrideCallback = null;
            rowData.CleanupWeightCallback = null;

            ve.Q<EnumField>("entryType")?.Unbind();
            ve.Q<ObjectField>("item")?.Unbind();
            ve.Q<ObjectField>("nestedTable")?.Unbind();
            ve.Q<IntegerField>("minQuantity")?.Unbind();
            ve.Q<IntegerField>("maxQuantity")?.Unbind();
            ve.Q<IntegerField>("weight")?.Unbind();
            ve.Q<EnumField>("equipableOverrideMode")?.Unbind();
            ve.Q<EnumField>("overrideFixedRarity")?.Unbind();
        }

        private void RefreshAllChanceChips()
        {
            int i = 0;

            foreach (var child in rowsContainer.Children())
            {
                if (i >= listProperty.arraySize)
                    break;

                var elementProp = listProperty.GetArrayElementAtIndex(i);
                i++;

                var weightProp = weightedMode ? elementProp.FindPropertyRelative("weight") : null;
                var chanceChip = child.Q<Label>("chance");

                if (chanceChip == null)
                    continue;

                if (!weightedMode || weightProp == null)
                {
                    chanceChip.text = "—";
                    chanceChip.tooltip = "Guaranteed entry";
                    continue;
                }

                int totalWeight = GetSerializedWeightTotal(listProperty);
                int weight = Mathf.Max(0, weightProp.intValue);

                if (totalWeight <= 0 || weight <= 0)
                {
                    chanceChip.text = "—";
                    chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
                    continue;
                }

                float pct = 100f * weight / totalWeight;
                chanceChip.text = $"{pct:F1}%";
                chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
            }
        }

        private void AddEntry()
        {
            serializedObject.Update();

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);

            var entry = listProperty.GetArrayElementAtIndex(index);

            SetEnum(entry, "entryType", 0);
            SetObject(entry, "item", null);
            SetObject(entry, "nestedTable", null);
            SetInt(entry, "weight", 1);
            SetInt(entry, "minQuantity", 1);
            SetInt(entry, "maxQuantity", 1);
            SetEnum(entry, "equipableOverrideMode", 0);
            SetEnum(entry, "overrideFixedRarity", 0);

            serializedObject.ApplyModifiedProperties();

            RebuildRows();
            NotifyChanged();
        }

        private void RemoveEntryAt(int index)
        {
            if (index < 0 || index >= listProperty.arraySize)
                return;

            serializedObject.Update();
            listProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();

            RebuildRows();
            NotifyChanged();
        }

        private void ClearEntries()
        {
            if (!EditorUtility.DisplayDialog(clearTitle, clearQuestion, "Clear", "Cancel"))
                return;

            serializedObject.Update();
            listProperty.ClearArray();
            serializedObject.ApplyModifiedProperties();

            RebuildRows();
            NotifyChanged();
        }

        private VisualElement BuildNestedLootTableInlineEditor(LootTable table)
        {
            var root = new ToolCard();
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;

            if (table == null)
                return root;

            if (table.guaranteedEntries != null)
            {
                foreach (var entry in table.guaranteedEntries)
                    root.Add(BuildNestedEntryPreviewRow(entry, hasWeight: false, totalWeight: 0));
            }

            if (table.weightedEntries != null)
            {
                int totalWeight = GetEntryWeightTotal(table.weightedEntries);

                foreach (var entry in table.weightedEntries)
                    root.Add(BuildNestedEntryPreviewRow(entry, hasWeight: true, totalWeight: totalWeight));
            }

            return root;
        }

        private VisualElement BuildNestedEntryPreviewRow(LootTable.Entry entry, bool hasWeight, int totalWeight)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.marginBottom = 2;

            var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 20;
            icon.style.height = 20;
            icon.style.minWidth = 20;
            icon.style.marginRight = 6;

            Texture iconTexture = null;
            string displayName = "<null>";
            string typeText = "Unknown";

            if (entry != null && entry.entryType == Enums.LootEntryType.Item)
            {
                var item = entry.item;
                var sprite = GetItemIcon(item);
                iconTexture = sprite != null ? sprite.texture : null;
                displayName = item != null ? item.name : "<null item>";
                typeText = "Item";
            }
            else if (entry != null && entry.entryType == Enums.LootEntryType.LootTable)
            {
                iconTexture = EditorGUIUtility.IconContent("ScriptableObject Icon").image;
                displayName = entry.nestedTable != null ? entry.nestedTable.name : "<null loot table>";
                typeText = "LootTable";
            }

            icon.image = iconTexture;
            icon.style.opacity = iconTexture != null ? 1f : 0.18f;
            row.Add(icon);

            var nameLabel = new Label(displayName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.marginRight = 6;
            row.Add(nameLabel);

            var typeLabel = new Label(typeText);
            typeLabel.style.width = 72;
            typeLabel.style.minWidth = 72;
            typeLabel.style.opacity = 0.65f;
            typeLabel.style.marginRight = 6;
            row.Add(typeLabel);

            var weightText = hasWeight && entry != null ? entry.weight.ToString() : "-";
            var weightLabel = new Label(weightText);
            weightLabel.style.width = 52;
            weightLabel.style.minWidth = 52;
            weightLabel.style.opacity = 0.75f;
            weightLabel.style.marginRight = 6;
            row.Add(weightLabel);

            int min = entry != null ? entry.minQuantity : 0;
            int max = entry != null ? entry.maxQuantity : 0;

            var minMaxLabel = new Label($"{min}-{max}");
            minMaxLabel.style.width = 56;
            minMaxLabel.style.minWidth = 56;
            minMaxLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            minMaxLabel.style.opacity = 0.75f;
            minMaxLabel.style.marginRight = 6;
            row.Add(minMaxLabel);

            var chanceChip = new Label();
            chanceChip.style.width = 72;
            chanceChip.style.minWidth = 72;
            ApplyChanceChipStyle(chanceChip);

            if (!hasWeight || entry == null)
            {
                chanceChip.text = "—";
                chanceChip.tooltip = "Guaranteed entry";
            }
            else
            {
                int weight = Mathf.Max(0, entry.weight);

                if (totalWeight <= 0 || weight <= 0)
                {
                    chanceChip.text = "—";
                    chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
                }
                else
                {
                    float pct = 100f * weight / totalWeight;
                    chanceChip.text = $"{pct:F1}%";
                    chanceChip.tooltip = $"Weight {weight} / {totalWeight}";
                }
            }

            row.Add(chanceChip);

            return row;
        }

        private static int GetSerializedWeightTotal(SerializedProperty listProp)
        {
            if (listProp == null || !listProp.isArray)
                return 0;

            int total = 0;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var entry = listProp.GetArrayElementAtIndex(i);
                var weightProp = entry.FindPropertyRelative("weight");

                if (weightProp != null)
                    total += Mathf.Max(0, weightProp.intValue);
            }

            return total;
        }

        private static int GetEntryWeightTotal(System.Collections.Generic.List<LootTable.Entry> entries)
        {
            if (entries == null)
                return 0;

            return entries
                .Where(e => e != null && e.weight > 0)
                .Sum(e => e.weight);
        }

        private static Sprite GetItemIcon(ItemData item)
        {
            return item != null ? item.icon : null;
        }

        private static void ApplyChanceChipStyle(Label chip)
        {
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.fontSize = 10;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
        }

        private static Label MakeFixedHeaderLabel(string text, float width, int marginRight)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.marginRight = marginRight;
            label.style.opacity = 0.65f;
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label MakeFlexibleHeaderLabel(string text, int marginRight)
        {
            var label = new Label(text);
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            label.style.marginRight = marginRight;
            label.style.opacity = 0.65f;
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static void SetEnum(SerializedProperty parent, string name, int value)
        {
            var prop = parent.FindPropertyRelative(name);

            if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                prop.enumValueIndex = value;
        }

        private static void SetObject(SerializedProperty parent, string name, UnityEngine.Object value)
        {
            var prop = parent.FindPropertyRelative(name);

            if (prop != null)
                prop.objectReferenceValue = value;
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            var prop = parent.FindPropertyRelative(name);

            if (prop != null && prop.propertyType == SerializedPropertyType.Integer)
                prop.intValue = value;
        }

        private void NotifyChanged()
        {
            onChanged?.Invoke();
        }

        private void SortEntries()
        {
            if (serializedObject == null || listProperty == null || !listProperty.isArray)
                return;

            serializedObject.Update();

            int count = listProperty.arraySize;

            var data = new List<EntrySortData>(count);

            for (int i = 0; i < count; i++)
            {
                var entry = listProperty.GetArrayElementAtIndex(i);

                data.Add(new EntrySortData
                {
                    EntryType = entry.FindPropertyRelative("entryType")?.enumValueIndex ?? 0,
                    Item = entry.FindPropertyRelative("item")?.objectReferenceValue,
                    NestedTable = entry.FindPropertyRelative("nestedTable")?.objectReferenceValue,
                    Weight = entry.FindPropertyRelative("weight")?.intValue ?? 0,
                    MinQuantity = entry.FindPropertyRelative("minQuantity")?.intValue ?? 1,
                    MaxQuantity = entry.FindPropertyRelative("maxQuantity")?.intValue ?? 1,
                    EquipableOverrideMode = entry.FindPropertyRelative("equipableOverrideMode")?.enumValueIndex ?? 0,
                    OverrideFixedRarity = entry.FindPropertyRelative("overrideFixedRarity")?.enumValueIndex ?? 0
                });
            }

            data = weightedMode
                ? data
                    .OrderByDescending(e => e.Weight)
                    .ThenBy(e => GetSortDisplayName(e), StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : data
                    .OrderBy(e => GetSortDisplayName(e), StringComparer.OrdinalIgnoreCase)
                    .ToList();

            for (int i = 0; i < data.Count; i++)
            {
                var entry = listProperty.GetArrayElementAtIndex(i);
                var d = data[i];

                SetEnum(entry, "entryType", d.EntryType);
                SetObject(entry, "item", d.Item);
                SetObject(entry, "nestedTable", d.NestedTable);
                SetInt(entry, "weight", d.Weight);
                SetInt(entry, "minQuantity", d.MinQuantity);
                SetInt(entry, "maxQuantity", d.MaxQuantity);
                SetEnum(entry, "equipableOverrideMode", d.EquipableOverrideMode);
                SetEnum(entry, "overrideFixedRarity", d.OverrideFixedRarity);
            }

            serializedObject.ApplyModifiedProperties();

            RebuildRows();
            NotifyChanged();
        }
        
        private static string GetSortDisplayName(EntrySortData data)
        {
            if (data.EntryType == (int)Enums.LootEntryType.Item)
                return data.Item != null ? data.Item.name : "~NULL_ITEM";

            if (data.EntryType == (int)Enums.LootEntryType.LootTable)
                return data.NestedTable != null ? data.NestedTable.name : "~NULL_TABLE";

            return string.Empty;
        }
    }
}
#endif