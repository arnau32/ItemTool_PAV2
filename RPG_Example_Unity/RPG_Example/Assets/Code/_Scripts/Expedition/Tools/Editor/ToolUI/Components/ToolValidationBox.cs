#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Wrapper reutilizable para mostrar mensajes de validación.
    /// Centraliza visibilidad y configuración del HelpBox.
    /// </summary>
    public class ToolValidationBox : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public HelpBox HelpBox { get; private set; }

        /// <summary>
        /// Indica si el validation box está visible actualmente.
        /// </summary>
        public bool Visible => HelpBox.style.display == DisplayStyle.Flex;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolValidationBox()
        {
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el HelpBox interno.
        /// </summary>
        protected override void Build()
        {
            style.marginTop = ToolUISizes.SmallGap;
            style.marginBottom = ToolUISizes.LargeGap;

            HelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            HelpBox.style.display = DisplayStyle.None;

            Add(HelpBox);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Muestra un mensaje de validación.
        /// Si el mensaje es null o vacío, el validation box se oculta automáticamente.
        /// </summary>
        public void Show(string message, HelpBoxMessageType type)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Hide();
                return;
            }

            HelpBox.text = message;
            HelpBox.messageType = type;
            HelpBox.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Oculta el validation box y limpia el texto actual.
        /// </summary>
        public void Hide()
        {
            HelpBox.text = string.Empty;
            HelpBox.style.display = DisplayStyle.None;
        }
    }
}
#endif