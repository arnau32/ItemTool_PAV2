using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CombatAnalyticsUploader : MonoBehaviour, IGameServices
{
    [SerializeField] private string _secretKey = "bs_analytics_2024";

    private string endpointUrl = "https://script.google.com/macros/s/AKfycby6-NonvKF-oAUEFHJTCJwO7anP60VjlrVQy91BHR7wuKQQnEKz8MxkXu3ZmqF8THdwLg/exec";

    public IEnumerator UploadJson(string json)
    {
        if (string.IsNullOrEmpty(endpointUrl)) yield break;

#if UNITY_EDITOR
        Debug.Log($"[CombatAnalytics] Sending {json.Length} chars. Preview: {json.Substring(0, Mathf.Min(800, json.Length))}");
#endif

        var url = $"{endpointUrl}?key={_secretKey}";
        using var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CombatAnalytics] Upload FAILED (network): {req.error} | {req.downloadHandler.text}");
            yield break;
        }

        var response = req.downloadHandler.text;
        if (response.Contains("\"ok\":false") || response.Contains("\"ok\": false"))
            Debug.LogWarning($"[CombatAnalytics] AppScript ERROR: {response}");
        else
            Debug.Log($"[CombatAnalytics] Upload OK: {response}");
    }
}
