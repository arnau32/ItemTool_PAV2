#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Barra reutilizable de tabs para cambiar entre secciones o categorías.
    /// Soporta color normal, color seleccionado y color de texto por tab.
    /// </summary>
    public class ToolTabBar : ToolUIElement
    {
        // ─────────────────────────────────────
        // Nested Types
        // ─────────────────────────────────────

        private sealed class TabStyleData
        {
            public Color NormalColor;
            public Color SelectedColor;
            public Color TextColor;
        }

        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public event Action<int> TabSelected;

        public int SelectedIndex { get; private set; } = -1;

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly List<ToolbarToggle> tabs = new();
        private readonly List<TabStyleData> tabStyles = new();

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolTabBar()
        {
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el layout horizontal de tabs.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexGrow = 0;
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un nuevo tab al final de la barra.
        /// Si es el primer tab, se selecciona automáticamente sin notificar.
        /// </summary>
        public void AddTab(string label)
        {
            int index = tabs.Count;

            var tab = CreateTab(label, index);

            tabs.Add(tab);
            tabStyles.Add(CreateDefaultStyle());

            Add(tab);

            if (tabs.Count == 1)
                SelectTab(0, notify: false);
            else
                RefreshTabVisual(index);
        }

        /// <summary>
        /// Cambia los colores visuales de un tab existente.
        /// </summary>
        public void SetTabColors(
            int index,
            Color normalColor,
            Color selectedColor,
            Color? textColor = null)
        {
            if (index < 0 || index >= tabStyles.Count)
                return;

            tabStyles[index].NormalColor = normalColor;
            tabStyles[index].SelectedColor = selectedColor;
            tabStyles[index].TextColor = textColor ?? Color.white;

            RefreshTabVisual(index);
        }

        /// <summary>
        /// Selecciona un tab por índice.
        /// Puede notificar o no mediante el evento TabSelected.
        /// </summary>
        public void SelectTab(int index, bool notify = true)
        {
            if (index < 0 || index >= tabs.Count)
                return;

            SelectedIndex = index;

            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].SetValueWithoutNotify(i == index);
                RefreshTabVisual(i);
            }

            if (notify)
                TabSelected?.Invoke(index);
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Crea un ToolbarToggle configurado como tab.
        /// </summary>
        private ToolbarToggle CreateTab(string label, int index)
        {
            var tab = new ToolbarToggle
            {
                text = label ?? string.Empty
            };

            tab.style.flexGrow = 1;
            tab.style.height = 22;
            tab.style.marginLeft = 2;
            tab.style.marginRight = 2;
            tab.style.paddingLeft = 8;
            tab.style.paddingRight = 8;
            tab.style.color = Color.white;
            tab.style.unityFontStyleAndWeight = FontStyle.Bold;

            tab.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                    return;

                SelectTab(index);
            });

            return tab;
        }

        /// <summary>
        /// Crea el estilo visual por defecto de un tab.
        /// </summary>
        private static TabStyleData CreateDefaultStyle()
        {
            return new TabStyleData
            {
                NormalColor = new Color(0.25f, 0.25f, 0.25f, 1f),
                SelectedColor = new Color(0.42f, 0.42f, 0.42f, 1f),
                TextColor = Color.white
            };
        }

        /// <summary>
        /// Refresca color y estado visual de un tab.
        /// </summary>
        private void RefreshTabVisual(int index)
        {
            if (index < 0 || index >= tabs.Count)
                return;

            var tab = tabs[index];
            var styleData = tabStyles[index];

            bool selected = index == SelectedIndex;

            var color = selected
                ? styleData.SelectedColor
                : styleData.NormalColor;

            tab.style.backgroundColor = color;
            tab.style.unityBackgroundImageTintColor = color;
            tab.style.color = styleData.TextColor;
        }
    }
}
#endif