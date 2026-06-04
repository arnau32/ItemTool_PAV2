#if UNITY_EDITOR
using ContentEditor;
using UnityEngine.UIElements;

namespace ContentEditor.Modules.Items
{
    /// <summary>
    /// Módulo de Items dentro de la Content Editor Tool.
    /// De momento construye la estructura base: toolbar, lista y panel de detalles.
    /// Más adelante absorberá la lógica actual de ItemBrowserWindow.
    /// </summary>
    public sealed class ItemBrowserModule : IContentEditorModule
    {
        public ContentEditorTab Tab => ContentEditorTab.Items;
        public string DisplayName => "Items";

        private readonly ContentEditorContext context;

        private ItemBrowserDomain domain;
        private ItemBrowserUI ui;

        public ItemBrowserModule(ContentEditorContext context)
        {
            this.context = context;
        }

        public void OnEnable()
        {
            domain = new ItemBrowserDomain(context);
            ui = new ItemBrowserUI(domain);
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

            if (context.ConsumeRequestedItem(out var requestedItem))
            {
                domain.OpenItemFromNavigation(requestedItem);
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