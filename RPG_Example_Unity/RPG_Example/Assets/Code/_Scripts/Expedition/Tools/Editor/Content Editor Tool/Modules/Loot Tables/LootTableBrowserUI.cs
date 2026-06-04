#if UNITY_EDITOR
using ToolUI;
using UnityEditor;
using UnityEditor.UIElements;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentEditor.Modules.LootTables
{
    public sealed class LootTableBrowserUI
    {
        private readonly LootTableBrowserDomain domain;
        
        private ToolValidationBox validationBox;
        private VisualElement inventoryPreviewGrid;
        private Label inventoryPreviewInfoLabel;
        private Label inventoryPreviewOverflowLabel;

        private ToolBrowserLayout layout;
        private ToolSearchToolbar search;
        private ToolbarButton sortButton;
        private ListView listView;
        private ToolDetailsHeader currentHeader;
        
        private TextField renameField;
        private Button renameApplyButton;
        
        private IntegerField simIterationsField;
        private Label simSummaryLabel;
        private VisualElement simResultsRoot;

        public LootTableBrowserUI(LootTableBrowserDomain domain)
        {
            this.domain = domain;
        }

        public void Build(VisualElement root)
        {
            root.Clear();

            layout = new ToolBrowserLayout(360f);

            BuildToolbar();
            BuildSidebar();

            root.Add(layout);
        }

        public void Refresh()
        {
            listView.itemsSource = domain.FilteredLootTables;
            listView.RefreshItems();

            RefreshSelectionVisual();
            RefreshDetails();
        }

        private void BuildToolbar()
        {
            layout.Toolbar.AddButtonLeft("New", ShowCreateMenu, 60f);

            search = new ToolSearchToolbar("Buscar por nombre o resumen.");
            search.SetValueWithoutNotify(domain.SearchText);

            search.SearchChanged += value =>
            {
                domain.SetSearch(value);
                Refresh();
            };

            search.style.flexGrow = 1;
            layout.Toolbar.Left.Add(search);

            sortButton = layout.Toolbar.AddButtonLeft(GetSortButtonText(), () =>
            {
                domain.CycleSortMode();
                UpdateSortButtonText();
                Refresh();
            }, 100f);

            layout.Toolbar.AddButtonRight("Reset", () =>
            {
                domain.ResetFilters();

                search.SetValueWithoutNotify(domain.SearchText);
                UpdateSortButtonText();

                Refresh();
            }, 60f);

            layout.Toolbar.AddButtonRight("Refresh", () =>
            {
                domain.RefreshAssets();
                domain.Refilter();
                Refresh();
            }, 70f);
        }

        private void BuildSidebar()
        {
            listView = BuildListView();
            layout.Sidebar.Add(listView);
        }

        private ListView BuildListView()
        {
            var list = new ListView
            {
                selectionType = SelectionType.Single,
                itemsSource = domain.FilteredLootTables
            };

            list.style.flexGrow = 1;

            list.makeItem = () =>
            {
                var row = new ToolListRow();
                row.userData = row;
                return row;
            };

            list.bindItem = (element, index) =>
            {
                var row = (ToolListRow)element.userData;
                var table = domain.FilteredLootTables[index];

                row.Icon.image = table != null
                    ? EditorGUIUtility.IconContent("ScriptableObject Icon").image
                    : null;

                row.Icon.style.opacity = row.Icon.image != null ? 1f : 0.2f;

                row.Title.text = table != null ? table.name : "<null>";
                row.Subtitle.text = domain.GetSummary(table);

                row.HideIndicator();

                var validation = domain.GetValidationResult(table);

                if (validation.HasIssues)
                {
                    row.SetIndicator(validation.Indicator, validation.Color);
                    row.tooltip = validation.Tooltip;
                }
                else
                {
                    row.HideIndicator();
                    row.tooltip = table != null ? AssetDatabase.GetAssetPath(table) : string.Empty;
                }
            };

            list.selectionChanged += selection =>
            {
                foreach (var obj in selection)
                {
                    domain.SelectLootTable(obj as LootTable);
                    RefreshDetails();
                    break;
                }
            };

            list.itemsChosen += _ =>
            {
                domain.PingSelected();
            };

            return list;
        }

        private void RefreshDetails()
        {
            layout.DetailsRoot.Clear();

            var table = domain.SelectedLootTable;

            if (table == null)
            {
                layout.DetailsRoot.Add(new ToolEmptyState("Selecciona una Loot Table para ver / editar sus datos."));
                return;
            }

            layout.DetailsRoot.Add(BuildHeader(table));

            validationBox = new ToolValidationBox();
            layout.DetailsRoot.Add(validationBox);
            layout.DetailsRoot.Add(BuildRenameRow(table));

            RefreshValidationBox(table);

            layout.DetailsRoot.Add(BuildLootTableEditor());
        }

        private VisualElement BuildHeader(LootTable table)
        {
            currentHeader = new ToolDetailsHeader(
                onSelect: domain.PingSelected,
                onCopy: () =>
                {
                    string path = AssetDatabase.GetAssetPath(table);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    EditorGUIUtility.systemCopyBuffer = guid;
                });

            RefreshHeader(table);

            currentHeader.AddActionButton("Contract", ContractAllFoldouts);

            return currentHeader;
        }
        
        private void ContractAllFoldouts()
        {
            if (layout?.DetailsRoot == null)
                return;

            foreach (var foldout in layout.DetailsRoot.Query<Foldout>().ToList())
                foldout.value = false;
        }

        private void RefreshHeader(LootTable table)
        {
            if (currentHeader == null || table == null)
                return;

            currentHeader.Icon.image = EditorGUIUtility.IconContent("ScriptableObject Icon").image;
            currentHeader.Icon.style.opacity = 1f;

            currentHeader.Title.text = table.name;
            currentHeader.Subtitle.text = domain.GetSummary(table);

            string path = AssetDatabase.GetAssetPath(table);
            currentHeader.Subtitle.text = AssetDatabase.AssetPathToGUID(path);
        }

        private void RefreshSelectionVisual()
        {
            if (listView == null)
                return;

            if (domain.SelectedLootTable == null)
            {
                listView.ClearSelection();
                return;
            }

            int index = domain.FilteredLootTables.IndexOf(domain.SelectedLootTable);

            if (index >= 0)
                listView.SetSelectionWithoutNotify(new[] { index });
            else
                listView.ClearSelection();
        }

        private string GetSortButtonText()
        {
            return domain.CurrentSortMode switch
            {
                LootTableSortMode.NameAz => "A → Z",
                LootTableSortMode.NameZa => "Z → A",
                LootTableSortMode.GuaranteedCount => "Guaranteed",
                LootTableSortMode.WeightedCount => "Weighted",
                _ => "Sort"
            };
        }

        private void UpdateSortButtonText()
        {
            if (sortButton != null)
                sortButton.text = GetSortButtonText();
        }

        // HELPERS
        
        private ToolSerializedSection CreatePersistentSerializedSection(
            string sectionName,
            SerializedObject serializedObject,
            bool defaultOpen)
        {
            string key = GetSectionPrefsKey(sectionName);
            bool isOpen = EditorPrefs.GetBool(key, defaultOpen);

            var section = new ToolSerializedSection(sectionName, serializedObject, isOpen);

            section.Foldout.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(key, evt.newValue);
            });

            return section;
        }

        private string GetSectionPrefsKey(string sectionName)
        {
            var table = domain.SelectedLootTable;

            if (table == null)
                return $"BS.ContentEditor.LootTables.Foldout.Global.{sectionName}";

            string path = AssetDatabase.GetAssetPath(table);
            string guid = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
                guid = table.GetInstanceID().ToString();

            return $"BS.ContentEditor.LootTables.Foldout.{guid}.{sectionName}";
        }       
        
        // LOOT TABLE EDITOR
        
        private VisualElement BuildLootTableEditor()
        {
            var selectedSo = domain.SelectedSo;

            if (selectedSo == null)
                return new ToolEmptyState("No hay Loot Table seleccionada.");

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = ToolUISizes.Gap;
            root.style.paddingTop = ToolUISizes.Gap;
            root.style.paddingBottom = ToolUISizes.Gap;

            root.Add(BuildGuaranteedEntriesSection(selectedSo));
            root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
            root.Add(BuildWeightedEntriesSection(selectedSo));
            root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
            root.Add(BuildSimulationSection());

            root.TrackSerializedObjectValue(selectedSo, _ =>
            {
                domain.MarkValidationDirty();
                domain.ClearSimulation();
                domain.Refilter();

                RefreshHeader(domain.SelectedLootTable);
                RefreshValidationBox(domain.SelectedLootTable);
                RefreshSimulationResults();
                RefreshInventoryPreview();

                listView.itemsSource = domain.FilteredLootTables;
                listView.RefreshItems();
            });

            root.Bind(selectedSo);

            return root;
        }
        
        
        private ToolSerializedSection BuildGuaranteedEntriesSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Guaranteed Drops", selectedSo, true);

            var entriesProp = selectedSo.FindProperty("guaranteedEntries");

            if (entriesProp != null)
            {
                section.Content.Add(new ToolFieldRow(
                    new ToolLootTableEntriesEditor(
                        selectedSo,
                        entriesProp,
                        false,
                        "+ Add Guaranteed",
                        "Clear Guaranteed Entries",
                        "¿Quieres borrar todas las guaranteed entries?",
                        OnEntriesChanged,
                        OnEntryTargetDoubleClicked)));
            }
            else
            {
                section.AddWarning("No se encontró la propiedad 'guaranteedEntries'.");
            }

            return section;
        }

        private ToolSerializedSection BuildWeightedEntriesSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Weighted Random Drops", selectedSo, true);

            var minProp = selectedSo.FindProperty("minRandomPicks");
            var maxProp = selectedSo.FindProperty("maxRandomPicks");

            if (minProp != null && maxProp != null)
            {
                var randomPicksRow = new VisualElement();
                randomPicksRow.style.flexDirection = FlexDirection.Row;
                randomPicksRow.style.alignItems = Align.Center;
                randomPicksRow.style.marginBottom = ToolUISizes.Gap;

                var minField = new IntegerField("Min Random Picks");
                minField.BindProperty(minProp);
                minField.style.flexGrow = 1;
                minField.style.marginRight = ToolUISizes.Gap;

                var maxField = new IntegerField("Max Random Picks");
                maxField.BindProperty(maxProp);
                maxField.style.flexGrow = 1;

                randomPicksRow.Add(minField);
                randomPicksRow.Add(maxField);

                section.Content.Add(new ToolFieldRow(randomPicksRow));
            }
            else
            {
                section.AddWarning("No se encontraron minRandomPicks / maxRandomPicks.");
            }

            var entriesProp = selectedSo.FindProperty("weightedEntries");

            if (entriesProp != null)
            {
                section.Content.Add(new ToolFieldRow(
                    new ToolLootTableEntriesEditor(
                        selectedSo,
                        entriesProp,
                        true,
                        "+ Add Weighted",
                        "Clear Weighted Entries",
                        "¿Quieres borrar todas las weighted entries?",
                        OnEntriesChanged,
                        OnEntryTargetDoubleClicked)));
            }
            else
            {
                section.AddWarning("No se encontró la propiedad 'weightedEntries'.");
            }

            return section;
        }
        
        
        private void OnEntriesChanged()
        {
            domain.MarkValidationDirty();
            domain.ClearSimulation();
            domain.Refilter();

            RefreshHeader(domain.SelectedLootTable);
            RefreshValidationBox(domain.SelectedLootTable);
            RefreshSimulationResults();
            RefreshInventoryPreview();

            listView?.RefreshItems();
        }
        
        private void RefreshValidationBox(LootTable table)
        {
            if (validationBox == null)
                return;

            var messages = domain.GetValidationMessages(table);

            if (messages.Count == 0)
            {
                validationBox.Hide();
                return;
            }

            var highest = messages.Max(m => m.Severity);

            var messageType = highest switch
            {
                LootTableValidationSeverity.Error => HelpBoxMessageType.Error,
                LootTableValidationSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };

            string text = "Validations:\n• " + string.Join("\n• ", messages.Select(m => m.Message));

            validationBox.Show(text, messageType);
        }
        
        private VisualElement BuildRenameRow(LootTable table)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = ToolUISizes.Gap;

            renameField = new TextField("Asset Name");
            renameField.style.flexGrow = 1;
            renameField.SetValueWithoutNotify(table != null ? table.name : string.Empty);

            renameApplyButton = new Button(() =>
            {
                if (domain.RenameSelectedLootTable(renameField.value))
                    Refresh();
            })
            {
                text = "Rename"
            };

            renameApplyButton.style.height = ToolUISizes.SmallButtonHeight;
            renameApplyButton.style.marginLeft = ToolUISizes.Gap;

            row.Add(renameField);
            row.Add(renameApplyButton);

            return row;
        }
        
        // SIMULATION

        private ToolSection BuildSimulationSection()
        {
            var section = CreatePersistentSection("Simulation", true);

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.justifyContent = Justify.Center;
            toolbar.style.marginBottom = ToolUISizes.Gap;

            simIterationsField = new IntegerField("Iterations");
            simIterationsField.value = 100;
            simIterationsField.style.width = 180;
            simIterationsField.style.marginRight = ToolUISizes.Gap;

            var rollOnceButton = new Button(() =>
            {
                domain.RunSimulation(1);
                RefreshSimulationResults();
                RefreshInventoryPreview();
            })
            {
                text = "Roll Once"
            };

            rollOnceButton.style.height = ToolUISizes.SmallButtonHeight;
            rollOnceButton.style.marginRight = ToolUISizes.Gap;

            var runButton = new Button(() =>
            {
                int iterations = Mathf.Max(1, simIterationsField.value);

                domain.RunSimulation(iterations);

                RefreshSimulationResults();
                RefreshInventoryPreview();
            })
            {
                text = "Run Simulation"
            };

            runButton.style.height = ToolUISizes.SmallButtonHeight;

            toolbar.Add(simIterationsField);
            toolbar.Add(rollOnceButton);
            toolbar.Add(runButton);

            var previewWrapper = new VisualElement();
            previewWrapper.style.alignItems = Align.Center;
            previewWrapper.style.marginTop = ToolUISizes.Gap;
            previewWrapper.style.marginBottom = ToolUISizes.LargeGap;

            previewWrapper.Add(BuildInventoryPreviewPanel());

            simSummaryLabel = new Label("No simulation run.");
            simSummaryLabel.style.opacity = 0.75f;
            simSummaryLabel.style.marginBottom = ToolUISizes.Gap;
            simSummaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            var tableScroll = new ScrollView(ScrollViewMode.Horizontal);

            tableScroll.style.flexGrow = 1;
            tableScroll.style.flexShrink = 1;
            tableScroll.style.maxWidth = Length.Percent(100);

            tableScroll.style.marginTop = ToolUISizes.LargeGap;

            tableScroll.contentContainer.style.alignItems = Align.Center;
            tableScroll.contentContainer.style.justifyContent = Justify.Center;

            simResultsRoot = new VisualElement();
            simResultsRoot.style.flexDirection = FlexDirection.Column;
            simResultsRoot.style.minWidth = 720;
            simResultsRoot.style.alignSelf = Align.Center;
            simResultsRoot.style.marginTop = ToolUISizes.LargeGap;

            tableScroll.Add(simResultsRoot);

            root.Add(toolbar);
            root.Add(previewWrapper);

            simSummaryLabel.style.marginTop = ToolUISizes.LargeGap;
            simSummaryLabel.style.marginBottom = ToolUISizes.LargeGap;

            root.Add(simSummaryLabel);
            root.Add(tableScroll);

            section.Content.Add(root);

            RefreshSimulationResults();
            RefreshInventoryPreview();

            return section;
        }

        private void RefreshInventoryPreview()
        {
            if (inventoryPreviewGrid == null || inventoryPreviewInfoLabel == null ||
                inventoryPreviewOverflowLabel == null)
                return;

            BuildInventoryGridBase();

            var preview = domain.LastSimulation?.InventoryPreview;

            if (preview == null)
            {
                inventoryPreviewInfoLabel.text = "Inventory Preview | Sin roll todavía.";
                inventoryPreviewOverflowLabel.text = string.Empty;
                return;
            }

            const int cellSize = 36;

            foreach (var placed in preview.Placed)
            {
                var itemVe = new VisualElement();

                itemVe.style.position = Position.Absolute;
                itemVe.style.left = placed.X * cellSize;
                itemVe.style.top = placed.Y * cellSize;
                itemVe.style.width = placed.Width * cellSize;
                itemVe.style.height = placed.Height * cellSize;

                Color rarityColor = GetRarityColor(placed.Rarity);
                Color rarityBgColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.28f);

                itemVe.style.backgroundColor = rarityBgColor;

                itemVe.style.borderLeftWidth = 2;
                itemVe.style.borderRightWidth = 2;
                itemVe.style.borderTopWidth = 2;
                itemVe.style.borderBottomWidth = 2;

                itemVe.style.borderLeftColor = rarityColor;
                itemVe.style.borderRightColor = rarityColor;
                itemVe.style.borderTopColor = rarityColor;
                itemVe.style.borderBottomColor = rarityColor;

                itemVe.tooltip = placed.Item != null
                    ? $"{placed.Item.name} | {placed.Rarity}"
                    : $"<null> | {placed.Rarity}";

                if (placed.Item != null && placed.Item.icon != null)
                {
                    var img = new Image
                    {
                        image = placed.Item.icon.texture,
                        scaleMode = ScaleMode.ScaleToFit
                    };

                    img.style.position = Position.Absolute;
                    img.style.left = 1;
                    img.style.top = 1;
                    img.style.right = 1;
                    img.style.bottom = 1;

                    itemVe.Add(img);
                }

                var rarityBadge = new Label(placed.Rarity.ToString()[0].ToString());
                rarityBadge.style.position = Position.Absolute;
                rarityBadge.style.left = 2;
                rarityBadge.style.top = 1;
                rarityBadge.style.fontSize = 10;
                rarityBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                rarityBadge.style.color = Color.white;
                rarityBadge.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
                rarityBadge.tooltip = placed.Rarity.ToString();

                itemVe.Add(rarityBadge);

                if (placed.Quantity > 1)
                {
                    var qty = new Label(placed.Quantity.ToString());
                    qty.style.position = Position.Absolute;
                    qty.style.right = 2;
                    qty.style.bottom = 1;
                    qty.style.fontSize = 10;
                    qty.style.unityFontStyleAndWeight = FontStyle.Bold;
                    qty.style.color = Color.white;
                    qty.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);

                    itemVe.Add(qty);
                }

                inventoryPreviewGrid.Add(itemVe);
            }

            inventoryPreviewInfoLabel.text =
                $"Last Roll Preview | Placed: {preview.Placed.Count} | Unplaced: {preview.Unplaced.Count} | Grid: 6×3";

            inventoryPreviewOverflowLabel.text = preview.Unplaced.Count > 0
                ? "No han cabido todos los items del último roll en el inventario 6×3."
                : string.Empty;
        }
        
        private void BuildInventoryGridBase()
        {
            if (inventoryPreviewGrid == null)
                return;

            const int gridWidth = 6;
            const int gridHeight = 3;
            const int cellSize = 36;

            inventoryPreviewGrid.Clear();

            inventoryPreviewGrid.style.position = Position.Relative;
            inventoryPreviewGrid.style.width = gridWidth * cellSize;
            inventoryPreviewGrid.style.height = gridHeight * cellSize;

            var emptyColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            var border = new Color(0f, 0f, 0f, 0.25f);

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var cell = new VisualElement();

                    cell.style.position = Position.Absolute;
                    cell.style.left = x * cellSize;
                    cell.style.top = y * cellSize;
                    cell.style.width = cellSize;
                    cell.style.height = cellSize;
                    cell.style.backgroundColor = emptyColor;

                    cell.style.borderLeftWidth = 1;
                    cell.style.borderRightWidth = 1;
                    cell.style.borderTopWidth = 1;
                    cell.style.borderBottomWidth = 1;

                    cell.style.borderLeftColor = border;
                    cell.style.borderRightColor = border;
                    cell.style.borderTopColor = border;
                    cell.style.borderBottomColor = border;

                    inventoryPreviewGrid.Add(cell);
                }
            }
        }

        private VisualElement BuildInventoryPreviewPanel()
        {
            var root = new ToolCard();

            root.style.paddingTop = ToolUISizes.Gap;
            root.style.paddingBottom = ToolUISizes.Gap;
            root.style.paddingLeft = ToolUISizes.Gap;
            root.style.paddingRight = ToolUISizes.Gap;
            root.style.alignSelf = Align.Center;

            var title = new Label("Inventory Preview (6x3)");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = ToolUISizes.Gap;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;

            inventoryPreviewInfoLabel = new Label("Inventory Preview | Sin roll todavía.");
            inventoryPreviewInfoLabel.style.opacity = 0.75f;
            inventoryPreviewInfoLabel.style.marginBottom = ToolUISizes.Gap;
            inventoryPreviewInfoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            inventoryPreviewGrid = new VisualElement();
            inventoryPreviewGrid.style.position = Position.Relative;
            inventoryPreviewGrid.style.width = 6 * 36;
            inventoryPreviewGrid.style.height = 3 * 36;
            inventoryPreviewGrid.style.alignSelf = Align.Center;

            inventoryPreviewOverflowLabel = new Label();
            inventoryPreviewOverflowLabel.style.marginTop = ToolUISizes.Gap;
            inventoryPreviewOverflowLabel.style.color = new Color(1f, 0.7f, 0.2f);
            inventoryPreviewOverflowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            root.Add(title);
            root.Add(inventoryPreviewInfoLabel);
            root.Add(inventoryPreviewGrid);
            root.Add(inventoryPreviewOverflowLabel);

            BuildInventoryGridBase();

            return root;
        }
        
        private VisualElement BuildInventoryPreviewSlot(ItemStack stack)
        {
            var slot = BuildInventoryBaseSlot();

            var item = stack?.data;
            var rarity = stack != null
                ? stack.rolledRarity
                : Enums.ItemRarity.Common;

            slot.style.backgroundColor = GetRarityColor(rarity);

            slot.tooltip =
                $"{item?.name ?? "<null>"}\n" +
                $"Quantity: {stack?.quantity ?? 0}\n" +
                $"Rarity: {rarity}";

            if (item != null && item.icon != null)
            {
                var image = new Image
                {
                    image = item.icon.texture,
                    scaleMode = ScaleMode.ScaleToFit
                };

                image.style.width = Length.Percent(100);
                image.style.height = Length.Percent(100);

                slot.Add(image);
            }

            int quantity = stack != null ? stack.quantity : 0;

            if (quantity > 1)
            {
                var qty = new Label(quantity.ToString());
                qty.style.position = Position.Absolute;
                qty.style.right = 2;
                qty.style.bottom = 1;

                qty.style.unityTextAlign = TextAnchor.MiddleRight;
                qty.style.unityFontStyleAndWeight = FontStyle.Bold;
                qty.style.fontSize = 10;

                qty.style.color = Color.white;
                qty.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
                qty.style.paddingLeft = 3;
                qty.style.paddingRight = 3;

                slot.Add(qty);
            }

            return slot;
        }
        
        private VisualElement BuildInventoryEmptySlot()
        {
            var slot = BuildInventoryBaseSlot();
            slot.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            return slot;
        }

        private VisualElement BuildInventoryBaseSlot()
        {
            var slot = new VisualElement();

            slot.style.width = 36;
            slot.style.height = 36;

            slot.style.borderTopWidth = 1;
            slot.style.borderBottomWidth = 1;
            slot.style.borderLeftWidth = 1;
            slot.style.borderRightWidth = 1;

            var border = new Color(0f, 0f, 0f, 0.35f);

            slot.style.borderTopColor = border;
            slot.style.borderBottomColor = border;
            slot.style.borderLeftColor = border;
            slot.style.borderRightColor = border;

            slot.style.paddingLeft = 2;
            slot.style.paddingRight = 2;
            slot.style.paddingTop = 2;
            slot.style.paddingBottom = 2;

            return slot;
        }

        private void RefreshSimulationResults()
        {
            if (simSummaryLabel == null || simResultsRoot == null)
                return;

            simResultsRoot.Clear();

            var snapshot = domain.LastSimulation;

            if (snapshot == null)
            {
                simSummaryLabel.text = "No simulation run.";
                return;
            }

            float avgStacks = snapshot.Iterations > 0
                ? (float)snapshot.TotalGeneratedStacks / snapshot.Iterations
                : 0f;

            float avgQty = snapshot.Iterations > 0
                ? (float)snapshot.TotalGeneratedQuantity / snapshot.Iterations
                : 0f;

            simSummaryLabel.text =
                $"Iterations: {snapshot.Iterations} | " +
                $"Avg Stacks: {avgStacks:F2} | " +
                $"Avg Quantity: {avgQty:F2}";

            if (snapshot.Rows.Count == 0)
            {
                simResultsRoot.Add(new HelpBox(
                    "La simulación no generó loot.",
                    HelpBoxMessageType.Info));

                return;
            }

            simResultsRoot.Add(BuildSimulationHeader());

            foreach (var row in snapshot.Rows)
                simResultsRoot.Add(BuildSimulationRow(row, snapshot.Iterations));
        }
        
        private VisualElement BuildSimulationHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginBottom = ToolUISizes.SmallGap;
            header.style.marginTop = ToolUISizes.Gap;
            header.style.minWidth = 720;

            header.Add(BuildSimHeaderLabel("Item", true));
            header.Add(BuildSimHeaderLabel("Seen %", false, 80));
            header.Add(BuildSimHeaderLabel("Rolls", false, 60));
            header.Add(BuildSimHeaderLabel("Total Qty", false, 80));
            header.Add(BuildSimHeaderLabel("Rarity %", false, 220));

            return header;
        }
        
        // HELPERS
        
        private VisualElement BuildSimulationRow(
            LootTableSimulationRow row,
            int iterations)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.minHeight = 28;
            line.style.marginBottom = 4;
            line.style.minWidth = 720;

            var itemCell = new VisualElement();
            itemCell.style.flexDirection = FlexDirection.Row;
            itemCell.style.alignItems = Align.Center;
            itemCell.style.flexGrow = 1;
            itemCell.style.flexShrink = 1;
            itemCell.style.marginRight = ToolUISizes.Gap;

            var iconGroup = BuildSimulationRarityIcons(row);

            var itemLabel = new Label(row.Item != null ? row.Item.name : "<null>");
            itemLabel.style.flexGrow = 1;
            itemLabel.style.flexShrink = 1;
            itemLabel.style.marginLeft = 6;
            itemLabel.style.overflow = Overflow.Hidden;
            itemLabel.tooltip = row.Item != null ? row.Item.name : "<null>";

            itemCell.Add(iconGroup);
            itemCell.Add(itemLabel);

            line.Add(itemCell);

            float seenPct = iterations > 0
                ? 100f * row.AppearsInRolls / iterations
                : 0f;

            line.Add(BuildSimValueLabel($"{seenPct:F1}%", 80));
            line.Add(BuildSimValueLabel(row.AppearsInRolls.ToString(), 60));
            line.Add(BuildSimValueLabel(row.TotalQuantity.ToString(), 80));
            line.Add(BuildRarityChips(row, 220));

            return line;
        }
        
        private VisualElement BuildSimulationRarityIcons(LootTableSimulationRow row)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginRight = 8;

            if (row == null || row.Item == null)
            {
                root.Add(BuildSimulationRarityIcon(null, Enums.ItemRarity.Common));
                return root;
            }

            foreach (var kv in row.RarityCounts.OrderBy(kv => kv.Key))
            {
                if (kv.Value <= 0)
                    continue;

                root.Add(BuildSimulationRarityIcon(row.Item, kv.Key));
            }

            if (root.childCount == 0)
                root.Add(BuildSimulationRarityIcon(row.Item, row.Item.itemRarity));

            return root;
        }
        
        private void BuildEmptyInventoryPreviewGrid()
        {
            if (inventoryPreviewGrid == null)
                return;

            inventoryPreviewGrid.Clear();

            const int totalCells = 18;

            for (int i = 0; i < totalCells; i++)
                inventoryPreviewGrid.Add(BuildInventoryEmptySlot());
        }
        
        private VisualElement BuildSimulationRarityIcon(ItemData item, Enums.ItemRarity rarity)
        {
            var iconBox = new VisualElement();

            iconBox.style.width = 26;
            iconBox.style.height = 26;
            iconBox.style.minWidth = 26;
            iconBox.style.marginRight = 3;

            iconBox.style.paddingLeft = 2;
            iconBox.style.paddingRight = 2;
            iconBox.style.paddingTop = 2;
            iconBox.style.paddingBottom = 2;

            iconBox.style.backgroundColor = GetRarityColor(rarity);

            iconBox.tooltip = rarity.ToString();

            if (item != null && item.icon != null)
            {
                var icon = new Image
                {
                    image = item.icon.texture,
                    scaleMode = ScaleMode.ScaleToFit
                };

                icon.style.width = Length.Percent(100);
                icon.style.height = Length.Percent(100);

                iconBox.Add(icon);
            }

            return iconBox;
        }
        
        private static Enums.ItemRarity GetDominantRarity(LootTableSimulationRow row)
        {
            if (row == null || row.RarityCounts == null || row.RarityCounts.Count == 0)
                return Enums.ItemRarity.Common;

            return row.RarityCounts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .First()
                .Key;
        }
        
        private VisualElement BuildRarityChips(
            LootTableSimulationRow row,
            float width)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.width = width;
            root.style.minWidth = width;
            root.style.maxWidth = width;
            root.style.marginRight = ToolUISizes.Gap;
            root.style.overflow = Overflow.Hidden;

            if (row == null || row.RarityCounts.Count == 0 || row.TotalQuantity <= 0)
            {
                root.Add(BuildRarityChip("-", Color.gray));
                return root;
            }

            foreach (var kv in row.RarityCounts.OrderBy(kv => kv.Key))
            {
                float pct = 100f * kv.Value / row.TotalQuantity;

                string shortName = GetRarityShortName(kv.Key);
                Color color = GetRarityColor(kv.Key);

                root.Add(BuildRarityChip($"{shortName} {pct:F1}%", color));
            }

            return root;
        }
        
        private Label BuildRarityChip(string text, Color color)
        {
            var chip = new Label(text);

            chip.style.height = 18;
            chip.style.minWidth = 52;
            chip.style.marginRight = 4;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;

            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.fontSize = 10;

            chip.style.backgroundColor = color;
            chip.style.color = Color.white;

            return chip;
        }
        
        private static string GetRarityShortName(Enums.ItemRarity rarity)
        {
            return rarity switch
            {
                Enums.ItemRarity.Common => "C",
                Enums.ItemRarity.Rare => "R",
                Enums.ItemRarity.Epic => "E",
                Enums.ItemRarity.Legendary => "L",
                _ => rarity.ToString()[0].ToString()
            };
        }
        
        private static Color GetRarityColor(Enums.ItemRarity rarity)
        {
            return rarity switch
            {
                Enums.ItemRarity.Common => new Color(0.55f, 0.55f, 0.55f),
                Enums.ItemRarity.Rare => new Color(0.20f, 0.45f, 0.90f),
                Enums.ItemRarity.Epic => new Color(0.55f, 0.25f, 0.85f),
                Enums.ItemRarity.Legendary => new Color(0.95f, 0.55f, 0.15f),
                _ => Color.gray
            };
        }

        private Label BuildSimHeaderLabel(string text, bool flexible, float width = 0)
        {
            var label = new Label(text);

            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.65f;
            label.style.marginRight = ToolUISizes.Gap;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (flexible)
            {
                label.style.flexGrow = 1;
            }
            else
            {
                label.style.width = width;
                label.style.minWidth = width;
                label.style.maxWidth = width;
            }

            return label;
        }

        private Label BuildSimValueLabel(string text, float width)
        {
            var label = new Label(text);

            label.style.width = width;
            label.style.minWidth = width;
            label.style.maxWidth = width;

            label.style.marginRight = ToolUISizes.Gap;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            return label;
        }

        private string BuildRaritySummary(LootTableSimulationRow row)
        {
            if (row == null || row.RarityCounts.Count == 0)
                return "-";

            return string.Join(
                " ",
                row.RarityCounts
                    .OrderBy(kv => kv.Key)
                    .Select(kv => $"{kv.Key}:{kv.Value}"));
        }
        
        private ToolSection CreatePersistentSection(string sectionName, bool defaultOpen)
        {
            string key = GetSectionPrefsKey(sectionName);
            bool isOpen = EditorPrefs.GetBool(key, defaultOpen);

            var section = new ToolSection(sectionName, isOpen);

            section.Foldout.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(key, evt.newValue);
            });

            return section;
        }
        
        private void OnEntryTargetDoubleClicked(UnityEngine.Object target)
        {
            switch (target)
            {
                case ItemData item:
                    domain.RequestOpenItem(item);
                    break;

                case LootTable table:
                    domain.RequestOpenLootTable(table);
                    break;
            }
        }
        
        
        // LOOT TABLE CREATION
        
        private void ShowCreateMenu()
        {
            domain.CreateLootTable();
            Refresh();
        }
    }
}
#endif