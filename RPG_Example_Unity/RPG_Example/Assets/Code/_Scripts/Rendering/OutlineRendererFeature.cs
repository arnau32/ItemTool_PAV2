using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class OutlineRendererFeature : ScriptableRendererFeature
{
    #region Settings

    [System.Serializable]
    public class OutlineSettings
    {
        public Material maskMaterial;
        public Material compositeMaterial;
        [ColorUsage(false, false)] public Color outlineColor = new Color(1f, 0.8f, 0.2f, 1f);
        [Range(1, 16)] public int outlineWidth = 3;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public OutlineSettings settings = new();

    #endregion

    OutlinePass _pass;

    public override void Create()
    {
        _pass = new OutlinePass(settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;
        if (renderingData.cameraData.renderType != CameraRenderType.Base) return;
        if (settings.maskMaterial == null || settings.compositeMaterial == null) return;
        if (!GameServices.TryGet<IOutlineService>(out var service)) return;
        if (!service.HasTargets) return;

        _pass.Setup(settings.maskMaterial, settings.compositeMaterial,
                    service.TargetRenderers, settings.outlineColor, settings.outlineWidth);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    // ── Inner pass ────────────────────────────────────────────────────────────

    sealed class OutlinePass : ScriptableRenderPass, IDisposable
    {
        static readonly int _outlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int _outlineWidthId  = Shader.PropertyToID("_OutlineWidth");

        Material _maskMat;
        Material _compositeMat;
        IReadOnlyList<Renderer> _renderers;
        Color _color;
        float _width;

        RTHandle _maskHandle;
        bool _disposed;

        class MaskPassData
        {
            public IReadOnlyList<Renderer> renderers;
            public Material maskMat;
        }

        class CompositePassData
        {
            public RTHandle    maskHandle;
            public Material    compositeMat;
            public Color       color;
            public float       width;
        }

        public OutlinePass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        public void Setup(Material maskMat, Material compositeMat,
                          IReadOnlyList<Renderer> renderers, Color color, float width)
        {
            _maskMat      = maskMat;
            _compositeMat = compositeMat;
            _renderers    = renderers;
            _color        = color;
            _width        = width;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            var maskDesc = cameraData.cameraTargetDescriptor;
            maskDesc.colorFormat     = RenderTextureFormat.R8;
            maskDesc.depthBufferBits = 0;
            maskDesc.msaaSamples     = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _maskHandle, maskDesc,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OutlineMask");

            TextureHandle maskHandleRG = renderGraph.ImportTexture(_maskHandle);
            TextureHandle cameraColor  = resourceData.activeColorTexture;

            // ── Pass 1: Stamp target meshes as white into the R8 mask RT ──────
            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                       "Outline_Mask", out var maskData))
            {
                maskData.renderers = _renderers;
                maskData.maskMat   = _maskMat;

                builder.SetRenderAttachment(maskHandleRG, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<MaskPassData>(static (data, ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                    foreach (Renderer r in data.renderers)
                    {
                        if (r == null || !r.isVisible) continue;
                        for (int i = 0; i < r.sharedMaterials.Length; i++)
                            ctx.cmd.DrawRenderer(r, data.maskMat, i, 0);
                    }
                });
            }

            // ── Pass 2: Cross-dilation → border → composite over camera color ─
            // Blitter.BlitTexture sets _BlitTexture and draws a full-screen triangle
            // via URP's built-in fullscreen mesh — more reliable than DrawProcedural
            // inside RasterRenderPass.
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                       "Outline_Composite", out var compositeData))
            {
                compositeData.maskHandle   = _maskHandle;
                compositeData.compositeMat = _compositeMat;
                compositeData.color        = _color;
                compositeData.width        = _width;

                builder.UseTexture(maskHandleRG, AccessFlags.Read);
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc<CompositePassData>(static (data, ctx) =>
                {
                    ctx.cmd.SetGlobalColor(_outlineColorId, data.color);
                    ctx.cmd.SetGlobalFloat(_outlineWidthId, data.width);
                    Blitter.BlitTexture(ctx.cmd, data.maskHandle,
                        new Vector4(1f, 1f, 0f, 0f), data.compositeMat, 0);
                });
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _maskHandle?.Release();
            _maskHandle = null;
            _disposed = true;
        }
    }
}