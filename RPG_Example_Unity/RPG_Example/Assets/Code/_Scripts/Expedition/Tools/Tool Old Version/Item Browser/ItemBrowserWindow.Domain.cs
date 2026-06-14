#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ItemBrowserWindow
{
    private sealed class Domain
    {
        private readonly ItemBrowserWindow _w;

        public Domain(ItemBrowserWindow w)
        {
            _w = w;
        }

        public void LoadToolbarPrefsEarly()
        {
            _w.SearchField?.SetValueWithoutNotify(EditorPrefs.GetString(PrefSearch, string.Empty));

            var savedSort = EditorPrefs.GetInt(PrefSortMode, (int)SortMode.NameAz);
            var max = Enum.GetValues(typeof(SortMode)).Length - 1;
            _w.CurrentSortMode = (SortMode)Mathf.Clamp(savedSort, 0, max);
            _w._ui.UpdateSortButtonText();

            _w.DupNameAsError = EditorPrefs.GetBool(PrefDupNameAsError, false);
            _w.DupStrictToggle?.SetValueWithoutNotify(_w.DupNameAsError);
            _w._ui.UpdateDupToggleVisual();
        }

        public void SaveToolbarPrefs()
        {
            if (_w.SearchField != null)
                EditorPrefs.SetString(PrefSearch, _w.SearchField.value ?? string.Empty);

            EditorPrefs.SetInt(PrefSortMode, (int)_w.CurrentSortMode);
            EditorPrefs.SetBool(PrefDupNameAsError, _w.DupNameAsError);

            SaveTypeFilterMask();
        }

        public void CycleSortMode()
        {
            if (!_w.IsUIReady) return;

            _w.CurrentSortMode = _w.CurrentSortMode switch
            {
                SortMode.NameAz => SortMode.NameZa,
                SortMode.NameZa => SortMode.Type,
                _ => SortMode.NameAz
            };

            _w._ui.UpdateSortButtonText();
            SaveToolbarPrefs();
            _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
        }

        public void ResetToolbarAndList()
        {
            if (!_w.IsUIReady) return;

            _w.SearchField?.SetValueWithoutNotify(string.Empty);
            EnableAllTypes();

            _w.CurrentSortMode = SortMode.NameAz;
            _w._ui.UpdateSortButtonText();

            _w.DupNameAsError = false;
            _w.DupStrictToggle?.SetValueWithoutNotify(false);
            _w._ui.UpdateDupToggleVisual();

            SaveToolbarPrefs();
            _w.Refresh(RefreshFlags.Soft);
        }

        public void ShowTypeFilterMenu()
        {
            if (!_w.IsUIReady) return;

            if (_w.ItemTypeEnumSystemType == null || _w.AllTypeEnumValues.Count == 0)
            {
                var menuEmpty = new GenericMenu();
                menuEmpty.AddDisabledItem(new GUIContent("No types found"));
                menuEmpty.ShowAsContext();
                return;
            }

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Select All"), false, () =>
            {
                EnableAllTypes();
                SaveToolbarPrefs();
                _w._ui.UpdateTypeFilterButtonText();
                _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
            });

            menu.AddItem(new GUIContent("Select None"), false, () =>
            {
                _w.EnabledTypeIndices.Clear();
                SaveToolbarPrefs();
                _w._ui.UpdateTypeFilterButtonText();
                _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
            });

            menu.AddSeparator("");

            for (var i = 0; i < _w.AllTypeEnumValues.Count; i++)
            {
                var idx = i;
                var isOn = _w.EnabledTypeIndices.Contains(idx);
                var label = _w.AllTypeEnumValues[idx].ToString();

                menu.AddItem(new GUIContent(label), isOn, () =>
                {
                    ToggleTypeIndex(idx);
                    SaveToolbarPrefs();
                    _w._ui.UpdateTypeFilterButtonText();
                    _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
                });
            }

            menu.ShowAsContext();
        }

        private void ToggleTypeIndex(int idx)
        {
            if (_w.AllTypeEnumValues.Count == 0) return;

            if (_w.EnabledTypeIndices.Contains(idx))
                _w.EnabledTypeIndices.Remove(idx);
            else
                _w.EnabledTypeIndices.Add(idx);
        }

        private void EnableAllTypes()
        {
            _w.EnabledTypeIndices.Clear();
            for (var i = 0; i < _w.AllTypeEnumValues.Count; i++)
                _w.EnabledTypeIndices.Add(i);

            _w._ui?.UpdateTypeFilterButtonText();
        }

        private void LoadTypeFilterMaskOrDefault()
        {
            _w.EnabledTypeIndices.Clear();

            if (_w.AllTypeEnumValues.Count == 0)
                return;

            if (EditorPrefs.HasKey(PrefTypeFilterMask))
            {
                var raw = EditorPrefs.GetString(PrefTypeFilterMask, string.Empty);
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask))
                {
                    for (var i = 0; i < _w.AllTypeEnumValues.Count; i++)
                    {
                        if ((mask & (1L << i)) != 0)
                            _w.EnabledTypeIndices.Add(i);
                    }

                    return;
                }
            }

            EnableAllTypes();
        }

        private void SaveTypeFilterMask()
        {
            if (_w.AllTypeEnumValues.Count == 0)
                return;

            var mask = 0L;
            foreach (var idx in _w.EnabledTypeIndices)
            {
                if (idx < 0 || idx >= _w.AllTypeEnumValues.Count) continue;
                mask |= 1L << idx;
            }

            EditorPrefs.SetString(PrefTypeFilterMask, mask.ToString(CultureInfo.InvariantCulture));
        }

        public void RefreshAssetsInternal()
        {
            _w.AllItems.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (asset != null) _w.AllItems.Add(asset);
            }

            SetupItemTypeEnum();
            _w.MarkNameIndexDirty();
        }

        private void SetupItemTypeEnum()
        {
            var sample = _w.AllItems.FirstOrDefault(i => i != null);
            _w.ItemTypeEnumSystemType = sample != null ? sample.itemType.GetType() : null;

            _w.AllTypeEnumValues.Clear();
            _w.TypeValueToIndex.Clear();

            if (_w.ItemTypeEnumSystemType == null)
                return;

            var values = Enum.GetValues(_w.ItemTypeEnumSystemType);
            if (values == null || values.Length == 0)
                return;

            for (var i = 0; i < values.Length; i++)
            {
                var e = (Enum)values.GetValue(i);
                _w.AllTypeEnumValues.Add(e);
                _w.TypeValueToIndex[Convert.ToInt32(e)] = i;
            }

            LoadTypeFilterMaskOrDefault();
            _w._ui?.UpdateTypeFilterButtonText();
        }

        private void RebuildNameIndex()
        {
            _w.NameIndex.Clear();

            foreach (var item in _w.AllItems)
            {
                if (item == null) continue;

                var key = NormalizeName(item.itemNameID);
                if (string.IsNullOrEmpty(key)) continue;

                if (!_w.NameIndex.TryGetValue(key, out var list))
                {
                    list = new List<ItemData>();
                    _w.NameIndex[key] = list;
                }

                list.Add(item);
            }
        }

        public void RefilterInternal()
        {
            var prev = _w.Selected;
            var q = _w.SearchField != null ? _w.SearchField.value ?? string.Empty : string.Empty;

            var query = _w.AllItems.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLowerInvariant();
                query = query.Where(i =>
                    (!string.IsNullOrEmpty(i.itemNameID) && i.itemNameID.ToLowerInvariant().Contains(s)) ||
                    i.itemType.ToString().ToLowerInvariant().Contains(s) ||
                    i.name.ToLowerInvariant().Contains(s));
            }

            if (_w.AllTypeEnumValues.Count > 0)
            {
                if (_w.EnabledTypeIndices.Count == 0)
                    query = Enumerable.Empty<ItemData>();
                else
                {
                    query = query.Where(i =>
                    {
                        var idx = GetTypeIndexFast(i.itemType);
                        return idx >= 0 && _w.EnabledTypeIndices.Contains(idx);
                    });
                }
            }

            query = ApplySort(query);

            _w.FilteredItems.Clear();
            _w.FilteredItems.AddRange(query);

            _w.ListView?.RefreshItems();

            if (prev != null && _w.FilteredItems.Contains(prev))
            {
                _w.ListView.SetSelection(_w.FilteredItems.IndexOf(prev));
            }
            else
            {
                _w.Selected = null;
                _w.SelectedSo = null;
                _w.ListView?.ClearSelection();
                _w._ui.ShowEmptyDetails();
            }
        }

        private int GetTypeIndexFast(object enumValue)
        {
            if (enumValue == null) return -1;
            try
            {
                var v = enumValue is Enum en ? Convert.ToInt32(en) : Convert.ToInt32(enumValue);
                return _w.TypeValueToIndex.TryGetValue(v, out var idx) ? idx : -1;
            }
            catch
            {
                return -1;
            }
        }

        private IEnumerable<ItemData> ApplySort(IEnumerable<ItemData> src)
        {
            static string NameKey(ItemData i)
            {
                return string.IsNullOrEmpty(i.itemNameID) ? i.name : i.itemNameID;
            }

            return _w.CurrentSortMode switch
            {
                SortMode.NameAz => src.OrderBy(NameKey, StringComparer.OrdinalIgnoreCase),
                SortMode.NameZa => src.OrderByDescending(NameKey, StringComparer.OrdinalIgnoreCase),
                _ => src.OrderBy(i => i.itemType.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(NameKey, StringComparer.OrdinalIgnoreCase)
            };
        }

        public void UpdateValidationsInternal()
        {
            if (_w.NameIndexDirty)
            {
                RebuildNameIndex();
                _w.NameIndexDirty = false;
            }

            if (_w.SeverityCacheDirty)
            {
                _w.SeverityCache.Clear();
                foreach (var item in _w.AllItems)
                {
                    if (item == null) continue;
                    _w.SeverityCache[item] = GetSeverityForItemFast(item);
                }

                _w.SeverityCacheDirty = false;
                _w.ListView?.RefreshItems();
            }

            if (_w.Selected != null)
            {
                ApplyValidationForSelectedFast(_w.Selected);
            }
            else
            {
                if (_w.ValidationBox != null) _w.ValidationBox.style.display = DisplayStyle.None;
                if (_w.FixTypeButton != null) _w.FixTypeButton.style.display = DisplayStyle.None;
            }
        }

        private ValidationSeverity GetSeverityForItemFast(ItemData item)
        {
            if (item == null) return ValidationSeverity.None;

            var severity = ValidationSeverity.None;

            if (string.IsNullOrWhiteSpace(item.itemNameID))
                severity = MaxSeverity(severity, ValidationSeverity.Error);

            if (item.icon == null)
                severity = MaxSeverity(severity, ValidationSeverity.Error);

            if (item.maxStack < 1)
                severity = MaxSeverity(severity, ValidationSeverity.Error);

            if (item.SlotDimension.Width < 1 || item.SlotDimension.Height < 1)
                severity = MaxSeverity(severity, ValidationSeverity.Error);

            if (!string.IsNullOrWhiteSpace(item.itemNameID))
            {
                var key = NormalizeName(item.itemNameID);
                if (!string.IsNullOrEmpty(key) && _w.NameIndex.TryGetValue(key, out var list))
                {
                    var others = list.Count(x => x != null && x != item);
                    if (others > 0)
                    {
                        severity = MaxSeverity(
                            severity,
                            _w.DupNameAsError ? ValidationSeverity.Error : ValidationSeverity.Warning);
                    }
                }
            }

            var expected = ExpectedTypeForRuntimeType(item.GetType());
            if (expected.HasValue && item.itemType != expected.Value)
                severity = MaxSeverity(severity, ValidationSeverity.Error);

            if (item is ConsumableItemData consumable)
            {
                if (consumable.buffs == null || consumable.buffs.Count == 0)
                {
                    severity = MaxSeverity(severity, ValidationSeverity.Warning);
                }
                else
                {
                    foreach (var buff in consumable.buffs)
                    {
                        if (buff == null) continue;

                        if (buff.duration < 0f)
                        {
                            severity = MaxSeverity(severity, ValidationSeverity.Error);
                            break;
                        }
                    }
                }
            }

            if (item is WeaponData weapon)
            {
                if (weapon.combos == null || weapon.combos.Count == 0)
                    severity = MaxSeverity(severity, ValidationSeverity.Warning);

                if (weapon.weaponSkill == null)
                    severity = MaxSeverity(severity, ValidationSeverity.Warning);

                if (weapon.skillScoreNeeded < 0f)
                    severity = MaxSeverity(severity, ValidationSeverity.Error);

                if (weapon.combos != null)
                {
                    foreach (var combo in weapon.combos)
                    {
                        if (combo == null || combo.steps == null || combo.steps.Count == 0)
                        {
                            severity = MaxSeverity(severity, ValidationSeverity.Warning);
                            continue;
                        }

                        foreach (var step in combo.steps)
                        {
                            if (step == null || step.attack == null)
                            {
                                severity = MaxSeverity(severity, ValidationSeverity.Warning);
                                break;
                            }
                        }
                    }
                }
            }

            return severity;
        }

        private void ApplyValidationForSelectedFast(ItemData item)
        {
            if (_w.ValidationBox == null || _w.FixTypeButton == null) return;

            var messages = new List<(ValidationSeverity sev, string msg)>();

            if (string.IsNullOrWhiteSpace(item.itemNameID))
                messages.Add((ValidationSeverity.Error, "Name es obligatorio."));

            if (item.icon == null)
                messages.Add((ValidationSeverity.Error, "Icon es obligatorio."));

            if (item.maxStack < 1)
                messages.Add((ValidationSeverity.Error, "Max Stack debe ser >= 1."));

            if (item.SlotDimension.Width < 1)
                messages.Add((ValidationSeverity.Error, "Dimensions.Width debe ser >= 1."));

            if (item.SlotDimension.Height < 1)
                messages.Add((ValidationSeverity.Error, "Dimensions.Height debe ser >= 1."));

            if (!string.IsNullOrWhiteSpace(item.itemNameID))
            {
                var key = NormalizeName(item.itemNameID);
                if (!string.IsNullOrEmpty(key) && _w.NameIndex.TryGetValue(key, out var list))
                {
                    var others = list.Where(x => x != null && x != item).ToList();
                    if (others.Count > 0)
                    {
                        var sev = _w.DupNameAsError ? ValidationSeverity.Error : ValidationSeverity.Warning;
                        var sample = string.Join(", ", others.Take(5).Select(o => o.name));
                        if (others.Count > 5) sample += ", ...";

                        messages.Add((sev,
                            $"itemName duplicado: \"{item.itemNameID.Trim()}\". Duplicados: {others.Count} ({sample})"));
                    }
                }
            }

            var expected = ExpectedTypeForRuntimeType(item.GetType());
            var typeMismatch = expected.HasValue && !Equals(item.itemType, expected.Value);
            if (typeMismatch)
            {
                messages.Add((ValidationSeverity.Error,
                    $"itemType NO coincide con el subtipo ({item.GetType().Name}). Esperado: {expected.Value}, actual: {item.itemType}."));
            }

            if (item is ConsumableItemData consumable)
            {
                if (consumable.buffs == null || consumable.buffs.Count == 0)
                {
                    messages.Add((ValidationSeverity.Warning, "Consumable sin buffs configurados."));
                }
                else
                {
                    for (var i = 0; i < consumable.buffs.Count; i++)
                    {
                        var buff = consumable.buffs[i];
                        if (buff == null) continue;

                        if (buff.duration < 0f)
                            messages.Add((ValidationSeverity.Error, $"Buff #{i} tiene duration < 0."));
                    }
                }
            }

            if (item is WeaponData weapon)
            {
                if (weapon.combos == null || weapon.combos.Count == 0)
                    messages.Add((ValidationSeverity.Warning, "Weapon sin combos configurados."));

                if (weapon.weaponSkill == null)
                    messages.Add((ValidationSeverity.Warning, "Weapon sin Weapon Skill asignado."));

                if (weapon.skillScoreNeeded < 0f)
                    messages.Add((ValidationSeverity.Error, "Skill Score Needed debe ser >= 0."));

                if (weapon.combos != null)
                {
                    for (var i = 0; i < weapon.combos.Count; i++)
                    {
                        var combo = weapon.combos[i];
                        if (combo == null || combo.steps == null || combo.steps.Count == 0)
                        {
                            messages.Add((ValidationSeverity.Warning, $"Combo #{i} no tiene steps."));
                            continue;
                        }

                        for (var j = 0; j < combo.steps.Count; j++)
                        {
                            var step = combo.steps[j];
                            if (step == null || step.attack == null)
                                messages.Add((ValidationSeverity.Warning, $"Combo #{i}, Step #{j} sin AttackData asignado."));
                        }
                    }
                }
            }

            if (messages.Count == 0)
            {
                _w.ValidationBox.style.display = DisplayStyle.None;
                _w.FixTypeButton.style.display = DisplayStyle.None;
                return;
            }

            var highest = messages.Max(m => m.sev);
            _w.ValidationBox.messageType = highest switch
            {
                ValidationSeverity.Error => HelpBoxMessageType.Error,
                ValidationSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };

            _w.ValidationBox.text = "Validations:\n• " + string.Join("\n• ", messages.Select(m => m.msg));
            _w.ValidationBox.style.display = DisplayStyle.Flex;
            _w.FixTypeButton.style.display = typeMismatch ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static ValidationSeverity MaxSeverity(ValidationSeverity a, ValidationSeverity b)
        {
            return (int)a >= (int)b ? a : b;
        }

        private static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "" : name.Trim().ToLowerInvariant();
        }

        public Enums.ItemType? ExpectedTypeForRuntimeType(Type t)
        {
            if (t == null) return null;

            if (typeof(EquipableItemData).IsAssignableFrom(t)) return Enums.ItemType.Equipable;
            if (typeof(ConsumableItemData).IsAssignableFrom(t)) return Enums.ItemType.Consumable;
            if (typeof(CraftingItemData).IsAssignableFrom(t)) return Enums.ItemType.Crafting;
            if (typeof(CollectableItemData).IsAssignableFrom(t)) return Enums.ItemType.Collectable;

            return null;
        }

        public void ShowCreateMenu()
        {
            var types = GetCreatableItemTypes().ToList();
            var menu = new GenericMenu();

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No creatable ItemData types found"));
                menu.ShowAsContext();
                return;
            }

            types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var t in types)
            {
                var nicified = ObjectNames.NicifyVariableName(t.Name);
                menu.AddItem(new GUIContent(nicified), false, () => CreateNewItemAsset(t));
            }

            menu.ShowAsContext();
        }

        private static IEnumerable<Type> GetCreatableItemTypes()
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<ItemData>())
            {
                if (t == null) continue;
                if (!typeof(ScriptableObject).IsAssignableFrom(t)) continue;
                if (t.IsAbstract) continue;
                if (t.IsGenericType) continue;
                yield return t;
            }
        }

        private void CreateNewItemAsset(Type concreteType)
        {
            if (concreteType == null) return;

            if (!typeof(ItemData).IsAssignableFrom(concreteType))
            {
                EditorUtility.DisplayDialog("Error", $"Type {concreteType.Name} is not an ItemData.", "OK");
                return;
            }

            var suggestedName = ObjectNames.NicifyVariableName(concreteType.Name).Replace(" ", "");
            if (string.IsNullOrWhiteSpace(suggestedName)) suggestedName = "NewItem";

            var defaultFolder = EditorPrefs.GetString(PrefLastCreateFolder, "Assets");
            if (!AssetDatabase.IsValidFolder(defaultFolder))
                defaultFolder = "Assets";

            var assetPath = EditorUtility.SaveFilePanelInProject(
                $"Create {concreteType.Name}",
                suggestedName,
                "asset",
                "Elige nombre y carpeta para el nuevo Item.",
                defaultFolder
            );

            if (string.IsNullOrEmpty(assetPath))
                return;

            var folder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                EditorPrefs.SetString(PrefLastCreateFolder, folder);

            var obj = CreateInstance(concreteType);
            if (obj == null)
            {
                EditorUtility.DisplayDialog("Error", $"No se pudo crear instancia de {concreteType.Name}.", "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(obj, "Create Item Asset");

            if (obj is ItemData item)
            {
                AssignTypeOnCreate(item, concreteType);

                if (string.IsNullOrWhiteSpace(item.itemNameID))
                    item.itemNameID = Path.GetFileNameWithoutExtension(assetPath);

                EditorUtility.SetDirty(item);
            }

            AssetDatabase.CreateAsset(obj, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _w.Refresh(RefreshFlags.Hard);

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            if (obj is ItemData created)
                _w.SelectItem(created);
        }

        private void AssignTypeOnCreate(ItemData item, Type concreteType)
        {
            if (item == null || concreteType == null) return;

            if (typeof(EquipableItemData).IsAssignableFrom(concreteType))
                item.itemType = Enums.ItemType.Equipable;
            else if (typeof(ConsumableItemData).IsAssignableFrom(concreteType))
                item.itemType = Enums.ItemType.Consumable;
            else if (typeof(CraftingItemData).IsAssignableFrom(concreteType))
                item.itemType = Enums.ItemType.Crafting;
            else if (typeof(CollectableItemData).IsAssignableFrom(concreteType))
                item.itemType = Enums.ItemType.Collectable;
        }
    }
}
#endif