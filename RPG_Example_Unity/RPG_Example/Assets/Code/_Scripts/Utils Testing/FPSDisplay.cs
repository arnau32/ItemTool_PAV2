using UnityEngine;
using TMPro;
using System;

[ExecuteAlways]
public class FPSDisplay : MonoBehaviour
{
    public float fps { get; private set; }
    public float frameMS { get; private set; }

    [Space]
    public float updateInterval = 0.5f;

    float elapsedIntervalTime;
    int intervalFrameCount;

    [Space]
    [Tooltip("Required for zero GC allocation.")]
    public TextMeshProUGUI textMesh;

    // Cached
    string cachedFPSText = "";
    float lastFPS = -1;
    float lastFrameMS = -1;

    public float GetIntervalFPS()
    {
        return intervalFrameCount / elapsedIntervalTime;
    }

    public float GetIntervalFrameMS()
    {
        return (elapsedIntervalTime * 1000.0f) / intervalFrameCount;
    }

    void Update()
    {
        intervalFrameCount++;
        elapsedIntervalTime += Time.unscaledDeltaTime;

        if (!(elapsedIntervalTime >= updateInterval)) return;
        
        fps = GetIntervalFPS();
        frameMS = GetIntervalFrameMS();

        fps = (float)Math.Round(fps, 2);
        frameMS = (float)Math.Round(frameMS, 2);

        if (!Mathf.Approximately(fps, lastFPS) || !Mathf.Approximately(frameMS, lastFrameMS))
        {
            cachedFPSText = "FPS: " + fps.ToString("F2") + " (" + frameMS.ToString("F2") + " ms)";
            lastFPS = fps;
            lastFrameMS = frameMS;
                    
            if (textMesh)
            {
                textMesh.text = cachedFPSText;
            }
        }

        intervalFrameCount = 0;
        elapsedIntervalTime = 0.0f;
    }
}
