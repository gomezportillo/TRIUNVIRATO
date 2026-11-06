using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class CRTFullscreenFeature : ScriptableRendererFeature
{
    private static bool effectsEnabled = true;

    public static void SetEffectsEnabled(bool enabled) => effectsEnabled = enabled;

    [System.Serializable]
    public sealed class Settings
    {
        public Shader shader;
        [Range(0f, 0.2f)] public float fisheye = 0.025f;
        [Range(0f, 1f)] public float vignette = 0.25f;
        [Range(0f, 0.02f)] public float chromaticAberration = 0.002f;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }

    [SerializeField] private Settings settings = new Settings();
    private Material material;
    private CRTPass pass;

    public override void Create()
    {
        if (settings.shader == null) settings.shader = Shader.Find("Hidden/TRIUNVIRATO CRT Fullscreen");
        CoreUtils.Destroy(material);
        material = settings.shader != null ? CoreUtils.CreateEngineMaterial(settings.shader) : null;
        pass = new CRTPass { renderPassEvent = settings.injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || renderingData.cameraData.isPreviewCamera) return;
        material.SetFloat("_Fisheye", settings.fisheye);
        material.SetFloat("_Vignette", settings.vignette);
        material.SetFloat("_ChromaticAberration", settings.chromaticAberration);
        material.SetFloat("_CRTEnabled", effectsEnabled ? 1f : 0f);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    private sealed class CRTPass : ScriptableRenderPass
    {
        private Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public CRTPass() => requiresIntermediateTexture = true;
        public void Setup(Material value) => material = value;
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraColor.IsValid())
                return;

            var source = resources.cameraColor;
            var destination = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_CRTFXSource";
            desc.clearBuffer = false;
            var copy = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(source, copy, Vector2.one, Vector2.zero, passName: "CRT Copy Source");

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Fullscreen", out var data))
            {
                data.source = copy;
                data.destination = destination;
                data.material = material;
                data.properties = properties;
                builder.UseTexture(copy, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                {
                    passData.properties.Clear();
                    passData.properties.SetTexture("_BlitTexture", passData.source);
                    passData.properties.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, passData.material, 0, MeshTopology.Triangles, 3, 1, passData.properties);
                });
            }
        }

        private sealed class PassData
        {
            public TextureHandle source;
            public TextureHandle destination;
            public Material material;
            public MaterialPropertyBlock properties;
        }
    }
}
