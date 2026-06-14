#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow
{
    private sealed class EntryRowData
    {
        public Action CleanupEntryTypeCallback;
        public Action CleanupItemCallback;
        public Action CleanupOverrideCallback;
        public Action CleanupWeightCallback;
    }

    private sealed partial class UI
    {
        private VisualElement BuildGuaranteedSection(SerializedProperty guaranteedProp)
        {
            return BuildEntryEditor(
                guaranteedProp,
                weightedMode: false,
                addButtonText: "+ Add Guaranteed",
                clearTitle: "Clear Guaranteed Entries",
                clearQuestion: "¿Quieres borrar todas las guaranteed entries?");
        }

        private VisualElement BuildWeightedSection(SerializedProperty weightedProp)
        {
            return BuildEntryEditor(
                weightedProp,
                weightedMode: true,
                addButtonText: "+ Add Weighted",
                clearTitle: "Clear Weighted Entries",
                clearQuestion: "¿Quieres borrar todas las weighted entries?");
        }

        private VisualElement BuildWeightedSettingsSection(SerializedProperty minProp, SerializedProperty maxProp)
        {
            var root = new VisualElement();
            root.style.flexDirection = _w.position.width < DetailsResponsiveBreak
                ? FlexDirection.Column
                : FlexDirection.Row;
            root.style.alignItems = Align.FlexStart;

            var minField = new IntegerField("Min Random Drops");
            minField.BindProperty(minProp);
            minField.style.flexGrow = 1;
            minField.style.marginRight = 8;
            root.Add(minField);

            var maxField = new IntegerField("Max Random Drops");
            maxField.BindProperty(maxProp);
            maxField.style.flexGrow = 1;
            root.Add(maxField);

            return root;
        }

        private VisualElement BuildEntryEditor(
            SerializedProperty listProp,
            bool weightedMode,
            string addButtonText,
            string clearTitle,
            string clearQuestion)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginTop = 8;

            // ── Bar ───────────────────────────────────────────────────────────

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            var addBtn = new Button(() => AddEntry(listProp, weightedMode)) { text = addButtonText };
            addBtn.style.height = 20;

            var sortBtn = new Button(() => SortEntries(listProp, weightedMode))
            {
                text = weightedMode ? "Sort Weight" : "Sort Name"
            };
            sortBtn.style.height = 20;
            sortBtn.style.marginLeft = 6;

            var clearBtn = new Button(() => ClearEntries(listProp, clearTitle, clearQuestion)) { text = "Clear" };
            clearBtn.style.height = 20;
            clearBtn.style.marginLeft = 6;

            bar.Add(addBtn);
            bar.Add(sortBtn);
            bar.Add(clearBtn);
            container.Add(bar);

            // ── Header ────────────────────────────────────────────────────────

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            header.Add(MakeFixedHeaderLabel("", 24, 6));
            header.Add(MakeFixedHeaderLabel("Type", 90, 6));
            header.Add(MakeFlexibleHeaderLabel("Item / Table", 6));

            if (weightedMode)
                header.Add(MakeFixedHeaderLabel("Weight", 72, 6));

            header.Add(MakeFixedHeaderLabel("Min", 64, 6));
            header.Add(MakeFixedHeaderLabel("Max", 64, 6));
            header.Add(MakeFixedHeaderLabel("Chance", 72, 6));
            header.Add(MakeFixedHeaderLabel("", 24, 6));

            container.Add(header);

            // ── Rows container ────────────────────────────────────────────────

            var borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.13f, 0.13f)
                : new Color(0.60f, 0.60f, 0.60f);

            var rowsContainer = new VisualElement();
            rowsContainer.style.flexDirection = FlexDirection.Column;
            rowsContainer.style.borderLeftWidth   = 1f;
            rowsContainer.style.borderRightWidth  = 1f;
            rowsContainer.style.borderTopWidth    = 1f;
            rowsContainer.style.borderBottomWidth = 1f;
            rowsContainer.style.borderLeftColor   = borderColor;
            rowsContainer.style.borderRightColor  = borderColor;
            rowsContainer.style.borderTopColor    = borderColor;
            rowsContainer.style.borderBottomColor = borderColor;

            container.Add(rowsContainer);

            // ── Row helpers ───────────────────────────────────────────────────

            VisualElement MakeRow()
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

                var itemField = new ObjectField { name = "item" };
                itemField.objectType = typeof(ItemData);
                itemField.allowSceneObjects = false;
                itemField.style.flexGrow = 1;
                itemField.style.flexShrink = 1;
                itemField.style.marginRight = 6;
                mainRow.Add(itemField);

                var nestedTableField = new ObjectField { name = "nestedTable" };
                nestedTableField.objectType = typeof(LootTable);
                nestedTableField.allowSceneObjects = false;
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

                // ── Override row (equipable only) ─────────────────────────────
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

            void UnbindRow(VisualElement ve)
            {
                var rowData = ve.userData as EntryRowData;
                if (rowData == null) return;

                rowData.CleanupEntryTypeCallback?.Invoke();
                rowData.CleanupItemCallback?.Invoke();
                rowData.CleanupOverrideCallback?.Invoke();
                rowData.CleanupWeightCallback?.Invoke();
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

            void BindRow(VisualElement ve, int arrayIndex)
            {
                var rowData = (EntryRowData)ve.userData;
                var elementProp = listProp.GetArrayElementAtIndex(arrayIndex);

                var entryTypeProp    = elementProp.FindPropertyRelative("entryType");
                var itemProp         = elementProp.FindPropertyRelative("item");
                var nestedTableProp  = elementProp.FindPropertyRelative("nestedTable");
                var minProp          = elementProp.FindPropertyRelative("minQuantity");
                var maxProp          = elementProp.FindPropertyRelative("maxQuantity");
                var weightProp       = weightedMode ? elementProp.FindPropertyRelative("weight") : null;
                var overrideModeProp = elementProp.FindPropertyRelative("equipableOverrideMode");
                var fixedRarityProp  = elementProp.FindPropertyRelative("overrideFixedRarity");

                var icon              = ve.Q<Image>("icon");
                var entryTypeField    = ve.Q<EnumField>("entryType");
                var itemField         = ve.Q<ObjectField>("item");
                var nestedTableField  = ve.Q<ObjectField>("nestedTable");
                var minField          = ve.Q<IntegerField>("minQuantity");
                var maxField          = ve.Q<IntegerField>("maxQuantity");
                var weightField       = weightedMode ? ve.Q<IntegerField>("weight") : null;
                var removeBtn         = ve.Q<Button>("remove");
                var overrideRow       = ve.Q<VisualElement>("override-row");
                var overrideModeField = ve.Q<EnumField>("equipableOverrideMode");
                var fixedRarityField  = ve.Q<EnumField>("overrideFixedRarity");
                var fixedRarityLabel  = ve.Q<Label>("fixedRarityLabel");
                var nestedDetails     = ve.Q<Foldout>("nested-details");
                var chanceChip       = ve.Q<Label>("chance");

                void SyncFieldsFromSerializedProperties()
                {
                    if (entryTypeProp != null && entryTypeField != null)
                    {
                        var value = (Enums.LootEntryType)entryTypeProp.enumValueIndex;
                        entryTypeField.SetValueWithoutNotify(value);
                    }

                    if (itemProp != null && itemField != null)
                        itemField.SetValueWithoutNotify(itemProp.objectReferenceValue);

                    if (nestedTableProp != null && nestedTableField != null)
                        nestedTableField.SetValueWithoutNotify(nestedTableProp.objectReferenceValue);

                    if (minProp != null && minField != null)
                        minField.SetValueWithoutNotify(minProp.intValue);

                    if (maxProp != null && maxField != null)
                        maxField.SetValueWithoutNotify(maxProp.intValue);

                    if (weightProp != null && weightField != null)
                        weightField.SetValueWithoutNotify(weightProp.intValue);

                    if (overrideModeProp != null && overrideModeField != null)
                    {
                        var value = (Enums.LootEntryEquipableOverrideMode)overrideModeProp.enumValueIndex;
                        overrideModeField.SetValueWithoutNotify(value);
                    }

                    if (fixedRarityProp != null && fixedRarityField != null)
                    {
                        var value = (Enums.ItemRarity)fixedRarityProp.enumValueIndex;
                        fixedRarityField.SetValueWithoutNotify(value);
                    }
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
                    if (entryTypeProp == null) return;
                    bool isItem = entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;
                    var currentItem = isItem ? itemProp?.objectReferenceValue as ItemData : null;
                    bool isEquipable = currentItem is EquipableItemData;
                    overrideRow.style.display = isEquipable ? DisplayStyle.Flex : DisplayStyle.None;

                    if (isEquipable && overrideModeProp != null)
                    {
                        var mode = (Enums.LootEntryEquipableOverrideMode)overrideModeProp.enumValueIndex;
                        bool needsFixed = mode == Enums.LootEntryEquipableOverrideMode.FixedRarityRandomStats
                                       || mode == Enums.LootEntryEquipableOverrideMode.FixedRarityFixedStats;
                        fixedRarityLabel.style.display = needsFixed ? DisplayStyle.Flex : DisplayStyle.None;
                        fixedRarityField.style.display = needsFixed ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                    else
                    {
                        fixedRarityLabel.style.display = DisplayStyle.None;
                        fixedRarityField.style.display = DisplayStyle.None;
                    }
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

                    int totalWeight = GetSerializedWeightTotal(listProp);
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

                void UpdateEntryTypeVisibility()
                {
                    if (entryTypeProp == null) return;

                    bool isItem = entryTypeProp.enumValueIndex == (int)Enums.LootEntryType.Item;
                    itemField.style.display = isItem ? DisplayStyle.Flex : DisplayStyle.None;
                    nestedTableField.style.display = isItem ? DisplayStyle.None : DisplayStyle.Flex;

                    var nestedTable = nestedTableProp?.objectReferenceValue as LootTable;
                    if (!isItem && nestedTable != null)
                    {
                        nestedDetails.style.display = DisplayStyle.Flex;
                        nestedDetails.text = "Entries";
                        nestedDetails.Clear();
                        nestedDetails.Add(BuildNestedLootTableInlineEditor(nestedTable));
                    }
                    else
                    {
                        nestedDetails.style.display = DisplayStyle.None;
                        nestedDetails.Clear();
                    }

                    UpdateOverrideVisibility();
                    UpdateIcon();
                    UpdateChanceChip();
                }

                if (entryTypeProp    != null) entryTypeField.BindProperty(entryTypeProp);
                if (itemProp         != null) itemField.BindProperty(itemProp);
                if (nestedTableProp  != null) nestedTableField.BindProperty(nestedTableProp);
                if (minProp          != null) minField.BindProperty(minProp);
                if (maxProp          != null) maxField.BindProperty(maxProp);
                if (weightProp       != null) weightField?.BindProperty(weightProp);
                if (overrideModeProp != null) overrideModeField.BindProperty(overrideModeProp);
                if (fixedRarityProp  != null) fixedRarityField.BindProperty(fixedRarityProp);

                SyncFieldsFromSerializedProperties();
                ve.schedule.Execute(() =>
                {
                    SyncFieldsFromSerializedProperties();
                    UpdateEntryTypeVisibility();
                    UpdateChanceChip();
                }).ExecuteLater(0);

                EventCallback<ChangeEvent<Enum>> entryTypeChangedCb = _ => UpdateEntryTypeVisibility();
                entryTypeField.RegisterValueChangedCallback(entryTypeChangedCb);
                rowData.CleanupEntryTypeCallback = () => entryTypeField.UnregisterValueChangedCallback(entryTypeChangedCb);

                EventCallback<ChangeEvent<UnityEngine.Object>> itemChangedCb = _ =>
                {
                    UpdateIcon();
                    UpdateOverrideVisibility();
                };
                itemField.RegisterValueChangedCallback(itemChangedCb);

                EventCallback<ChangeEvent<UnityEngine.Object>> nestedTableChangedCb = _ => UpdateEntryTypeVisibility();
                nestedTableField.RegisterValueChangedCallback(nestedTableChangedCb);

                rowData.CleanupItemCallback = () =>
                {
                    itemField.UnregisterValueChangedCallback(itemChangedCb);
                    nestedTableField.UnregisterValueChangedCallback(nestedTableChangedCb);
                };

                EventCallback<ChangeEvent<Enum>> overrideModeChangedCb = _ => UpdateOverrideVisibility();
                overrideModeField.RegisterValueChangedCallback(overrideModeChangedCb);
                rowData.CleanupOverrideCallback = () => overrideModeField.UnregisterValueChangedCallback(overrideModeChangedCb);

                if (weightedMode && weightField != null)
                {
                    EventCallback<ChangeEvent<int>> weightChangedCb = _ => RefreshAllChanceChips();
                    weightField.RegisterValueChangedCallback(weightChangedCb);
                    rowData.CleanupWeightCallback = () => weightField.UnregisterValueChangedCallback(weightChangedCb);
                }

                removeBtn.clicked += () =>
                {
                    RemoveEntryAt(listProp, arrayIndex);
                };

                UpdateEntryTypeVisibility();
                UpdateChanceChip();
            }

            int lastCount = -1;

            void RefreshAllChanceChips()
            {
                int i = 0;
                foreach (var child in rowsContainer.Children())
                {
                    var elementProp = listProp.GetArrayElementAtIndex(i);
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

                    int totalWeight = GetSerializedWeightTotal(listProp);
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

            void RebuildRows()
            {
                foreach (var child in rowsContainer.Children().ToList())
                    UnbindRow(child);

                rowsContainer.Clear();

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    var row = MakeRow();
                    BindRow(row, i);
                    rowsContainer.Add(row);
                }

                lastCount = listProp.arraySize;
            }

            container.TrackPropertyValue(listProp, _ =>
            {
                if (listProp.arraySize != lastCount)
                    RebuildRows();
                else
                    RefreshAllChanceChips();

                _w.MarkSeverityDirty();
                _w.ClearSimulation();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
            });

            RebuildRows();
            return container;
        }

        private VisualElement BuildNestedLootTableInlineEditor(LootTable table)
        {
            var root = BuildCardBase();
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

            var min = entry != null ? entry.minQuantity : 0;
            var max = entry != null ? entry.maxQuantity : 0;

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
                var element = listProp.GetArrayElementAtIndex(i);
                var weightProp = element?.FindPropertyRelative("weight");
                if (weightProp == null)
                    continue;

                total += Mathf.Max(0, weightProp.intValue);
            }

            return total;
        }

        private static int GetEntryWeightTotal(List<LootTable.Entry> entries)
        {
            if (entries == null)
                return 0;

            int total = 0;
            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;

                total += Mathf.Max(0, entry.weight);
            }

            return total;
        }

        private static void ApplyChanceChipStyle(Label chip)
        {
            if (chip == null)
                return;

            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.fontSize = 10;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.color = Color.white;
            chip.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.70f);
        }

        private void AddEntry(SerializedProperty listProp, bool weightedMode)
        {
            _w.SelectedSo.Update();

            var index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);

            var newProp = listProp.GetArrayElementAtIndex(index);
            ResetEntryElement(newProp, weightedMode);

            _w.SelectedSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(_w.Selected);

            _w.MarkSeverityDirty();
            _w.ClearSimulation();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
        }

        private void RemoveEntryAt(SerializedProperty listProp, int index)
        {
            if (index < 0 || index >= listProp.arraySize)
                return;

            _w.SelectedSo.Update();
            listProp.DeleteArrayElementAtIndex(index);
            _w.SelectedSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(_w.Selected);

            _w.MarkSeverityDirty();
            _w.ClearSimulation();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
        }

        private void ClearEntries(SerializedProperty listProp, string dialogTitle, string dialogQuestion)
        {
            if (listProp == null || listProp.arraySize == 0)
                return;

            if (!EditorUtility.DisplayDialog(dialogTitle, dialogQuestion, "Sí", "No"))
                return;

            _w.SelectedSo.Update();
            listProp.ClearArray();
            _w.SelectedSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(_w.Selected);

            _w.MarkSeverityDirty();
            _w.ClearSimulation();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
        }

        private static void ResetEntryElement(SerializedProperty elementProp, bool weightedMode)
        {
            if (elementProp == null)
                return;

            var entryTypeProp  = elementProp.FindPropertyRelative("entryType");
            var itemProp       = elementProp.FindPropertyRelative("item");
            var nestedProp     = elementProp.FindPropertyRelative("nestedTable");
            var minProp        = elementProp.FindPropertyRelative("minQuantity");
            var maxProp        = elementProp.FindPropertyRelative("maxQuantity");
            var weightProp     = weightedMode ? elementProp.FindPropertyRelative("weight") : null;

            if (entryTypeProp != null) entryTypeProp.enumValueIndex = 0;
            if (itemProp      != null) itemProp.objectReferenceValue = null;
            if (nestedProp    != null) nestedProp.objectReferenceValue = null;
            if (minProp       != null) minProp.intValue = 1;
            if (maxProp       != null) maxProp.intValue = 1;
            if (weightProp    != null) weightProp.intValue = 1;
        }

        private void SortEntries(SerializedProperty listProp, bool weightedMode)
        {
            _w.SelectedSo.Update();

            var n = listProp.arraySize;
            if (n <= 1)
                return;

            var data = new List<(int entryType, UnityEngine.Object item, UnityEngine.Object nestedTable, int min, int max, bool hasWeight, int weight, int overrideMode, int fixedRarity)>(n);

            for (var i = 0; i < n; i++)
            {
                var p           = listProp.GetArrayElementAtIndex(i);
                var entryType   = p.FindPropertyRelative("entryType")?.enumValueIndex ?? 0;
                var item        = p.FindPropertyRelative("item")?.objectReferenceValue;
                var nestedTable = p.FindPropertyRelative("nestedTable")?.objectReferenceValue;
                var min         = p.FindPropertyRelative("minQuantity")?.intValue ?? 1;
                var max         = p.FindPropertyRelative("maxQuantity")?.intValue ?? 1;
                var wProp       = p.FindPropertyRelative("weight");
                var hasWeight   = wProp != null;
                var weight      = hasWeight ? wProp.intValue : 0;
                var overrideMode  = p.FindPropertyRelative("equipableOverrideMode")?.enumValueIndex ?? 0;
                var fixedRarity   = p.FindPropertyRelative("overrideFixedRarity")?.enumValueIndex ?? 0;

                data.Add((entryType, item, nestedTable, min, max, hasWeight, weight, overrideMode, fixedRarity));
            }

            string DisplayName(int entryType, UnityEngine.Object item, UnityEngine.Object nestedTable)
            {
                if (entryType == (int)Enums.LootEntryType.Item)
                    return item != null ? item.name : string.Empty;
                return nestedTable != null ? nestedTable.name : string.Empty;
            }

            if (weightedMode)
            {
                var dir = _w.WeightedSortDirection;

                data = dir == EntrySortDirection.Desc
                    ? data.OrderByDescending(x => x.weight)
                          .ThenBy(x => DisplayName(x.entryType, x.item, x.nestedTable), StringComparer.OrdinalIgnoreCase)
                          .ToList()
                    : data.OrderBy(x => x.weight)
                          .ThenBy(x => DisplayName(x.entryType, x.item, x.nestedTable), StringComparer.OrdinalIgnoreCase)
                          .ToList();

                _w.WeightedSortDirection = dir == EntrySortDirection.Desc ? EntrySortDirection.Asc : EntrySortDirection.Desc;
            }
            else
            {
                var dir = _w.GuaranteedSortDirection;

                data = dir == EntrySortDirection.Desc
                    ? data.OrderByDescending(x => DisplayName(x.entryType, x.item, x.nestedTable), StringComparer.OrdinalIgnoreCase).ToList()
                    : data.OrderBy(x => DisplayName(x.entryType, x.item, x.nestedTable), StringComparer.OrdinalIgnoreCase).ToList();

                _w.GuaranteedSortDirection = dir == EntrySortDirection.Desc ? EntrySortDirection.Asc : EntrySortDirection.Desc;
            }

            for (var i = 0; i < data.Count; i++)
            {
                var p = listProp.GetArrayElementAtIndex(i);

                var entryTypeProp = p.FindPropertyRelative("entryType");
                if (entryTypeProp != null) entryTypeProp.enumValueIndex = data[i].entryType;

                p.FindPropertyRelative("item").objectReferenceValue = data[i].item;

                var nestedProp = p.FindPropertyRelative("nestedTable");
                if (nestedProp != null) nestedProp.objectReferenceValue = data[i].nestedTable;

                p.FindPropertyRelative("minQuantity").intValue = data[i].min;
                p.FindPropertyRelative("maxQuantity").intValue = data[i].max;

                var wProp = p.FindPropertyRelative("weight");
                if (wProp != null) wProp.intValue = data[i].weight;

                var overrideModeProp = p.FindPropertyRelative("equipableOverrideMode");
                if (overrideModeProp != null) overrideModeProp.enumValueIndex = data[i].overrideMode;

                var fixedRarityProp = p.FindPropertyRelative("overrideFixedRarity");
                if (fixedRarityProp != null) fixedRarityProp.enumValueIndex = data[i].fixedRarity;
            }

            _w.SelectedSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(_w.Selected);

            _w.MarkSeverityDirty();
            _w.ClearSimulation();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
        }
    }
}
#endif
