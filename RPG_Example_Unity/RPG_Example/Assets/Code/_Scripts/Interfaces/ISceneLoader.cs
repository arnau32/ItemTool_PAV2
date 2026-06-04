public interface ISceneLoader : IGameServices
{
    void LoadScene(string sceneName, float minDuration = 0f);
    void LoadScene(string sceneName, float minDuration, int loadingScreenId);
}
