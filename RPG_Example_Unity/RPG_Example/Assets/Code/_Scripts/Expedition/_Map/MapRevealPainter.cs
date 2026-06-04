using UnityEngine;

public class MapRevealPainter : System.IDisposable
{
    private readonly Material revealMaterial;

    private RenderTexture explorationMask;
    private RenderTexture tempMask;

    public RenderTexture ExplorationMask => explorationMask;

    public MapRevealPainter(Shader revealShader, int resolution)
    {
        revealMaterial = new Material(revealShader);

        explorationMask = CreateMask(resolution);
        tempMask = CreateMask(resolution);

        ClearMask();
    }

    private RenderTexture CreateMask(int resolution)
    {
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
            ? RenderTextureFormat.R8
            : RenderTextureFormat.ARGB32;

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
            resolution,
            resolution,
            format,
            0
        );

        descriptor.sRGB = false;
        descriptor.useMipMap = false;
        descriptor.autoGenerateMips = false;
        descriptor.msaaSamples = 1;

        RenderTexture rt = new RenderTexture(descriptor)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        rt.Create();
        return rt;
    }

    public void ClearMask()
    {
        RenderTexture active = RenderTexture.active;

        RenderTexture.active = explorationMask;
        GL.Clear(false, true, Color.black);

        RenderTexture.active = tempMask;
        GL.Clear(false, true, Color.black);

        RenderTexture.active = active;
    }

    public void Reveal(Vector2 uv, float radiusNormalized, float softness)
    {
        revealMaterial.SetVector("_RevealCenter", new Vector4(uv.x, uv.y, 0f, 0f));
        revealMaterial.SetFloat("_RevealRadius", radiusNormalized);
        revealMaterial.SetFloat("_RevealSoftness", softness);

        Graphics.Blit(explorationMask, tempMask, revealMaterial);
        Graphics.Blit(tempMask, explorationMask);
    }

    public byte[] GetMaskPNGBytes()
    {
        Texture2D tex = new Texture2D(explorationMask.width, explorationMask.height, TextureFormat.RGBA32, false);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = explorationMask;
        tex.ReadPixels(new Rect(0, 0, explorationMask.width, explorationMask.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        byte[] pngBytes = tex.EncodeToPNG();
        Object.Destroy(tex);
        return pngBytes;
    }

    public void LoadMaskPNGBytes(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!ImageConversion.LoadImage(tex, pngBytes))
        {
            Object.Destroy(tex);
            return;
        }

        Graphics.Blit(tex, explorationMask);
        Object.Destroy(tex);
    }

    public void Dispose()
    {
        if (revealMaterial != null)
            Object.Destroy(revealMaterial);

        if (explorationMask != null)
        {
            explorationMask.Release();
            Object.Destroy(explorationMask);
        }

        if (tempMask != null)
        {
            tempMask.Release();
            Object.Destroy(tempMask);
        }
    }
}