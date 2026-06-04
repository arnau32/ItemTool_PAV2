using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderService : MonoBehaviour, ISceneLoader, IInitializable
{
    #region Fields

    [SerializeField] private string _loadingScreenPath = "LoadingScreen";

    private LoadingScreenView _view;
    private bool _isLoading;

    #endregion

    #region IInitializable

    public void Initialize()
    {
        var prefab = Resources.Load<GameObject>(_loadingScreenPath);

        if (prefab == null)
        {
            Debug.LogError("[SceneLoaderService] LoadingScreen prefab not found in Resources.");
            return;
        }

        _view = Instantiate(prefab).GetComponent<LoadingScreenView>();
        DontDestroyOnLoad(_view.gameObject);
        _view.gameObject.SetActive(false);
    }

    #endregion

    #region Public API

    public void LoadScene(string sceneName, float minDuration = 0f)
    {
        if (_isLoading) return;
        if (_view == null) return;

        _isLoading = true;
        _view.Show();
        _view.SetFill(0f);
        StartCoroutine(LoadRoutine(sceneName, minDuration));
    }
    
    public void LoadScene(string sceneName, float minDuration, int id)
    {
        if (_isLoading) return;
        if (_view == null) return;

        _isLoading = true;
        _view.Show(id);
        _view.SetFill(0f);
        StartCoroutine(LoadRoutine(sceneName, minDuration));
    }

    #endregion

    #region Private

    private IEnumerator LoadRoutine(string sceneName,  float minDuration)
    {


        yield return null; // wait one frame so the canvas renders before load begins

        // Lower background thread priority so Unity yields frames to the main thread,
        // keeping animations smooth at the cost of slightly slower loading.
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        float elapsed = 0f;

        while (asyncOp.progress < 0.9f || elapsed < minDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float loadProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
            float timeProgress = minDuration > 0f ? elapsed / minDuration : 1f;
            _view.SetFill(Mathf.Min(loadProgress, timeProgress));

            yield return null;
        }

        _view.SetFill(1f);

        Application.backgroundLoadingPriority = ThreadPriority.Normal;

        asyncOp.allowSceneActivation = true;
        yield return asyncOp;

        _view.Hide();
        _isLoading = false;
    }

    #endregion
}
