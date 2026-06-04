#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Card reutilizable para previews, bloques visuales o agrupaciones de contenido.
    /// Aplica padding, borde y helpers para añadir cabeceras e imágenes.
    /// </summary>
    public class ToolCard : ToolUIElement
    {
        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolCard()
        {
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el estilo visual base de la card.
        /// </summary>
        protected override void Build()
        {
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;

            var border = new Color(0f, 0f, 0f, 0.25f);

            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un título en negrita dentro de la card.
        /// </summary>
        public Label AddHeader(string text)
        {
            var label = new Label(text ?? string.Empty);

            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.85f;
            label.style.marginBottom = ToolUISizes.Gap;

            Add(label);

            return label;
        }

        /// <summary>
        /// Añade una imagen centrada para preview visual.
        /// </summary>
        public Image AddPreviewImage(float size = ToolUISizes.PreviewIcon)
        {
            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };

            image.style.width = size;
            image.style.height = size;
            image.style.alignSelf = Align.Center;
            image.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);

            Add(image);

            return image;
        }

        /// <summary>
        /// Limpia todo el contenido añadido a la card.
        /// </summary>
        public void ClearContent()
        {
            Clear();
        }
    }
}
#endif