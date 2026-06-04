#if UNITY_EDITOR
using ContentEditor;
using UnityEngine.UIElements;

namespace ContentEditor.Modules.LootTables
{
    public sealed class LootTableBrowserModule : IContentEditorModule
    {
        public ContentEditorTab Tab => ContentEditorTab.LootTables;
        public string DisplayName => "Loot Tables";

        private readonly ContentEditorContext context;

        private LootTableBrowserDomain domain;
        private LootTableBrowserUI ui;

        public LootTableBrowserModule(ContentEditorContext context)
        {
            this.context = context;
        }

        public void OnEnable()
        {
            domain = new LootTableBrowserDomain(context);
            ui = new LootTableBrowserUI(domain);
        }

        public void OnDisable()
        {
            domain?.SavePrefs();
        }

        public void Build(VisualElement root)
        {
            ui.Build(root);
        }

        public void Refresh()
        {
            domain.RefreshAssets();

            if (context.ConsumeRequestedLootTable(out var requestedTable))
            {
                domain.OpenLootTableFromNavigation(requestedTable);
            }
            else
            {
                domain.Refilter();
                domain.RestoreSelection();
            }

            ui.Refresh();
        }

        public void OnUndoRedo()
        {
            Refresh();
        }
    }
}
#endif