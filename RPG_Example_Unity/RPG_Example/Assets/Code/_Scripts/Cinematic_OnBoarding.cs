using Cysharp.Threading.Tasks;
using UnityEngine;

public class Cinematic_OnBoarding : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private GameObject _playerHUD;
    [SerializeField] private GDTFadeEffect _fadeEffect;
    [SerializeField] private GameObject _tutorialCanvas;

    [Header("Wakeup")]
    [SerializeField] private Animator _wakeupAnimator;
    [SerializeField] private string _wakeupLoopState = "WakeUpLoop";
    [SerializeField] private string _wakeupTrigger = "WakeUp";

    private void Start()
    {
        _playerHUD.SetActive(false);
        if (_tutorialCanvas != null) _tutorialCanvas.SetActive(false);
        RunSequenceAsync().Forget();
    }

    private async UniTaskVoid RunSequenceAsync()
    {
        await UniTask.WaitUntil(() => UIManager.Instance != null && UIManager.Instance.AllUIInitFinish);

        var input = GameServices.Get<InputService>();
        input.OnUIOpen();

        try
        {
            _wakeupAnimator.Play(_wakeupLoopState, PlayerAnimHashes.LayerOverride, 0f);
            await PlayFadeEffectAsync();
            await PlayWakeupAnimationAsync();
        }
        finally
        {
            input.OnUIClose();
            _playerHUD.SetActive(true);
            if (_tutorialCanvas != null) _tutorialCanvas.SetActive(true);
        }
    }

    private async UniTask PlayWakeupAnimationAsync()
    {
        _wakeupAnimator.SetTrigger(_wakeupTrigger);
        await UniTask.NextFrame();
        await UniTask.WaitUntil(() =>
        {
            var state = _wakeupAnimator.GetCurrentAnimatorStateInfo(PlayerAnimHashes.LayerOverride);
            return state.normalizedTime >= 1f && !_wakeupAnimator.IsInTransition(PlayerAnimHashes.LayerOverride);
        });
    }

    private async UniTask PlayFadeEffectAsync()
    {
        if (_fadeEffect == null)
        {
            Debug.LogWarning("Cinematic_OnBoarding: _fadeEffect is null.");
            return;
        }

        _fadeEffect.disableWhenFinish = false;
        _fadeEffect.StartEffect();

        await UniTask.WaitUntil(() => _fadeEffect.HasFinished());
    }
}
