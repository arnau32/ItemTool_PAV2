#if UNITY_EDITOR
using System.Collections.Generic;
using ToolUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ContentEditor.Modules.Items;
using ContentEditor.Modules.LootTables;

namespace ContentEditor
{
    /// <summary>
    /// Ventana principal fusionada para editar Items, Loot Tables y contenido de Expeditions.
    /// Usa módulos independientes para mantener separada la lógica de cada tool.
    /// </summary>
    public sealed class ContentEditorWindow : EditorWindow
    {
        private const string PrefActiveTab = "BS.ContentEditor.ActiveTab";

        private static readonly Vector2 MinWindowSize = new(1200f, 700f);

        private readonly Dictionary<ContentEditorTab, IContentEditorModule> modules = new();

        private ContentEditorContext context;
        private ContentEditorTab currentTab;

        private ToolTabBar tabBar;
        private VisualElement contentRoot;

        [MenuItem("Tools/Content Editors/Content Editor")]
        public static void Open()
        {
            var window = GetWindow<ContentEditorWindow>("Content Editor");
            window.minSize = MinWindowSize;
        }

        private void OnEnable()
        {
            minSize = MinWindowSize;

            Undo.undoRedoPerformed += OnUndoRedo;
            
            context = new ContentEditorContext(this);
            context.TabChangeRequested += SelectTab;

            currentTab = (ContentEditorTab)EditorPrefs.GetInt(PrefActiveTab, (int)ContentEditorTab.Items);

            RegisterModules();

            foreach (var module in modules.Values)
                module.OnEnable();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt(PrefActiveTab, (int)currentTab);

            Undo.undoRedoPerformed -= OnUndoRedo;
            
            if (context != null)
                context.TabChangeRequested -= SelectTab;

            foreach (var module in modules.Values)
                module.OnDisable();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            BuildTabs();
            BuildContentRoot();

            SelectTab(currentTab);
        }

        private void RegisterModules()
        {
            modules.Add(ContentEditorTab.Expeditions, new ContentEditorPlaceholderModule(
                ContentEditorTab.Expeditions,
                "Expeditions",
                "Expedition Browser se migrará al final."));

            modules.Add(ContentEditorTab.Items, new ItemBrowserModule(context));

            modules.Add(ContentEditorTab.LootTables, new LootTableBrowserModule(context));

        }

        private void BuildTabs()
        {
            tabBar = new ToolTabBar();

            tabBar.AddTab("Expeditions");
            tabBar.AddTab("Items");
            tabBar.AddTab("Loot Tables");

            tabBar.TabSelected += index =>
            {
                SelectTab((ContentEditorTab)index);
            };

            rootVisualElement.Add(tabBar);
        }

        private void BuildContentRoot()
        {
            contentRoot = new VisualElement();
            contentRoot.style.flexGrow = 1;
            contentRoot.style.minHeight = 0;

            rootVisualElement.Add(contentRoot);
        }

        public void SelectTab(ContentEditorTab tab)
        {
            currentTab = tab;
            EditorPrefs.SetInt(PrefActiveTab, (int)currentTab);

            if (tabBar != null)
                tabBar.SelectTab((int)tab, false);

            contentRoot?.Clear();

            if (modules.TryGetValue(tab, out var module))
            {
                module.Build(contentRoot);
                module.Refresh();
            }
        }
        
        private void OnUndoRedo()
        {
            if (modules.TryGetValue(currentTab, out var module))
                module.OnUndoRedo();
        }
    }
}
#endif