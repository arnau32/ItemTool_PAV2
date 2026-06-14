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
        private readonly ItemBrowserWindow _w;

        public UI(ItemBrowserWindow w)
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

        public void UpdateDupToggleVisual()
        {
            if (_w.DupStrictToggle == null) return;

            _w.DupStrictToggle.style.backgroundColor = StyleKeyword.Null;
            _w.DupStrictToggle.style.unityBackgroundImageTintColor = StyleKeyword.Null;

            var c = _w.DupNameAsError ? DupErrorColor : DupWarnColor;
            _w.DupStrictToggle.style.backgroundColor = c;
            _w.DupStrictToggle.style.unityBackgroundImageTintColor = c;
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
            _w.NewBtn.tooltip = "Crear un nuevo Item (elige subtipo).";
            _w.NewBtn.style.width = 60;
            AddLeft(_w.NewBtn, 0, 8);

            _w.SearchField = new ToolbarSearchField();
            _w.SearchField.style.flexGrow = 1;
            _w.SearchField.style.flexShrink = 1;
            _w.SearchField.style.minWidth = 240;
            _w.SearchField.tooltip = "Buscar por nombre, tipo o nombre de asset.";
            _w.SearchField.RegisterValueChangedCallback(_ =>
            {
                if (!_w.IsUIReady) return;
                _w._domain.SaveToolbarPrefs();
                _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
            });
            AddLeft(_w.SearchField, 0, 8);

            _w.TypeFilterBtn = new ToolbarButton(_w._domain.ShowTypeFilterMenu) { text = "Type ▾" };
            _w.TypeFilterBtn.tooltip = "Filtro por tipo (multi-select).";
            _w.TypeFilterBtn.style.width = 90;
            AddLeft(_w.TypeFilterBtn);
            UpdateTypeFilterButtonText();

            _w.SortBtn = new ToolbarButton(_w._domain.CycleSortMode) { text = "" };
            _w.SortBtn.tooltip = "Ordenar (clic para cambiar): A→Z / Z→A / Type";
            _w.SortBtn.style.width = 70;
            AddLeft(_w.SortBtn, 0, 0);
            UpdateSortButtonText();

            _w.DupStrictToggle = new ToolbarToggle { text = "Duplicate" };
            _w.DupStrictToggle.tooltip = "OFF: itemNameID duplicado = WARNING (amarillo). ON: ERROR (rojo).";
            _w.DupStrictToggle.style.width = 90;
            _w.DupStrictToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_w.IsUIReady) return;

                _w.DupNameAsError = evt.newValue;
                _w._domain.SaveToolbarPrefs();
                UpdateDupToggleVisual();
                _w.MarkSeverityDirty();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
            });
            AddRight(_w.DupStrictToggle, 0, 6);

            _w.ResetBtn = new ToolbarButton(_w._domain.ResetToolbarAndList) { text = "Reset" };
            _w.ResetBtn.tooltip = "Reset rápido: limpia búsqueda, resetea orden y habilita todos los tipos.";
            _w.ResetBtn.style.width = 60;
            AddRight(_w.ResetBtn, 0, 6);

            _w.RefreshBtn = new ToolbarButton(() => _w.Refresh(RefreshFlags.Hard)) { text = "Refresh" };
            _w.RefreshBtn.tooltip = "Reescanea assets ItemData.";
            _w.RefreshBtn.style.width = 70;
            AddRight(_w.RefreshBtn, 0);

            UpdateDupToggleVisual();

            main.Add(_w.ToolbarLeft);
            main.Add(_w.ToolbarRight);
            toolbar.Add(main);

            return toolbar;
        }

        public void UpdateSortButtonText()
        {
            if (_w.SortBtn == null) return;

            _w.SortBtn.text = _w.CurrentSortMode switch
            {
                SortMode.NameAz => "A → Z",
                SortMode.NameZa => "Z → A",
                SortMode.Type => "Type",
                _ => "Sort"
            };
        }

        internal void UpdateTypeFilterButtonText()
        {
            if (_w.TypeFilterBtn == null) return;

            var total = _w.AllTypeEnumValues.Count;
            var enabled = _w.EnabledTypeIndices.Count;
            _w.TypeFilterBtn.text = total > 0 ? $"Type {enabled}/{total} ▾" : "Type ▾";
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
                itemsSource = _w.FilteredItems
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

                var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = 20;
                icon.style.height = 20;
                icon.style.marginRight = 8;
                row.Add(icon);

                var name = new Label();
                name.style.flexGrow = 1;
                name.style.flexShrink = 1;
                row.Add(name);

                var type = new Label();
                type.style.minWidth = 90;
                type.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(type);

                var indicator = new Label();
                indicator.style.width = 16;
                indicator.style.minWidth = 16;
                indicator.style.unityTextAlign = TextAnchor.MiddleCenter;
                indicator.style.marginLeft = 6;
                indicator.style.fontSize = 12;
                indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
                indicator.style.display = DisplayStyle.None;
                row.Add(indicator);

                row.userData = new ListRowRefs { Icon = icon, Name = name, Type = type, Indicator = indicator };
                return row;
            };

            list.bindItem = (element, index) =>
            {
                var refs = (ListRowRefs)element.userData;
                var item = _w.FilteredItems[index];

                refs.Icon.image = item != null && item.icon != null ? item.icon.texture : null;
                refs.Name.text = item != null ? item.itemNameID : "<null>";
                refs.Type.text = item != null ? item.itemType.ToString() : "";

                if (item == null || !_w.SeverityCache.TryGetValue(item, out var sev) || sev <= ValidationSeverity.Info)
                {
                    refs.Indicator.style.display = DisplayStyle.None;
                }
                else
                {
                    refs.Indicator.text = "!";
                    refs.Indicator.style.display = DisplayStyle.Flex;
                    refs.Indicator.style.color = sev == ValidationSeverity.Error ? ErrorColor : WarnColor;
                }
            };

            list.selectionChanged += selected =>
            {
                if (!_w.IsUIReady) return;
                _w.SelectItem(selected.FirstOrDefault() as ItemData);
            };

            list.itemsChosen += chosen =>
            {
                var item = chosen.FirstOrDefault() as ItemData;
                if (item != null) SelectAndPing(item);
            };

            return list;
        }

        public void ShowEmptyDetails()
        {
            _w.DetailsPanel.Clear();

            _w.HeaderIcon = null;
            _w.HeaderName = null;
            _w.HeaderType = null;
            _w.HeaderGuidValue = null;
            _w.HeaderSelectButton = null;
            _w.HeaderCopyGuidButton = null;

            _w.ValidationContainer = null;
            _w.ValidationBox = null;
            _w.FixTypeButton = null;

            _w.BasicIconPreview = null;
            _w.DimsGrid = null;
            _w.DimsGridOuter = null;
            _w.DimsOverflowLabel = null;

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

        public void ShowItemHeaderAndForm(ItemData item)
        {
            _w.DetailsPanel.Clear();

            _w.DetailsPanel.Add(BuildHeader(item));
            _w.DetailsPanel.Add(BuildValidationUI());
            _w.DetailsPanel.Add(BuildItemForm());
        }

        private VisualElement BuildHeader(ItemData item)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;

            _w.HeaderIcon = new Image();
            _w.HeaderIcon.style.width = 64;
            _w.HeaderIcon.style.height = 64;
            _w.HeaderIcon.style.marginRight = 10;
            _w.HeaderIcon.scaleMode = ScaleMode.ScaleToFit;

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

            _w.HeaderSelectButton = new Button(() => SelectAndPing(_w.Selected)) { text = "Select" };
            _w.HeaderSelectButton.style.height = 20;
            _w.HeaderSelectButton.style.paddingLeft = 8;
            _w.HeaderSelectButton.style.paddingRight = 8;

            _w.HeaderCopyGuidButton = new Button(() =>
                {
                    var g = GetGuid(_w.Selected);
                    if (!string.IsNullOrEmpty(g))
                    {
                        EditorGUIUtility.systemCopyBuffer = g;
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

            header.Add(_w.HeaderIcon);
            header.Add(textCol);
            header.Add(rightCol);

            RefreshHeader(item);

            header.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var w = evt.newRect.width;
                header.style.flexDirection = w < 520f ? FlexDirection.Column : FlexDirection.Row;
                rightCol.style.alignSelf = w < 520f ? Align.FlexStart : Align.Auto;
                rightCol.style.marginTop = w < 520f ? 6 : 0;
            });

            return header;
        }

        public void RefreshHeader(ItemData item)
        {
            if (item == null) return;

            if (_w.HeaderName != null) _w.HeaderName.text = item.itemNameID;
            if (_w.HeaderType != null) _w.HeaderType.text = item.itemType.ToString();

            if (_w.HeaderIcon != null)
            {
                if (item.icon != null)
                {
                    _w.HeaderIcon.image = item.icon.texture;
                    _w.HeaderIcon.style.opacity = 1f;
                }
                else
                {
                    _w.HeaderIcon.image = null;
                    _w.HeaderIcon.style.opacity = 0.25f;
                }
            }

            if (_w.HeaderGuidValue != null)
                _w.HeaderGuidValue.text = GetGuid(item);

            _w.HeaderSelectButton?.SetEnabled(true);
            _w.HeaderCopyGuidButton?.SetEnabled(true);
        }

        private VisualElement BuildValidationUI()
        {
            _w.ValidationContainer = new VisualElement();
            _w.ValidationContainer.style.marginTop = 4;
            _w.ValidationContainer.style.marginBottom = 8;

            _w.ValidationBox = new HelpBox("", HelpBoxMessageType.Info);
            _w.ValidationBox.style.display = DisplayStyle.None;

            _w.FixTypeButton = new Button(() =>
                {
                    if (_w.Selected == null) return;

                    var expected = _w._domain.ExpectedTypeForRuntimeType(_w.Selected.GetType());
                    if (expected.HasValue)
                    {
                        Undo.RecordObject(_w.Selected, "Fix Item Type");
                        _w.Selected.itemType = expected.Value;
                        EditorUtility.SetDirty(_w.Selected);
                        AssetDatabase.SaveAssets();

                        _w.Refresh(RefreshFlags.Details | RefreshFlags.Validations | RefreshFlags.List);
                    }
                })
                { text = "Fix Type" };

            _w.FixTypeButton.style.display = DisplayStyle.None;
            _w.FixTypeButton.style.height = 20;
            _w.FixTypeButton.style.marginTop = 6;

            _w.ValidationContainer.Add(_w.ValidationBox);
            _w.ValidationContainer.Add(_w.FixTypeButton);
            return _w.ValidationContainer;
        }

        private VisualElement BuildItemForm()
        {
            var outer = new VisualElement();
            outer.style.marginTop = 6;
            outer.style.paddingTop = 8;
            outer.style.paddingBottom = 8;

            if (_w.SelectedSo == null)
            {
                outer.Add(new Label("No hay item seleccionado."));
                return outer;
            }

            var itemTypeProp = _w.SelectedSo.FindProperty("itemType");
            var itemNameProp = _w.SelectedSo.FindProperty("itemNameID");
            var localizedNameProp = _w.SelectedSo.FindProperty("localizedDisplayName");
            var descriptionBlocksProp = _w.SelectedSo.FindProperty("descriptionBlocks");
            var iconProp = _w.SelectedSo.FindProperty("icon");
            var maxStackProp = _w.SelectedSo.FindProperty("maxStack");
            var dimsProp = _w.SelectedSo.FindProperty("SlotDimension");

            if (itemNameProp == null || itemTypeProp == null ||
                iconProp == null || maxStackProp == null || dimsProp == null)
            {
                outer.Add(new HelpBox(
                    "Alguna propiedad no se encontró en ItemData (itemNameID/itemType/icon/maxStack/SlotDimension).",
                    HelpBoxMessageType.Error));
                return outer;
            }

            outer.TrackSerializedObjectValue(_w.SelectedSo, _ =>
            {
                RefreshHeaderAndList();
                RefreshBasicIconPreview(iconProp);
                RefreshDimensionsPreviewFromProp(dimsProp);
                ScaleDimsGridToFit();

                var currentName = itemNameProp.stringValue ?? string.Empty;
                var currentType = int.MinValue;
                if (itemTypeProp != null) currentType = itemTypeProp.intValue;

                var nameChanged = !_w._selectedSnapshotInit ||
                                  !string.Equals(currentName, _w._selectedNameLast, StringComparison.Ordinal);
                var typeChanged = !_w._selectedSnapshotInit || currentType != _w._selectedTypeLast;

                _w._selectedSnapshotInit = true;
                _w._selectedNameLast = currentName;
                _w._selectedTypeLast = currentType;

                if (nameChanged)
                    _w.MarkNameIndexDirty();
                else
                    _w.MarkSeverityDirty();

                var flags = RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details;
                if (nameChanged || typeChanged)
                    flags |= RefreshFlags.Filter;

                _w.RequestRefreshDebounced(flags);
            });

            var basicFoldout = CreatePersistentFoldout("Basic", DefaultBasicOpen);
            basicFoldout.Add(BuildBasicSection(itemTypeProp, itemNameProp, localizedNameProp, descriptionBlocksProp, iconProp));
            outer.Add(basicFoldout);

            outer.Add(BuildSectionSpacer());

            var stackFoldout = CreatePersistentFoldout("Stack & Size", DefaultStackOpen);
            stackFoldout.Add(BuildStackAndSizeSection(maxStackProp, dimsProp));
            outer.Add(stackFoldout);

            outer.Add(BuildSectionSpacer());

            if (_w.Selected is EquipableItemData)
            {
                var equipFoldout = CreatePersistentFoldout("Equipable", true);
                equipFoldout.Add(BuildEquipableSection(_w.SelectedSo));
                outer.Add(equipFoldout);
                outer.Add(BuildSectionSpacer());
            }

            if (_w.Selected is ConsumableItemData)
            {
                var consumableFoldout = CreatePersistentFoldout("Consumable", true);
                consumableFoldout.Add(BuildConsumableSection(_w.SelectedSo));
                outer.Add(consumableFoldout);
                outer.Add(BuildSectionSpacer());
            }

            if (_w.Selected is WeaponData)
            {
                var weaponFoldout = CreatePersistentFoldout("Weapon", true);
                weaponFoldout.Add(BuildWeaponSection(_w.SelectedSo));
                outer.Add(weaponFoldout);
            }

            outer.Bind(_w.SelectedSo);

            RefreshBasicIconPreview(iconProp);
            RefreshDimensionsPreviewFromProp(dimsProp);
            ScaleDimsGridToFit();

            return outer;
        }

        private void RefreshHeaderAndList()
        {
            if (_w.Selected != null)
                RefreshHeader(_w.Selected);

            _w.ListView?.RefreshItems();
        }

        private VisualElement BuildBasicSection(
            SerializedProperty itemTypeProp,
            SerializedProperty itemNameProp,
            SerializedProperty localizedNameProp,
            SerializedProperty descriptionBlocksProp,
            SerializedProperty iconProp)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;

            var fieldsCol = new VisualElement();
            fieldsCol.style.flexDirection = FlexDirection.Column;
            fieldsCol.style.flexGrow = 1;
            fieldsCol.style.flexShrink = 1;
            fieldsCol.style.marginRight = 14;

            var previewCol = new VisualElement();
            previewCol.style.flexDirection = FlexDirection.Column;
            previewCol.style.flexShrink = 1;
            previewCol.style.minWidth = 120;

            AddRow(fieldsCol, Bind(new EnumField("Type"), itemTypeProp));

            AddRow(fieldsCol, Bind(new TextField("Name ID") { isDelayed = true }, itemNameProp));

            if (localizedNameProp != null)
                AddRow(fieldsCol, new PropertyField(localizedNameProp) { label = "Display Name" });

            if (descriptionBlocksProp != null)
                AddRow(fieldsCol, new PropertyField(descriptionBlocksProp) { label = "Description Blocks" });

            AddRow(fieldsCol, Bind(new ObjectField("Icon")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            }, iconProp));

            previewCol.Add(BuildPreviewCardHeader("Icon Preview"));
            previewCol.Add(BuildBasicIconPreviewCard());

            row.Add(fieldsCol);
            row.Add(previewCol);

            row.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var w = evt.newRect.width;
                var small = w < DetailsResponsiveBreak;
                row.style.flexDirection = small ? FlexDirection.Column : FlexDirection.Row;
                fieldsCol.style.marginRight = small ? 0 : 14;
                previewCol.style.marginTop = small ? 8 : 0;
            });

            return row;
        }

        private VisualElement BuildStackAndSizeSection(SerializedProperty maxStackProp, SerializedProperty dimsProp)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;

            var fieldsCol = new VisualElement();
            fieldsCol.style.flexDirection = FlexDirection.Column;
            fieldsCol.style.flexGrow = 1;
            fieldsCol.style.flexShrink = 1;
            fieldsCol.style.marginRight = 14;

            var previewCol = new VisualElement();
            previewCol.style.flexDirection = FlexDirection.Column;
            previewCol.style.flexShrink = 1;
            previewCol.style.minWidth = 120;

            var maxStackField = new IntegerField("Max Stack");
            maxStackField.BindProperty(maxStackProp);
            maxStackField.RegisterValueChangedCallback(evt =>
                ClampIntProperty(maxStackField, maxStackProp, evt.newValue, 1, int.MaxValue));
            AddRow(fieldsCol, maxStackField);

            fieldsCol.Add(BuildDimensionsFieldsOnly(dimsProp));

            previewCol.Add(BuildPreviewCardHeader("Dimensions Preview"));
            previewCol.Add(BuildDimensionsPreviewCard(dimsProp));

            row.Add(fieldsCol);
            row.Add(previewCol);

            row.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var w = evt.newRect.width;
                var small = w < DetailsResponsiveBreak;
                row.style.flexDirection = small ? FlexDirection.Column : FlexDirection.Row;
                fieldsCol.style.marginRight = small ? 0 : 14;
                previewCol.style.marginTop = small ? 8 : 0;
                ScaleDimsGridToFit();
            });

            return row;
        }

        private void ClampIntProperty(IntegerField field, SerializedProperty prop, int newValue, int min, int max)
        {
            if (_w.SelectedSo == null || prop == null) return;

            var clamped = Mathf.Clamp(newValue, min, max);
            if (clamped == newValue) return;

            _w.SelectedSo.Update();
            prop.intValue = clamped;
            _w.SelectedSo.ApplyModifiedProperties();

            field.SetValueWithoutNotify(clamped);
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void ClampFloatProperty(FloatField field, SerializedProperty prop, float newValue, float min, float max)
        {
            if (_w.SelectedSo == null || prop == null) return;

            var clamped = Mathf.Clamp(newValue, min, max);
            if (Mathf.Approximately(clamped, newValue)) return;

            _w.SelectedSo.Update();
            prop.floatValue = clamped;
            _w.SelectedSo.ApplyModifiedProperties();

            field.SetValueWithoutNotify(clamped);
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private Foldout CreatePersistentFoldout(string sectionName, bool defaultOpen)
        {
            var foldout = new Foldout { text = sectionName };
            var key = GetFoldoutKey(sectionName);
            foldout.value = EditorPrefs.GetBool(key, defaultOpen);
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(key, evt.newValue));
            return foldout;
        }

        private string GetFoldoutKey(string sectionName)
        {
            if (_w.Selected == null)
                return $"BS.ItemBrowser.Foldout.Global.{sectionName}";

            return $"BS.ItemBrowser.Foldout.{GetGuid(_w.Selected)}.{sectionName}";
        }

        private static VisualElement BuildSectionSpacer()
        {
            var s = new VisualElement();
            s.style.height = SectionGap;
            return s;
        }

        private static void AddRow(VisualElement parent, VisualElement field)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = RowGap;
            row.Add(field);
            parent.Add(row);
        }

        private static VisualElement BuildPreviewCardHeader(string text)
        {
            var h = new Label(text);
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.opacity = 0.85f;
            h.style.marginBottom = 6;
            return h;
        }

        private static VisualElement BuildCardBase()
        {
            var card = new VisualElement();
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;

            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;

            var border = new Color(0f, 0f, 0f, 0.25f);
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;

            return card;
        }

        private static T Bind<T>(T field, SerializedProperty prop) where T : VisualElement
        {
            switch (field)
            {
                case TextField tf: tf.BindProperty(prop); break;
                case IntegerField inf: inf.BindProperty(prop); break;
                case EnumField ef: ef.BindProperty(prop); break;
                case ObjectField of: of.BindProperty(prop); break;
                case PropertyField pf: pf.BindProperty(prop); break;
            }

            return field;
        }

        private VisualElement BuildBasicIconPreviewCard()
        {
            var card = BuildCardBase();

            _w.BasicIconPreview = new Image();
            _w.BasicIconPreview.style.width = 96;
            _w.BasicIconPreview.style.height = 96;
            _w.BasicIconPreview.style.alignSelf = Align.Center;
            _w.BasicIconPreview.style.backgroundColor = new Color(0, 0, 0, 0.12f);
            _w.BasicIconPreview.scaleMode = ScaleMode.ScaleToFit;

            card.Add(_w.BasicIconPreview);
            return card;
        }

        private void RefreshBasicIconPreview(SerializedProperty iconProp)
        {
            if (_w.BasicIconPreview == null || iconProp == null) return;

            var sprite = iconProp.objectReferenceValue as Sprite;
            _w.BasicIconPreview.image = sprite != null ? sprite.texture : null;
            _w.BasicIconPreview.style.opacity = sprite != null ? 1f : 0.25f;
        }

        private VisualElement BuildDimensionsFieldsOnly(SerializedProperty dimsProp)
        {
            var widthProp = dimsProp.FindPropertyRelative("Width");
            var heightProp = dimsProp.FindPropertyRelative("Height");

            var block = new VisualElement();
            block.style.marginTop = 6;

            var label = new Label("Slot Dimensions");
            label.style.opacity = 0.7f;
            label.style.marginBottom = 6;
            block.Add(label);

            if (widthProp == null || heightProp == null)
            {
                block.Add(new HelpBox("No se encontraron Width/Height dentro de SlotDimension.",
                    HelpBoxMessageType.Error));
                return block;
            }

            var wField = new IntegerField("W");
            wField.BindProperty(widthProp);
            wField.RegisterValueChangedCallback(evt =>
                ClampIntProperty(wField, widthProp, evt.newValue, 1, int.MaxValue));

            var spacer = new VisualElement { style = { height = 6 } };

            var hField = new IntegerField("H");
            hField.BindProperty(heightProp);
            hField.RegisterValueChangedCallback(evt =>
                ClampIntProperty(hField, heightProp, evt.newValue, 1, int.MaxValue));

            AddRow(block, wField);
            block.Add(spacer);
            AddRow(block, hField);

            return block;
        }

        private VisualElement BuildDimensionsPreviewCard(SerializedProperty dimsProp)
        {
            var widthProp = dimsProp.FindPropertyRelative("Width");
            var heightProp = dimsProp.FindPropertyRelative("Height");

            var card = BuildCardBase();

            _w.DimsGridOuter = new VisualElement();
            _w.DimsGridOuter.style.alignSelf = Align.Center;
            _w.DimsGridOuter.style.paddingLeft = 6;
            _w.DimsGridOuter.style.paddingRight = 6;
            _w.DimsGridOuter.style.paddingTop = 6;
            _w.DimsGridOuter.style.paddingBottom = 6;
            _w.DimsGridOuter.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

            _w.DimsGridOuter.style.borderTopWidth = 1;
            _w.DimsGridOuter.style.borderBottomWidth = 1;
            _w.DimsGridOuter.style.borderLeftWidth = 1;
            _w.DimsGridOuter.style.borderRightWidth = 1;
            _w.DimsGridOuter.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);
            _w.DimsGridOuter.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
            _w.DimsGridOuter.style.borderLeftColor = new Color(0f, 0f, 0f, 0.35f);
            _w.DimsGridOuter.style.borderRightColor = new Color(0f, 0f, 0f, 0.35f);

            _w.DimsGrid = new VisualElement();
            _w.DimsGrid.style.flexDirection = FlexDirection.Row;
            _w.DimsGrid.style.flexWrap = Wrap.Wrap;

            _w.DimsGridOuter.Add(_w.DimsGrid);
            card.Add(_w.DimsGridOuter);

            _w.DimsOverflowLabel = new Label();
            _w.DimsOverflowLabel.style.opacity = 0.6f;
            _w.DimsOverflowLabel.style.marginTop = 6;
            _w.DimsOverflowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(_w.DimsOverflowLabel);

            if (widthProp != null && heightProp != null)
            {
                card.TrackPropertyValue(widthProp, _ =>
                {
                    RefreshDimensionsPreview(widthProp.intValue, heightProp.intValue);
                    ScaleDimsGridToFit();
                });
                card.TrackPropertyValue(heightProp, _ =>
                {
                    RefreshDimensionsPreview(widthProp.intValue, heightProp.intValue);
                    ScaleDimsGridToFit();
                });
                RefreshDimensionsPreview(widthProp.intValue, heightProp.intValue);
            }

            card.RegisterCallback<GeometryChangedEvent>(_ => ScaleDimsGridToFit());
            return card;
        }

        private void RefreshDimensionsPreviewFromProp(SerializedProperty dimsProp)
        {
            if (_w.DimsGrid == null || dimsProp == null) return;

            var wProp = dimsProp.FindPropertyRelative("Width");
            var hProp = dimsProp.FindPropertyRelative("Height");
            if (wProp == null || hProp == null) return;

            RefreshDimensionsPreview(wProp.intValue, hProp.intValue);
        }

        private void RefreshDimensionsPreview(int w, int h)
        {
            if (_w.DimsGrid == null) return;

            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);

            var gridW = Mathf.Max(3, w);
            var gridH = Mathf.Max(3, h);

            var drawW = Mathf.Min(gridW, PreviewMaxCellsPerAxis);
            var drawH = Mathf.Min(gridH, PreviewMaxCellsPerAxis);

            var occW = Mathf.Min(w, drawW);
            var occH = Mathf.Min(h, drawH);

            BuildGridCells(_w.DimsGrid, drawW, drawH, occW, occH);

            if (_w.DimsOverflowLabel != null)
            {
                var clipped = gridW > PreviewMaxCellsPerAxis || gridH > PreviewMaxCellsPerAxis;
                _w.DimsOverflowLabel.text = clipped
                    ? $"Mostrando {drawW}×{drawH} (mín 3×3) de {gridW}×{gridH} (item {w}×{h})"
                    : $"Grid {gridW}×{gridH} (item {w}×{h})";
            }

            ScaleDimsGridToFit();
        }

        private static void BuildGridCells(VisualElement grid, int drawW, int drawH, int occW, int occH)
        {
            grid.Clear();
            grid.style.width = drawW * GridCellSize;
            grid.style.height = drawH * GridCellSize;

            var emptyColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            var filledColor = Color.white;
            var border = new Color(0f, 0f, 0f, 0.25f);

            for (var y = 0; y < drawH; y++)
            for (var x = 0; x < drawW; x++)
            {
                var filled = x < occW && y < occH;

                var cell = new VisualElement();
                cell.style.width = GridCellSize;
                cell.style.height = GridCellSize;
                cell.style.backgroundColor = filled ? filledColor : emptyColor;

                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;
                cell.style.borderTopWidth = 1;
                cell.style.borderBottomWidth = 1;

                cell.style.borderLeftColor = border;
                cell.style.borderRightColor = border;
                cell.style.borderTopColor = border;
                cell.style.borderBottomColor = border;

                grid.Add(cell);
            }
        }

        public void ScaleDimsGridToFit()
        {
            if (_w.DimsGrid == null || _w.DimsGridOuter == null) return;

            var outerW = _w.DimsGridOuter.resolvedStyle.width;
            var gridW = _w.DimsGrid.resolvedStyle.width;

            if (outerW <= 1f || gridW <= 1f)
            {
                _w.DimsGridOuter.schedule.Execute(ScaleDimsGridToFit).ExecuteLater(0);
                return;
            }

            var padding = _w.DimsGridOuter.resolvedStyle.paddingLeft + _w.DimsGridOuter.resolvedStyle.paddingRight;
            var maxAllowed = Mathf.Max(1f, outerW - padding);

            var scale = Mathf.Min(1f, maxAllowed / gridW);
            _w.DimsGrid.style.scale = new Vector3(scale, scale, 1f);
            _w.DimsGrid.style.transformOrigin = new TransformOrigin(0.5f, 0.5f, 0f);
        }

        private static void DisableInternalScroll(TextField field)
        {
            field.schedule.Execute(() =>
            {
                var scrollView = field.Q<ScrollView>();
                if (scrollView == null) return;

                scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

                scrollView.style.overflow = Overflow.Hidden;
                scrollView.contentContainer.style.overflow = Overflow.Visible;
            }).ExecuteLater(0);
        }

        private static void MakeTextFieldAutoHeight(TextField field, float minHeight)
        {
            field.multiline = true;
            field.RegisterValueChangedCallback(_ => ScheduleAutoHeightRecalc(field, minHeight));
            field.RegisterCallback<GeometryChangedEvent>(_ => ScheduleAutoHeightRecalc(field, minHeight));
            ScheduleAutoHeightRecalc(field, minHeight);
        }

        private static void ScheduleAutoHeightRecalc(TextField field, float minHeight)
        {
            const float hugeMax = 100000f;

            field.schedule.Execute(() =>
            {
                var input = field.Q<TextElement>("unity-text-input");
                if (input == null) return;

                var width = input.resolvedStyle.width;
                if (width <= 0.01f) return;

                var text = field.value ?? string.Empty;

                var measured = input.MeasureTextSize(
                    text.Length == 0 ? " " : text,
                    width,
                    VisualElement.MeasureMode.AtMost,
                    0,
                    VisualElement.MeasureMode.Undefined
                );

                var padding = input.resolvedStyle.paddingTop + input.resolvedStyle.paddingBottom;
                var extra = 10f;

                var target = Mathf.Clamp(measured.y + padding + extra, minHeight, hugeMax);
                field.style.height = target;
            }).ExecuteLater(0);
        }

        private static Label MakeFlexibleHeaderLabel(string text, int marginRight = 0)
        {
            var label = new Label(text);
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            label.style.marginRight = marginRight;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.75f;
            return label;
        }

        private static Label MakeFixedHeaderLabel(string text, float width, int marginRight = 0)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.marginRight = marginRight;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.75f;
            return label;
        }

        private static void BindOrDisable(VisualElement field, SerializedProperty prop, string tooltip)
        {
            switch (field)
            {
                case EnumField ef:
                    if (prop != null) ef.BindProperty(prop);
                    else { ef.SetEnabled(false); ef.tooltip = tooltip; }
                    break;

                case FloatField ff:
                    if (prop != null) ff.BindProperty(prop);
                    else { ff.SetEnabled(false); ff.tooltip = tooltip; }
                    break;

                case Toggle tg:
                    if (prop != null) tg.BindProperty(prop);
                    else { tg.SetEnabled(false); tg.tooltip = tooltip; }
                    break;
            }
        }

        private sealed class ListRowRefs
        {
            public Image Icon;
            public Label Indicator;
            public Label Name;
            public Label Type;
        }

        private sealed class StatRowBinding
        {
            public int ArrayIndex;
            public Action ClickHandler;
        }

        private sealed class BuffRowBinding
        {
            public int ArrayIndex;
            public Action ClickHandler;
        }
    }
}
#endif