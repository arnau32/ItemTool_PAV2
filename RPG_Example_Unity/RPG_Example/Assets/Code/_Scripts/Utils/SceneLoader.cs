using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// Two-step scene transition designed for Timeline Signals:
//   Signal A (fade-to-black starts) → BeginPreload()
//   Signal B (screen fully black)   → Activate()
// Keep this GO always active in the scene.
public class SceneLoader : MonoBehaviour
{
    [SerializeField] private int _sceneIndex;

    private AsyncOperation _asyncOp;

    public void BeginPreload()
    {
        if (_asyncOp != null) return;
        _asyncOp = SceneManager.LoadSceneAsync(_sceneIndex);
        _asyncOp.allowSceneActivation = false;
    }

    public void Activate()
    {
        ActivateAsync().Forget();
    }

    private async UniTaskVoid ActivateAsync()
    {
        if (_asyncOp == null)
        {
            _asyncOp = SceneManager.LoadSceneAsync(_sceneIndex);
            _asyncOp.allowSceneActivation = false;
        }

        await UniTask.WaitUntil(() => _asyncOp.progress >= 0.9f);
        _asyncOp.allowSceneActivation = true;
    }
}