#if UNITY_EDITOR
using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Campo de búsqueda reutilizable para toolbars.
    /// Encapsula ToolbarSearchField y expone un evento simple con el texto actual.
    /// </summary>
    public class ToolSearchToolbar : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public event Action<string> SearchChanged;

        public string Value => searchField?.value ?? string.Empty;

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly string tooltip;
        private ToolbarSearchField searchField;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolSearchToolbar(string tooltip = "Search")
        {
            this.tooltip = tooltip;
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el campo de búsqueda y registra el callback de cambio.
        /// </summary>
        protected override void Build()
        {
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minWidth = 240;

            searchField = new ToolbarSearchField
            {
                tooltip = tooltip ?? string.Empty
            };

            searchField.style.flexGrow = 1;
            searchField.style.flexShrink = 1;
            searchField.style.minWidth = 240;

            searchField.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));

            Add(searchField);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Cambia el valor del campo sin disparar eventos.
        /// Útil para restaurar preferencias o sincronizar estado externo.
        /// </summary>
        public void SetValueWithoutNotify(string value)
        {
            searchField?.SetValueWithoutNotify(value ?? string.Empty);
        }

        /// <summary>
        /// Limpia el campo de búsqueda disparando el flujo normal de cambio.
        /// </summary>
        public void Clear()
        {
            if (searchField == null)
                return;

            searchField.value = string.Empty;
        }
    }
}
#endif