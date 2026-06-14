#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ExpeditionBrowserWindow
{
    private sealed class UI
    {
        private readonly ExpeditionBrowserWindow _w;

        private static readonly Color EnemyTabColor = new(0.78f, 0.18f, 0.18f, 1f);
        private static readonly Color EnemyTabColorSelected = new(0.95f, 0.28f, 0.28f, 1f);

        private static readonly Color LootTabColor = new(0.18f, 0.38f, 0.85f, 1f);
        private static readonly Color LootTabColorSelected = new(0.28f, 0.52f, 1f, 1f);

        private static readonly Color MapTabColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color MapTabColorSelected = new(0.42f, 0.42f, 0.42f, 1f);

        public UI(ExpeditionBrowserWindow w)
        {
            _w = w;
        }

        public void BuildRoot(VisualElement root)
        {
            root.Add(BuildToolbar());
            root.Add(BuildContentArea());
        }

        public void RefreshList()
        {
            RebuildLeftList();
        }

        public void RefreshDetails()
        {
            _w.DetailsPanel?.Clear();

            if (_w.CurrentTab == ExpeditionListTab.Map)
            {
                ShowMapPlaceholder();
                return;
            }

            if (_w.SelectedRecord == null)
            {
                ShowEmptyDetails();
                return;
            }

            _w.DetailsPanel.Add(BuildHeader(_w.SelectedRecord));
            _w.DetailsPanel.Add(BuildValidationArea(_w.SelectedRecord));
            _w.DetailsPanel.Add(BuildDetailsContent(_w.SelectedRecord));
        }

        public void ShowEmptyDetails()
        {
            _w.DetailsPanel?.Clear();

            var center = new VisualElement();
            center.style.flexGrow = 1;
            center.style.justifyContent = Justify.Center;
            center.style.alignItems = Align.Center;

            var label = new Label(EmptyDetailsText);
            label.style.opacity = 0.6f;

            center.Add(label);
            _w.DetailsPanel.Add(center);
        }

        private void ShowMapPlaceholder()
        {
            _w.DetailsPanel?.Clear();

            var center = new VisualElement();
            center.style.flexGrow = 1;
            center.style.justifyContent = Justify.Center;
            center.style.alignItems = Align.Center;

            var label = new Label(EmptyMapText);
            label.style.opacity = 0.6f;
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            center.Add(label);
            _w.DetailsPanel.Add(center);
        }

        public void UpdateTabToggles()
        {
            bool enemiesSelected = _w.CurrentTab == ExpeditionListTab.Enemies;
            bool lootSelected = _w.CurrentTab == ExpeditionListTab.LootPoints;
            bool mapSelected = _w.CurrentTab == ExpeditionListTab.Map;

            _w.EnemiesTabToggle?.SetValueWithoutNotify(enemiesSelected);
            _w.LootPointsTabToggle?.SetValueWithoutNotify(lootSelected);
            _w.MapTabToggle?.SetValueWithoutNotify(mapSelected);

            ApplyTabStyle(_w.EnemiesTabToggle, enemiesSelected ? EnemyTabColorSelected : EnemyTabColor);
            ApplyTabStyle(_w.LootPointsTabToggle, lootSelected ? LootTabColorSelected : LootTabColor);
            ApplyTabStyle(_w.MapTabToggle, mapSelected ? MapTabColorSelected : MapTabColor);
        }

        private static void ApplyTabStyle(ToolbarToggle toggle, Color color)
        {
            if (toggle == null) return;

            toggle.style.backgroundColor = color;
            toggle.style.unityBackgroundImageTintColor = color;
            toggle.style.color = Color.white;
            toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            toggle.style.marginLeft = 2;
            toggle.style.marginRight = 2;
            toggle.style.paddingLeft = 8;
            toggle.style.paddingRight = 8;
            toggle.style.height = 22;
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();

            _w.RefreshBtn = new ToolbarButton(() => _w.Refresh(RefreshFlags.Hard)) { text = "Refresh" };
            _w.RefreshBtn.style.width = 70;
            toolbar.Add(_w.RefreshBtn);

            _w.SearchField = new ToolbarSearchField();
            _w.SearchField.style.flexGrow = 1;
            _w.SearchField.tooltip = "Buscar por nombre o path.";
            _w.SearchField.RegisterValueChangedCallback(_ =>
            {
                if (!_w.IsUIReady) return;
                _w._domain.SavePrefs();
                _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
            });
            toolbar.Add(_w.SearchField);

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginLeft = 6;
            tabs.style.marginRight = 6;
            tabs.style.alignItems = Align.Center;

            _w.EnemiesTabToggle = new ToolbarToggle { text = "Enemies" };
            _w.EnemiesTabToggle.tooltip = "Muestra enemigos y enemy points.";
            _w.EnemiesTabToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_w.IsUIReady || !evt.newValue) return;
                _w._domain.SetActiveTab(ExpeditionListTab.Enemies);
            });
            tabs.Add(_w.EnemiesTabToggle);

            _w.LootPointsTabToggle = new ToolbarToggle { text = "Loot Points" };
            _w.LootPointsTabToggle.tooltip = "Muestra loot points agrupados por zona.";
            _w.LootPointsTabToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_w.IsUIReady || !evt.newValue) return;
                _w._domain.SetActiveTab(ExpeditionListTab.LootPoints);
            });
            tabs.Add(_w.LootPointsTabToggle);

            _w.MapTabToggle = new ToolbarToggle { text = "Map" };
            _w.MapTabToggle.tooltip = "Tab de mapa. Vacío de momento.";
            _w.MapTabToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_w.IsUIReady || !evt.newValue) return;
                _w._domain.SetActiveTab(ExpeditionListTab.Map);
            });
            tabs.Add(_w.MapTabToggle);

            toolbar.Add(tabs);

            _w.IncludeInactiveToggle = new ToolbarToggle { text = "Inactive" };
            _w.IncludeInactiveToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_w.IsUIReady) return;
                _w._domain.SetIncludeInactive(evt.newValue);
            });
            toolbar.Add(_w.IncludeInactiveToggle);

            UpdateTabToggles();

            return toolbar;
        }

        private VisualElement BuildContentArea()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexGrow = 1;
            root.style.minHeight = 0;

            var listPanel = new VisualElement();
            listPanel.style.width = FixedListWidth;
            listPanel.style.minWidth = FixedListWidth;
            listPanel.style.maxWidth = FixedListWidth;
            listPanel.style.flexShrink = 0;
            listPanel.style.flexGrow = 0;
            listPanel.style.borderRightWidth = 1;
            listPanel.style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);

            _w.ListScroll = new ScrollView(ScrollViewMode.Vertical);
            _w.ListScroll.style.flexGrow = 1;
            _w.ListScroll.style.width = FixedListWidth;
            _w.ListScroll.style.minWidth = FixedListWidth;
            _w.ListScroll.style.maxWidth = FixedListWidth;

            _w.ListRoot = new VisualElement();
            _w.ListRoot.style.flexDirection = FlexDirection.Column;
            _w.ListRoot.style.flexGrow = 1;

            _w.ListScroll.Add(_w.ListRoot);
            listPanel.Add(_w.ListScroll);

            _w.DetailsScroll = new ScrollView(ScrollViewMode.Vertical);
            _w.DetailsScroll.style.flexGrow = 1;
            _w.DetailsScroll.style.minWidth = 0;
            _w.DetailsScroll.style.paddingLeft = 8;
            _w.DetailsScroll.style.paddingRight = 8;
            _w.DetailsScroll.style.paddingTop = 6;

            _w.DetailsPanel = new VisualElement();
            _w.DetailsPanel.style.flexDirection = FlexDirection.Column;
            _w.DetailsPanel.style.flexGrow = 1;
            _w.DetailsPanel.style.minWidth = 0;

            _w.DetailsScroll.Add(_w.DetailsPanel);

            root.Add(listPanel);
            root.Add(_w.DetailsScroll);

            return root;
        }

        private void RebuildLeftList()
        {
            if (_w.ListRoot == null)
                return;

            _w.ListRoot.Clear();

            if (_w.CurrentTab == ExpeditionListTab.Map)
            {
                var label = new Label("Map tab vacío.");
                label.style.opacity = 0.6f;
                label.style.marginTop = 10;
                label.style.marginLeft = 8;
                _w.ListRoot.Add(label);
                return;
            }

            if (_w.CurrentTab == ExpeditionListTab.LootPoints)
            {
                BuildLootPointZoneGroups();
                return;
            }

            if (_w.CurrentTab == ExpeditionListTab.Enemies)
            {
                BuildEnemyZoneGroups();
                return;
            }
        }

        private void BuildLootPointZoneGroups()
        {
            var groups = _w.FilteredRecords
                .Where(r => r.Kind == SceneEntityKind.LootPoint)
                .GroupBy(GetRecordZoneName)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                bool isOpen = true;

                if (_w.LootZoneFoldoutStates.TryGetValue(group.Key, out bool savedState))
                    isOpen = savedState;

                var foldout = new Foldout
                {
                    text = $"{group.Key} ({group.Count()})",
                    value = isOpen
                };

                foldout.RegisterValueChangedCallback(evt =>
                {
                    _w.LootZoneFoldoutStates[group.Key] = evt.newValue;
                });

                foldout.style.marginLeft = 4;
                foldout.style.marginRight = 4;
                foldout.style.marginTop = 4;
                foldout.style.marginBottom = 2;

                var headerLabel = foldout.Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    headerLabel.style.color = LootTabColorSelected;
                }

                foreach (var record in group.OrderBy(r => r.Name))
                    foldout.Add(BuildRecordRow(record, indent: 12));

                _w.ListRoot.Add(foldout);
            }
        }
        
        private void BuildEnemyZoneGroups()
        {
            var groups = _w.FilteredRecords
                .Where(r => r.Kind == SceneEntityKind.Enemy || r.Kind == SceneEntityKind.EnemyPoint)
                .GroupBy(GetRecordZoneName)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                bool isOpen = true;

                if (_w.EnemyZoneFoldoutStates.TryGetValue(group.Key, out bool savedState))
                    isOpen = savedState;

                var foldout = new Foldout
                {
                    text = $"{group.Key} ({group.Count()})",
                    value = isOpen
                };

                foldout.RegisterValueChangedCallback(evt =>
                {
                    _w.EnemyZoneFoldoutStates[group.Key] = evt.newValue;
                });

                foldout.style.marginLeft = 4;
                foldout.style.marginRight = 4;
                foldout.style.marginTop = 4;
                foldout.style.marginBottom = 2;

                var headerLabel = foldout.Q<Label>();
                if (headerLabel != null)
                {
                    headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    headerLabel.style.color = EnemyTabColorSelected;
                }

                foreach (var record in group.OrderBy(r => r.Name))
                    foldout.Add(BuildRecordRow(record, indent: 12));

                _w.ListRoot.Add(foldout);
            }
        }

        private static string GetRecordZoneName(SceneEntityRecord record)
        {
            if (record?.GameObject == null)
                return "No Zone";

            var parent = record.GameObject.transform.parent;
            if (parent == null)
                return "No Zone";

            return string.IsNullOrWhiteSpace(parent.name) ? "No Zone" : parent.name;
        }

        private VisualElement BuildRecordRow(SceneEntityRecord record, int indent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            row.style.paddingLeft = 6 + indent;
            row.style.paddingRight = 6;

            bool selected = ReferenceEquals(record, _w.SelectedRecord);
            row.style.backgroundColor = selected
                ? new Color(0.24f, 0.38f, 0.62f, 0.45f)
                : StyleKeyword.Null;

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                evt.StopPropagation();

                _w._domain.SelectRecord(record);
                RebuildLeftList();
            });

            var name = new Label(record.Name);
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            name.style.overflow = Overflow.Hidden;
            name.tooltip = record.HierarchyPath;
            row.Add(name);

            var indicator = new Label();
            indicator.style.width = 16;
            indicator.style.minWidth = 16;
            indicator.style.marginLeft = 6;
            indicator.style.unityTextAlign = TextAnchor.MiddleCenter;
            indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            indicator.style.display = DisplayStyle.None;

            if (_w.SeverityCache.TryGetValue(record, out var severity))
            {
                if (severity == ValidationSeverity.Warning)
                {
                    indicator.text = "!";
                    indicator.style.color = WarnColor;
                    indicator.style.display = DisplayStyle.Flex;
                }
                else if (severity == ValidationSeverity.Error)
                {
                    indicator.text = "!";
                    indicator.style.color = ErrorColor;
                    indicator.style.display = DisplayStyle.Flex;
                }
            }

            row.Add(indicator);

            return row;
        }

        private VisualElement BuildHeader(SceneEntityRecord record)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;

            var textCol = new VisualElement();
            textCol.style.flexDirection = FlexDirection.Column;
            textCol.style.flexGrow = 1;

            _w.HeaderName = new Label(record.Name);
            _w.HeaderName.style.fontSize = 18;
            _w.HeaderName.style.unityFontStyleAndWeight = FontStyle.Bold;
            textCol.Add(_w.HeaderName);

            string zoneName = GetRecordZoneName(record);

            _w.HeaderType = new Label(zoneName);
            _w.HeaderType.style.unityFontStyleAndWeight = FontStyle.Bold;
            _w.HeaderType.style.marginTop = 2;

            if (record.Kind == SceneEntityKind.LootPoint)
                _w.HeaderType.style.color = LootTabColorSelected;
            else
                _w.HeaderType.style.color = EnemyTabColorSelected;

            textCol.Add(_w.HeaderType);
            top.Add(textCol);

            _w.HeaderSelectButton = new Button(() => _w._domain.SelectInEditor(record)) { text = "Select" };
            _w.HeaderSelectButton.style.height = 20;

            _w.HeaderPingButton = new Button(() => _w._domain.Ping(record)) { text = "Ping" };
            _w.HeaderPingButton.style.height = 20;
            _w.HeaderPingButton.style.marginLeft = 6;

            _w.HeaderFrameButton = new Button(() => _w._domain.Frame(record)) { text = "Frame" };
            _w.HeaderFrameButton.style.height = 20;
            _w.HeaderFrameButton.style.marginLeft = 6;

            top.Add(_w.HeaderSelectButton);
            top.Add(_w.HeaderPingButton);
            top.Add(_w.HeaderFrameButton);

            root.Add(top);
            return root;
        }

        private VisualElement BuildValidationArea(SceneEntityRecord record)
        {
            _w.ValidationContainer = new VisualElement();
            _w.ValidationContainer.style.marginTop = 4;
            _w.ValidationContainer.style.marginBottom = 8;

            _w.ValidationBox = new HelpBox("", HelpBoxMessageType.Info);

            var messages = _w._domain.GetMessages(record);
            if (messages.Count == 0)
            {
                _w.ValidationContainer.style.display = DisplayStyle.None;
                return _w.ValidationContainer;
            }

            var highest = messages.Max(m => m.Severity);
            _w.ValidationBox.messageType = highest switch
            {
                ValidationSeverity.Error => HelpBoxMessageType.Error,
                ValidationSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };

            _w.ValidationBox.text = "Validation Summary\n• " + string.Join("\n• ", messages.Select(m => m.Message));
            _w.ValidationContainer.Add(_w.ValidationBox);

            return _w.ValidationContainer;
        }

        private VisualElement BuildDetailsContent(SceneEntityRecord record)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            switch (record.Kind)
            {
                case SceneEntityKind.Enemy:
                    root.Add(BuildEnemyInspector(record));
                    break;

                case SceneEntityKind.EnemyPoint:
                    root.Add(BuildEnemyPointInspector(record));
                    break;

                case SceneEntityKind.LootPoint:
                    root.Add(BuildLootPointInspector(record));
                    break;
            }

            return root;
        }

        private VisualElement BuildEnemyInspector(SceneEntityRecord record)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var enemy = record.Component as EnemyBase;
            if (enemy == null)
            {
                root.Add(new HelpBox("EnemyBase inválido.", HelpBoxMessageType.Error));
                return root;
            }

            root.Add(BuildSectionTitle("Loot Tables"));

            var lootTables = GetLootTablesFromEnemy(enemy);

            if (lootTables.Count == 0)
            {
                root.Add(new HelpBox("No se han encontrado LootTables asociadas a este enemigo.", HelpBoxMessageType.Warning));
                return root;
            }

            foreach (var table in lootTables)
            {
                root.Add(BuildLootTablePreview(table));
                root.Add(Spacer());
            }

            return root;
        }

        private VisualElement BuildEnemyPointInspector(SceneEntityRecord record)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var point = record.Component as EnemyPoint;
            if (point == null)
            {
                root.Add(new HelpBox("EnemyPoint inválido.", HelpBoxMessageType.Error));
                return root;
            }

            var so = new SerializedObject(point);

            root.Add(BuildSectionTitle("Enemy Point Setup"));
            root.Add(BindField(so, "selectionWeight"));

            root.Add(Spacer());
            root.Add(BuildSectionTitle("Enemy Spawn"));
            root.Add(BindField(so, "Enemies"));
            root.Add(BindField(so, "allowMultipleEnemies"));
            root.Add(BindField(so, "maxEnemies"));
            root.Add(BindField(so, "spawnRadius"));
            root.Add(BindField(so, "forceSpawn"));
            root.Add(BindField(so, "forceMax"));

            return root;
        }

        private VisualElement BuildLootTablePreview(LootTable table)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.marginBottom = 6;

            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;

            var border = new Color(0f, 0f, 0f, 0.25f);
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;

            if (table == null)
            {
                card.Add(new HelpBox("LootTable null.", HelpBoxMessageType.Warning));
                return card;
            }

            var title = new Label(table.name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            card.Add(title);

            if (table.guaranteedEntries != null && table.guaranteedEntries.Count > 0)
            {
                var guaranteedTitle = new Label("Guaranteed Drops");
                guaranteedTitle.style.opacity = 0.75f;
                guaranteedTitle.style.marginBottom = 3;
                card.Add(guaranteedTitle);

                foreach (var entry in table.guaranteedEntries)
                    card.Add(BuildLootDropPreviewBlock(entry, 100f, true, 0));            }

            if (table.weightedEntries != null && table.weightedEntries.Count > 0)
            {
                var weightedTitle = new Label("Weighted Drops");
                weightedTitle.style.opacity = 0.75f;
                weightedTitle.style.marginTop = 6;
                weightedTitle.style.marginBottom = 3;
                card.Add(weightedTitle);

                int totalWeight = table.weightedEntries
                    .Where(e => e != null && e.weight > 0)
                    .Sum(e => e.weight);

                foreach (var entry in table.weightedEntries)
                {
                    float chance = 0f;

                    if (entry != null && entry.weight > 0 && totalWeight > 0)
                        chance = (float)entry.weight / totalWeight * 100f;

                    card.Add(BuildLootDropPreviewBlock(entry, chance, false, 0));                }
            }

            return card;
        }

        private VisualElement BuildLootDropPreviewBlock(LootTable.Entry entry, float chance, bool guaranteed, int indent)
        {
            var block = new VisualElement();
            block.style.flexDirection = FlexDirection.Column;

            block.Add(BuildLootDropPreviewRow(entry, chance, guaranteed, indent));

            if (entry == null ||
                entry.entryType != Enums.LootEntryType.LootTable ||
                entry.nestedTable == null)
            {
                return block;
            }

            var nestedTable = entry.nestedTable;

            if (nestedTable.guaranteedEntries != null)
            {
                foreach (var nestedEntry in nestedTable.guaranteedEntries)
                    block.Add(BuildLootDropPreviewBlock(nestedEntry, 100f, true, indent + 18));
            }

            if (nestedTable.weightedEntries != null && nestedTable.weightedEntries.Count > 0)
            {
                int totalWeight = nestedTable.weightedEntries
                    .Where(e => e != null && e.weight > 0)
                    .Sum(e => e.weight);

                foreach (var nestedEntry in nestedTable.weightedEntries)
                {
                    float nestedChance = 0f;

                    if (nestedEntry != null && nestedEntry.weight > 0 && totalWeight > 0)
                        nestedChance = (float)nestedEntry.weight / totalWeight * 100f;

                    block.Add(BuildLootDropPreviewBlock(nestedEntry, nestedChance, false, indent + 18));
                }
            }

            return block;
        }
        
        private VisualElement BuildLootDropPreviewRow(LootTable.Entry entry, float chance, bool guaranteed, int indent)        
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 26;
            row.style.marginBottom = 2;
            row.style.marginLeft = indent;

            var iconBox = new VisualElement();
            iconBox.style.width = 22;
            iconBox.style.height = 22;
            iconBox.style.minWidth = 22;
            iconBox.style.marginRight = 6;
            iconBox.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);

            row.Add(iconBox);

            string displayName = "<null>";
            string typeName = "None";

            if (entry != null)
            {
                if (entry.entryType == Enums.LootEntryType.Item && entry.item != null)
                {
                    displayName = !string.IsNullOrWhiteSpace(entry.item.itemNameID)
                        ? entry.item.itemNameID
                        : entry.item.name;

                    typeName = entry.item.itemType.ToString();

                    if (entry.item.icon != null)
                    {
                        var img = new Image
                        {
                            image = entry.item.icon.texture,
                            scaleMode = ScaleMode.ScaleToFit
                        };

                        img.style.width = Length.Percent(100);
                        img.style.height = Length.Percent(100);
                        iconBox.Add(img);
                    }
                }
                else if (entry.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null)
                {
                    displayName = entry.nestedTable.name;
                    typeName = "LootTable";

                    var lt = new Label("LT");
                    lt.style.unityTextAlign = TextAnchor.MiddleCenter;
                    lt.style.unityFontStyleAndWeight = FontStyle.Bold;
                    lt.style.fontSize = 9;
                    lt.style.width = Length.Percent(100);
                    lt.style.height = Length.Percent(100);
                    iconBox.Add(lt);
                }
            }

            var nameLabel = new Label(displayName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexShrink = 1;
            row.Add(nameLabel);

            var typeLabel = new Label(typeName);
            typeLabel.style.width = 80;
            typeLabel.style.opacity = 0.65f;
            row.Add(typeLabel);

            var chanceLabel = new Label(guaranteed ? "100%" : chance <= 0f ? "—" : $"{chance:0.##}%");
            chanceLabel.style.width = 64;
            chanceLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            chanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            bool isNested = indent > 0;
            chanceLabel.style.backgroundColor = isNested
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0.18f, 0.38f, 0.85f, 0.18f);

            chanceLabel.style.color = isNested
                ? new Color(0.85f, 0.85f, 0.85f, 1f)
                : LootTabColorSelected;
            chanceLabel.style.paddingRight = 4;
            chanceLabel.style.paddingLeft = 4;

            row.Add(chanceLabel);

            return row;
        }


        private VisualElement BuildLootPointInspector(SceneEntityRecord record)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var point = record.Component as LootPoint;
            if (point == null)
            {
                root.Add(new HelpBox("LootPoint inválido.", HelpBoxMessageType.Error));
                return root;
            }

            root.Add(BuildSectionTitle("Loot Tables"));

            if (point.LootTables == null || point.LootTables.Count == 0)
            {
                root.Add(new HelpBox("Este LootPoint no tiene LootTables asignadas.", HelpBoxMessageType.Warning));
                return root;
            }

            foreach (var table in point.LootTables)
            {
                root.Add(BuildLootTablePreview(table));
                root.Add(Spacer());
            }

            return root;
        }

        private static VisualElement BindField(SerializedObject so, string propertyPath)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                return new HelpBox($"No se encontró la propiedad '{propertyPath}'.", HelpBoxMessageType.Error);

            var field = new PropertyField(prop);
            field.Bind(so);
            return field;
        }

        private static Label BuildSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement Spacer()
        {
            var spacer = new VisualElement();
            spacer.style.height = 10;
            return spacer;
        }
    }
    
    private static List<LootTable> GetLootTablesFromEnemy(EnemyBase enemy)
    {
        var result = new List<LootTable>();

        if (enemy == null)
            return result;

        CollectLootTablesFromObject(enemy, result);

        var enemySo = new SerializedObject(enemy);
        var definitionProp = enemySo.FindProperty("_definition");

        if (definitionProp != null && definitionProp.objectReferenceValue != null)
            CollectLootTablesFromObject(definitionProp.objectReferenceValue, result);

        return result
            .Where(t => t != null)
            .Distinct()
            .ToList();
    }

    private static void CollectLootTablesFromObject(Object target, List<LootTable> result)
    {
        if (target == null || result == null)
            return;

        var so = new SerializedObject(target);
        var iterator = so.GetIterator();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (iterator.objectReferenceValue is LootTable table && !result.Contains(table))
                result.Add(table);
        }
    }
}
#endif