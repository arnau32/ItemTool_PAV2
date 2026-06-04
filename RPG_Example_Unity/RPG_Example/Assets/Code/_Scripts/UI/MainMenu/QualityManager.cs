using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class QualityManager : MonoBehaviour, IGameServices
{
    [Header("URP Assets for each quality level")]
    public UniversalRenderPipelineAsset lowQualityAsset;
    public UniversalRenderPipelineAsset mediumQualityAsset;
    public UniversalRenderPipelineAsset highQualityAsset;
    public UniversalRenderPipelineAsset ultraQualityAsset;

    public enum QualityLevel { Low, Medium, High, Ultra }

    public void SetQuality(QualityLevel level)
    {
        UniversalRenderPipelineAsset asset = level switch
        {
            QualityLevel.Low => lowQualityAsset,
            QualityLevel.Medium => mediumQualityAsset,
            QualityLevel.High => highQualityAsset,
            QualityLevel.Ultra => ultraQualityAsset,
            _ => mediumQualityAsset
        };

        GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
    }

    public void SetQuality(int index) => SetQuality((QualityLevel)index);
}