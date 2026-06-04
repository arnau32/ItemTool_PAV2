#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Utilidades compartidas para trabajar con SerializedProperty dentro de componentes ToolUI.
    /// Centraliza operaciones repetidas como binding defensivo y reseteo de valores.
    /// </summary>
    public static class ToolSerializedPropertyUtility
    {
        #region Binding

        /// <summary>
        /// Bindea un campo si la propiedad existe.
        /// Si no existe, deshabilita el campo y muestra tooltip.
        /// </summary>
        public static void BindOrDisable<T>(
            T field,
            SerializedProperty property,
            string tooltip)
            where T : VisualElement, IBindable
        {
            if (field == null)
                return;

            if (property == null)
            {
                field.SetEnabled(false);
                field.tooltip = tooltip ?? string.Empty;
                return;
            }

            switch (field)
            {
                case EnumField enumField:
                    enumField.BindProperty(property);
                    break;

                case FloatField floatField:
                    floatField.BindProperty(property);
                    break;

                case ObjectField objectField:
                    objectField.BindProperty(property);
                    break;

                case PropertyField propertyField:
                    propertyField.BindProperty(property);
                    break;
            }

            field.SetEnabled(true);
            field.tooltip = string.Empty;
        }

        #endregion

        #region Reset

        /// <summary>
        /// Reinicia una propiedad enum al primer valor.
        /// </summary>
        public static void SetEnumToFirst(SerializedProperty element, string propertyName)
        {
            var prop = element?.FindPropertyRelative(propertyName);

            if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                prop.enumValueIndex = 0;
        }

        /// <summary>
        /// Asigna un valor float si la propiedad existe y es compatible.
        /// </summary>
        public static void SetFloat(
            SerializedProperty element,
            string propertyName,
            float value)
        {
            var prop = element?.FindPropertyRelative(propertyName);

            if (prop != null && prop.propertyType == SerializedPropertyType.Float)
                prop.floatValue = value;
        }

        /// <summary>
        /// Limpia una referencia de objeto si la propiedad existe.
        /// </summary>
        public static void ClearObjectReference(
            SerializedProperty element,
            string propertyName)
        {
            var prop = element?.FindPropertyRelative(propertyName);

            if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                prop.objectReferenceValue = null;
        }

        #endregion
    }
}
#endif