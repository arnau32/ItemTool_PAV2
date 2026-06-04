using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableTarget : MonoBehaviour
{
    #region Fields

    [Header("Outline")]
    [SerializeField] private Renderer[] _renderers;

    [Header("World-Space Prompt")]
    [SerializeField] private GameObject _promptCanvas;

    public IInteractable Interactable { get; private set; }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        Interactable = GetComponent<IInteractable>();
        AutoFillRenderers();

        if (_promptCanvas != null)
            _promptCanvas.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (GameServices.TryGet<IOutlineService>(out var outline))
            outline.Unregister(_renderers);
    }

    #endregion

    #region Public API

    public void SetHighlighted(bool value)
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (!GameServices.TryGet<IOutlineService>(out var outline)) return;

        if (value)
            outline.Register(_renderers);
        else
            outline.Unregister(_renderers);
    }

    public void ShowPrompt(KeyCode key)
    {
        _promptCanvas.SetActive(true);
    }

    public void HidePrompt()
    {
        _promptCanvas.SetActive(false);
    }
    
    [ContextMenu("Auto Fill Mesh Renderers")]
    public void AutoFillRenderers()
    {
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);

        _renderers = new Renderer[meshRenderers.Length];

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            _renderers[i] = meshRenderers[i];
        }

#if UNITY_EDITOR
        Debug.Log($"[InteractableTarget] Auto-filled {_renderers.Length} MeshRenderers.", this);
#endif
    }

    #endregion
}
