#if UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ContentEditor
{
    /// <summary>
    /// Contexto compartido entre módulos.
    /// Sirve para comunicar tabs entre sí sin acoplar directamente Items, Loot Tables y Expeditions.
    /// </summary>
    public sealed class ContentEditorContext
    {
        public ContentEditorWindow Window { get; }

        public ItemData SelectedItem { get; private set; }
        public LootTable SelectedLootTable { get; private set; }
        public GameObject SelectedSceneObject { get; private set; }

        public event Action<ItemData> ItemSelected;
        public event Action<LootTable> LootTableSelected;
        public event Action<GameObject> SceneObjectSelected;

        public event Action<ContentEditorTab> TabChangeRequested;
        
        // Navigation
        public ItemData RequestedItemToOpen { get; private set; }
        public LootTable RequestedLootTableToOpen { get; private set; }

        public ContentEditorContext(ContentEditorWindow window)
        {
            Window = window;
        }

        public void SelectItem(ItemData item, bool openTab = false)
        {
            SelectedItem = item;
            ItemSelected?.Invoke(item);

            if (openTab)
                TabChangeRequested?.Invoke(ContentEditorTab.Items);
        }

        public void SelectLootTable(LootTable lootTable, bool openTab = false)
        {
            SelectedLootTable = lootTable;
            LootTableSelected?.Invoke(lootTable);

            if (openTab)
                TabChangeRequested?.Invoke(ContentEditorTab.LootTables);
        }

        public void SelectSceneObject(GameObject gameObject, bool openTab = false)
        {
            SelectedSceneObject = gameObject;
            SceneObjectSelected?.Invoke(gameObject);

            if (openTab)
                TabChangeRequested?.Invoke(ContentEditorTab.Expeditions);
        }

        public void Ping(Object obj)
        {
            if (obj == null)
                return;

            UnityEditor.Selection.activeObject = obj;
            UnityEditor.EditorGUIUtility.PingObject(obj);
        }
        
        public void RequestOpenItem(ItemData item)
        {
            if (item == null)
                return;

            RequestedItemToOpen = item;
            SelectItem(item, true);
        }

        public void RequestOpenLootTable(LootTable lootTable)
        {
            if (lootTable == null)
                return;

            RequestedLootTableToOpen = lootTable;
            SelectLootTable(lootTable, true);
        }

        public bool ConsumeRequestedItem(out ItemData item)
        {
            item = RequestedItemToOpen;
            RequestedItemToOpen = null;
            return item != null;
        }

        public bool ConsumeRequestedLootTable(out LootTable lootTable)
        {
            lootTable = RequestedLootTableToOpen;
            RequestedLootTableToOpen = null;
            return lootTable != null;
        }
    }
}
#endif