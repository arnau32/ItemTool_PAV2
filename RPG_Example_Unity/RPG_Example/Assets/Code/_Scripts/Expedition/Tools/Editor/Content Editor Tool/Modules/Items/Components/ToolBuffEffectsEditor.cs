#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Editor reutilizable para listas de BuffEffect.
    /// Permite añadir, limpiar, eliminar y editar efectos de consumibles.
    /// </summary>
    public class ToolBuffEffectsEditor : ToolUIElement
    {
        #region Constants

        private const float RowHeight = 26f;
        private const float FloatFieldWidth = 90f;
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
        /// Crea un editor de BuffEffects sobre una lista serializada.
        /// </summary>
        public ToolBuffEffectsEditor(
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
                Add(new HelpBox("BuffEffects inválidos o no encontrados.", HelpBoxMessageType.Warning));
                return;
            }

            var toolbar = new ToolConfirmToolbar(
                onAdd: AddBuff,
                onSort: null,
                onClear: ClearBuffs,
                addText: "+ Add Buff",
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
        /// Crea el ListView usado para editar los buffs.
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

            row.Add(CreateEnumField("applicationMode"));
            row.Add(CreateEnumField("statType"));
            row.Add(CreateEnumField("modifierType"));
            row.Add(CreateFloatField("baseValue"));
            row.Add(CreateFloatField("duration"));
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

            var modeProp = element.FindPropertyRelative("applicationMode");
            var statProp = element.FindPropertyRelative("statType");
            var modifierProp = element.FindPropertyRelative("modifierType");
            var valueProp = element.FindPropertyRelative("baseValue");
            var durationProp = element.FindPropertyRelative("duration");

            var modeField = row.Q<EnumField>("applicationMode");
            var statField = row.Q<EnumField>("statType");
            var modifierField = row.Q<EnumField>("modifierType");
            var valueField = row.Q<FloatField>("baseValue");
            var durationField = row.Q<FloatField>("duration");
            var removeBtn = row.Q<Button>("remove");

            modeField.Unbind();
            statField.Unbind();
            modifierField.Unbind();
            valueField.Unbind();
            durationField.Unbind();

            ToolSerializedPropertyUtility.BindOrDisable(
                modeField,
                modeProp,
                "No se encontró applicationMode.");

            ToolSerializedPropertyUtility.BindOrDisable(
                statField,
                statProp,
                "No se encontró statType.");

            ToolSerializedPropertyUtility.BindOrDisable(
                modifierField,
                modifierProp,
                "No se encontró modifierType.");

            ToolSerializedPropertyUtility.BindOrDisable(
                valueField,
                valueProp,
                "No se encontró baseValue.");

            ToolSerializedPropertyUtility.BindOrDisable(
                durationField,
                durationProp,
                "No se encontró duration.");

            var binding = (RowBinding)row.userData;

            if (binding.RemoveHandler != null)
                removeBtn.clicked -= binding.RemoveHandler;

            binding.ArrayIndex = arrayIndex;
            binding.RemoveHandler = () => RemoveBuffAt(binding.ArrayIndex);

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
        /// Añade un nuevo BuffEffect inicializado con valores seguros.
        /// </summary>
        private void AddBuff()
        {
            serializedObject.Update();

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);

            ResetBuff(listProperty.GetArrayElementAtIndex(index));

            serializedObject.ApplyModifiedProperties();

            RebuildIndexSource();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina el BuffEffect indicado.
        /// </summary>
        private void RemoveBuffAt(int index)
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
        /// Limpia todos los BuffEffects previa confirmación.
        /// </summary>
        private void ClearBuffs()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Buff Effects",
                    "¿Seguro que quieres borrar TODOS los Buff Effects?",
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
        /// Crea un campo enum de fila.
        /// </summary>
        private static EnumField CreateEnumField(string name)
        {
            var field = new EnumField
            {
                name = name
            };

            field.style.flexGrow = 1;
            field.style.marginRight = ToolUISizes.Gap;

            return field;
        }

        /// <summary>
        /// Crea un campo float de fila.
        /// </summary>
        private static FloatField CreateFloatField(string name)
        {
            var field = new FloatField
            {
                name = name
            };

            field.style.width = FloatFieldWidth;
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
        /// Reinicia un BuffEffect recién creado.
        /// </summary>
        private static void ResetBuff(SerializedProperty element)
        {
            if (element == null)
                return;

            ToolSerializedPropertyUtility.SetEnumToFirst(element, "applicationMode");
            ToolSerializedPropertyUtility.SetEnumToFirst(element, "statType");
            ToolSerializedPropertyUtility.SetEnumToFirst(element, "modifierType");

            ToolSerializedPropertyUtility.SetFloat(element, "baseValue", 0f);
            ToolSerializedPropertyUtility.SetFloat(element, "duration", 0f);
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