#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Editor reutilizable para listas de StatModifier.
    /// Permite añadir, limpiar, eliminar y editar modifiers de una lista serializada.
    /// </summary>
    public class ToolStatModifiersEditor : ToolUIElement
    {
        #region Constants

        private const float RowHeight = 26f;
        private const float TypeFieldWidth = 110f;
        private const float ValueFieldWidth = 90f;
        private const float RemoveButtonWidth = 24f;

        #endregion

        #region Fields

        private readonly SerializedObject serializedObject;
        private readonly SerializedProperty listProperty;
        private readonly Action onChanged;

        private readonly List<int> indices = new();

        private ListView listView;

        #endregion

        #region Constructor

        /// <summary>
        /// Crea un editor de StatModifiers sobre una lista serializada.
        /// </summary>
        public ToolStatModifiersEditor(
            SerializedObject serializedObject,
            SerializedProperty listProperty,
            Action onChanged = null)
        {
            this.serializedObject = serializedObject;
            this.listProperty = listProperty;
            this.onChanged = onChanged;

            Initialize();
        }

        #endregion

        #region Build

        /// <summary>
        /// Construye la UI principal del editor.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Column;

            if (!IsValid())
            {
                Add(new HelpBox("StatModifiers inválidos o no encontrados.", HelpBoxMessageType.Warning));
                return;
            }

            var toolbar = new ToolConfirmToolbar(
                onAdd: AddModifier,
                onSort: null,
                onClear: ClearModifiers,
                addText: "+ Add Modifier",
                sortText: "Sort",
                clearText: "Clear");

            toolbar.SetSortVisible(false);

            Add(toolbar);

            listView = BuildListView();

            Add(listView);
            RebuildIndexSource();
        }

        #endregion

        #region ListView

        /// <summary>
        /// Crea el ListView usado para editar los modifiers.
        /// </summary>
        private ListView BuildListView()
        {
            var list = new ListView
            {
                reorderable = true,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };

            list.makeItem = MakeRow;
            list.bindItem = BindRow;
            list.itemIndexChanged += OnItemIndexChanged;

            return list;
        }

        /// <summary>
        /// Crea una fila visual reciclable del ListView.
        /// </summary>
        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = RowHeight;

            row.Add(CreateEnumField("statTypeAffected", grow: true));
            row.Add(CreateEnumField("type", minWidth: TypeFieldWidth));
            row.Add(CreateFloatField("value", ValueFieldWidth));
            row.Add(CreateRemoveButton());

            row.userData = new RowBinding();

            return row;
        }

        /// <summary>
        /// Bindea una fila reciclada al índice actual de la lista serializada.
        /// </summary>
        private void BindRow(VisualElement row, int listIndex)
        {
            int arrayIndex = indices[listIndex];

            if (arrayIndex < 0 || arrayIndex >= listProperty.arraySize)
                return;

            var element = listProperty.GetArrayElementAtIndex(arrayIndex);

            var statProp = element.FindPropertyRelative("statTypeAffected");
            var typeProp = element.FindPropertyRelative("type");
            var valueProp = element.FindPropertyRelative("value");

            var statField = row.Q<EnumField>("statTypeAffected");
            var typeField = row.Q<EnumField>("type");
            var valueField = row.Q<FloatField>("value");
            var removeBtn = row.Q<Button>("remove");

            statField.Unbind();
            typeField.Unbind();
            valueField.Unbind();

            ToolSerializedPropertyUtility.BindOrDisable(
                statField,
                statProp,
                "No se encontró statTypeAffected.");

            ToolSerializedPropertyUtility.BindOrDisable(
                typeField,
                typeProp,
                "No se encontró type.");

            ToolSerializedPropertyUtility.BindOrDisable(
                valueField,
                valueProp,
                "No se encontró value.");

            var binding = (RowBinding)row.userData;

            if (binding.RemoveHandler != null)
                removeBtn.clicked -= binding.RemoveHandler;

            binding.ArrayIndex = arrayIndex;
            binding.RemoveHandler = () => RemoveModifierAt(binding.ArrayIndex);

            removeBtn.clicked += binding.RemoveHandler;
        }

        /// <summary>
        /// Reconstruye el origen de índices usado por el ListView.
        /// </summary>
        private void RebuildIndexSource()
        {
            indices.Clear();

            for (int i = 0; i < listProperty.arraySize; i++)
                indices.Add(i);

            listView.itemsSource = indices;
            listView.RefreshItems();
        }

        #endregion

        #region Operations

        /// <summary>
        /// Añade un nuevo StatModifier inicializado con valores seguros.
        /// </summary>
        private void AddModifier()
        {
            serializedObject.Update();

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);

            ResetModifier(listProperty.GetArrayElementAtIndex(index));

            serializedObject.ApplyModifiedProperties();

            RebuildIndexSource();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina el StatModifier indicado.
        /// </summary>
        private void RemoveModifierAt(int index)
        {
            if (index < 0 || index >= listProperty.arraySize)
                return;

            serializedObject.Update();

            listProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            RebuildIndexSource();
            NotifyChanged();
        }

        /// <summary>
        /// Limpia todos los StatModifiers previa confirmación.
        /// </summary>
        private void ClearModifiers()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Stat Modifiers",
                    "¿Seguro que quieres borrar TODOS los Stat Modifiers?",
                    "Clear",
                    "Cancel"))
                return;

            serializedObject.Update();

            listProperty.ClearArray();

            serializedObject.ApplyModifiedProperties();

            RebuildIndexSource();
            NotifyChanged();
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Notifica cambios cuando Unity reordena elementos del ListView.
        /// </summary>
        private void OnItemIndexChanged(int oldIndex, int newIndex)
        {
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();

            RebuildIndexSource();
            NotifyChanged();
        }

        #endregion

        #region Factory Helpers

        /// <summary>
        /// Crea un campo enum para una propiedad de la fila.
        /// </summary>
        private static EnumField CreateEnumField(
            string name,
            bool grow = false,
            float minWidth = 0f)
        {
            var field = new EnumField
            {
                name = name
            };

            if (grow)
            {
                field.style.flexGrow = 1;
                field.style.flexShrink = 1;
            }

            if (minWidth > 0f)
            {
                field.style.minWidth = minWidth;
                field.style.flexShrink = 1;
            }

            field.style.marginRight = ToolUISizes.Gap;

            return field;
        }

        /// <summary>
        /// Crea un campo float para una propiedad de la fila.
        /// </summary>
        private static FloatField CreateFloatField(string name, float minWidth)
        {
            var field = new FloatField
            {
                name = name
            };

            field.style.minWidth = minWidth;
            field.style.flexShrink = 1;
            field.style.marginRight = ToolUISizes.Gap;

            return field;
        }

        /// <summary>
        /// Crea el botón de eliminar usado por cada fila.
        /// </summary>
        private static Button CreateRemoveButton()
        {
            var button = new Button
            {
                name = "remove",
                text = "X"
            };

            button.style.width = RemoveButtonWidth;
            button.style.height = ToolUISizes.SmallButtonHeight;

            return button;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Comprueba que el SerializedObject y la lista son válidos.
        /// </summary>
        private bool IsValid()
        {
            return serializedObject != null &&
                   listProperty != null &&
                   listProperty.isArray;
        }

        /// <summary>
        /// Reinicia un StatModifier recién creado.
        /// </summary>
        private static void ResetModifier(SerializedProperty element)
        {
            if (element == null)
                return;

            ToolSerializedPropertyUtility.SetEnumToFirst(element, "statTypeAffected");
            ToolSerializedPropertyUtility.SetEnumToFirst(element, "type");
            ToolSerializedPropertyUtility.SetFloat(element, "value", 0f);
        }

        /// <summary>
        /// Notifica al consumidor externo que el editor ha cambiado.
        /// </summary>
        private void NotifyChanged()
        {
            onChanged?.Invoke();
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Estado asociado a una fila reciclada del ListView.
        /// Evita acumular callbacks de remove al rebindear filas.
        /// </summary>
        private sealed class RowBinding
        {
            public int ArrayIndex;
            public Action RemoveHandler;
        }

        #endregion
    }
}
#endif