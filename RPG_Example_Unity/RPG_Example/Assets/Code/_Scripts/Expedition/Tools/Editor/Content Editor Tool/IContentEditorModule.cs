#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace ContentEditor
{
    /// <summary>
    /// Contrato común para cada módulo de la Content Editor Tool.
    /// Permite que Items, Loot Tables y Expeditions funcionen como tabs independientes.
    /// </summary>
    public interface IContentEditorModule
    {
        ContentEditorTab Tab { get; }
        string DisplayName { get; }

        void OnEnable();
        void OnDisable();

        void Build(VisualElement root);
        void Refresh();

        void OnUndoRedo();
    }
}
#endif