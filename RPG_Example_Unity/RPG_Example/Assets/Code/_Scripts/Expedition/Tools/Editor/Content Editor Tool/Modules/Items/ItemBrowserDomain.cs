#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContentEditor.Modules.Items
{
    /// <summary>
    /// Capa de dominio del módulo de Items.
    /// Gestiona búsqueda, carga de assets y selección de ItemData.
    /// </summary>
    public sealed class ItemBrowserDomain
    {
        public enum ItemTypeTab
        {
            Equipable,
            Consumable,
            Crafting,
            Collectable
        }
        
        public enum SortMode
        {
            NameAz,
            NameZa,
        }
        
        private const string PrefActiveTypeTab = "BS.ContentEditor.Items.ActiveTypeTab";
        public ItemTypeTab CurrentTypeTab { get; private set; } = ItemTypeTab.Equipable;
        
        private const string PrefSelectedGuid = "BS.ContentEditor.Items.SelectedGuid";
        
        private const string PrefSortMode = "BS.ContentEditor.Items.SortMode";
        public SortMode CurrentSortMode { get; private set; } = SortMode.NameAz;
        
        private const string PrefSearch = "BS.ContentEditor.Items.Search";
        private const string PrefLastCreateFolder = "BS.ContentEditor.Items.LastCreateFolder";
        
        private readonly ContentEditorContext context;

        public readonly List<ItemData> AllItems = new();
        public readonly List<ItemData> FilteredItems = new();

        public string SearchText { get; private set; } = string.Empty;
        public ItemData SelectedItem { get; private set; }
        
        public SerializedObject SelectedSo { get; private set; }
        
        // Validation
        private readonly Dictionary<ItemData, List<ItemValidationMessage>> validationCache = new();
        private bool validationCacheDirty = true;
        
        // Duplicates
        private readonly Dictionary<string, List<ItemData>> nameIndex = new();
        private bool nameIndexDirty = true;

        public ItemBrowserDomain(ContentEditorContext context)
        {
            this.context = context;
            SearchText = EditorPrefs.GetString(PrefSearch, string.Empty);
            
            var savedTab = EditorPrefs.GetInt(PrefActiveTypeTab, (int)ItemTypeTab.Equipable);

            if (System.Enum.IsDefined(typeof(ItemTypeTab), savedTab))
                CurrentTypeTab = (ItemTypeTab)savedTab;
            
            var savedSort = EditorPrefs.GetInt(PrefSortMode, (int)SortMode.NameAz);

            if (System.Enum.IsDefined(typeof(SortMode), savedSort))
                CurrentSortMode = (SortMode)savedSort;
        }

        public void SavePrefs()
        {
            EditorPrefs.SetString(PrefSearch, SearchText ?? string.Empty);
        }

        public void SetSearch(string value)
        {
            SearchText = value ?? string.Empty;
            SavePrefs();
            Refilter();
        }

        public void RefreshAssets()
        {
            AllItems.Clear();

            var guids = AssetDatabase.FindAssets("t:ItemData");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

                if (item != null)
                    AllItems.Add(item);
            }
            MarkValidationDirty();
        }

        public void Refilter()
        {
            FilteredItems.Clear();

            IEnumerable<ItemData> query = AllItems.Where(i => i != null);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string s = SearchText.Trim().ToLowerInvariant();

                query = query.Where(i =>
                    (i.name != null && i.name.ToLowerInvariant().Contains(s)) ||
                    (!string.IsNullOrEmpty(i.itemNameID) && i.itemNameID.ToLowerInvariant().Contains(s)) ||
                    i.itemType.ToString().ToLowerInvariant().Contains(s));
            }

            query = CurrentTypeTab switch
            {
                ItemTypeTab.Equipable => query.Where(i => i is EquipableItemData),
                ItemTypeTab.Consumable => query.Where(i => i is ConsumableItemData),
                ItemTypeTab.Crafting => query.Where(i => i is CraftingItemData),
                ItemTypeTab.Collectable => query.Where(i => i is CollectableItemData),
                _ => query
            };

            query = CurrentSortMode switch
            {
                SortMode.NameAz => query.OrderBy(GetItemSortName),
                SortMode.NameZa => query.OrderByDescending(GetItemSortName),
                _ => query.OrderBy(GetItemSortName)
            };

            FilteredItems.AddRange(query);
            EnsureSelectionIsVisible();
            
        }

        public void SelectItem(ItemData item)
        {
            SelectedItem = item;
            SelectedSo = item != null ? new SerializedObject(item) : null;

            SaveSelectedGuid();

            context.SelectItem(item);
        }
        
        private void SaveSelectedGuid()
        {
            if (SelectedItem == null)
            {
                EditorPrefs.DeleteKey(PrefSelectedGuid);
                return;
            }

            string path = AssetDatabase.GetAssetPath(SelectedItem);
            string guid = AssetDatabase.AssetPathToGUID(path);

            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(PrefSelectedGuid, guid);
        }
        
        public void RestoreSelection()
        {
            string guid = EditorPrefs.GetString(PrefSelectedGuid, string.Empty);

            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                return;

            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (item == null)
                return;

            if (!FilteredItems.Contains(item))
                return;

            SelectItem(item);
        }

        public void PingSelected()
        {
            if (SelectedItem == null)
                return;

            Selection.activeObject = SelectedItem;
            EditorGUIUtility.PingObject(SelectedItem);
        }
        
        public void SetActiveTypeTab(ItemTypeTab tab)
        {
            if (CurrentTypeTab == tab)
                return;

            CurrentTypeTab = tab;
            EditorPrefs.SetInt(PrefActiveTypeTab, (int)CurrentTypeTab);

            Refilter();
        }
        
        public void CycleSortMode()
        {
            CurrentSortMode = CurrentSortMode switch
            {
                SortMode.NameAz => SortMode.NameZa,
                _ => SortMode.NameAz
            };

            EditorPrefs.SetInt(PrefSortMode, (int)CurrentSortMode);
            Refilter();
        }

        public void ResetFilters()
        {
            SearchText = string.Empty;
            CurrentTypeTab = ItemTypeTab.Equipable;
            CurrentSortMode = SortMode.NameAz;

            SavePrefs();
            EditorPrefs.SetInt(PrefActiveTypeTab, (int)CurrentTypeTab);
            EditorPrefs.SetInt(PrefSortMode, (int)CurrentSortMode);

            Refilter();
        }
        
        private static string GetItemSortName(ItemData item)
        {
            if (item == null)
                return string.Empty;

            return string.IsNullOrEmpty(item.itemNameID)
                ? item.name
                : item.itemNameID;
        }
        
        private void RebuildNameIndex()
        {
            nameIndex.Clear();

            foreach (var item in AllItems)
            {
                if (item == null)
                    continue;

                string key = NormalizeName(item.itemNameID);

                if (string.IsNullOrEmpty(key))
                    continue;

                if (!nameIndex.TryGetValue(key, out var list))
                {
                    list = new List<ItemData>();
                    nameIndex[key] = list;
                }

                list.Add(item);
            }

            nameIndexDirty = false;
        }
        
        // CLEAR & SELECTION
        
        public void ClearSelection()
        {
            SelectedItem = null;
            SelectedSo = null;
            EditorPrefs.DeleteKey(PrefSelectedGuid);
            context.SelectItem(null);
        }

        public void EnsureSelectionIsVisible()
        {
            if (SelectedItem == null)
                return;

            if (!FilteredItems.Contains(SelectedItem))
                ClearSelection();
        }
        
        
        // ITEM CREATION
        
        public void CreateItem(ItemTypeTab type)
        {
            string suggestedName = type switch
            {
                ItemTypeTab.Equipable => "NewEquipableItem",
                ItemTypeTab.Consumable => "NewConsumableItem",
                ItemTypeTab.Crafting => "NewCraftingItem",
                ItemTypeTab.Collectable => "NewCollectableItem",
                _ => "NewItem"
            };

            string defaultFolder = EditorPrefs.GetString(PrefLastCreateFolder, "Assets");

            if (!AssetDatabase.IsValidFolder(defaultFolder))
                defaultFolder = "Assets";

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Item",
                suggestedName,
                "asset",
                "Elige nombre y carpeta para el nuevo item.",
                defaultFolder);

            if (string.IsNullOrEmpty(path))
                return;

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");

            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                EditorPrefs.SetString(PrefLastCreateFolder, folder);

            ItemData item = type switch
            {
                ItemTypeTab.Equipable => ScriptableObject.CreateInstance<EquipableItemData>(),
                ItemTypeTab.Consumable => ScriptableObject.CreateInstance<ConsumableItemData>(),
                ItemTypeTab.Crafting => ScriptableObject.CreateInstance<CraftingItemData>(),
                ItemTypeTab.Collectable => ScriptableObject.CreateInstance<CollectableItemData>(),
                _ => null
            };

            if (item == null)
                return;

            Undo.RegisterCreatedObjectUndo(item, "Create Item");

            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshAssets();

            CurrentTypeTab = type;
            EditorPrefs.SetInt(PrefActiveTypeTab, (int)CurrentTypeTab);

            Refilter();
            SelectItem(item);

            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
        }
        
        
        // VALIDATION 

        public List<ItemValidationMessage> Validate(ItemData item)
        {
            var messages = new List<ItemValidationMessage>();

            if (item == null)
            {
                messages.Add(new ItemValidationMessage(
                    ItemValidationSeverity.Error,
                    "Item inválido o referencia null."));

                return messages;
            }
            
            if (!string.IsNullOrWhiteSpace(item.itemNameID))
            {
                string key = NormalizeName(item.itemNameID);

                if (!string.IsNullOrEmpty(key) &&
                    nameIndex.TryGetValue(key, out var duplicates))
                {
                    int duplicateCount = duplicates.Count(i => i != null && i != item);

                    if (duplicateCount > 0)
                    {
                        messages.Add(new ItemValidationMessage(
                            ItemValidationSeverity.Warning,
                            $"Item Name ID duplicado: '{item.itemNameID}'. Hay {duplicateCount} item(s) con el mismo ID."));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(item.itemNameID))
            {
                messages.Add(new ItemValidationMessage(
                    ItemValidationSeverity.Error,
                    "Falta Item Name ID. Este item no podrá localizarse correctamente."));
            }

            if (item.icon == null)
            {
                messages.Add(new ItemValidationMessage(
                    ItemValidationSeverity.Warning,
                    "Falta icono. El item aparecerá sin preview visual."));
            }

            if (item.maxStack <= 0)
            {
                messages.Add(new ItemValidationMessage(
                    ItemValidationSeverity.Error,
                    "Max Stack debe ser mayor que 0."));
            }

            if (item.SlotDimension.Width <= 0 || item.SlotDimension.Height <= 0)
            {
                messages.Add(new ItemValidationMessage(
                    ItemValidationSeverity.Error,
                    "Las dimensiones del item deben ser mayores que 0."));
            }

            if (item is ConsumableItemData consumable)
            {
                if (consumable.buffs == null || consumable.buffs.Count == 0)
                {
                    messages.Add(new ItemValidationMessage(
                        ItemValidationSeverity.Warning,
                        "Consumable sin buffs configurados."));
                }
            }

            if (item is WeaponData weapon)
            {
                if (weapon.combos == null || weapon.combos.Count == 0)
                {
                    messages.Add(new ItemValidationMessage(
                        ItemValidationSeverity.Warning,
                        "Weapon sin combos configurados."));
                }

                if (weapon.weaponSkill == null)
                {
                    messages.Add(new ItemValidationMessage(
                        ItemValidationSeverity.Warning,
                        "Weapon sin Weapon Skill asignado."));
                }

                if (weapon.skillScoreNeeded < 0f)
                {
                    messages.Add(new ItemValidationMessage(
                        ItemValidationSeverity.Error,
                        "Skill Score Needed debe ser mayor o igual que 0."));
                }
            }

            return messages;
        }
        
        public ItemValidationResult GetValidationResult(ItemData item)
        {
            var messages = GetValidationMessages(item);
            
            if (messages.Count == 0)
                return ItemValidationResult.None;

            var highest = messages.Max(m => m.Severity);

            string indicator = highest switch
            {
                ItemValidationSeverity.Error => "!",
                ItemValidationSeverity.Warning => "!",
                ItemValidationSeverity.Info => "i",
                _ => ""
            };

            Color color = highest switch
            {
                ItemValidationSeverity.Error => new Color(1f, 0.25f, 0.25f),
                ItemValidationSeverity.Warning => new Color(1f, 0.7f, 0f),
                ItemValidationSeverity.Info => new Color(0.4f, 0.7f, 1f),
                _ => Color.clear
            };

            string tooltip = string.Join("\n", messages.Select(m => $"• {m.Message}"));

            return new ItemValidationResult(
                highest,
                indicator,
                color,
                tooltip,
                true);
        }
        
        public void MarkValidationDirty()
        {
            validationCacheDirty = true;
            nameIndexDirty = true;
        }

        public void RebuildValidationCache()
        {
            if (!validationCacheDirty)
                return;

            if (nameIndexDirty)
                RebuildNameIndex();

            validationCache.Clear();

            foreach (var item in AllItems)
            {
                if (item == null)
                    continue;

                validationCache[item] = Validate(item);
            }

            validationCacheDirty = false;
        }

        public List<ItemValidationMessage> GetValidationMessages(ItemData item)
        {
            if (item == null)
                return Validate(null);

            RebuildValidationCache();

            if (validationCache.TryGetValue(item, out var messages))
                return messages;

            return Validate(item);
        }
        
        public void OpenItemFromNavigation(ItemData item)
        {
            if (item == null)
                return;

            SearchText = string.Empty;
            CurrentTypeTab = GetTypeTabForItem(item);

            SavePrefs();
            EditorPrefs.SetInt(PrefActiveTypeTab, (int)CurrentTypeTab);

            Refilter();

            if (FilteredItems.Contains(item))
                SelectItem(item);
        }
        
        // HELPERS
        
        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
        
        private static ItemTypeTab GetTypeTabForItem(ItemData item)
        {
            return item switch
            {
                EquipableItemData => ItemTypeTab.Equipable,
                ConsumableItemData => ItemTypeTab.Consumable,
                CraftingItemData => ItemTypeTab.Crafting,
                CollectableItemData => ItemTypeTab.Collectable,
                _ => ItemTypeTab.Equipable
            };
        }
        
    }
}
#endif