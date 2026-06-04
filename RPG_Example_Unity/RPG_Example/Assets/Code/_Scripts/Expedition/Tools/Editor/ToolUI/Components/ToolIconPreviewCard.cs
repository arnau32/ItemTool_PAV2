#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Card reutilizable para previsualizar sprites o texturas.
    /// Se usa en editores de items, loot, enemigos u otros assets con icono.
    /// </summary>
    public class ToolIconPreviewCard : ToolCard
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Image PreviewImage { get; private set; }

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolIconPreviewCard(
            string title = "Icon Preview",
            float imageSize = ToolUISizes.PreviewIcon)
        {
            AddHeader(title);
            PreviewImage = AddPreviewImage(imageSize);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Muestra el sprite indicado en la preview.
        /// Si es null, limpia la imagen y reduce la opacidad.
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            SetTexture(sprite != null ? sprite.texture : null);
        }

        /// <summary>
        /// Muestra la textura indicada en la preview.
        /// Si es null, limpia la imagen y reduce la opacidad.
        /// </summary>
        public void SetTexture(Texture texture)
        {
            if (PreviewImage == null)
                return;

            PreviewImage.image = texture;
            PreviewImage.style.opacity = texture != null ? 1f : 0.25f;
        }
    }
}
#endif