using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputs : MonoBehaviour
{
    #region Fields

    [SerializeField] private float _stickDeadzone = 0.18f;

    private InputService _input;
    private Transform _mainCameraTransform;

    private bool _subscribed;

    private struct CachedInput
    {
        public int frame;
        public Vector2 raw;
        public Vector3 world;
    }

    // Frame-cached input values (deterministic per frame, avoids duplicate world conversions)
    private CachedInput _moveCache;
    private CachedInput _rightStickCache;

    private Vector3 _cameraForward;
    private Vector3 _cameraRight;
    private int _cameraTransformFrame = -1;

    #endregion

    #region Events

    public event Action<Enums.AttackInputs> OnAttackInput;
    public event Action OnParry;
    public event Action<bool> OnSprint;
    public event Action OnDodge;
    public event Action OnInteract;
    public event Action OnLockOn;
    public event Action<Vector2> OnUseConsumable;

    #endregion

    #region Properties

    private PlayerInputActions InputActions => _input.Actions;
    public float StickDeadZone => _stickDeadzone;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _input = GameServices.Get<InputService>();

        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }

        SubscribeToInputEvents();
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();
    }

    private void OnDisable()
    {
        UnSuscribeToInputEvents();
    }

    #endregion

    #region Input Subscription

    private void SubscribeToInputEvents()
    {
        if (_subscribed) return;

        InputActions.Player.Dodge.performed += OnDodge_performed;
        InputActions.Player.Interact.performed += OnInteract_performed;
        InputActions.Player.LightAttack.performed += OnLightAttack_perfomed;
        InputActions.Player.HeavyAttack.performed += OnHeavyAttack_perfomed;
        InputActions.Player.Sprint.performed += Sprint_performed;
        InputActions.Player.Sprint.canceled += Sprint_canceled;
        InputActions.Player.LockOn.performed += LockOn_performed;
        InputActions.Player.Parry.performed += Parry_performed;
        InputActions.Player.UseConsumable.performed += UseConsumable_performed;

        _subscribed = true;
    }

    private void UnSuscribeToInputEvents()
    {
        if (!_subscribed) return;

        InputActions.Player.Dodge.performed -= OnDodge_performed;
        InputActions.Player.Interact.performed -= OnInteract_performed;
        InputActions.Player.LightAttack.performed -= OnLightAttack_perfomed;
        InputActions.Player.HeavyAttack.performed -= OnHeavyAttack_perfomed;
        InputActions.Player.Sprint.performed -= Sprint_performed;
        InputActions.Player.Sprint.canceled -= Sprint_canceled;
        InputActions.Player.LockOn.performed -= LockOn_performed;
        InputActions.Player.Parry.performed -= Parry_performed;
        InputActions.Player.UseConsumable.performed -= UseConsumable_performed;

        _subscribed = false;
    }

    #endregion

    #region Callbacks

    private void LockOn_performed(InputAction.CallbackContext ctx) => OnLockOn?.Invoke();
    private void OnLightAttack_perfomed(InputAction.CallbackContext ctx) => OnAttackInput?.Invoke(Enums.AttackInputs.LightInput);
    private void OnHeavyAttack_perfomed(InputAction.CallbackContext ctx) => OnAttackInput?.Invoke(Enums.AttackInputs.HeavyInput);
    private void OnInteract_performed(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
    private void OnDodge_performed(InputAction.CallbackContext ctx) => OnDodge?.Invoke();
    private void Sprint_performed(InputAction.CallbackContext ctx) => OnSprint?.Invoke(true);
    private void Sprint_canceled(InputAction.CallbackContext ctx) => OnSprint?.Invoke(false);
    private void Parry_performed(InputAction.CallbackContext ctx) => OnParry?.Invoke();
    private void UseConsumable_performed(InputAction.CallbackContext ctx) => OnUseConsumable?.Invoke(ctx.ReadValue<Vector2>());

    #endregion

    #region Public API

    public float RawMag2()
    {
        var dir = GetDirectionNormalized();
        return dir.sqrMagnitude;
    }

    public bool HasRaw() => RawMag2() > (_stickDeadzone * _stickDeadzone);

    public Vector2 GetDirection()
    {
        CacheMoveIfNeeded();
        return _moveCache.raw;
    }

    public Vector3 GetDirectionNormalized()
    {
        CacheMoveIfNeeded();
        return _moveCache.world;
    }

    public bool IsMoving()
    {
        CacheMoveIfNeeded();
        return _moveCache.raw.sqrMagnitude >= 0.0001f;
    }

    public Vector2 GetRightStickDirection()
    {
        CacheRightStickIfNeeded();
        return _rightStickCache.raw;
    }

    public Vector3 GetRightStickDirectionNormalized()
    {
        CacheRightStickIfNeeded();
        return _rightStickCache.world;
    }

    #endregion

    #region Cache Internals

    private void CacheMoveIfNeeded()
    {
        int f = Time.frameCount;
        if (_moveCache.frame == f) return;

        _moveCache.frame = f;
        _moveCache.raw = InputActions.Player.Move.ReadValue<Vector2>();

        CacheCameraVectorsIfNeeded();
        _moveCache.world = _cameraForward * _moveCache.raw.y + _cameraRight * _moveCache.raw.x;
    }

    private void CacheRightStickIfNeeded()
    {
        int f = Time.frameCount;
        if (_rightStickCache.frame == f) return;

        _rightStickCache.frame = f;
        _rightStickCache.raw = InputActions.Player.LockOnSwap.ReadValue<Vector2>();

        CacheCameraVectorsIfNeeded();
        _rightStickCache.world = _cameraForward * _rightStickCache.raw.y
                                 + _cameraRight * _rightStickCache.raw.x;
    }

    private void CacheCameraVectorsIfNeeded()
    {
        int f = Time.frameCount;
        if (_cameraTransformFrame == f) return;

        _cameraTransformFrame = f;

        if (_mainCameraTransform == null) return;

        _cameraForward = UtilsNagu.GetCameraForwardNormalized(_mainCameraTransform);
        _cameraRight = UtilsNagu.GetCameraRightNormalized(_mainCameraTransform);
    }

    #endregion
}