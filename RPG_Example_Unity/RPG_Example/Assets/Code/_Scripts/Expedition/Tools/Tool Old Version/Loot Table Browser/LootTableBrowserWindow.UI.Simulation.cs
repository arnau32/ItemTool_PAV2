#if UNITY_EDITOR
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow
{
    private sealed partial class UI
    {
        private VisualElement BuildSimulationStatsSectionOnly()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var controls = new VisualElement();
            controls.style.flexDirection = _w.position.width < DetailsResponsiveBreak
                ? FlexDirection.Column
                : FlexDirection.Row;
            controls.style.alignItems = Align.FlexStart;
            controls.style.marginBottom = 8;

            _w.SimIterationsField = new IntegerField("Iterations");
            _w.SimIterationsField.value = 100;
            _w.SimIterationsField.style.width = 180;
            controls.Add(_w.SimIterationsField);

            _w.SimRollOnceBtn = new Button(() =>
            {
                _w._domain.RunSimulation(1);
            })
            { text = "Roll Once" };
            _w.SimRollOnceBtn.style.marginLeft = 8;
            _w.SimRollOnceBtn.style.height = 20;
            controls.Add(_w.SimRollOnceBtn);

            _w.SimRunBtn = new Button(() =>
            {
                var iterations = _w.SimIterationsField != null ? Mathf.Max(1, _w.SimIterationsField.value) : 100;
                _w._domain.RunSimulation(iterations);
            })
            { text = "Run Simulation" };
            _w.SimRunBtn.style.marginLeft = 6;
            _w.SimRunBtn.style.height = 20;
            controls.Add(_w.SimRunBtn);

            root.Add(controls);

            _w.SimSummaryLabel = new Label("Sin simulación.");
            _w.SimSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _w.SimSummaryLabel.style.marginBottom = 6;
            root.Add(_w.SimSummaryLabel);

            _w.SimResultsRoot = new VisualElement();
            _w.SimResultsRoot.style.flexDirection = FlexDirection.Column;
            root.Add(_w.SimResultsRoot);

            RefreshSimulationView();
            return root;
        }

        private VisualElement BuildInventoryPreviewPanel()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var title = new Label("Inventory Preview (6x3)");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            root.Add(title);

            _w.SimInventoryInfoLabel = new Label("Inventory Preview | Resultado del último Roll.");
            _w.SimInventoryInfoLabel.style.marginBottom = 6;
            root.Add(_w.SimInventoryInfoLabel);

            var invCard = BuildCardBase();
            invCard.style.alignSelf = Align.FlexStart;

            _w.SimInventoryGridOuter = new VisualElement();
            _w.SimInventoryGridOuter.style.alignSelf = Align.FlexStart;
            _w.SimInventoryGridOuter.style.width = InventoryPreviewWidth * InventoryCellSize + 12;
            _w.SimInventoryGridOuter.style.height = InventoryPreviewHeight * InventoryCellSize + 12;
            _w.SimInventoryGridOuter.style.paddingLeft = 6;
            _w.SimInventoryGridOuter.style.paddingRight = 6;
            _w.SimInventoryGridOuter.style.paddingTop = 6;
            _w.SimInventoryGridOuter.style.paddingBottom = 6;
            _w.SimInventoryGridOuter.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

            _w.SimInventoryGridOuter.style.borderTopWidth = 1;
            _w.SimInventoryGridOuter.style.borderBottomWidth = 1;
            _w.SimInventoryGridOuter.style.borderLeftWidth = 1;
            _w.SimInventoryGridOuter.style.borderRightWidth = 1;
            _w.SimInventoryGridOuter.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);
            _w.SimInventoryGridOuter.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
            _w.SimInventoryGridOuter.style.borderLeftColor = new Color(0f, 0f, 0f, 0.35f);
            _w.SimInventoryGridOuter.style.borderRightColor = new Color(0f, 0f, 0f, 0.35f);

            _w.SimInventoryGrid = new VisualElement();
            _w.SimInventoryGrid.style.position = Position.Relative;
            _w.SimInventoryGrid.style.width = InventoryPreviewWidth * InventoryCellSize;
            _w.SimInventoryGrid.style.height = InventoryPreviewHeight * InventoryCellSize;

            _w.SimInventoryGridOuter.Add(_w.SimInventoryGrid);
            invCard.Add(_w.SimInventoryGridOuter);

            _w.SimInventoryOverflowLabel = new Label();
            _w.SimInventoryOverflowLabel.style.opacity = 0.75f;
            _w.SimInventoryOverflowLabel.style.marginTop = 6;
            _w.SimInventoryOverflowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            invCard.Add(_w.SimInventoryOverflowLabel);

            root.Add(invCard);

            RefreshSimulationView();
            return root;
        }
    }
}
#endif