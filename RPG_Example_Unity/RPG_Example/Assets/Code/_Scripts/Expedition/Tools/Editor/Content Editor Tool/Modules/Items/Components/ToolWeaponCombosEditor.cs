#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Editor reutilizable para listas de combos de WeaponData.
    /// Permite añadir, eliminar y limpiar combos, además de editar sus steps.
    /// </summary>
    public class ToolWeaponCombosEditor : ToolUIElement
    {
        #region Constants

        private const float RowHeight = 26f;
        private const float RemoveButtonWidth = 24f;

        #endregion

        #region Fields

        private readonly SerializedObject serializedObject;
        private readonly SerializedProperty combosProperty;
        private readonly Action onChanged;

        private VisualElement listRoot;

        #endregion

        #region Constructor

        /// <summary>
        /// Crea un editor de combos sobre una lista serializada.
        /// </summary>
        public ToolWeaponCombosEditor(
            SerializedObject serializedObject,
            SerializedProperty combosProperty,
            Action onChanged = null)
        {
            this.serializedObject = serializedObject;
            this.combosProperty = combosProperty;
            this.onChanged = onChanged;

            Initialize();
        }

        #endregion

        #region Build

        /// <summary>
        /// Construye el editor visual de combos.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Column;

            if (!IsValid())
            {
                Add(new HelpBox("Combos inválidos o no encontrados.", HelpBoxMessageType.Warning));
                return;
            }

            var toolbar = new ToolConfirmToolbar(
                onAdd: AddCombo,
                onSort: null,
                onClear: ClearCombos,
                addText: "+ Add Combo",
                sortText: "Sort",
                clearText: "Clear");

            toolbar.SetSortVisible(false);

            Add(toolbar);

            listRoot = new VisualElement();
            listRoot.style.flexDirection = FlexDirection.Column;

            Add(listRoot);

            Rebuild();
        }

        #endregion

        #region Refresh

        /// <summary>
        /// Reconstruye todas las cards de combos y steps.
        /// </summary>
        private void Rebuild()
        {
            if (listRoot == null)
                return;

            serializedObject.Update();

            listRoot.Clear();

            for (int i = 0; i < combosProperty.arraySize; i++)
            {
                var comboProp = combosProperty.GetArrayElementAtIndex(i);
                listRoot.Add(BuildComboCard(comboProp, i));
            }
        }

        #endregion

        #region Combo UI

        /// <summary>
        /// Construye la card visual de un combo.
        /// </summary>
        private VisualElement BuildComboCard(SerializedProperty comboProp, int comboIndex)
        {
            var card = new ToolCard();
            card.style.marginBottom = ToolUISizes.LargeGap;

            card.Add(BuildComboHeader(comboIndex));

            var stepsProp = comboProp?.FindPropertyRelative("steps");

            if (stepsProp == null || !stepsProp.isArray)
            {
                card.Add(new HelpBox(
                    "No se encontró la propiedad array 'steps' en Combo.",
                    HelpBoxMessageType.Warning));

                return card;
            }

            card.Add(BuildStepsEditor(stepsProp));

            return card;
        }

        /// <summary>
        /// Construye la cabecera de una card de combo.
        /// </summary>
        private VisualElement BuildComboHeader(int comboIndex)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = ToolUISizes.Gap;

            var title = new Label($"Combo {comboIndex + 1}");
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;

            var removeBtn = CreateRemoveButton(() => RemoveComboAt(comboIndex));

            header.Add(title);
            header.Add(removeBtn);

            return header;
        }

        #endregion

        #region Steps UI

        /// <summary>
        /// Construye el editor visual de steps de un combo.
        /// </summary>
        private VisualElement BuildStepsEditor(SerializedProperty stepsProp)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var toolbar = new ToolConfirmToolbar(
                onAdd: () => AddStep(stepsProp),
                onSort: null,
                onClear: () => ClearSteps(stepsProp),
                addText: "+ Add Step",
                sortText: "Sort",
                clearText: "Clear Steps");

            toolbar.SetSortVisible(false);

            root.Add(toolbar);

            for (int i = 0; i < stepsProp.arraySize; i++)
            {
                var stepProp = stepsProp.GetArrayElementAtIndex(i);
                root.Add(BuildStepRow(stepsProp, stepProp, i));
            }

            return root;
        }

        /// <summary>
        /// Construye una fila editable de step.
        /// </summary>
        private VisualElement BuildStepRow(
            SerializedProperty stepsProp,
            SerializedProperty stepProp,
            int stepIndex)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = RowHeight;
            row.style.marginBottom = ToolUISizes.SmallGap;

            var inputProp = stepProp?.FindPropertyRelative("input");
            var attackProp = stepProp?.FindPropertyRelative("attack");

            var inputField = CreateInputField();
            var attackField = CreateAttackField();

            ToolSerializedPropertyUtility.BindOrDisable(
                inputField,
                inputProp,
                "No se encontró input.");

            ToolSerializedPropertyUtility.BindOrDisable(
                attackField,
                attackProp,
                "No se encontró attack.");

            var removeBtn = CreateRemoveButton(() => RemoveStepAt(stepsProp, stepIndex));

            row.Add(inputField);
            row.Add(attackField);
            row.Add(removeBtn);

            return row;
        }

        #endregion

        #region Combo Operations

        /// <summary>
        /// Añade un combo vacío.
        /// </summary>
        private void AddCombo()
        {
            serializedObject.Update();

            int index = combosProperty.arraySize;
            combosProperty.InsertArrayElementAtIndex(index);

            var comboProp = combosProperty.GetArrayElementAtIndex(index);
            var stepsProp = comboProp.FindPropertyRelative("steps");

            if (stepsProp != null && stepsProp.isArray)
                stepsProp.ClearArray();

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina el combo indicado.
        /// </summary>
        private void RemoveComboAt(int index)
        {
            if (index < 0 || index >= combosProperty.arraySize)
                return;

            serializedObject.Update();

            combosProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina todos los combos previa confirmación.
        /// </summary>
        private void ClearCombos()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Combos",
                    "¿Seguro que quieres borrar TODOS los combos?",
                    "Clear",
                    "Cancel"))
                return;

            serializedObject.Update();

            combosProperty.ClearArray();

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        #endregion

        #region Step Operations

        /// <summary>
        /// Añade un step vacío al combo indicado.
        /// </summary>
        private void AddStep(SerializedProperty stepsProp)
        {
            if (stepsProp == null || !stepsProp.isArray)
                return;

            serializedObject.Update();

            int index = stepsProp.arraySize;
            stepsProp.InsertArrayElementAtIndex(index);

            ResetStep(stepsProp.GetArrayElementAtIndex(index));

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina un step concreto.
        /// </summary>
        private void RemoveStepAt(SerializedProperty stepsProp, int index)
        {
            if (stepsProp == null || !stepsProp.isArray)
                return;

            if (index < 0 || index >= stepsProp.arraySize)
                return;

            serializedObject.Update();

            stepsProp.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        /// <summary>
        /// Elimina todos los steps de un combo previa confirmación.
        /// </summary>
        private void ClearSteps(SerializedProperty stepsProp)
        {
            if (stepsProp == null || !stepsProp.isArray)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Clear Combo Steps",
                    "¿Seguro que quieres borrar TODOS los steps de este combo?",
                    "Clear",
                    "Cancel"))
                return;

            serializedObject.Update();

            stepsProp.ClearArray();

            serializedObject.ApplyModifiedProperties();

            Rebuild();
            NotifyChanged();
        }

        #endregion

        #region Factory Helpers

        /// <summary>
        /// Crea el campo de input del step.
        /// </summary>
        private static EnumField CreateInputField()
        {
            var field = new EnumField();

            field.style.flexGrow = 1;
            field.style.marginRight = ToolUISizes.Gap;

            return field;
        }

        /// <summary>
        /// Crea el campo de AttackData del step.
        /// </summary>
        private static ObjectField CreateAttackField()
        {
            var field = new ObjectField
            {
                objectType = typeof(AttackData),
                allowSceneObjects = false
            };

            field.style.flexGrow = 1;
            field.style.marginRight = ToolUISizes.Gap;

            return field;
        }

        /// <summary>
        /// Crea un botón pequeño de eliminación.
        /// </summary>
        private static Button CreateRemoveButton(Action onClick)
        {
            var button = new Button(() => onClick?.Invoke())
            {
                text = "X"
            };

            button.style.width = RemoveButtonWidth;
            button.style.height = ToolUISizes.SmallButtonHeight;

            button.SetEnabled(onClick != null);

            return button;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Comprueba que las referencias serializadas del editor son válidas.
        /// </summary>
        private bool IsValid()
        {
            return serializedObject != null &&
                   combosProperty != null &&
                   combosProperty.isArray;
        }

        /// <summary>
        /// Reinicia los valores de un step recién creado.
        /// </summary>
        private static void ResetStep(SerializedProperty stepProp)
        {
            if (stepProp == null)
                return;

            ToolSerializedPropertyUtility.SetEnumToFirst(stepProp, "input");
            ToolSerializedPropertyUtility.ClearObjectReference(stepProp, "attack");
        }

        /// <summary>
        /// Notifica cambios al consumidor del componente.
        /// </summary>
        private void NotifyChanged()
        {
            onChanged?.Invoke();
        }

        #endregion
    }
}
#endif