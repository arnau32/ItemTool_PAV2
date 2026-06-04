#if UNITY_EDITOR
using ToolUI;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ContentEditor.Modules.Items
{
    /// <summary>
    /// Capa visual del módulo de Items.
    /// Usa componentes ToolUI reutilizables para construir toolbar, split view, lista y detalles.
    /// </summary>
    public sealed class ItemBrowserUI
    {
        private readonly ItemBrowserDomain domain;
        private ToolbarButton sortButton;
        private ToolValidationBox validationBox;

        private Button contractButton;
        
        private ToolDetailsHeader currentHeader;
        
        private VisualElement root;
        private ToolSearchToolbar search;
        private ListView listView;
        
        private ToolBrowserLayout layout;

        private ToolTabBar typeTabBar;
        
        // Dimension Preview
        private VisualElement dimsGrid;
        private Label dimsOverflowLabel;
        private const int PreviewGridWidth = 6;
        private const int PreviewGridHeight = 3;
        private const int GridCellSize = 24;

        
        public ItemBrowserUI(ItemBrowserDomain domain)
        {
            this.domain = domain;
        }

        public void Build(VisualElement root)
        {
            this.root = root;

            root.Clear();

            layout = new ToolBrowserLayout(360f);

            BuildToolbar();
            BuildSidebar();

            root.Add(layout);
        }

        public void Refresh()
        {
            listView.itemsSource = domain.FilteredItems;
            listView.RefreshItems();

            RefreshSelectionVisual();
            RefreshDetails();
        }

        private void BuildToolbar()
        {
            layout.Toolbar.AddButtonLeft("New", ShowCreateMenu, 60f);

            search = new ToolSearchToolbar("Buscar por nombre, tipo o asset.");
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
            }, 80f);

            layout.Toolbar.AddButtonRight("Reset", () =>
            {
                domain.ResetFilters();

                search.SetValueWithoutNotify(domain.SearchText);
                typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);
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
            BuildTypeTabs();

            listView = BuildListView();
            layout.Sidebar.Add(listView);
        }

        private void BuildTypeTabs()
        {
            typeTabBar = new ToolTabBar();

            typeTabBar.AddTab("Equipable");
            typeTabBar.AddTab("Consumable");
            typeTabBar.AddTab("Crafting");
            typeTabBar.AddTab("Collectable");

            typeTabBar.SetTabColors(
                0,
                new Color(0.25f, 0.35f, 0.7f),
                new Color(0.35f, 0.5f, 1f));

            typeTabBar.SetTabColors(
                1,
                new Color(0.2f, 0.55f, 0.3f),
                new Color(0.3f, 0.8f, 0.4f));

            typeTabBar.SetTabColors(
                2,
                new Color(0.65f, 0.45f, 0.2f),
                new Color(0.9f, 0.6f, 0.25f));

            typeTabBar.SetTabColors(
                3,
                new Color(0.45f, 0.25f, 0.6f),
                new Color(0.7f, 0.35f, 0.9f));

            typeTabBar.TabSelected += index =>
            {
                domain.SetActiveTypeTab((ItemBrowserDomain.ItemTypeTab)index);

                Refresh();
            };

            typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);

            layout.Sidebar.Add(typeTabBar);
        }

        private ListView BuildListView()
        {
            var list = new ListView
            {
                selectionType = SelectionType.Single,
                itemsSource = domain.FilteredItems
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
                var item = domain.FilteredItems[index];

                row.Icon.image = item != null && item.icon != null ? item.icon.texture : null;
                row.Icon.style.opacity = row.Icon.image != null ? 1f : 0.2f;

                row.Title.text = item != null ? item.itemNameID : "<null>";
                row.Subtitle.text = item != null ? item.itemType.ToString() : string.Empty;
                row.HideIndicator();
                
                var validation = domain.GetValidationResult(item);
                
                if (validation.HasIssues)
                {
                    row.SetIndicator(validation.Indicator, validation.Color);
                    row.tooltip = validation.Tooltip;
                }
                else
                {
                    row.HideIndicator();
                    row.tooltip = string.Empty;
                }
            };

            list.selectionChanged += selection =>
            {
                foreach (var obj in selection)
                {
                    domain.SelectItem(obj as ItemData);
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
            
            var item = domain.SelectedItem;

            if (item == null)
            {
                layout.DetailsRoot.Add(new ToolEmptyState("Selecciona un item para ver / editar sus datos."));
                return;
            }

            layout.DetailsRoot.Add(BuildHeader(item));
            validationBox = new ToolValidationBox();
            layout.DetailsRoot.Add(validationBox);

            RefreshValidationBox(item);

            layout.DetailsRoot.Add(BuildBasicItemEditor());
            
        }

        private VisualElement BuildHeader(ItemData item)
        {
            currentHeader = new ToolDetailsHeader(
                onSelect: domain.PingSelected,
                onCopy: () =>
                {
                    string path = UnityEditor.AssetDatabase.GetAssetPath(item);
                    string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    UnityEditor.EditorGUIUtility.systemCopyBuffer = guid;
                });
            
            currentHeader.AddActionButton("Contract", ContractAllFoldouts);
            
            RefreshHeader(item);

            return currentHeader;
        }
        
        private void ContractAllFoldouts()
        {
            if (layout?.DetailsRoot == null)
                return;

            foreach (var foldout in layout.DetailsRoot.Query<Foldout>().ToList())
            {
                foldout.value = false;
            }
        }
        
        private VisualElement BuildBasicItemEditor()
        {
            var selectedSo = domain.SelectedSo;

            if (selectedSo == null)
                return new ToolEmptyState("No hay item seleccionado.");

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = ToolUISizes.Gap;

            root.Add(BuildItemInformation(selectedSo));

            root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
            root.Add(BuildLocalizationSection(selectedSo));

            if (domain.SelectedItem is EquipableItemData)
            {
                root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
                root.Add(BuildEquipableSection(selectedSo));
            }

            if (domain.SelectedItem is ConsumableItemData)
            {
                root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
                root.Add(BuildConsumableSection(selectedSo));
            }

            if (domain.SelectedItem is WeaponData)
            {
                root.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
                root.Add(BuildWeaponSection(selectedSo));
            }

            root.TrackSerializedObjectValue(selectedSo, _ =>
            {
                domain.MarkValidationDirty();
                domain.Refilter();
                domain.EnsureSelectionIsVisible();

                if (domain.SelectedItem == null)
                {
                    Refresh();
                    return;
                }

                RefreshHeader(domain.SelectedItem);
                RefreshValidationBox(domain.SelectedItem);
                RefreshDimensionsPreview();

                listView.itemsSource = domain.FilteredItems;
                listView.RefreshItems();
            });

            root.Bind(selectedSo);

            return root;
        }
        
        private VisualElement BuildItemInformation(SerializedObject selectedSo)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var columns = new ToolResponsiveColumns();

            AddProperty(columns.Left, selectedSo, "itemNameID", "Name ID");
            AddProperty(columns.Left, selectedSo, "itemRarity", "Rarity");
            AddProperty(columns.Left, selectedSo, "maxStack", "Max Stack");

            AddProperty(columns.Left, selectedSo, "SlotDimension", "Slot Dimension");

            columns.Right.Add(BuildDimensionsPreviewCard(selectedSo));

            root.Add(columns);

            return root;
        }
        
        private ToolSerializedSection BuildLocalizationSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Localization", selectedSo, true);

            section.AddProperty("localizedDisplayName", "Localized Display Name");
            section.AddProperty("descriptionBlocks", "Description Blocks");

            return section;
        }
        
        private VisualElement BuildDimensionsPreviewCard(SerializedObject selectedSo)
        {
            var card = new ToolCard();
            card.AddHeader("Dimensions Preview");

            dimsGrid = new VisualElement();
            dimsGrid.style.flexDirection = FlexDirection.Row;
            dimsGrid.style.flexWrap = Wrap.Wrap;
            dimsGrid.style.alignSelf = Align.Center;

            dimsOverflowLabel = new Label();
            dimsOverflowLabel.style.opacity = 0.7f;
            dimsOverflowLabel.style.marginTop = ToolUISizes.Gap;
            dimsOverflowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            card.Add(dimsGrid);
            card.Add(dimsOverflowLabel);

            RefreshDimensionsPreview();

            return card;
        }
        
        private void RefreshDimensionsPreview()
        {
            if (dimsGrid == null || domain.SelectedItem == null)
                return;

            dimsGrid.Clear();

            int itemWidth = Mathf.Max(1, domain.SelectedItem.SlotDimension.Width);
            int itemHeight = Mathf.Max(1, domain.SelectedItem.SlotDimension.Height);

            dimsGrid.style.width = PreviewGridWidth * GridCellSize;
            dimsGrid.style.height = PreviewGridHeight * GridCellSize;

            dimsGrid.style.flexDirection = FlexDirection.Row;
            dimsGrid.style.flexWrap = Wrap.Wrap;

            for (int y = 0; y < PreviewGridHeight; y++)
            {
                for (int x = 0; x < PreviewGridWidth; x++)
                {
                    bool occupied = x < itemWidth && y < itemHeight;

                    var cell = new VisualElement();
                    cell.style.width = GridCellSize;
                    cell.style.height = GridCellSize;

                    cell.style.borderTopWidth = 1;
                    cell.style.borderBottomWidth = 1;
                    cell.style.borderLeftWidth = 1;
                    cell.style.borderRightWidth = 1;

                    cell.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);
                    cell.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
                    cell.style.borderLeftColor = new Color(0f, 0f, 0f, 0.35f);
                    cell.style.borderRightColor = new Color(0f, 0f, 0f, 0.35f);

                    cell.style.backgroundColor = occupied
                        ? new Color(0.92f, 0.92f, 0.92f, 1f)   // item slot
                        : new Color(0.22f, 0.22f, 0.22f, 1f);  // empty slot
                    
                    var borderColor = occupied
                        ? new Color(0f, 0f, 0f, 0.55f)
                        : new Color(0f, 0f, 0f, 0.35f);

                    cell.style.borderTopColor = borderColor;
                    cell.style.borderBottomColor = borderColor;
                    cell.style.borderLeftColor = borderColor;
                    cell.style.borderRightColor = borderColor;

                    dimsGrid.Add(cell);
                }
            }

            bool overflow =
                itemWidth > PreviewGridWidth ||
                itemHeight > PreviewGridHeight;
            
            dimsOverflowLabel.text = overflow
                ? $"{itemWidth}x{itemHeight} excede preview {PreviewGridWidth}x{PreviewGridHeight}"
                : $"{itemWidth}x{itemHeight}";

            dimsOverflowLabel.style.display = DisplayStyle.Flex;
        }
        
        private ToolSection CreatePersistentSection(string sectionName, bool defaultOpen)
        {
            string key = GetSectionPrefsKey(sectionName);
            bool isOpen = UnityEditor.EditorPrefs.GetBool(key, defaultOpen);

            var section = new ToolSection(sectionName, isOpen);

            section.Foldout.RegisterValueChangedCallback(evt =>
            {
                UnityEditor.EditorPrefs.SetBool(key, evt.newValue);
            });

            return section;
        }
        
        private string GetSectionPrefsKey(string sectionName)
        {
            var item = domain.SelectedItem;

            if (item == null)
                return $"BS.ContentEditor.Items.Foldout.Global.{sectionName}";

            string path = UnityEditor.AssetDatabase.GetAssetPath(item);
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
                guid = item.GetInstanceID().ToString();

            return $"BS.ContentEditor.Items.Foldout.{guid}.{sectionName}";
        }
        
        /// <summary>
        /// Añade un PropertyField bindeado a una propiedad serializada.
        /// Si la propiedad no existe, muestra un warning inline.
        /// </summary>
        private static void AddProperty(
            VisualElement parent,
            SerializedObject serializedObject,
            string propertyName,
            string label)
        {
            if (parent == null)
                return;

            if (serializedObject == null)
            {
                parent.Add(new ToolFieldRow(new HelpBox(
                    "SerializedObject es null.",
                    HelpBoxMessageType.Error)));

                return;
            }

            var property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                parent.Add(new ToolFieldRow(new HelpBox(
                    $"No se encontró la propiedad '{propertyName}'.",
                    HelpBoxMessageType.Warning)));

                return;
            }

            var field = new PropertyField(property, label);
    
            ToolSerializedPropertyUtility.BindOrDisable(
                field,
                property,
                $"No se pudo bindear la propiedad '{propertyName}'.");

            parent.Add(new ToolFieldRow(field));
        }
        
        private string GetSortButtonText()
        {
            return domain.CurrentSortMode switch
            {
                ItemBrowserDomain.SortMode.NameAz => "A → Z",
                ItemBrowserDomain.SortMode.NameZa => "Z → A",
                _ => "Sort"
            };
        }
        
        /// <summary>
        /// Refresca el header de detalles del item seleccionado.
        /// </summary>
        private void RefreshHeader(ItemData item)
        {
            if (currentHeader == null || item == null)
                return;

            string title = string.IsNullOrEmpty(item.itemNameID)
                ? item.name
                : item.itemNameID;

            string subtitle = item.itemType.ToString();

            currentHeader.SetIcon(item.icon != null ? item.icon.texture : null);
            currentHeader.SetTexts(title, subtitle);
        }

        private void UpdateSortButtonText()
        {
            if (sortButton != null)
                sortButton.text = GetSortButtonText();
        }
        
        // SERIALIZABLE SECTIONS
        
        private ToolSerializedSection BuildEquipableSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Equipable", selectedSo, true);

            section.AddInfo(
                "FixedRarityFixedStats → usa stats base del SO.\n" +
                "FixedRarityRandomStats → rarity fija, stats rolados por rarity.\n" +
                "RandomRarityRandomStats → rarity y stats aleatorios.");
            
            section.AddProperty("rollMode", "Roll Mode");
            section.AddProperty("equipSlot", "Equip Slot");
            section.AddProperty("prefab", "Prefab");
            
            var modifiersProp = FindFirstArrayElementTypeContains(selectedSo, "StatModifier");
            
            if (modifiersProp != null)
            {
                section.Content.Add(new ToolFieldRow(
                    new ToolStatModifiersEditor(selectedSo, modifiersProp, () =>
                    {
                        RefreshHeader(domain.SelectedItem);
                        RefreshValidationBox(domain.SelectedItem);
                        listView?.RefreshItems();
                    })));
            }
            else
            {
                section.AddWarning("No se encontró ninguna lista/array de StatModifier en este EquipableItemData.");
                
            }

            return section;
        }
        
        private ToolSerializedSection BuildConsumableSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Consumable", selectedSo, true);

            var buffsProp = selectedSo.FindProperty("buffs");

            if (buffsProp != null)
            {
                section.Content.Add(new ToolFieldRow(
                    new ToolBuffEffectsEditor(selectedSo, buffsProp, () =>
                    {
                        RefreshHeader(domain.SelectedItem);
                        RefreshValidationBox(domain.SelectedItem);
                        listView?.RefreshItems();
                    })));
            }
            else
            {
                section.AddWarning("No se encontró la propiedad 'buffs' en ConsumableItemData.");
            }

            return section;
        }
        
        private ToolSerializedSection BuildWeaponSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("Weapon", selectedSo, true);

            section.AddProperty("animatorOverride", "Animator Override");
            section.AddProperty("handType", "Hand Type");
            section.AddProperty("familyType", "Weapon Family");

            var skillScoreProp = selectedSo.FindProperty("skillScoreNeeded");

            if (skillScoreProp != null)
            {
                var skillScoreField = new FloatField("Skill Score Needed");
                skillScoreField.BindProperty(skillScoreProp);

                skillScoreField.RegisterValueChangedCallback(evt =>
                {
                    ClampFloatProperty(skillScoreField, selectedSo, skillScoreProp, evt.newValue, 0f, float.MaxValue);
                });

                section.Content.Add(new ToolFieldRow(skillScoreField));
            }
            else
            {
                section.AddWarning("No se encontró la propiedad 'skillScoreNeeded' en WeaponData.");
            }

            section.Content.Add(BuildWeaponReferencesSection(selectedSo));
            section.Content.Add(new VisualElement { style = { height = ToolUISizes.LargeGap } });
            section.Content.Add(BuildWeaponCombosSection(selectedSo));

            return section;
        }
        
        private ToolSerializedSection BuildWeaponReferencesSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSerializedSection("References", selectedSo, true);

            section.AddProperty("prefabVariant", "Prefab Variant");
            section.AddProperty("dodgeSet", "Dodge Set");
            section.AddProperty("parry", "Parry");
            section.AddProperty("weaponSkill", "Weapon Skill");

            return section;
        }
        
        private ToolSection BuildWeaponCombosSection(SerializedObject selectedSo)
        {
            var section = CreatePersistentSection("Combos", true);

            var combosProp = selectedSo.FindProperty("combos");

            if (combosProp != null)
            {
                section.Content.Add(new ToolFieldRow(
                    new ToolWeaponCombosEditor(selectedSo, combosProp, () =>
                    {
                        RefreshHeader(domain.SelectedItem);
                        RefreshValidationBox(domain.SelectedItem);
                        listView?.RefreshItems();
                    })));
            }
            else
            {
                section.Content.Add(new ToolFieldRow(new HelpBox(
                    "No se encontró la propiedad 'combos' en WeaponData.",
                    HelpBoxMessageType.Warning)));
            }

            return section;
        }
        
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
        
        // REFRESH SELECTION
        
        private void RefreshSelectionVisual()
        {
            if (listView == null)
                return;

            if (domain.SelectedItem == null)
            {
                listView.ClearSelection();
                return;
            }

            int index = domain.FilteredItems.IndexOf(domain.SelectedItem);

            if (index >= 0)
                listView.SetSelectionWithoutNotify(new[] { index });
            else
                listView.ClearSelection();
        }
        
        // ITEM CREATION
        
        private void ShowCreateMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Equipable"), false, () =>
            {
                domain.CreateItem(ItemBrowserDomain.ItemTypeTab.Equipable);
                typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);
                Refresh();
            });

            menu.AddItem(new GUIContent("Consumable"), false, () =>
            {
                domain.CreateItem(ItemBrowserDomain.ItemTypeTab.Consumable);
                typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);
                Refresh();
            });

            menu.AddItem(new GUIContent("Crafting"), false, () =>
            {
                domain.CreateItem(ItemBrowserDomain.ItemTypeTab.Crafting);
                typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);
                Refresh();
            });

            menu.AddItem(new GUIContent("Collectable"), false, () =>
            {
                domain.CreateItem(ItemBrowserDomain.ItemTypeTab.Collectable);
                typeTabBar.SelectTab((int)domain.CurrentTypeTab, false);
                Refresh();
            });

            menu.ShowAsContext();
        }
        
        
        // VALIDATION
        
        private void RefreshValidationBox(ItemData item)
        {
            if (validationBox == null)
                return;

            var messages = domain.GetValidationMessages(item);
            
            if (messages.Count == 0)
            {
                validationBox.Hide();
                return;
            }

            var highest = messages.Max(m => m.Severity);

            var messageType = highest switch
            {
                ItemValidationSeverity.Error => HelpBoxMessageType.Error,
                ItemValidationSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };

            string text = "Validations:\n• " + string.Join("\n• ", messages.Select(m => m.Message));

            validationBox.Show(text, messageType);
        }
        
        // HELPERS
        
        private void ClampFloatProperty(
            FloatField field,
            SerializedObject serializedObject,
            SerializedProperty property,
            float newValue,
            float min,
            float max)
        {
            if (field == null || serializedObject == null || property == null)
                return;

            float clamped = Mathf.Clamp(newValue, min, max);

            if (Mathf.Approximately(clamped, newValue))
                return;

            serializedObject.Update();

            property.floatValue = clamped;

            serializedObject.ApplyModifiedProperties();

            field.SetValueWithoutNotify(clamped);

            domain.MarkValidationDirty();

            RefreshHeader(domain.SelectedItem);
            RefreshValidationBox(domain.SelectedItem);
            listView?.RefreshItems();
        }
        
        private static SerializedProperty FindFirstArrayElementTypeContains(
            SerializedObject serializedObject,
            string typeNamePart)
        {
            if (serializedObject == null || string.IsNullOrWhiteSpace(typeNamePart))
                return null;

            var iterator = serializedObject.GetIterator();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (!iterator.isArray)
                    continue;

                if (iterator.propertyType == SerializedPropertyType.String)
                    continue;

                var copy = iterator.Copy();

                if (copy.arraySize <= 0)
                    continue;

                var firstElement = copy.GetArrayElementAtIndex(0);

                if (firstElement == null)
                    continue;

                string typeName = firstElement.type ?? string.Empty;

                if (typeName.Contains(typeNamePart))
                    return copy;
            }

            return null;
        }
        
    }
}
#endif