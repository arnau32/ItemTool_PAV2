using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractable : MonoBehaviour
{
    #region Fields

    [Header("Detection (trigger-based)")] [SerializeField]
    private string _interactableLayerName = "Interactable";

    [SerializeField] private Transform _detectionOrigin;

    [Header("Input")] [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private readonly List<InteractableTarget> _touchingInteractables = new List<InteractableTarget>();
    private InteractableTarget _currentTarget;

    private PlayerPlayableController _playableController;
    private PlayerMovement _playerMovement;

    #endregion

    #region Properties

    public bool CanInteract => _currentTarget != null;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_detectionOrigin == null)
        {
            _detectionOrigin = transform;
        }
    }

    private void Update()
    {
        UpdateCurrentTargetByDistance();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOnInteractableLayer(other.gameObject)) return;

        var target = other.GetComponentInParent<InteractableTarget>();
        if (target == null || !target.enabled || target.Interactable == null) return;

        if (!_touchingInteractables.Contains(target))
        {
            _touchingInteractables.Add(target);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOnInteractableLayer(other.gameObject)) return;

        // includeInactive:true — the GameObject may already be inactive when the exit fires
        // (e.g. RepairInteractable disabling _brokenObjects during the same frame).
        var target = other.GetComponentInParent<InteractableTarget>(true);
        if (target == null) return;

        RemoveInteractable(target);
    }

    #endregion

    #region Public API

    public void Initialize(PlayerPlayableController playableController, PlayerMovement playerMovement)
    {
        _playableController = playableController;
        _playerMovement = playerMovement;
    }

    public void ClearInteractables()
    {
        _touchingInteractables.Clear();
        SetCurrentTarget(null);
    }

    public void InteractPerformed()
    {
        if (_currentTarget == null) return;

        var interactable = _currentTarget.Interactable;

        // If the interactable wants an animated action, route through the playable controller.
        // The world object stays completely unaware of the player.
        if (interactable is IAnimatedInteractable animated && _playableController != null)
        {
            var action = animated.GetPlayableAction();
            SnapFacingToTarget(_currentTarget.transform.position);
            _playableController.StartAction(action);
            return;
        }

        // Plain interactable — immediate effect as before.
        interactable.OnInteract();
    }

    #endregion

    #region Internal Logic

    private bool IsOnInteractableLayer(GameObject go) => go.layer == LayerMask.NameToLayer(_interactableLayerName);

    private void RemoveInteractable(InteractableTarget target)
    {
        if (_touchingInteractables.Contains(target))
        {
            _touchingInteractables.Remove(target);
        }

        if (_currentTarget != target) return;

        SetCurrentTarget(null);
        UpdateCurrentTargetByDistance();
    }

    private void UpdateCurrentTargetByDistance()
    {
        if (_touchingInteractables.Count == 0)
        {
            if (_currentTarget != null)
            {
                SetCurrentTarget(null);
            }

            return;
        }

        InteractableTarget best = null;
        float bestDistSqr = float.MaxValue;
        Vector3 originPos = _detectionOrigin.position;

        for (int i = _touchingInteractables.Count - 1; i >= 0; i--)
        {
            var t = _touchingInteractables[i];
            if (t == null || !t.enabled || t.Interactable == null || !t.gameObject.activeInHierarchy)
            {
                _touchingInteractables.RemoveAt(i);
                continue;
            }

            float distSqr = (t.transform.position - originPos).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = t;
            }
        }

        if (best != _currentTarget)
        {
            SetCurrentTarget(best);
        }
    }

    private void SetCurrentTarget(InteractableTarget newTarget)
    {
        if (_currentTarget != null)
        {
            _currentTarget.SetHighlighted(false);
            _currentTarget.HidePrompt();
        }

        _currentTarget = newTarget;

        if (_currentTarget == null) return;

        _currentTarget.SetHighlighted(true);
        _currentTarget.ShowPrompt(_interactKey);
    }

    private void SnapFacingToTarget(Vector3 targetWorldPos)
    {
        if (_playerMovement == null) return;

        Vector3 dir = targetWorldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        _playerMovement.SnapFacing(dir.normalized);
    }

    #endregion
}