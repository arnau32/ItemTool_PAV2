#if UNITY_EDITOR
using UnityEngine;

namespace ContentEditor.Modules.Items
{
    public enum ItemValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Mensaje individual de validación para un ItemData.
    /// Permite acumular varios problemas en un mismo item.
    /// </summary>
    public readonly struct ItemValidationMessage
    {
        public readonly ItemValidationSeverity Severity;
        public readonly string Message;

        public ItemValidationMessage(ItemValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// Resultado agregado de validación para lista y panel de detalles.
    /// </summary>
    public readonly struct ItemValidationResult
    {
        public readonly ItemValidationSeverity HighestSeverity;
        public readonly string Indicator;
        public readonly Color Color;
        public readonly string Tooltip;
        public readonly bool HasIssues;

        public ItemValidationResult(
            ItemValidationSeverity highestSeverity,
            string indicator,
            Color color,
            string tooltip,
            bool hasIssues)
        {
            HighestSeverity = highestSeverity;
            Indicator = indicator;
            Color = color;
            Tooltip = tooltip;
            HasIssues = hasIssues;
        }

        public static ItemValidationResult None =>
            new(ItemValidationSeverity.None, "", Color.clear, "", false);
    }
}
#endif