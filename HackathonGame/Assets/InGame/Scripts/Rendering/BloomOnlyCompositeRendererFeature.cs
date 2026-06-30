using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

/// <summary>
/// Additively composites <see cref="BloomOnlyBloomController.BloomTexture"/> onto the main color buffer.
/// </summary>
public sealed class BloomOnlyCompositeRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [Min(0f)] public float intensity = 1f;

        public bool syncIntensityFromVolume = true;
    }

    [SerializeField] private Settings settings = new Settings();
    [SerializeField] private Shader compositeShader;

    private BloomOnlyCompositeRenderPass _pass;
    private Material _compositeMaterial;

    public override void Create()
    {
        _pass = new BloomOnlyCompositeRenderPass(settings);
        EnsureMaterial();
        _pass.SetMaterial(_compositeMaterial);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.ReleaseBloomHandle();
        CoreUtils.Destroy(_compositeMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_compositeMaterial == null)
        {
            return;
        }

        if (renderingData.cameraData.renderType != CameraRenderType.Base)
        {
            return;
        }

        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        BloomOnlyBloomController controller = BloomOnlyBloomController.Instance;
        if (controller == null || controller.BloomTexture == null)
        {
            return;
        }

        _pass.UpdateIntensity(settings);
        _pass.SetBloomTexture(controller.BloomTexture);
        _pass.renderPassEvent = settings.renderPassEvent;
        renderer.EnqueuePass(_pass);
    }

    private void EnsureMaterial()
    {
        if (compositeShader == null)
        {
            compositeShader = Shader.Find("Hidden/InGame/BloomOnlyComposite");
        }

        if (compositeShader != null && (_compositeMaterial == null || _compositeMaterial.shader != compositeShader))
        {
            CoreUtils.Destroy(_compositeMaterial);
            _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
        }
    }

    private sealed class BloomOnlyCompositeRenderPass : ScriptableRenderPass
    {
        private readonly Settings _settings;
        private Material _compositeMaterial;
        private RenderTexture _bloomTexture;
        private RTHandle _bloomRtHandle;
        private float _intensity;

        public BloomOnlyCompositeRenderPass(Settings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;
            profilingSampler = new ProfilingSampler("BloomOnlyComposite");
        }

        public void SetMaterial(Material material)
        {
            _compositeMaterial = material;
        }

        public void SetBloomTexture(RenderTexture bloomTexture)
        {
            if (_bloomTexture == bloomTexture)
            {
                return;
            }

            ReleaseBloomHandle();
            _bloomTexture = bloomTexture;

            if (_bloomTexture != null)
            {
                _bloomRtHandle = RTHandles.Alloc(_bloomTexture);
            }
        }

        public void ReleaseBloomHandle()
        {
            if (_bloomRtHandle != null)
            {
                RTHandles.Release(_bloomRtHandle);
                _bloomRtHandle = null;
            }

            _bloomTexture = null;
        }

        public void UpdateIntensity(Settings settings)
        {
            _intensity = settings.intensity;

            if (settings.syncIntensityFromVolume)
            {
                Bloom bloom = VolumeManager.instance.stack.GetComponent<Bloom>();
                if (bloom != null)
                {
                    _intensity = Mathf.Max(bloom.intensity.value, 0f);
                }
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_compositeMaterial == null || _bloomRtHandle == null)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.activeColorTexture.IsValid())
            {
                return;
            }

            _compositeMaterial.SetFloat("_Intensity", _intensity);

            TextureHandle bloomHandle = renderGraph.ImportTexture(_bloomRtHandle);
            TextureHandle destination = resourceData.activeColorTexture;

            renderGraph.AddBlitPass(
                new BlitMaterialParameters(bloomHandle, destination, _compositeMaterial, 0),
                passName: "BloomOnly Composite");
        }
    }
}
