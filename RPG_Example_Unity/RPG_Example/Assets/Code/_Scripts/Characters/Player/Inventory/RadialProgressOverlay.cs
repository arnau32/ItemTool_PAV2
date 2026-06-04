using UnityEngine;
using UnityEngine.UIElements;

public class RadialProgressOverlay : MonoBehaviour
{
    #region Singleton

    private static RadialProgressOverlay _instance;

    public static RadialProgressOverlay Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[RadialProgressOverlay]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RadialProgressOverlay>();
            }
            return _instance;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    #endregion

    #region Fields

    private RadialArcElement _arc;
    private VisualElement    _container;
    private bool             _initialized;

    #endregion

    #region Public API

    public void Show(Rect worldRect)
    {
        EnsureInit();
        if (!_initialized) return;

        var localMin = _container.WorldToLocal(new Vector2(worldRect.xMin, worldRect.yMin));
        var localMax = _container.WorldToLocal(new Vector2(worldRect.xMax, worldRect.yMax));
        float w    = localMax.x - localMin.x;
        float h    = localMax.y - localMin.y;
        float size = Mathf.Min(w, h);

        _arc.style.width  = size;
        _arc.style.height = size;
        _arc.style.left   = localMin.x + (w - size) * 0.5f;
        _arc.style.top    = localMin.y + (h - size) * 0.5f;

        _arc.SetProgress(0f);
        _arc.MarkDirtyRepaint();
        _arc.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (!_initialized) return;
        _arc.style.display = DisplayStyle.None;
    }

    public void SetProgress(float t)
    {
        if (!_initialized) return;
        _arc.SetProgress(t);
        _arc.MarkDirtyRepaint();
    }

    #endregion

    #region Private

    private void EnsureInit()
    {
        // Reinit if the UIToolkit panel was destroyed (scene reload / death)
        if (_initialized && (_container == null || _container.panel == null))
        {
            _initialized = false;
            _arc         = null;
            _container   = null;
        }

        if (_initialized) return;
        if (UIManager.Instance?.tabViewPanel == null) return;

        _container              = UIManager.Instance.tabViewPanel;
        _arc                    = new RadialArcElement();
        _arc.style.position     = Position.Absolute;
        _arc.style.display      = DisplayStyle.None;
        _arc.pickingMode        = PickingMode.Ignore;
        _container.Add(_arc);
        _initialized = true;
    }

    private sealed class RadialArcElement : VisualElement
    {
        private float _progress;

        public RadialArcElement()
        {
            generateVisualContent += Draw;
        }

        public void SetProgress(float t) => _progress = Mathf.Clamp01(t);

        private void Draw(MeshGenerationContext ctx)
        {
            var   p    = ctx.painter2D;
            var   rect = contentRect;
            float cx   = rect.width  * 0.5f;
            float cy   = rect.height * 0.5f;
            float r    = Mathf.Min(cx, cy) - 5f;
            if (r <= 0f) return;

            p.lineWidth = 6f;
            p.lineCap   = LineCap.Round;

            p.strokeColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, 0f, 360f);
            p.Stroke();

            if (_progress <= 0f) return;

            // Counter-clockwise from 12 o'clock
            p.strokeColor = Color.white;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, -90f, -90f - _progress * 360f, ArcDirection.CounterClockwise);
            p.Stroke();
        }
    }

    #endregion
}
