#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContentEditor.Modules.LootTables
{
    /// <summary>
    /// Capa de dominio del módulo Loot Tables.
    /// Gestiona carga de assets, búsqueda, ordenación, selección y preferencias.
    /// </summary>
    public sealed class LootTableBrowserDomain
    {
        private const string PrefSearch = "BS.ContentEditor.LootTables.Search";
        private const string PrefSortMode = "BS.ContentEditor.LootTables.SortMode";
        private const string PrefSelectedGuid = "BS.ContentEditor.LootTables.SelectedGuid";

        private readonly ContentEditorContext context;

        private const string PrefLastCreateFolder = "BS.ContentEditor.LootTables.LastCreateFolder";
        
        // Inventory Preview
        private const int InventoryPreviewWidth = 6;
        private const int InventoryPreviewHeight = 3;

        //Validation
        private readonly Dictionary<LootTable, List<LootTableValidationMessage>> validationCache = new();
        private bool validationCacheDirty = true;

        public readonly List<LootTable> AllLootTables = new();
        public readonly List<LootTable> FilteredLootTables = new();

        public LootTableSimulationSnapshot LastSimulation { get; private set; }
        
        public string SearchText { get; private set; }
        public LootTableSortMode CurrentSortMode { get; private set; } = LootTableSortMode.NameAz;

        public LootTable SelectedLootTable { get; private set; }
        public SerializedObject SelectedSo { get; private set; }

        public LootTableBrowserDomain(ContentEditorContext context)
        {
            this.context = context;

            SearchText = EditorPrefs.GetString(PrefSearch, string.Empty);

            int savedSort = EditorPrefs.GetInt(PrefSortMode, (int)LootTableSortMode.NameAz);

            if (System.Enum.IsDefined(typeof(LootTableSortMode), savedSort))
                CurrentSortMode = (LootTableSortMode)savedSort;
        }

        public void SavePrefs()
        {
            EditorPrefs.SetString(PrefSearch, SearchText ?? string.Empty);
            EditorPrefs.SetInt(PrefSortMode, (int)CurrentSortMode);
        }

        public void RefreshAssets()
        {
            AllLootTables.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:LootTable"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);

                if (table != null)
                    AllLootTables.Add(table);
            }

            MarkValidationDirty();
        }

        public void SetSearch(string value)
        {
            SearchText = value ?? string.Empty;
            SavePrefs();
            Refilter();
        }

        public void CycleSortMode()
        {
            CurrentSortMode = CurrentSortMode switch
            {
                LootTableSortMode.NameAz => LootTableSortMode.NameZa,
                LootTableSortMode.NameZa => LootTableSortMode.GuaranteedCount,
                LootTableSortMode.GuaranteedCount => LootTableSortMode.WeightedCount,
                _ => LootTableSortMode.NameAz
            };

            SavePrefs();
            Refilter();
        }

        public void ResetFilters()
        {
            SearchText = string.Empty;
            CurrentSortMode = LootTableSortMode.NameAz;

            SavePrefs();
            Refilter();
        }

        public void Refilter()
        {
            FilteredLootTables.Clear();

            IEnumerable<LootTable> query = AllLootTables.Where(t => t != null);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim().ToLowerInvariant();

                query = query.Where(t =>
                    t.name.ToLowerInvariant().Contains(s) ||
                    GetSummary(t).ToLowerInvariant().Contains(s));
            }

            query = CurrentSortMode switch
            {
                LootTableSortMode.NameAz => query.OrderBy(t => t.name),
                LootTableSortMode.NameZa => query.OrderByDescending(t => t.name),
                LootTableSortMode.GuaranteedCount => query
                    .OrderByDescending(t => t.guaranteedEntries != null ? t.guaranteedEntries.Count : 0)
                    .ThenBy(t => t.name),
                LootTableSortMode.WeightedCount => query
                    .OrderByDescending(t => t.weightedEntries != null ? t.weightedEntries.Count : 0)
                    .ThenBy(t => t.name),
                _ => query.OrderBy(t => t.name)
            };

            FilteredLootTables.AddRange(query);

            EnsureSelectionIsVisible();
        }

        public void SelectLootTable(LootTable table)
        {
            SelectedLootTable = table;
            SelectedSo = table != null ? new SerializedObject(table) : null;
            ClearSimulation();

            SaveSelectedGuid();

            context.SelectLootTable(table);
        }

        public void ClearSelection()
        {
            SelectedLootTable = null;
            SelectedSo = null;

            EditorPrefs.DeleteKey(PrefSelectedGuid);

            context.SelectLootTable(null);
        }

        public void EnsureSelectionIsVisible()
        {
            if (SelectedLootTable == null)
                return;

            if (!FilteredLootTables.Contains(SelectedLootTable))
                ClearSelection();
        }

        public void RestoreSelection()
        {
            string guid = EditorPrefs.GetString(PrefSelectedGuid, string.Empty);

            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                return;

            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);

            if (table == null || !FilteredLootTables.Contains(table))
                return;

            SelectLootTable(table);
        }

        public void PingSelected()
        {
            if (SelectedLootTable == null)
                return;

            Selection.activeObject = SelectedLootTable;
            EditorGUIUtility.PingObject(SelectedLootTable);
        }

        public string GetSummary(LootTable table)
        {
            if (table == null)
                return string.Empty;

            int guaranteed = table.guaranteedEntries != null ? table.guaranteedEntries.Count : 0;
            int weighted = table.weightedEntries != null ? table.weightedEntries.Count : 0;

            return $"G:{guaranteed} W:{weighted}";
        }

        private void SaveSelectedGuid()
        {
            if (SelectedLootTable == null)
            {
                EditorPrefs.DeleteKey(PrefSelectedGuid);
                return;
            }

            string path = AssetDatabase.GetAssetPath(SelectedLootTable);
            string guid = AssetDatabase.AssetPathToGUID(path);

            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(PrefSelectedGuid, guid);
        }

        // LOOT TABLE CREATION

        public void CreateLootTable()
        {
            string defaultFolder = EditorPrefs.GetString(PrefLastCreateFolder, "Assets");

            if (!AssetDatabase.IsValidFolder(defaultFolder))
                defaultFolder = "Assets";

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Loot Table",
                "NewLootTable",
                "asset",
                "Elige nombre y carpeta para la nueva LootTable.",
                defaultFolder);

            if (string.IsNullOrEmpty(path))
                return;

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");

            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                EditorPrefs.SetString(PrefLastCreateFolder, folder);

            var table = UnityEngine.ScriptableObject.CreateInstance<LootTable>();

            Undo.RegisterCreatedObjectUndo(table, "Create Loot Table");

            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshAssets();
            Refilter();
            SelectLootTable(table);

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }

        // VALIDATION

        public void MarkValidationDirty()
        {
            validationCacheDirty = true;
        }

        public void RebuildValidationCache()
        {
            if (!validationCacheDirty)
                return;

            validationCache.Clear();

            foreach (var table in AllLootTables)
            {
                if (table == null)
                    continue;

                validationCache[table] = Validate(table);
            }

            validationCacheDirty = false;
        }

        public List<LootTableValidationMessage> GetValidationMessages(LootTable table)
        {
            if (table == null)
                return Validate(null);

            RebuildValidationCache();

            if (validationCache.TryGetValue(table, out var messages))
                return messages;

            return Validate(table);
        }

        public LootTableValidationResult GetValidationResult(LootTable table)
        {
            var messages = GetValidationMessages(table);

            if (messages.Count == 0)
                return LootTableValidationResult.None;

            var highest = messages.Max(m => m.Severity);

            string indicator = highest switch
            {
                LootTableValidationSeverity.Error => "!",
                LootTableValidationSeverity.Warning => "!",
                LootTableValidationSeverity.Info => "i",
                _ => ""
            };

            Color color = highest switch
            {
                LootTableValidationSeverity.Error => new Color(1f, 0.25f, 0.25f),
                LootTableValidationSeverity.Warning => new Color(1f, 0.7f, 0f),
                LootTableValidationSeverity.Info => new Color(0.4f, 0.7f, 1f),
                _ => Color.clear
            };

            string tooltip = string.Join("\n", messages.Select(m => $"• {m.Message}"));

            return new LootTableValidationResult(highest, indicator, color, tooltip, true);
        }

        public List<LootTableValidationMessage> Validate(LootTable table)
        {
            var messages = new List<LootTableValidationMessage>();

            if (table == null)
            {
                messages.Add(new LootTableValidationMessage(
                    LootTableValidationSeverity.Error,
                    "LootTable inválida o referencia null."));

                return messages;
            }

            var guaranteed = table.guaranteedEntries ?? new List<LootTable.Entry>();
            var weighted = table.weightedEntries ?? new List<LootTable.Entry>();

            if (guaranteed.Count == 0 && weighted.Count == 0)
            {
                messages.Add(new LootTableValidationMessage(
                    LootTableValidationSeverity.Warning,
                    "La tabla no tiene entries garantizadas ni weighted."));
            }

            ValidateEntries(table, messages, guaranteed, false, "Guaranteed");
            ValidateEntries(table, messages, weighted, true, "Weighted");

            int validWeightedCount = weighted.Count(IsValidWeighted);

            if ((table.minRandomPicks > 0 || table.maxRandomPicks > 0) && validWeightedCount == 0)
            {
                messages.Add(new LootTableValidationMessage(
                    LootTableValidationSeverity.Warning,
                    "Hay random picks configurados pero no hay weighted entries válidas."));
            }
            
            if (validWeightedCount > 0 &&
                table.minRandomPicks <= 0 &&
                table.maxRandomPicks <= 0)
            {
                messages.Add(new LootTableValidationMessage(
                    LootTableValidationSeverity.Info,
                    "Hay weighted entries válidas, pero Min/Max Random Picks están a 0. No se harán rolls weighted."));
            }

            ValidateGuaranteedAndWeightedDuplicates(messages, guaranteed, weighted);
            ValidateNestedReferences(table, messages);

            return messages;
        }

        // SIMULATION
        
        public void ClearSimulation()
        {
            LastSimulation = null;
        }
        
        public void RunSimulation(int iterations)
        {
            if (SelectedLootTable == null)
                return;

            iterations = Mathf.Max(1, iterations);

            var snapshot = new LootTableSimulationSnapshot
            {
                Iterations = iterations
            };

            var rowsByItem = new Dictionary<ItemData, LootTableSimulationRow>();

            List<ItemStack> previewRoll = null;

            for (int i = 0; i < iterations; i++)
            {
                var stacks = LootSystem.Roll(SelectedLootTable);

                if (i == 0)
                    previewRoll = stacks;

                if (stacks == null)
                    continue;

                var appearedThisRoll = new HashSet<ItemData>();

                foreach (var stack in stacks)
                {
                    if (stack == null || stack.data == null)
                        continue;

                    int quantity = Mathf.Max(0, stack.quantity);

                    snapshot.TotalGeneratedStacks++;
                    snapshot.TotalGeneratedQuantity += quantity;

                    if (!rowsByItem.TryGetValue(stack.data, out var row))
                    {
                        row = new LootTableSimulationRow
                        {
                            Item = stack.data
                        };

                        rowsByItem[stack.data] = row;
                        snapshot.Rows.Add(row);
                    }

                    row.TotalQuantity += quantity;

                    if (appearedThisRoll.Add(stack.data))
                        row.AppearsInRolls++;

                    if (!row.RarityCounts.ContainsKey(stack.rolledRarity))
                        row.RarityCounts[stack.rolledRarity] = 0;

                    row.RarityCounts[stack.rolledRarity] += quantity;
                }
            }

            snapshot.Rows.Sort((a, b) =>
                b.TotalQuantity.CompareTo(a.TotalQuantity));

            snapshot.InventoryPreview = BuildInventoryPreview(previewRoll);

            LastSimulation = snapshot;
        }
        
        
        // HELPERS
        
        private void ValidateEntries(
            LootTable owner,
            List<LootTableValidationMessage> messages,
            List<LootTable.Entry> entries,
            bool isWeighted,
            string label)
        {
            if (entries == null)
                return;

            var seenItems = new HashSet<ItemData>();
            var seenTables = new HashSet<LootTable>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry == null)
                {
                    messages.Add(new LootTableValidationMessage(
                        LootTableValidationSeverity.Warning,
                        $"{label} entry #{i} es null."));

                    continue;
                }

                ValidateEntryTarget(owner, messages, entry, label, i, seenItems, seenTables);

                if (isWeighted && entry.weight <= 0)
                {
                    messages.Add(new LootTableValidationMessage(
                        LootTableValidationSeverity.Warning,
                        $"{label} entry #{i} tiene weight <= 0 y no podrá ser seleccionada."));
                }
            }
        }

        private static void ValidateEntryTarget(
            LootTable owner,
            List<LootTableValidationMessage> messages,
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
                        messages.Add(new LootTableValidationMessage(
                            LootTableValidationSeverity.Error,
                            $"{label} entry #{index} es Item pero no tiene Item asignado."));
                    }
                    else if (!seenItems.Add(entry.item))
                    {
                        messages.Add(new LootTableValidationMessage(
                            LootTableValidationSeverity.Info,
                            $"{label} contiene items duplicados: '{entry.item.name}'."));
                    }

                    if (entry.nestedTable != null)
                    {
                        messages.Add(new LootTableValidationMessage(
                            LootTableValidationSeverity.Info,
                            $"{label} entry #{index} es Item pero tiene Nested Table asignada; será ignorada."));
                    }

                    break;
                }

                case Enums.LootEntryType.LootTable:
                {
                    if (entry.nestedTable == null)
                    {
                        messages.Add(new LootTableValidationMessage(
                            LootTableValidationSeverity.Error,
                            $"{label} entry #{index} es LootTable pero no tiene Nested Table asignada."));
                    }
                    else
                    {
                        if (!seenTables.Add(entry.nestedTable))
                        {
                            messages.Add(new LootTableValidationMessage(
                                LootTableValidationSeverity.Info,
                                $"{label} contiene nested tables duplicadas: '{entry.nestedTable.name}'."));
                        }

                        if (CouldProduceNoLoot(entry.nestedTable))
                        {
                            string nestedName = entry.nestedTable != null
                                ? entry.nestedTable.name
                                : "NULL";

                            messages.Add(new LootTableValidationMessage(
                                LootTableValidationSeverity.Info,
                                $"{label} entry #{index} apunta a la nested table '{nestedName}', que podría no producir loot."));
                        }
                    }

                    if (entry.item != null)
                    {
                        messages.Add(new LootTableValidationMessage(
                            LootTableValidationSeverity.Info,
                            $"{label} entry #{index} es LootTable pero tiene Item asignado; será ignorado."));
                    }

                    break;
                }
            }
        }

        private static void ValidateGuaranteedAndWeightedDuplicates(
            List<LootTableValidationMessage> messages,
            List<LootTable.Entry> guaranteed,
            List<LootTable.Entry> weighted)
        {
            var guaranteedItems = new HashSet<ItemData>();
            var guaranteedTables = new HashSet<LootTable>();

            foreach (var entry in guaranteed)
            {
                if (entry == null)
                    continue;

                if (entry.entryType == Enums.LootEntryType.Item && entry.item != null)
                    guaranteedItems.Add(entry.item);
                else if (entry.entryType == Enums.LootEntryType.LootTable && entry.nestedTable != null)
                    guaranteedTables.Add(entry.nestedTable);
            }

            foreach (var entry in weighted)
            {
                if (entry == null)
                    continue;

                if (entry.entryType == Enums.LootEntryType.Item &&
                    entry.item != null &&
                    guaranteedItems.Contains(entry.item))
                {
                    messages.Add(new LootTableValidationMessage(
                        LootTableValidationSeverity.Info,
                        $"El item '{entry.item.name}' existe tanto en guaranteed como en weighted."));
                }
                else if (entry.entryType == Enums.LootEntryType.LootTable &&
                         entry.nestedTable != null &&
                         guaranteedTables.Contains(entry.nestedTable))
                {
                    messages.Add(new LootTableValidationMessage(
                        LootTableValidationSeverity.Info,
                        $"La nested table '{entry.nestedTable.name}' existe tanto en guaranteed como en weighted."));
                }
            }
        }
        
        private static bool IsValidGuaranteed(LootTable.Entry entry)
        {
            if (entry == null)
                return false;

            if (entry.minQuantity < 1 || entry.maxQuantity < entry.minQuantity)
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
            return entry != null &&
                   entry.weight > 0 &&
                   IsValidGuaranteed(entry);
        }
        
        private LootTableInventoryPreviewSnapshot BuildInventoryPreview(List<ItemStack> stacks)
        {
            var preview = new LootTableInventoryPreviewSnapshot();
            var occupied = new bool[InventoryPreviewHeight, InventoryPreviewWidth];
        
            if (stacks == null || stacks.Count == 0)
                return preview;
        
            foreach (var stack in stacks)
            {
                if (stack == null || stack.data == null)
                    continue;
        
                var (w, h) = GetItemSize(stack.data);
        
                if (TryPlaceItem(occupied, w, h, out int x, out int y))
                {
                    preview.Placed.Add(new LootTableInventoryPlacedItem
                    {
                        Item = stack.data,
                        Rarity = stack.rolledRarity,
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h,
                        Quantity = stack.quantity
                    });
        
                    for (int py = y; py < y + h; py++)
                    {
                        for (int px = x; px < x + w; px++)
                            occupied[py, px] = true;
                    }
                }
                else
                {
                    preview.Unplaced.Add(stack);
                }
            }
        
            return preview;
        }
        
        private static bool TryPlaceItem(
            bool[,] occupied,
            int itemW,
            int itemH,
            out int outX,
            out int outY)
        {
            for (int y = 0; y <= InventoryPreviewHeight - itemH; y++)
            {
                for (int x = 0; x <= InventoryPreviewWidth - itemW; x++)
                {
                    bool fits = true;
        
                    for (int iy = 0; iy < itemH && fits; iy++)
                    {
                        for (int ix = 0; ix < itemW; ix++)
                        {
                            if (occupied[y + iy, x + ix])
                            {
                                fits = false;
                                break;
                            }
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
        
        private static bool CouldProduceNoLoot(LootTable table)
        {
            if (table == null)
                return true;

            var guaranteed = table.guaranteedEntries ?? new List<LootTable.Entry>();
            var weighted = table.weightedEntries ?? new List<LootTable.Entry>();

            bool hasValidGuaranteed = guaranteed.Any(IsValidGuaranteed);
            bool hasValidWeighted = weighted.Any(IsValidWeighted);

            if (hasValidGuaranteed)
                return false;

            if (!hasValidWeighted)
                return true;

            return table.minRandomPicks <= 0;
        }
        
        private void ValidateNestedReferences(
            LootTable root,
            List<LootTableValidationMessage> messages)
        {
            if (root == null)
                return;

            var path = new List<LootTable>();
            var visited = new HashSet<LootTable>();

            if (HasNestedCycle(root, root, visited, path))
            {
                messages.Add(new LootTableValidationMessage(
                    LootTableValidationSeverity.Error,
                    "Hay una referencia circular indirecta entre nested loot tables. LootSystem cortará el roll para evitar recursión infinita."));
            }
        }
        
        private static bool HasNestedCycle(
            LootTable current,
            LootTable target,
            HashSet<LootTable> visited,
            List<LootTable> path)
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
                {
                    if (entry?.entryType == Enums.LootEntryType.LootTable &&
                        entry.nestedTable != null)
                    {
                        yield return entry.nestedTable;
                    }
                }
            }

            if (table?.weightedEntries != null)
            {
                foreach (var entry in table.weightedEntries)
                {
                    if (entry?.entryType == Enums.LootEntryType.LootTable &&
                        entry.nestedTable != null)
                    {
                        yield return entry.nestedTable;
                    }
                }
            }
        }
        
        public bool RenameSelectedLootTable(string newName)
        {
            if (SelectedLootTable == null)
                return false;

            newName = (newName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(newName))
                return false;

            string oldPath = AssetDatabase.GetAssetPath(SelectedLootTable);

            if (string.IsNullOrEmpty(oldPath))
                return false;

            string error = AssetDatabase.RenameAsset(oldPath, newName);

            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename Loot Table", error, "OK");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshAssets();
            Refilter();

            string dir = System.IO.Path.GetDirectoryName(oldPath)?.Replace("\\", "/");
            string newPath = string.IsNullOrEmpty(dir)
                ? $"{newName}.asset"
                : $"{dir}/{newName}.asset";

            var renamed = AssetDatabase.LoadAssetAtPath<LootTable>(newPath);

            if (renamed != null)
                SelectLootTable(renamed);

            return true;
        }
        
        public void OpenLootTableFromNavigation(LootTable table)
        {
            if (table == null)
                return;

            SearchText = string.Empty;
            SavePrefs();

            Refilter();

            if (FilteredLootTables.Contains(table))
                SelectLootTable(table);
        }
        
        public void RequestOpenItem(ItemData item)
        {
            context.RequestOpenItem(item);
        }

        public void RequestOpenLootTable(LootTable table)
        {
            context.RequestOpenLootTable(table);
        }

    }
}
#endif