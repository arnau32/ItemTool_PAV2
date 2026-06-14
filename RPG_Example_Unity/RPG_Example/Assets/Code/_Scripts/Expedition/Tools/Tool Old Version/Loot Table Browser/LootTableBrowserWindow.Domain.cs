#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow
{
    private sealed class Domain
    {
        private readonly LootTableBrowserWindow _w;

        public Domain(LootTableBrowserWindow w)
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
        }

        public void SaveToolbarPrefs()
        {
            if (_w.SearchField != null)
                EditorPrefs.SetString(PrefSearch, _w.SearchField.value ?? string.Empty);

            EditorPrefs.SetInt(PrefSortMode, (int)_w.CurrentSortMode);
        }

        public void CycleSortMode()
        {
            if (!_w.IsUIReady) return;

            _w.CurrentSortMode = _w.CurrentSortMode switch
            {
                SortMode.NameAz => SortMode.NameZa,
                SortMode.NameZa => SortMode.GuaranteedCount,
                SortMode.GuaranteedCount => SortMode.WeightedCount,
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
            _w.CurrentSortMode = SortMode.NameAz;
            _w._ui.UpdateSortButtonText();

            SaveToolbarPrefs();
            _w.Refresh(RefreshFlags.Soft);
        }

        public void RefreshAssetsInternal()
        {
            _w.AllLootTables.Clear();

            var guids = AssetDatabase.FindAssets("t:LootTable");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<LootTable>(path);
                if (asset != null)
                    _w.AllLootTables.Add(asset);
            }

            _w.MarkSeverityDirty();
        }

        public void RefilterInternal()
        {
            _w.FilteredLootTables.Clear();

            IEnumerable<LootTable> query = _w.AllLootTables;

            var search = (_w.SearchField?.value ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(t =>
                {
                    if (t == null) return false;
                    if ((t.name ?? string.Empty).ToLowerInvariant().Contains(s)) return true;

                    var guaranteed = t.guaranteedEntries?.Count ?? 0;
                    var weighted = t.weightedEntries?.Count ?? 0;
                    var summary = $"g:{guaranteed} w:{weighted} r:{t.minRandomPicks}-{t.maxRandomPicks}";
                    return summary.ToLowerInvariant().Contains(s);
                });
            }

            query = _w.CurrentSortMode switch
            {
                SortMode.NameAz => query.OrderBy(t => t != null ? t.name : string.Empty, StringComparer.OrdinalIgnoreCase),
                SortMode.NameZa => query.OrderByDescending(t => t != null ? t.name : string.Empty, StringComparer.OrdinalIgnoreCase),
                SortMode.GuaranteedCount => query.OrderByDescending(t => t?.guaranteedEntries?.Count ?? 0)
                    .ThenBy(t => t != null ? t.name : string.Empty, StringComparer.OrdinalIgnoreCase),
                SortMode.WeightedCount => query.OrderByDescending(t => t?.weightedEntries?.Count ?? 0)
                    .ThenBy(t => t != null ? t.name : string.Empty, StringComparer.OrdinalIgnoreCase),
                _ => query.OrderBy(t => t != null ? t.name : string.Empty, StringComparer.OrdinalIgnoreCase)
            };

            _w.FilteredLootTables.AddRange(query);

            if (_w.Selected != null && !_w.FilteredLootTables.Contains(_w.Selected))
            {
                _w.Selected = null;
                _w.SelectedSo = null;
                _w.ClearSimulation();
                _w._ui.ShowEmptyDetails();
            }
        }

        public void UpdateValidationsInternal()
        {
            _w.SeverityCache.Clear();
            _w.ValidationCache.Clear();

            foreach (var table in _w.AllLootTables)
            {
                if (table == null)
                    continue;

                var messages = Validate(table);
                _w.ValidationCache[table] = messages;
                _w.SeverityCache[table] = messages.Count == 0
                    ? ValidationSeverity.None
                    : messages.Max(m => m.Severity);
            }

            _w.SeverityCacheDirty = false;

            if (_w.Selected != null)
                UpdateSelectedValidationBox(_w.Selected);
        }

        internal List<ValidationMessage> GetMessages(LootTable table)
        {
            if (table == null)
                return new List<ValidationMessage>();

            if (_w.ValidationCache.TryGetValue(table, out var cached))
                return cached;

            return Validate(table);
        }

        private void UpdateSelectedValidationBox(LootTable table)
        {
            if (_w.ValidationBox == null || _w.ValidationContainer == null)
                return;

            if (!_w.ValidationCache.TryGetValue(table, out var messages) || messages.Count == 0)
            {
                _w.ValidationContainer.style.display = DisplayStyle.None;
                _w.ValidationBox.style.display = DisplayStyle.None;
                return;
            }

            var highest = messages.Max(m => m.Severity);
            _w.ValidationBox.messageType = highest switch
            {
                ValidationSeverity.Error => HelpBoxMessageType.Error,
                ValidationSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };

            _w.ValidationBox.text = "Validation Summary\n• " + string.Join("\n• ", messages.Select(m => m.Message));
            _w.ValidationContainer.style.display = DisplayStyle.Flex;
            _w.ValidationBox.style.display = DisplayStyle.Flex;
        }

        private List<ValidationMessage> Validate(LootTable table)
        {
            var messages = new List<ValidationMessage>();
            if (table == null)
                return messages;

            if (table.maxRandomPicks < table.minRandomPicks)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    Message = "maxRandomPicks no puede ser menor que minRandomPicks."
                });
            }

            var guaranteed = table.guaranteedEntries ?? new List<LootTable.Entry>();
            var weighted = table.weightedEntries ?? new List<LootTable.Entry>();

            if (guaranteed.Count == 0 && weighted.Count == 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "La tabla no tiene entries garantizadas ni weighted."
                });
            }

            ValidateEntries(table, messages, guaranteed, false, "Guaranteed");
            ValidateEntries(table, messages, weighted, true, "Weighted");

            var validWeightedCount = weighted.Count(e => IsValidWeighted(e));

            if ((table.minRandomPicks > 0 || table.maxRandomPicks > 0) && validWeightedCount == 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Hay random picks configurados pero no hay weighted entries válidas."
                });
            }

            if (table.minRandomPicks > validWeightedCount && validWeightedCount > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"minRandomPicks ({table.minRandomPicks}) es mayor que las weighted entries válidas ({validWeightedCount}). LootSystem lo limitará automáticamente."
                });
            }

            if (table.maxRandomPicks > validWeightedCount && validWeightedCount > 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Info,
                    Message = $"maxRandomPicks ({table.maxRandomPicks}) es mayor que las weighted entries válidas ({validWeightedCount}). LootSystem lo limitará automáticamente."
                });
            }

            ValidateGuaranteedAndWeightedDuplicates(messages, guaranteed, weighted);
            ValidateNestedReferences(table, messages);

            return messages;
        }

        private void ValidateEntries(
            LootTable owner,
            List<ValidationMessage> messages,
            List<LootTable.Entry> entries,
            bool isWeighted,
            string label)
        {
            if (entries == null)
                return;

            var seenItems = new HashSet<ItemData>();
            var seenTables = new HashSet<LootTable>();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry == null)
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"{label} entry #{i} es null."
                    });
                    continue;
                }

                ValidateEntryTarget(owner, messages, entry, label, i, seenItems, seenTables);

                if (entry.minQuantity < 1)
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"{label} entry #{i} tiene minQuantity < 1."
                    });
                }

                if (entry.maxQuantity < entry.minQuantity)
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"{label} entry #{i} tiene maxQuantity < minQuantity."
                    });
                }

                if (isWeighted && entry.weight <= 0)
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"{label} entry #{i} tiene weight <= 0 y no podrá ser seleccionada."
                    });
                }

                ValidateEquipableOverride(messages, entry, label, i);
            }
        }

        private static void ValidateEntryTarget(
            LootTable owner,
            List<ValidationMessage> messages,
            LootTable.Entry entry,
            string label,
            int index,
            HashSet<ItemData> seenItems,
            HashSet<LootTable> seenTables)
        {
            switch (entry.entryType)
            {
                case Enums.LootEntryType.Item:
                {
                    if (entry.item == null)
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"{label} entry #{index} es Item pero no tiene Item asignado."
                        });
                    }
                    else if (!seenItems.Add(entry.item))
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Info,
                            Message = $"{label} contiene items duplicados: '{entry.item.name}'."
                        });
                    }

                    if (entry.nestedTable != null)
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Info,
                            Message = $"{label} entry #{index} es Item pero tiene Nested Table asignada; será ignorada."
                        });
                    }

                    break;
                }

                case Enums.LootEntryType.LootTable:
                {
                    if (entry.nestedTable == null)
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"{label} entry #{index} es LootTable pero no tiene Nested Table asignada."
                        });
                    }
                    else
                    {
                        if (entry.nestedTable == owner)
                        {
                            messages.Add(new ValidationMessage
                            {
                                Severity = ValidationSeverity.Error,
                                Message = $"{label} entry #{index} referencia directamente a la misma LootTable."
                            });
                        }

                        if (!seenTables.Add(entry.nestedTable))
                        {
                            messages.Add(new ValidationMessage
                            {
                                Severity = ValidationSeverity.Info,
                                Message = $"{label} contiene nested tables duplicadas: '{entry.nestedTable.name}'."
                            });
                        }

                        if (CouldProduceNoLoot(entry.nestedTable))
                        {
                            messages.Add(new ValidationMessage
                            {
                                Severity = ValidationSeverity.Info,
                                Message = $"{label} entry #{index} usa nested table '{entry.nestedTable.name}', que puede producir 0 drops."
                            });
                        }
                    }

                    if (entry.item != null)
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Info,
                            Message = $"{label} entry #{index} es LootTable pero tiene Item asignado; será ignorado."
                        });
                    }

                    break;
                }

                default:
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"{label} entry #{index} tiene entryType no soportado: {entry.entryType}."
                    });
                    break;
            }
        }

        private static void ValidateEquipableOverride(List<ValidationMessage> messages, LootTable.Entry entry, string label, int index)
        {
            if (entry.equipableOverrideMode == Enums.LootEntryEquipableOverrideMode.None)
                return;

            if (entry.entryType != Enums.LootEntryType.Item || !(entry.item is EquipableItemData))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"{label} entry #{index} tiene override de equipable pero no apunta a un EquipableItemData. El override no tendrá efecto."
                });
            }
            
        }

        private static void ValidateGuaranteedAndWeightedDuplicates(
            List<ValidationMessage> messages,
            List<LootTable.Entry> guaranteed,
            List<LootTable.Entry> weighted)
        {
            var guaranteedItems = new HashSet<ItemData>();
            var guaranteedTables = new HashSet<LootTable>();

            foreach (var entry in guaranteed)
            {
                if (entry == null) continue;

                if (entry.entryType == Enums.LootEntryType.Item && entry.item != null)
                    guaranteedItems.Add(entry.item);
                else if (entry.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null)
                    guaranteedTables.Add(entry.nestedTable);
            }

            foreach (var entry in weighted)
            {
                if (entry == null) continue;

                if (entry.entryType == Enums.LootEntryType.Item && entry.item != null && guaranteedItems.Contains(entry.item))
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Info,
                        Message = $"El item '{entry.item.name}' existe tanto en guaranteed como en weighted."
                    });
                }
                else if (entry.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null && guaranteedTables.Contains(entry.nestedTable))
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = ValidationSeverity.Info,
                        Message = $"La nested table '{entry.nestedTable.name}' existe tanto en guaranteed como en weighted."
                    });
                }
            }
        }

        private void ValidateNestedReferences(LootTable root, List<ValidationMessage> messages)
        {
            if (root == null)
                return;

            var path = new List<LootTable>();
            var visited = new HashSet<LootTable>();

            if (HasNestedCycle(root, root, visited, path))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Hay una referencia circular indirecta entre nested loot tables. LootSystem cortará el roll para evitar recursión infinita."
                });
            }
        }

        private static bool HasNestedCycle(LootTable current, LootTable target, HashSet<LootTable> visited, List<LootTable> path)
        {
            if (current == null)
                return false;

            if (!visited.Add(current))
                return false;

            path.Add(current);

            foreach (var child in EnumerateNestedTables(current))
            {
                if (child == null)
                    continue;

                if (child == target)
                    return true;

                if (path.Contains(child))
                    return true;

                if (HasNestedCycle(child, target, visited, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static IEnumerable<LootTable> EnumerateNestedTables(LootTable table)
        {
            if (table?.guaranteedEntries != null)
            {
                foreach (var entry in table.guaranteedEntries)
                    if (entry?.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null)
                        yield return entry.nestedTable;
            }

            if (table?.weightedEntries != null)
            {
                foreach (var entry in table.weightedEntries)
                    if (entry?.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null)
                        yield return entry.nestedTable;
            }
        }

        private static bool CouldProduceNoLoot(LootTable table)
        {
            if (table == null)
                return true;

            var guaranteed = table.guaranteedEntries ?? new List<LootTable.Entry>();
            var weighted = table.weightedEntries ?? new List<LootTable.Entry>();

            var hasValidGuaranteed = guaranteed.Any(IsValidGuaranteed);
            var hasValidWeighted = weighted.Any(IsValidWeighted);

            if (hasValidGuaranteed)
                return false;

            if (!hasValidWeighted)
                return true;

            return table.minRandomPicks <= 0;
        }

        public void SelectLootTable(LootTable table)
        {
            _w.Selected = table;
            _w.SelectedSo = table != null ? new SerializedObject(table) : null;
            _w.ClearSimulation();

            if (_w.Selected == null)
            {
                _w._ui.ShowEmptyDetails();
                return;
            }

            _w._ui.ShowLootTableDetails();
            UpdateSelectedValidationBox(_w.Selected);
            _w._ui.RefreshList();
        }

        public void PingSelectedLootTable()
        {
            if (_w.Selected == null)
                return;

            Selection.activeObject = _w.Selected;
            EditorGUIUtility.PingObject(_w.Selected);
        }

        public bool RenameSelectedLootTable(string newName)
        {
            if (_w.Selected == null)
                return false;

            newName = (newName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            var oldPath = AssetDatabase.GetAssetPath(_w.Selected);
            if (string.IsNullOrEmpty(oldPath))
                return false;

            var dir = Path.GetDirectoryName(oldPath)?.Replace("\\", "/");
            var error = AssetDatabase.RenameAsset(oldPath, newName);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename Loot Table", error, "OK");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newPath = string.IsNullOrEmpty(dir) ? $"{newName}.asset" : $"{dir}/{newName}.asset";
            var renamed = AssetDatabase.LoadAssetAtPath<LootTable>(newPath);

            _w.Refresh(RefreshFlags.Hard);
            SelectLootTable(renamed != null ? renamed : _w.Selected);

            if (renamed != null)
                EditorGUIUtility.PingObject(renamed);

            return true;
        }

        public void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Loot Table"), false, CreateNewLootTableAsset);
            menu.ShowAsContext();
        }

        private void CreateNewLootTableAsset()
        {
            var suggestedName = "NewLootTable";

            var defaultFolder = EditorPrefs.GetString(PrefLastCreateFolder, "Assets");
            if (!AssetDatabase.IsValidFolder(defaultFolder))
                defaultFolder = "Assets";

            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Loot Table",
                suggestedName,
                "asset",
                "Elige nombre y carpeta para la nueva LootTable.",
                defaultFolder);

            if (string.IsNullOrEmpty(assetPath))
                return;

            var folder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                EditorPrefs.SetString(PrefLastCreateFolder, folder);

            var obj = CreateInstance<LootTable>();
            Undo.RegisterCreatedObjectUndo(obj, "Create Loot Table");

            AssetDatabase.CreateAsset(obj, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _w.Refresh(RefreshFlags.Hard);
            SelectLootTable(obj);
            EditorGUIUtility.PingObject(obj);
        }

        public string GetSummary(LootTable table)
        {
            if (table == null) return "-";
            var guaranteed = table.guaranteedEntries?.Count ?? 0;
            var weighted = table.weightedEntries?.Count ?? 0;
            return $"G:{guaranteed} | W:{weighted}  | Picks: {table.minRandomPicks}-{table.maxRandomPicks}";
        }

        public void RunSimulation(int iterations)
        {
            if (_w.Selected == null)
            {
                _w.LastSimulation = null;
                _w._ui.RefreshSimulationView();
                return;
            }

            iterations = Mathf.Max(1, iterations);

            var snapshot = new SimulationSnapshot
            {
                Iterations = iterations
            };

            var map = new Dictionary<ItemData, SimRow>();
            List<ItemStack> lastRollStacks = null;

            for (var i = 0; i < iterations; i++)
            {
                var producedThisRoll = new HashSet<ItemData>();
                var resultStacks = RollOnceUsingRuntimeSystem(_w.Selected);
                lastRollStacks = resultStacks;

                snapshot.TotalGeneratedStacks += resultStacks.Count;

                foreach (var stack in resultStacks)
                {
                    if (stack?.data == null)
                        continue;

                    snapshot.TotalGeneratedQuantity += stack.quantity;

                    if (!map.TryGetValue(stack.data, out var row))
                    {
                        row = new SimRow { Item = stack.data };
                        map.Add(stack.data, row);
                    }

                    row.TotalQuantity += stack.quantity;

                    var rarity = stack.GetEffectiveRarity();
                    if (!row.RarityCounts.ContainsKey(rarity))
                        row.RarityCounts[rarity] = 0;

                    row.RarityCounts[rarity] += stack.quantity;

                    if (producedThisRoll.Add(stack.data))
                        row.AppearsInRolls++;
                }
            }

            snapshot.Rows.AddRange(
                map.Values
                    .OrderByDescending(r => r.AppearsInRolls)
                    .ThenBy(r => r.Item != null ? r.Item.name : string.Empty, StringComparer.OrdinalIgnoreCase));

            snapshot.InventoryPreview = BuildInventoryPreview(lastRollStacks);

            _w.LastSimulation = snapshot;
            _w._ui.RefreshSimulationView();
        }

        private static List<ItemStack> RollOnceUsingRuntimeSystem(LootTable table)
        {
            if (table == null)
                return new List<ItemStack>();

            return LootSystem.Roll(table) ?? new List<ItemStack>();
        }

        private static List<ItemStack> BuildAllPossibleStacks(LootTable table)
        {
            var result = new List<ItemStack>();
            if (table == null)
                return result;

            var addedGuaranteed = new HashSet<ItemData>();
            if (table.guaranteedEntries != null)
            {
                foreach (var entry in table.guaranteedEntries)
                {
                    if (!IsValidGuaranteed(entry) || entry.item == null)
                        continue;

                    if (!addedGuaranteed.Add(entry.item))
                        continue;

                    result.Add(CreateStack(entry.item, Mathf.Max(1, entry.maxQuantity)));
                }
            }

            var addedWeighted = new HashSet<ItemData>();
            if (table.weightedEntries != null)
            {
                foreach (var entry in table.weightedEntries)
                {
                    if (!IsValidWeighted(entry) || entry.item == null)
                        continue;

                    if (!addedWeighted.Add(entry.item))
                        continue;

                    result.Add(CreateStack(entry.item, Mathf.Max(1, entry.maxQuantity)));
                }
            }

            return result;
        }

        private InventoryPreviewSnapshot BuildInventoryPreview(List<ItemStack> stacks)
        {
            var preview = new InventoryPreviewSnapshot();
            var occupied = new bool[InventoryPreviewHeight, InventoryPreviewWidth];

            if (stacks == null || stacks.Count == 0)
                return preview;

            foreach (var stack in stacks)
            {
                if (stack == null || stack.data == null)
                    continue;

                var (w, h) = GetItemSize(stack.data);

                if (TryPlaceItem(occupied, w, h, out var x, out var y))
                {
                    preview.Placed.Add(new InventoryPlacedItem
                    {
                        Item = stack.data,
                        Rarity = stack.GetEffectiveRarity(),
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h,
                        Quantity = stack.quantity
                    });

                    for (var py = y; py < y + h; py++)
                    for (var px = x; px < x + w; px++)
                        occupied[py, px] = true;
                }
                else
                {
                    preview.Unplaced.Add(stack);
                }
            }

            return preview;
        }

        private static bool TryPlaceItem(bool[,] occupied, int itemW, int itemH, out int outX, out int outY)
        {
            for (var y = 0; y <= InventoryPreviewHeight - itemH; y++)
            {
                for (var x = 0; x <= InventoryPreviewWidth - itemW; x++)
                {
                    var fits = true;

                    for (var iy = 0; iy < itemH && fits; iy++)
                    for (var ix = 0; ix < itemW; ix++)
                    {
                        if (occupied[y + iy, x + ix])
                        {
                            fits = false;
                            break;
                        }
                    }

                    if (!fits)
                        continue;

                    outX = x;
                    outY = y;
                    return true;
                }
            }

            outX = -1;
            outY = -1;
            return false;
        }

        private static (int width, int height) GetItemSize(ItemData item)
        {
            if (item == null)
                return (1, 1);

            var so = new SerializedObject(item);

            var dims = so.FindProperty("SlotDimension") ??
                       so.FindProperty("slotDimensions") ??
                       so.FindProperty("dimensions") ??
                       so.FindProperty("size");

            if (dims != null)
            {
                var wProp = dims.FindPropertyRelative("Width");
                var hProp = dims.FindPropertyRelative("Height");

                if (wProp != null && hProp != null)
                    return (Mathf.Max(1, wProp.intValue), Mathf.Max(1, hProp.intValue));
            }

            return (1, 1);
        }

        private static List<ItemStack> SimulateOnce(LootTable table)
        {
            var results = new List<ItemStack>();
            if (table == null)
                return results;

            if (table.guaranteedEntries != null)
            {
                foreach (var entry in table.guaranteedEntries)
                {
                    if (!IsValidGuaranteed(entry))
                        continue;

                    var qty = UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                    results.Add(CreateStack(entry.item, qty));
                }
            }

            if (table.weightedEntries == null || table.weightedEntries.Count == 0)
                return results;

            var pool = table.weightedEntries
                .Where(IsValidWeighted)
                .ToList();

            if (pool.Count == 0)
                return results;

            var minDrops = Mathf.Max(0, table.minRandomPicks);
            var maxDrops = Mathf.Max(minDrops, table.maxRandomPicks);

            var maxPossible = Mathf.Min(maxDrops, pool.Count);
            var minPossible = Mathf.Min(minDrops, maxPossible);
            var rollCount = UnityEngine.Random.Range(minPossible, maxPossible + 1);

            for (var i = 0; i < rollCount; i++)
            {
                var selected = SelectByWeight(pool);
                if (selected == null)
                    break;

                var qty = UnityEngine.Random.Range(selected.minQuantity, selected.maxQuantity + 1);
                results.Add(CreateStack(selected.item, qty));
                pool.Remove(selected);
            }

            return results;
        }

        private static LootTable.Entry SelectByWeight(List<LootTable.Entry> pool)
        {
            if (pool == null || pool.Count == 0)
                return null;

            var totalWeight = 0;
            foreach (var entry in pool)
                totalWeight += Mathf.Max(0, entry.weight);

            if (totalWeight <= 0)
                return null;

            var roll = UnityEngine.Random.Range(0, totalWeight);
            var cumulative = 0;

            foreach (var entry in pool)
            {
                cumulative += Mathf.Max(0, entry.weight);
                if (roll < cumulative)
                    return entry;
            }

            return pool[pool.Count - 1];
        }

        private static bool IsValidGuaranteed(LootTable.Entry entry)
        {
            if (entry == null || entry.minQuantity <= 0 || entry.maxQuantity < entry.minQuantity)
                return false;

            return entry.entryType switch
            {
                Enums.LootEntryType.Item => entry.item != null,
                Enums.LootEntryType.LootTable => entry.nestedTable != null,
                _ => false
            };
        }

        private static bool IsValidWeighted(LootTable.Entry entry)
        {
            return IsValidGuaranteed(entry) && entry.weight > 0;
        }

        private static ItemStack CreateStack(ItemData item, int quantity)
        {
            return new ItemStack
            {
                data = item,
                quantity = quantity,
                gridX = 0,
                gridY = 0,
                rotationIndex = 0,
                rotated = false
            };
        }
    }
}
#endif