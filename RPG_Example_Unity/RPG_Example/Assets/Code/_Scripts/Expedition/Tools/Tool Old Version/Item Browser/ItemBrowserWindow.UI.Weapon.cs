#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ItemBrowserWindow
{
    private sealed partial class UI
    {
        private VisualElement BuildWeaponSection(SerializedObject weaponSo)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var animatorProp = weaponSo.FindProperty("animatorOverride");
            var handTypeProp = weaponSo.FindProperty("handType");
            var familyProp = weaponSo.FindProperty("familyType");
            var prefabVariantProp = weaponSo.FindProperty("prefabVariant");

            var combosProp = weaponSo.FindProperty("combos");

            var dodgeSetProp = weaponSo.FindProperty("dodgeSet");
            var parryProp = weaponSo.FindProperty("parry");
            var weaponSkillProp = weaponSo.FindProperty("weaponSkill");
            var skillScoreNeededProp = weaponSo.FindProperty("skillScoreNeeded");

            var setupTitle = new Label("Weapon Setup");
            setupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            setupTitle.style.marginBottom = 6;
            root.Add(setupTitle);

            if (animatorProp != null) AddRow(root, new PropertyField(animatorProp, "Animator Override"));
            if (handTypeProp != null) AddRow(root, new PropertyField(handTypeProp, "Hand Type"));
            if (familyProp != null) AddRow(root, new PropertyField(familyProp, "Weapon Family"));
            if (prefabVariantProp != null) AddRow(root, new PropertyField(prefabVariantProp, "Prefab Variant"));

            root.Add(BuildSectionSpacer());

            var combatTitle = new Label("Combat References");
            combatTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            combatTitle.style.marginBottom = 6;
            root.Add(combatTitle);

            if (dodgeSetProp != null) AddRow(root, new PropertyField(dodgeSetProp, "Dodge Set"));
            if (parryProp != null) AddRow(root, new PropertyField(parryProp, "Parry"));
            if (weaponSkillProp != null) AddRow(root, new PropertyField(weaponSkillProp, "Weapon Skill"));

            if (skillScoreNeededProp != null)
            {
                var scoreField = new FloatField("Skill Score Needed");
                scoreField.BindProperty(skillScoreNeededProp);
                scoreField.RegisterValueChangedCallback(evt =>
                    ClampFloatProperty(scoreField, skillScoreNeededProp, evt.newValue, 0f, float.MaxValue));
                AddRow(root, scoreField);
            }

            root.Add(BuildSectionSpacer());

            var combosTitle = new Label("Combos");
            combosTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            combosTitle.style.marginBottom = 6;
            root.Add(combosTitle);

            if (combosProp != null)
            {
                root.Add(BuildCombosEditor(combosProp));
            }
            else
            {
                root.Add(new HelpBox("No se encontró la propiedad combos en WeaponData.", HelpBoxMessageType.Warning));
            }

            return root;
        }

        private VisualElement BuildCombosEditor(SerializedProperty combosProp)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginTop = 8;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 6;

            var addBtn = new Button(() => AddCombo(combosProp)) { text = "+ Add Combo" };
            addBtn.style.height = 20;

            var clearBtn = new Button(() => ClearCombos(combosProp)) { text = "Clear" };
            clearBtn.style.height = 20;
            clearBtn.style.marginLeft = 6;

            toolbar.Add(addBtn);
            toolbar.Add(clearBtn);
            container.Add(toolbar);

            var listRoot = new VisualElement();
            listRoot.style.flexDirection = FlexDirection.Column;
            container.Add(listRoot);

            void Rebuild()
            {
                listRoot.Clear();

                for (var i = 0; i < combosProp.arraySize; i++)
                {
                    var comboProp = combosProp.GetArrayElementAtIndex(i);
                    listRoot.Add(BuildSingleComboEditor(combosProp, comboProp, i, Rebuild));
                }
            }

            container.TrackPropertyValue(combosProp, _ =>
            {
                Rebuild();
                _w.MarkSeverityDirty();
                _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
            });

            Rebuild();
            return container;
        }

        private VisualElement BuildSingleComboEditor(SerializedProperty combosProp, SerializedProperty comboProp, int comboIndex, Action onChanged)
        {
            var wrapper = BuildCardBase();
            wrapper.style.marginBottom = 8;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            var title = new Label($"Combo {comboIndex + 1}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            var moveUpBtn = new Button(() =>
            {
                MoveCombo(combosProp, comboIndex, -1);
                onChanged?.Invoke();
            })
            { text = "↑" };
            moveUpBtn.style.width = 24;
            moveUpBtn.style.height = 18;
            moveUpBtn.style.marginRight = 4;
            header.Add(moveUpBtn);

            var moveDownBtn = new Button(() =>
            {
                MoveCombo(combosProp, comboIndex, 1);
                onChanged?.Invoke();
            })
            { text = "↓" };
            moveDownBtn.style.width = 24;
            moveDownBtn.style.height = 18;
            moveDownBtn.style.marginRight = 4;
            header.Add(moveDownBtn);

            var removeBtn = new Button(() =>
            {
                RemoveComboAt(combosProp, comboIndex);
                onChanged?.Invoke();
            })
            { text = "X" };
            removeBtn.style.width = 24;
            removeBtn.style.height = 18;
            header.Add(removeBtn);

            wrapper.Add(header);

            var stepsProp = comboProp.FindPropertyRelative("steps");
            if (stepsProp == null)
            {
                wrapper.Add(new HelpBox("No se encontró la propiedad steps en Combo.", HelpBoxMessageType.Warning));
                return wrapper;
            }

            var stepBar = new VisualElement();
            stepBar.style.flexDirection = FlexDirection.Row;
            stepBar.style.alignItems = Align.Center;
            stepBar.style.marginBottom = 6;

            var addStepBtn = new Button(() =>
            {
                AddComboStep(stepsProp);
                onChanged?.Invoke();
            })
            { text = "+ Add Step" };
            addStepBtn.style.height = 20;

            var clearStepsBtn = new Button(() =>
            {
                ClearComboSteps(stepsProp);
                onChanged?.Invoke();
            })
            { text = "Clear Steps" };
            clearStepsBtn.style.height = 20;
            clearStepsBtn.style.marginLeft = 6;

            stepBar.Add(addStepBtn);
            stepBar.Add(clearStepsBtn);
            wrapper.Add(stepBar);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            headerRow.Add(MakeFlexibleHeaderLabel("Input", 6));
            headerRow.Add(MakeFlexibleHeaderLabel("Attack", 6));
            headerRow.Add(MakeFixedHeaderLabel("", 24, 6));

            wrapper.Add(headerRow);

            for (var i = 0; i < stepsProp.arraySize; i++)
            {
                var stepProp = stepsProp.GetArrayElementAtIndex(i);
                wrapper.Add(BuildComboStepRow(stepsProp, stepProp, i, onChanged));
            }

            return wrapper;
        }

        private VisualElement BuildComboStepRow(SerializedProperty stepsProp, SerializedProperty stepProp, int stepIndex, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var inputProp = stepProp.FindPropertyRelative("input");
            var attackProp = stepProp.FindPropertyRelative("attack");

            var inputField = new EnumField();
            inputField.style.flexGrow = 1;
            inputField.style.flexShrink = 1;
            inputField.style.marginRight = 6;

            var attackField = new ObjectField
            {
                objectType = typeof(AttackData),
                allowSceneObjects = false
            };
            attackField.style.flexGrow = 1;
            attackField.style.flexShrink = 1;
            attackField.style.marginRight = 6;

            var removeBtn = new Button(() =>
            {
                RemoveComboStepAt(stepsProp, stepIndex);
                onChanged?.Invoke();
            })
            { text = "X" };
            removeBtn.style.width = 24;
            removeBtn.style.height = 18;

            if (inputProp != null) inputField.BindProperty(inputProp);
            else
            {
                inputField.SetEnabled(false);
                inputField.tooltip = "No se encontró input en ComboStep.";
            }

            if (attackProp != null) attackField.BindProperty(attackProp);
            else
            {
                attackField.SetEnabled(false);
                attackField.tooltip = "No se encontró attack en ComboStep.";
            }

            row.Add(inputField);
            row.Add(attackField);
            row.Add(removeBtn);

            return row;
        }

        private void AddCombo(SerializedProperty combosProp)
        {
            _w.SelectedSo.Update();

            var index = combosProp.arraySize;
            combosProp.InsertArrayElementAtIndex(index);

            var comboProp = combosProp.GetArrayElementAtIndex(index);
            ResetComboElement(comboProp);

            _w.SelectedSo.ApplyModifiedProperties();
            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void RemoveComboAt(SerializedProperty combosProp, int index)
        {
            if (index < 0 || index >= combosProp.arraySize) return;

            _w.SelectedSo.Update();
            combosProp.DeleteArrayElementAtIndex(index);
            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void ClearCombos(SerializedProperty combosProp)
        {
            if (!EditorUtility.DisplayDialog("Clear Combos",
                    "¿Seguro que quieres borrar TODOS los combos?", "Clear", "Cancel"))
                return;

            _w.SelectedSo.Update();
            combosProp.ClearArray();
            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void MoveCombo(SerializedProperty combosProp, int index, int direction)
        {
            var newIndex = index + direction;
            if (index < 0 || index >= combosProp.arraySize) return;
            if (newIndex < 0 || newIndex >= combosProp.arraySize) return;

            _w.SelectedSo.Update();
            combosProp.MoveArrayElement(index, newIndex);
            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private static void ResetComboElement(SerializedProperty comboProp)
        {
            if (comboProp == null) return;

            var stepsProp = comboProp.FindPropertyRelative("steps");
            if (stepsProp != null && stepsProp.isArray)
                stepsProp.ClearArray();
        }

        private void AddComboStep(SerializedProperty stepsProp)
        {
            _w.SelectedSo.Update();

            var index = stepsProp.arraySize;
            stepsProp.InsertArrayElementAtIndex(index);

            var stepProp = stepsProp.GetArrayElementAtIndex(index);
            ResetComboStepElement(stepProp);

            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void RemoveComboStepAt(SerializedProperty stepsProp, int index)
        {
            if (index < 0 || index >= stepsProp.arraySize) return;

            _w.SelectedSo.Update();
            stepsProp.DeleteArrayElementAtIndex(index);
            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private void ClearComboSteps(SerializedProperty stepsProp)
        {
            _w.SelectedSo.Update();
            stepsProp.ClearArray();
            _w.SelectedSo.ApplyModifiedProperties();

            _w.MarkSeverityDirty();
            _w.RequestRefreshDebounced(RefreshFlags.Validations | RefreshFlags.List | RefreshFlags.Details);
        }

        private static void ResetComboStepElement(SerializedProperty stepProp)
        {
            if (stepProp == null) return;

            var inputProp = stepProp.FindPropertyRelative("input");
            if (inputProp != null && inputProp.propertyType == SerializedPropertyType.Enum)
                inputProp.enumValueIndex = 0;

            var attackProp = stepProp.FindPropertyRelative("attack");
            if (attackProp != null && attackProp.propertyType == SerializedPropertyType.ObjectReference)
                attackProp.objectReferenceValue = null;
        }
    }
}
#endif