#if UNITY_EDITOR
using ToolUI;
using UnityEngine.UIElements;

namespace ContentEditor
{
    /// <summary>
    /// Módulo temporal usado mientras se migran las tools reales.
    /// Permite validar la arquitectura de tabs antes de mover lógica compleja.
    /// </summary>
    public sealed class ContentEditorPlaceholderModule : IContentEditorModule
    {
        public ContentEditorTab Tab { get; }
        public string DisplayName { get; }

        private readonly string message;

        public ContentEditorPlaceholderModule(ContentEditorTab tab, string displayName, string message)
        {
            Tab = tab;
            DisplayName = displayName;
            this.message = message;
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }

        public void Build(VisualElement root)
        {
            root.Clear();
            root.Add(new ToolEmptyState(message));
        }

        public void Refresh()
        {
        }
        
        public void OnUndoRedo()
        {
        }
    }
}
#endif