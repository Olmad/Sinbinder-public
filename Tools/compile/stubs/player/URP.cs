// Заглушка URP 17 / Core RP: сигнатуры по исходникам 6000.3/staging.
using System;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public static class CoreUtils
    {
        public static Material CreateEngineMaterial(Shader shader) => null;
        public static void Destroy(UnityEngine.Object obj) { }
    }

    public class RTHandle { }

    public class RasterCommandBuffer { }

    public static class Blitter
    {
        public static void BlitTexture(RasterCommandBuffer cmd, RTHandle source, Vector4 scaleBias, Material material, int pass) { }
    }
}

namespace UnityEngine.Rendering.RenderGraphModule
{
    public enum AccessFlags { None = 0, Read = 1, Write = 2, ReadWrite = 3, Discard = 4, WriteAll = 6 }

    public struct TextureHandle
    {
        public bool IsValid() => false;
        public static implicit operator RTHandle(TextureHandle h) => null;
    }

    public struct TextureDesc
    {
        public string name;
        public bool clearBuffer;
    }

    public class RasterGraphContext { public RasterCommandBuffer cmd; }

    public delegate void BaseRenderFunc<PassData, ContextType>(PassData data, ContextType renderGraphContext) where PassData : class, new();

    public interface IRasterRenderGraphBuilder : IDisposable
    {
        void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read);
        void UseAllGlobalTextures(bool enable);
        void SetRenderAttachment(TextureHandle tex, int index, AccessFlags flags = AccessFlags.Write);
        void SetRenderFunc<PassData>(BaseRenderFunc<PassData, RasterGraphContext> renderFunc) where PassData : class, new();
    }

    public class RenderGraph
    {
        public TextureDesc GetTextureDesc(TextureHandle texture) => default;
        public TextureHandle CreateTexture(in TextureDesc desc) => default;
        public IRasterRenderGraphBuilder AddRasterRenderPass<PassData>(string passName, out PassData passData) where PassData : class, new()
        { passData = new PassData(); return null; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    using UnityEngine.Rendering.RenderGraphModule;

    public enum RenderPassEvent { BeforeRenderingTransparents = 450 }

    [Flags] public enum ScriptableRenderPassInput { None = 0, Depth = 1, Normal = 2, Color = 4, Motion = 8 }

    public class CameraData { public CameraType cameraType; }
    public struct RenderingData { public CameraData cameraData; }

    public class ContextContainer { public T Get<T>() where T : class, new() => new T(); }

    public class UniversalResourceData
    {
        public bool isActiveTargetBackBuffer;
        public TextureHandle activeColorTexture;
        public TextureHandle cameraDepthTexture;
        public TextureHandle cameraColor;
    }

    public abstract class ScriptableRenderPass
    {
        public RenderPassEvent renderPassEvent { get; set; }
        public bool requiresIntermediateTexture { get; set; }
        public void ConfigureInput(ScriptableRenderPassInput passInput) { }
        public virtual void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
    }

    public abstract class ScriptableRenderer { public void EnqueuePass(ScriptableRenderPass pass) { } }

    public abstract class ScriptableRendererFeature : ScriptableObject, IDisposable
    {
        public abstract void Create();
        public abstract void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData);
        protected virtual void Dispose(bool disposing) { }
        public void Dispose() { Dispose(true); }
    }
}
