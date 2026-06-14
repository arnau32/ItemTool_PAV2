#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow
{
    private sealed partial class UI
    {
        private readonly LootTableBrowserWindow _w;

        public UI(LootTableBrowserWindow w)
        {
            _w = w;
        }

        public void BuildRoot(VisualElement root)
        {
            root.Add(BuildToolbar());
            root.Add(BuildSplitView());
        }

        public void RefreshList()
        {
            _w.ListView?.RefreshItems();
        }

        public void UpdateSortButtonText()
        {
            if (_w.SortBtn == null) return;

            _w.SortBtn.text = _w.CurrentSortMode switch
            {
                SortMode.NameAz => "A → Z",
                SortMode.NameZa => "Z → A",
                SortMode.GuaranteedCount => "Guaranteed",
                SortMode.WeightedCount => "Weighted",
                _ => "Sort"
            };
        }

        internal void RefreshSimulationView()
        {
            RefreshInventoryPreview();

            if (_w.SimSummaryLabel == null || _w.SimResultsRoot == null)
                return;

            _w.SimResultsRoot.Clear();

            if (_w.LastSimulation == null)
            {
                _w.SimSummaryLabel.text = "Sin simulación.";
                _w.SimResultsRoot.Add(new HelpBox("Ejecuta una simulación para ver resultados agregados.", HelpBoxMessageType.Info));
                return;
            }

            var sim = _w.LastSimulation;
            var avgStacks = sim.Iterations > 0 ? (float)sim.TotalGeneratedStacks / sim.Iterations : 0f;
            var avgQuantity = sim.Iterations > 0 ? (float)sim.TotalGeneratedQuantity / sim.Iterations : 0f;

            _w.SimSummaryLabel.text =
                $"Iterations: {sim.Iterations} | Avg Stacks: {avgStacks:F2} | Avg Quantity: {avgQuantity:F2}";

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginBottom = 4;

            header.Add(MakeFlexibleHeaderLabel("Item", 6));
            header.Add(MakeFixedHeaderLabel("Seen %", 72, 6));
            header.Add(MakeFixedHeaderLabel("Rolls", 72, 6));
            header.Add(MakeFixedHeaderLabel("Total Qty", 84, 6));
            header.Add(MakeFixedHeaderLabel("Rarity %", 220, 6));

            _w.SimResultsRoot.Add(header);

            foreach (var row in sim.Rows)
            {
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.style.minHeight = 24;
                line.style.marginBottom = 2;

                line.Add(BuildSimResultItemCell(row));

                var pct = sim.Iterations > 0 ? (100f * row.AppearsInRolls / sim.Iterations) : 0f;
                line.Add(MakeFixedValueLabel($"{pct:F1}%", 72, 6));
                line.Add(MakeFixedValueLabel(row.AppearsInRolls.ToString(), 72, 6));
                line.Add(MakeFixedValueLabel(row.TotalQuantity.ToString(), 84, 6));
                line.Add(BuildRarityBreakdownCell(row, 220, 6));

                _w.SimResultsRoot.Add(line);
            }

            if (sim.Rows.Count == 0)
                _w.SimResultsRoot.Add(new HelpBox("La simulación no ha generado ningún resultado.", HelpBoxMessageType.Warning));
        }

        private VisualElement BuildSimResultItemCell(SimRow row)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.flexGrow = 1;
            root.style.flexShrink = 1;
            root.style.marginRight = 6;

            var iconStrip = new VisualElement();
            iconStrip.style.flexDirection = FlexDirection.Row;
            iconStrip.style.alignItems = Align.Center;
            iconStrip.style.marginRight = 6;
            iconStrip.style.minWidth = 24;

            var rarities = GetRaritiesForRow(row).ToList();
            if (rarities.Count == 0)
                rarities.Add(row?.Item != null ? row.Item.itemRarity : Enums.ItemRarity.Common);

            foreach (var rarity in rarities)
                iconStrip.Add(BuildRarityIcon(row?.Item, rarity));

            root.Add(iconStrip);

            var label = new Label(row?.Item != null ? row.Item.name : "<null>");
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            root.Add(label);

            return root;
        }

        private VisualElement BuildRarityBreakdownCell(SimRow row, float width, int marginRight)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.width = width;
            root.style.minWidth = width;
            root.style.marginRight = marginRight;

            if (row == null || row.RarityCounts == null || row.RarityCounts.Count == 0 || row.TotalQuantity <= 0)
            {
                var fallback = new Label("-");
                fallback.style.opacity = 0.65f;
                root.Add(fallback);
                return root;
            }

            foreach (var kvp in row.RarityCounts.OrderBy(k => (int)k.Key))
            {
                if (kvp.Value <= 0)
                    continue;

                float pct = 100f * kvp.Value / Mathf.Max(1, row.TotalQuantity);
                root.Add(BuildRarityChip(kvp.Key, pct));
            }

            return root;
        }

        private VisualElement BuildRarityIcon(ItemData item, Enums.ItemRarity rarity)
        {
            var rarityColor = GetRarityColor(rarity);
            var iconBox = new VisualElement();
            iconBox.style.width = 20;
            iconBox.style.height = 20;
            iconBox.style.minWidth = 20;
            iconBox.style.marginRight = 3;
            iconBox.style.backgroundColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.25f);

            iconBox.style.borderLeftWidth = 2;
            iconBox.style.borderRightWidth = 2;
            iconBox.style.borderTopWidth = 2;
            iconBox.style.borderBottomWidth = 2;
            iconBox.style.borderLeftColor = rarityColor;
            iconBox.style.borderRightColor = rarityColor;
            iconBox.style.borderTopColor = rarityColor;
            iconBox.style.borderBottomColor = rarityColor;
            iconBox.tooltip = item != null ? $"{item.name} | {rarity}" : $"<null> | {rarity}";

            var sprite = GetItemIcon(item);
            if (sprite != null)
            {
                var img = new Image
                {
                    image = sprite.texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                img.style.width = Length.Percent(100);
                img.style.height = Length.Percent(100);
                iconBox.Add(img);
            }

            return iconBox;
        }

        private VisualElement BuildRarityChip(Enums.ItemRarity rarity, float pct)
        {
            var color = GetRarityColor(rarity);

            var chip = new Label($"{GetRarityShortName(rarity)} {pct:F1}%");
            chip.style.marginRight = 4;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.fontSize = 10;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = Color.white;
            chip.style.backgroundColor = new Color(color.r, color.g, color.b, 0.70f);
            chip.tooltip = rarity.ToString();

            return chip;
        }

        private static System.Collections.Generic.IEnumerable<Enums.ItemRarity> GetRaritiesForRow(SimRow row)
        {
            if (row?.RarityCounts == null || row.RarityCounts.Count == 0)
                yield break;

            foreach (var kvp in row.RarityCounts.OrderBy(k => (int)k.Key))
            {
                if (kvp.Value > 0)
                    yield return kvp.Key;
            }
        }

        private static string GetRarityShortName(Enums.ItemRarity rarity)
        {
            return rarity switch
            {
                Enums.ItemRarity.Common => "C",
                Enums.ItemRarity.NonCommon => "U",
                Enums.ItemRarity.Epic => "E",
                Enums.ItemRarity.Legendary => "L",
                _ => rarity.ToString()[0].ToString()
            };
        }

        public void ScaleInventoryGridToFit()
        {
            if (_w.SimInventoryGrid == null || _w.SimInventoryGridOuter == null)
                return;

            var outerW = _w.SimInventoryGridOuter.resolvedStyle.width;
            var gridW = _w.SimInventoryGrid.resolvedStyle.width;

            if (outerW <= 1f || gridW <= 1f)
            {
                _w.SimInventoryGridOuter.schedule.Execute(ScaleInventoryGridToFit).ExecuteLater(0);
                return;
            }

            var padding = _w.SimInventoryGridOuter.resolvedStyle.paddingLeft +
                          _w.SimInventoryGridOuter.resolvedStyle.paddingRight;

            var available = Mathf.Max(1f, outerW - padding);
            var scale = Mathf.Min(1f, available / gridW);

            _w.SimInventoryGrid.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _w.SimInventoryGrid.style.transformOrigin = new TransformOrigin(0, 0, 0);
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();

            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexWrap = Wrap.NoWrap;
            main.style.alignItems = Align.Center;
            main.style.justifyContent = Justify.SpaceBetween;
            main.style.flexGrow = 1;

            _w.ToolbarLeft = new VisualElement();
            _w.ToolbarLeft.style.flexDirection = FlexDirection.Row;
            _w.ToolbarLeft.style.flexWrap = Wrap.NoWrap;
            _w.ToolbarLeft.style.alignItems = Align.Center;
            _w.ToolbarLeft.style.flexGrow = 1;

            _w.ToolbarRight = new VisualElement();
            _w.ToolbarRight.style.flexDirection = FlexDirection.Row;
            _w.ToolbarRight.style.flexWrap = Wrap.NoWrap;
            _w.ToolbarRight.style.alignItems = Align.Center;

            void AddLeft(VisualElement ve, int ml = 0, int mr = 6)
            {
                ve.style.marginLeft = ml;
                ve.style.marginRight = mr;
                ve.style.marginBottom = 0;
                _w.ToolbarLeft.Add(ve);
            }

            void AddRight(VisualElement ve, int ml = 6, int mr = 0)
            {
                ve.style.marginLeft = ml;
                ve.style.marginRight = mr;
                ve.style.marginBottom = 0;
                _w.ToolbarRight.Add(ve);
            }

            _w.NewBtn = new ToolbarButton(_w._domain.ShowCreateMenu) { text = "New" };
            _w.NewBtn.style.width = 60;
            _w.NewBtn.tooltip = "Crear una nueva LootTable.";
            AddLeft(_w.NewBtn, 0, 8);

            _w.SearchField = new ToolbarSearchField();
            _w.SearchField.style.flexGrow = 1;
            _w.SearchField.style.flexShrink = 1;
            _w.SearchField.style.minWidth = 240;
            _w.SearchField.tooltip = "Buscar por nombre o resumen.";
            _w.SearchField.RegisterValueChangedCallback(_ =>
            {
                if (!_w.IsUIReady) return;
                _w._domain.SaveToolbarPrefs();
                _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
            });
            AddLeft(_w.SearchField, 0, 8);

            _w.SortBtn = new ToolbarButton(_w._domain.CycleSortMode);
            _w.SortBtn.style.width = 96;
            _w.SortBtn.tooltip = "Cambiar orden.";
            UpdateSortButtonText();
            AddRight(_w.SortBtn, 0, 6);

            _w.ResetBtn = new ToolbarButton(_w._domain.ResetToolbarAndList) { text = "Reset" };
            _w.ResetBtn.style.width = 60;
            _w.ResetBtn.tooltip = "Limpiar búsqueda y resetear orden.";
            AddRight(_w.ResetBtn, 0, 6);

            _w.RefreshBtn = new ToolbarButton(() => _w.Refresh(RefreshFlags.Hard)) { text = "Refresh" };
            _w.RefreshBtn.style.width = 70;
            _w.RefreshBtn.tooltip = "Reescanea assets LootTable.";
            AddRight(_w.RefreshBtn, 0);

            main.Add(_w.ToolbarLeft);
            main.Add(_w.ToolbarRight);
            toolbar.Add(main);

            return toolbar;
        }

        private TwoPaneSplitView BuildSplitView()
        {
            var leftInitial = Mathf.RoundToInt(MinWindowSize.x * (1f / 3f));
            var split = new TwoPaneSplitView(0, leftInitial, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            _w.ListView = BuildListView();
            _w.ListView.style.minWidth = 300;
            split.Add(_w.ListView);

            _w.DetailsScroll = new ScrollView(ScrollViewMode.Vertical);
            _w.DetailsScroll.style.flexGrow = 1;
            _w.DetailsScroll.style.minWidth = 600;
            _w.DetailsScroll.style.paddingLeft = 8;
            _w.DetailsScroll.style.paddingRight = 8;
            _w.DetailsScroll.style.paddingTop = 6;

            _w.DetailsPanel = new VisualElement();
            _w.DetailsPanel.style.flexDirection = FlexDirection.Column;
            _w.DetailsPanel.style.flexGrow = 1;

            _w.DetailsScroll.Add(_w.DetailsPanel);
            split.Add(_w.DetailsScroll);

            return split;
        }

        private ListView BuildListView()
        {
            var list = new ListView
            {
                selectionType = SelectionType.Single,
                itemsSource = _w.FilteredLootTables
            };

            list.style.flexGrow = 1;

            list.makeItem = () =>
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        height = 28,
                        paddingLeft = 6,
                        paddingRight = 6
                    }
                };

                var name = new Label();
                name.style.flexGrow = 1;
                name.style.flexShrink = 1;
                row.Add(name);

                var summary = new Label();
                summary.style.minWidth = 160;
                summary.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(summary);

                var indicator = new Label();
                indicator.style.width = 16;
                indicator.style.minWidth = 16;
                indicator.style.unityTextAlign = TextAnchor.MiddleCenter;
                indicator.style.marginLeft = 6;
                indicator.style.fontSize = 12;
                indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
                indicator.style.display = DisplayStyle.None;
                row.Add(indicator);

                row.userData = new ListRowRefs
                {
                    Name = name,
                    Summary = summary,
                    Indicator = indicator
                };

                return row;
            };

            list.bindItem = (element, index) =>
            {
                var refs = (ListRowRefs)element.userData;
                var table = _w.FilteredLootTables[index];

                refs.Name.text = table != null ? table.name : "<null>";
                refs.Summary.text = _w._domain.GetSummary(table);

                refs.Indicator.style.display = DisplayStyle.None;
                refs.Indicator.text = string.Empty;
                refs.Indicator.style.color = StyleKeyword.Null;

                if (table != null && _w.SeverityCache.TryGetValue(table, out var severity))
                {
                    if (severity == ValidationSeverity.Warning)
                    {
                        refs.Indicator.text = "!";
                        refs.Indicator.style.color = WarnColor;
                        refs.Indicator.style.display = DisplayStyle.Flex;
                    }
                    else if (severity == ValidationSeverity.Error)
                    {
                        refs.Indicator.text = "!";
                        refs.Indicator.style.color = ErrorColor;
                        refs.Indicator.style.display = DisplayStyle.Flex;
                    }
                }
            };

            list.selectionChanged += selection =>
            {
                var selected = selection.FirstOrDefault() as LootTable;
                _w._domain.SelectLootTable(selected);
            };

            list.itemsChosen += chosen =>
            {
                var chosenTable = chosen.FirstOrDefault() as LootTable;
                if (chosenTable == null) return;

                _w._domain.SelectLootTable(chosenTable);
                _w._domain.PingSelectedLootTable();
            };

            return list;
        }

        internal void ShowEmptyDetails()
        {
            _w.DetailsPanel.Clear();

            _w.HeaderName = null;
            _w.HeaderType = null;
            _w.HeaderGuidValue = null;
            _w.HeaderSelectButton = null;
            _w.HeaderCopyGuidButton = null;
            _w.RenameField = null;
            _w.RenameApplyButton = null;

            _w.ValidationContainer = null;
            _w.ValidationBox = null;

            _w.SimInventoryGrid = null;
            _w.SimInventoryGridOuter = null;
            _w.SimInventoryInfoLabel = null;
            _w.SimInventoryOverflowLabel = null;

            var centerContainer = new VisualElement();
            centerContainer.style.flexGrow = 1;
            centerContainer.style.flexDirection = FlexDirection.Column;
            centerContainer.style.justifyContent = Justify.Center;
            centerContainer.style.alignItems = Align.Center;

            var label = new Label(EmptyDetailsText);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.opacity = 0.6f;
            label.style.fontSize = 13;

            centerContainer.Add(label);
            _w.DetailsPanel.Add(centerContainer);
        }

        internal void ShowLootTableDetails()
        {
            _w.DetailsPanel.Clear();

            if (_w.Selected == null || _w.SelectedSo == null)
            {
                ShowEmptyDetails();
                return;
            }

            _w.DetailsPanel.Add(BuildHeader(_w.Selected));
            _w.DetailsPanel.Add(BuildValidationArea());
            _w.DetailsPanel.Add(BuildLootTableForm());
        }

        internal void RefreshHeader(LootTable table)
        {
            if (_w.HeaderName == null || table == null)
                return;

            _w.HeaderName.text = table.name;
            _w.HeaderType.text = _w._domain.GetSummary(table);

            var path = AssetDatabase.GetAssetPath(table);
            _w.HeaderGuidValue.text = AssetDatabase.AssetPathToGUID(path);

            if (_w.RenameField != null)
                _w.RenameField.SetValueWithoutNotify(table.name);

            _w.HeaderSelectButton?.SetEnabled(true);
            _w.HeaderCopyGuidButton?.SetEnabled(true);
        }

        private VisualElement BuildHeader(LootTable table)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;

            var textCol = new VisualElement();
            textCol.style.flexDirection = FlexDirection.Column;
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;

            _w.HeaderName = new Label();
            _w.HeaderName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _w.HeaderName.style.fontSize = 16;

            _w.HeaderType = new Label();
            _w.HeaderType.style.opacity = 0.7f;

            textCol.Add(_w.HeaderName);
            textCol.Add(_w.HeaderType);

            var rightCol = new VisualElement();
            rightCol.style.flexDirection = FlexDirection.Column;
            rightCol.style.alignItems = Align.FlexEnd;
            rightCol.style.marginLeft = 10;
            rightCol.style.minWidth = 140;
            rightCol.style.flexShrink = 1;

            var guidRow = new VisualElement();
            guidRow.style.flexDirection = FlexDirection.Row;
            guidRow.style.alignItems = Align.Center;
            guidRow.style.justifyContent = Justify.FlexEnd;

            var guidLabel = new Label("UniqueID:");
            guidLabel.style.fontSize = 10;
            guidLabel.style.opacity = 0.4f;
            guidLabel.style.marginRight = 6;

            _w.HeaderGuidValue = new Label();
            _w.HeaderGuidValue.style.fontSize = 11;
            _w.HeaderGuidValue.style.opacity = 0.55f;
            _w.HeaderGuidValue.style.unityTextAlign = TextAnchor.MiddleRight;
            _w.HeaderGuidValue.style.unityFont = new StyleFont(EditorStyles.miniFont);

            guidRow.Add(guidLabel);
            guidRow.Add(_w.HeaderGuidValue);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.alignItems = Align.Center;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 4;

            _w.HeaderSelectButton = new Button(() =>
            {
                if (_w.Selected != null)
                {
                    Selection.activeObject = _w.Selected;
                    EditorGUIUtility.PingObject(_w.Selected);
                }
            })
            { text = "Select" };
            _w.HeaderSelectButton.style.height = 20;
            _w.HeaderSelectButton.style.paddingLeft = 8;
            _w.HeaderSelectButton.style.paddingRight = 8;

            _w.HeaderCopyGuidButton = new Button(() =>
            {
                if (_w.Selected == null) return;
                var path = AssetDatabase.GetAssetPath(_w.Selected);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                {
                    EditorGUIUtility.systemCopyBuffer = guid;
                    _w.ShowNotification(new GUIContent("GUID copiado"));
                }
            })
            { text = "Copy" };
            _w.HeaderCopyGuidButton.style.height = 20;
            _w.HeaderCopyGuidButton.style.marginLeft = 6;
            _w.HeaderCopyGuidButton.style.paddingLeft = 8;
            _w.HeaderCopyGuidButton.style.paddingRight = 8;

            btnRow.Add(_w.HeaderSelectButton);
            btnRow.Add(_w.HeaderCopyGuidButton);

            rightCol.Add(guidRow);
            rightCol.Add(btnRow);

            header.Add(textCol);
            header.Add(rightCol);

            var renameRow = new VisualElement();
            renameRow.style.flexDirection = FlexDirection.Row;
            renameRow.style.alignItems = Align.Center;
            renameRow.style.marginTop = 6;

            _w.RenameField = new TextField("Asset Name");
            _w.RenameField.style.flexGrow = 1;
            _w.RenameField.style.flexShrink = 1;
            _w.RenameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    TryRename();
            });

            _w.RenameApplyButton = new Button(TryRename) { text = "Rename" };
            _w.RenameApplyButton.style.height = 20;
            _w.RenameApplyButton.style.marginLeft = 6;

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.Add(header);

            renameRow.Add(_w.RenameField);
            renameRow.Add(_w.RenameApplyButton);
            root.Add(renameRow);

            RefreshHeader(table);

            header.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var w = evt.newRect.width;
                header.style.flexDirection = w < 520f ? FlexDirection.Column : FlexDirection.Row;
                rightCol.style.alignSelf = w < 520f ? Align.FlexStart : Align.Auto;
                rightCol.style.marginTop = w < 520f ? 6 : 0;
            });

            return root;

            void TryRename()
            {
                if (_w.RenameField == null) return;
                _w._domain.RenameSelectedLootTable(_w.RenameField.value);
            }
        }

        private VisualElement BuildValidationArea()
        {
            _w.ValidationContainer = new VisualElement();
            _w.ValidationContainer.style.marginTop = 4;
            _w.ValidationContainer.style.marginBottom = 8;
            _w.ValidationContainer.style.display = DisplayStyle.None;

            _w.ValidationBox = new HelpBox("", HelpBoxMessageType.Info);
            _w.ValidationBox.style.display = DisplayStyle.None;

            _w.ValidationContainer.Add(_w.ValidationBox);
            return _w.ValidationContainer;
        }

        private VisualElement BuildLootTableForm()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = 6;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            if (_w.SelectedSo == null)
            {
                root.Add(new Label("No hay loot table seleccionada."));
                return root;
            }

            var minRandomDropsProp = _w.SelectedSo.FindProperty("minRandomPicks");
            var maxRandomDropsProp = _w.SelectedSo.FindProperty("maxRandomPicks");
            var guaranteedProp = _w.SelectedSo.FindProperty("guaranteedEntries");
            var weightedProp = _w.SelectedSo.FindProperty("weightedEntries");

            if (minRandomDropsProp == null || maxRandomDropsProp == null || guaranteedProp == null || weightedProp == null)
            {
                root.Add(new HelpBox(
                    "No se encontraron las propiedades esperadas en LootTable (guaranteedEntries, weightedEntries, minRandomDrops, maxRandomDrops).",
                    HelpBoxMessageType.Error));
                return root;
            }

            root.TrackSerializedObjectValue(_w.SelectedSo, _ =>
            {
                if (_w.Selected != null)
                    RefreshHeader(_w.Selected);

                _w.MarkSeverityDirty();
                _w.ClearSimulation();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details | RefreshFlags.Simulation);
            });

            _w.DetailsBodyRow = new VisualElement();
            _w.DetailsBodyRow.style.flexDirection = _w.position.width < 1200f ? FlexDirection.Column : FlexDirection.Row;
            _w.DetailsBodyRow.style.alignItems = Align.FlexStart;
            _w.DetailsBodyRow.style.flexGrow = 1;

            _w.DetailsLeftColumn = new VisualElement();
            _w.DetailsLeftColumn.style.flexDirection = FlexDirection.Column;
            _w.DetailsLeftColumn.style.flexGrow = 1;
            _w.DetailsLeftColumn.style.flexShrink = 1;
            _w.DetailsLeftColumn.style.minWidth = 420;

            _w.DetailsRightColumn = new VisualElement();
            _w.DetailsRightColumn.style.flexDirection = FlexDirection.Column;
            _w.DetailsRightColumn.style.flexShrink = 0;
            _w.DetailsRightColumn.style.width = 360;
            _w.DetailsRightColumn.style.minWidth = 320;
            _w.DetailsRightColumn.style.marginLeft = _w.position.width < 1200f ? 0 : 12;
            _w.DetailsRightColumn.style.marginTop = _w.position.width < 1200f ? 12 : 0;

            var guaranteedFoldout = CreatePersistentFoldout("Guaranteed Drops", DefaultGuaranteedOpen);
            guaranteedFoldout.Add(BuildGuaranteedSection(guaranteedProp));
            _w.DetailsLeftColumn.Add(guaranteedFoldout);

            _w.DetailsLeftColumn.Add(BuildSectionSpacer());

            var weightedFoldout = CreatePersistentFoldout("Weighted Random Drops", DefaultWeightedOpen);
            weightedFoldout.Add(BuildWeightedSettingsSection(minRandomDropsProp, maxRandomDropsProp));
            weightedFoldout.Add(BuildSectionSpacer());
            weightedFoldout.Add(BuildWeightedSection(weightedProp));
            _w.DetailsLeftColumn.Add(weightedFoldout);

            _w.DetailsLeftColumn.Add(BuildSectionSpacer());

            var simulationFoldout = CreatePersistentFoldout("Simulation", DefaultSimulationOpen);
            simulationFoldout.Add(BuildSimulationStatsSectionOnly());
            _w.DetailsLeftColumn.Add(simulationFoldout);

            _w.DetailsRightColumn.Add(BuildInventoryPreviewPanel());

            _w.DetailsBodyRow.Add(_w.DetailsLeftColumn);
            _w.DetailsBodyRow.Add(_w.DetailsRightColumn);

            root.Add(_w.DetailsBodyRow);

            root.Bind(_w.SelectedSo);

            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_w.DetailsBodyRow == null || _w.DetailsRightColumn == null)
                    return;

                var stacked = _w.position.width < 1200f;
                _w.DetailsBodyRow.style.flexDirection = stacked ? FlexDirection.Column : FlexDirection.Row;
                _w.DetailsRightColumn.style.marginLeft = stacked ? 0 : 12;
                _w.DetailsRightColumn.style.marginTop = stacked ? 12 : 0;
                ScaleInventoryGridToFit();
            });

            return root;
        }

        private Foldout CreatePersistentFoldout(string title, bool defaultValue)
        {
            var guid = _w.Selected != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_w.Selected)) : "noguid";
            var key = $"BS.LootTableBrowser.Foldout.{guid}.{title}";
            var value = EditorPrefs.GetBool(key, defaultValue);

            var foldout = new Foldout
            {
                text = title,
                value = value
            };

            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(key, evt.newValue));
            return foldout;
        }

        private void RefreshInventoryPreview()
        {
            if (_w.SimInventoryGrid == null || _w.SimInventoryInfoLabel == null || _w.SimInventoryOverflowLabel == null)
                return;

            BuildInventoryGridBase();

            if (_w.LastSimulation?.InventoryPreview == null)
            {
                _w.SimInventoryInfoLabel.text = "Inventory Preview | Sin roll todavía.";
                _w.SimInventoryOverflowLabel.text = string.Empty;
                ScaleInventoryGridToFit();
                return;
            }

            var preview = _w.LastSimulation.InventoryPreview;

            foreach (var placed in preview.Placed)
            {
                var sprite = GetItemIcon(placed.Item);
                var itemVe = new VisualElement();
                itemVe.style.position = Position.Absolute;
                itemVe.style.left = placed.X * InventoryCellSize;
                itemVe.style.top = placed.Y * InventoryCellSize;
                itemVe.style.width = placed.Width * InventoryCellSize;
                itemVe.style.height = placed.Height * InventoryCellSize;
                var rarityColor = GetRarityColor(placed.Rarity);
                var rarityBgColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.28f);

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

                if (sprite != null)
                {
                    var img = new Image
                    {
                        image = sprite.texture,
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

                _w.SimInventoryGrid.Add(itemVe);
            }

            _w.SimInventoryInfoLabel.text =
                $"Last Roll Preview | Placed: {preview.Placed.Count} | Unplaced: {preview.Unplaced.Count} | Grid: 6×3";

            _w.SimInventoryOverflowLabel.text = preview.Unplaced.Count > 0
                ? "No han cabido todos los items del último roll en el inventario 6×3."
                : string.Empty;

            ScaleInventoryGridToFit();
        }

        private void BuildInventoryGridBase()
        {
            _w.SimInventoryGrid.Clear();
            _w.SimInventoryGrid.style.width = InventoryPreviewWidth * InventoryCellSize;
            _w.SimInventoryGrid.style.height = InventoryPreviewHeight * InventoryCellSize;

            var emptyColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            var border = new Color(0f, 0f, 0f, 0.25f);

            for (var y = 0; y < InventoryPreviewHeight; y++)
            for (var x = 0; x < InventoryPreviewWidth; x++)
            {
                var cell = new VisualElement();
                cell.style.position = Position.Absolute;
                cell.style.left = x * InventoryCellSize;
                cell.style.top = y * InventoryCellSize;
                cell.style.width = InventoryCellSize;
                cell.style.height = InventoryCellSize;
                cell.style.backgroundColor = emptyColor;

                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;
                cell.style.borderTopWidth = 1;
                cell.style.borderBottomWidth = 1;

                cell.style.borderLeftColor = border;
                cell.style.borderRightColor = border;
                cell.style.borderTopColor = border;
                cell.style.borderBottomColor = border;

                _w.SimInventoryGrid.Add(cell);
            }
        }


        private static Color GetRarityColor(Enums.ItemRarity rarity)
        {
            return rarity switch
            {
                Enums.ItemRarity.Common => new Color(0.72f, 0.72f, 0.72f, 1f),
                Enums.ItemRarity.NonCommon => new Color(0.25f, 0.55f, 1f, 1f),
                Enums.ItemRarity.Epic => new Color(0.68f, 0.32f, 1f, 1f),
                Enums.ItemRarity.Legendary => new Color(1f, 0.58f, 0.12f, 1f),
                _ => Color.white
            };
        }

        private static Sprite GetItemIcon(ItemData item)
        {
            if (item == null)
                return null;

            var so = new SerializedObject(item);
            var iconProp = so.FindProperty("icon");
            return iconProp?.objectReferenceValue as Sprite;
        }

        private VisualElement BuildCardBase()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.marginBottom = 6;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            root.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            root.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
            root.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            root.style.borderTopLeftRadius = 4;
            root.style.borderTopRightRadius = 4;
            root.style.borderBottomLeftRadius = 4;
            root.style.borderBottomRightRadius = 4;
            return root;
        }

        private VisualElement BuildSectionSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.height = SectionGap;
            return spacer;
        }

        private Label MakeFlexibleHeaderLabel(string text, int mr)
        {
            var label = new Label(text);
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = mr;
            return label;
        }

        private Label MakeFixedHeaderLabel(string text, float width, int mr)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = mr;
            return label;
        }

        private Label MakeFixedValueLabel(string text, float width, int mr)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.marginRight = mr;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }
    }
}
#endif