using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

/// Editor Window que exporta todas las opciones del Universal Render Pipeline Asset a JSON.
/// Menú: Window > Rendering > URP Asset Exporter
public class URPAssetExporter : EditorWindow
{
    private UniversalRenderPipelineAsset _urpAsset;
    private string _outputPath = "Assets/URPSettings.json";
    private Vector2 _scroll;
    private string _lastJson = "";
    private bool _prettyPrint = true;
    private GUIStyle _jsonStyle;
    private bool _stylesInit;

    [MenuItem("Window/Rendering/URP Asset Exporter")]
    public static void ShowWindow()
    {
        var w = GetWindow<URPAssetExporter>("URP Asset Exporter");
        w.minSize = new Vector2(520, 600);
        w.Show();
    }

    private void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            _urpAsset = urp;
    }

    private void InitStyles()
    {
        if (_stylesInit) return;
        _jsonStyle = new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont("Courier New", 11),
            wordWrap = false,
            richText = false
        };
        _stylesInit = true;
    }

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space(8);
        GUILayout.Label("Universal Render Pipeline Asset Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Selecciona un URP Asset y exporta su configuración a JSON para revisión.",
            MessageType.Info);
        EditorGUILayout.Space(4);

        _urpAsset = (UniversalRenderPipelineAsset)EditorGUILayout.ObjectField(
            "URP Asset", _urpAsset, typeof(UniversalRenderPipelineAsset), false);

        if (_urpAsset == null)
        {
            EditorGUILayout.HelpBox(
                "No hay ningún URP Asset seleccionado. " +
                "Arrastra uno aquí o asigna uno en Project Settings > Graphics.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Ruta de salida", _outputPath);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string path = EditorUtility.SaveFilePanel(
                "Guardar JSON",
                Path.GetDirectoryName(_outputPath),
                Path.GetFileName(_outputPath),
                "json");
            if (!string.IsNullOrEmpty(path))
                _outputPath = FileUtil.GetProjectRelativePath(path);
        }
        EditorGUILayout.EndHorizontal();

        _prettyPrint = EditorGUILayout.Toggle("Pretty Print", _prettyPrint);
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generar Preview", GUILayout.Height(30)))
            _lastJson = BuildJson(_urpAsset, _prettyPrint);

        GUI.enabled = !string.IsNullOrEmpty(_lastJson);
        if (GUILayout.Button("Exportar a archivo", GUILayout.Height(30)))
            ExportToFile();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        if (!string.IsNullOrEmpty(_lastJson))
        {
            GUILayout.Label("Preview JSON:", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_lastJson, _jsonStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Copiar al portapapeles"))
                EditorGUIUtility.systemCopyBuffer = _lastJson;
        }
    }

    // ── JSON build ──────────────────────────────────────────────────────────

    private static string BuildJson(UniversalRenderPipelineAsset a, bool pretty)
    {
        var so   = new SerializedObject(a);
        var data = new URPAssetData();

        // ── Rendering (pipeline-level only — depthPrimingMode/copyDepthMode/useNativeRenderPass
        //    live on each UniversalRendererData, not on the pipeline asset)
        data.rendering = new URPAssetData.Rendering
        {
            supportsHDR              = a.supportsHDR,
            hdrColorBufferPrecision  = a.hdrColorBufferPrecision.ToString(),
            msaaSampleCount          = a.msaaSampleCount,
            storeActionsOptimization = a.storeActionsOptimization.ToString(),
        };

        // ── Quality
        data.quality = new URPAssetData.Quality
        {
            renderScale               = a.renderScale,
            upscalingFilter           = a.upscalingFilter.ToString(),
            fsrSharpness              = a.fsrSharpness,
            enableLODCrossFade        = a.enableLODCrossFade,
            lodCrossFadeDitheringType = a.lodCrossFadeDitheringType.ToString(),
        };

        // ── Lighting
        data.lighting = new URPAssetData.Lighting
        {
            mainLightRenderingMode                     = a.mainLightRenderingMode.ToString(),
            supportsMainLightShadows                   = a.supportsMainLightShadows,
            mainLightShadowmapResolution               = a.mainLightShadowmapResolution,
            additionalLightsRenderingMode              = a.additionalLightsRenderingMode.ToString(),
            maxAdditionalLightsCount                   = a.maxAdditionalLightsCount,
            supportsAdditionalLightShadows             = a.supportsAdditionalLightShadows,
            additionalLightsShadowmapResolution        = a.additionalLightsShadowmapResolution,
            additionalLightsShadowResolutionTierLow    = a.additionalLightsShadowResolutionTierLow,
            additionalLightsShadowResolutionTierMedium = a.additionalLightsShadowResolutionTierMedium,
            additionalLightsShadowResolutionTierHigh   = a.additionalLightsShadowResolutionTierHigh,
            reflectionProbeBlending                    = a.reflectionProbeBlending,
            reflectionProbeBoxProjection               = a.reflectionProbeBoxProjection,
            supportsMixedLighting                      = a.supportsMixedLighting,
            useRenderingLayers                         = a.useRenderingLayers,
        };

        // ── Shadows
        // softShadowQuality has no public accessor on the pipeline asset in URP 17.x —
        // read via SerializedObject.
        data.shadows = new URPAssetData.Shadows
        {
            shadowDistance               = a.shadowDistance,
            shadowCascadeCount           = a.shadowCascadeCount,
            cascade2Split                = a.cascade2Split,
            cascade3Split                = new float[] { a.cascade3Split.x, a.cascade3Split.y },
            cascade4Split                = new float[] { a.cascade4Split.x, a.cascade4Split.y, a.cascade4Split.z },
            cascadeBorder                = a.cascadeBorder,
            shadowDepthBias              = a.shadowDepthBias,
            shadowNormalBias             = a.shadowNormalBias,
            supportsSoftShadows          = a.supportsSoftShadows,
            softShadowQuality            = ReadEnumName(so, "m_SoftShadowQuality"),
            conservativeEnclosingSphere  = a.conservativeEnclosingSphere,
            numIterationsEnclosingSphere = a.numIterationsEnclosingSphere,
        };

        // ── Post-processing
        data.postProcessing = new URPAssetData.PostProcessing
        {
            colorGradingMode            = a.colorGradingMode.ToString(),
            colorGradingLutSize         = a.colorGradingLutSize,
            useFastSRGBLinearConversion = a.useFastSRGBLinearConversion,
            supportDataDrivenLensFlare  = a.supportDataDrivenLensFlare,
            supportScreenSpaceLensFlare = a.supportScreenSpaceLensFlare,
        };

        // ── Advanced
        data.advanced = new URPAssetData.Advanced
        {
            useSRPBatcher             = a.useSRPBatcher,
            supportsDynamicBatching   = a.supportsDynamicBatching,
            storeActionsOptimization  = a.storeActionsOptimization.ToString(),
            gpuResidentDrawerMode     = a.gpuResidentDrawerMode.ToString(),
            smallMeshScreenPercentage = a.smallMeshScreenPercentage,
        };

        // ── Renderer list (per-renderer settings are inside each renderer data asset)
        data.rendererList = ReadRendererList(a, so);

        return JsonUtility.ToJson(data, pretty);
    }

    // ── SerializedObject helpers ────────────────────────────────────────────

    private static string[] ReadRendererList(UniversalRenderPipelineAsset a, SerializedObject so)
    {
        var result = new List<string>();
        var prop   = so.FindProperty("m_RendererDataList");

        if (prop == null || !prop.isArray)
            return result.ToArray();

        for (int i = 0; i < prop.arraySize; i++)
        {
            var obj = prop.GetArrayElementAtIndex(i).objectReferenceValue;
            result.Add(obj != null ? $"[{i}] {obj.GetType().Name}: {obj.name}" : $"[{i}] (null)");
        }

        return result.ToArray();
    }

    private static string ReadEnumName(SerializedObject so, string propertyPath)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null) return "N/A";

        if (prop.propertyType == SerializedPropertyType.Enum &&
            prop.enumValueIndex >= 0 &&
            prop.enumValueIndex < prop.enumDisplayNames.Length)
            return prop.enumDisplayNames[prop.enumValueIndex];

        return prop.enumValueIndex.ToString();
    }

    private void ExportToFile()
    {
        if (string.IsNullOrEmpty(_lastJson))
            _lastJson = BuildJson(_urpAsset, _prettyPrint);

        string fullPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            _outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, _lastJson, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("URP Exporter", $"JSON exportado en:\n{_outputPath}", "OK");
        Debug.Log($"[URP Exporter] JSON guardado en: {fullPath}");
    }

    // ── Data model ──────────────────────────────────────────────────────────

    [System.Serializable]
    private class URPAssetData
    {
        public Rendering      rendering;
        public Quality        quality;
        public Lighting       lighting;
        public Shadows        shadows;
        public PostProcessing postProcessing;
        public Advanced       advanced;
        public string[]       rendererList;

        [System.Serializable]
        public class Rendering
        {
            public bool   supportsHDR;
            public string hdrColorBufferPrecision;
            public int    msaaSampleCount;
            public string storeActionsOptimization;
        }

        [System.Serializable]
        public class Quality
        {
            public float  renderScale;
            public string upscalingFilter;
            public float  fsrSharpness;
            public bool   enableLODCrossFade;
            public string lodCrossFadeDitheringType;
        }

        [System.Serializable]
        public class Lighting
        {
            public string mainLightRenderingMode;
            public bool   supportsMainLightShadows;
            public int    mainLightShadowmapResolution;
            public string additionalLightsRenderingMode;
            public int    maxAdditionalLightsCount;
            public bool   supportsAdditionalLightShadows;
            public int    additionalLightsShadowmapResolution;
            public int    additionalLightsShadowResolutionTierLow;
            public int    additionalLightsShadowResolutionTierMedium;
            public int    additionalLightsShadowResolutionTierHigh;
            public bool   reflectionProbeBlending;
            public bool   reflectionProbeBoxProjection;
            public bool   supportsMixedLighting;
            public bool   useRenderingLayers;
        }

        [System.Serializable]
        public class Shadows
        {
            public float   shadowDistance;
            public int     shadowCascadeCount;
            public float   cascade2Split;
            public float[] cascade3Split;
            public float[] cascade4Split;
            public float   cascadeBorder;
            public float   shadowDepthBias;
            public float   shadowNormalBias;
            public bool    supportsSoftShadows;
            public string  softShadowQuality;
            public bool    conservativeEnclosingSphere;
            public int     numIterationsEnclosingSphere;
        }

        [System.Serializable]
        public class PostProcessing
        {
            public string colorGradingMode;
            public int    colorGradingLutSize;
            public bool   useFastSRGBLinearConversion;
            public bool   supportDataDrivenLensFlare;
            public bool   supportScreenSpaceLensFlare;
        }

        [System.Serializable]
        public class Advanced
        {
            public bool   useSRPBatcher;
            public bool   supportsDynamicBatching;
            public string storeActionsOptimization;
            public string gpuResidentDrawerMode;
            public float  smallMeshScreenPercentage;
        }
    }
}
