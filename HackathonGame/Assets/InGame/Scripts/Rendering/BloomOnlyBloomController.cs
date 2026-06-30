using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders the BloomOnly layer to an HDR RenderTexture with URP Volume Bloom,
/// then <see cref="BloomOnlyCompositeRendererFeature"/> additively composites it onto the main view.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BloomOnlyBloomController : MonoBehaviour
{
    public static BloomOnlyBloomController Instance { get; private set; }

    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _captureCamera;
    [SerializeField] [Range(0f, 4f)] private float _compositeIntensity = 1f;
    [SerializeField] private Color _clearColor = Color.black;

    private RenderTexture _bloomTexture;
    private int _lastWidth;
    private int _lastHeight;
    private Camera _runtimeSyncCamera;

    public RenderTexture BloomTexture => _bloomTexture;
    public float CompositeIntensity => _compositeIntensity;

    /// <summary>QTE 中など、BloomOnly キャプチャの投影をこのカメラに合わせる（例: UICamera）。</summary>
    public void SetSyncCamera(Camera camera)
    {
        _runtimeSyncCamera = camera;
    }

    /// <summary>同期カメラを Main Camera に戻す。</summary>
    public void ClearSyncCamera()
    {
        _runtimeSyncCamera = null;
    }

    private void OnEnable()
    {
        Instance = this;
        EnsureCameras();
        ApplyCaptureSettings();
        EnsureRenderTexture();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ReleaseRenderTexture();
    }

    private void LateUpdate()
    {
        if (_mainCamera == null || _captureCamera == null)
        {
            return;
        }

        SyncCaptureFromMain();
        EnsureRenderTexture();
    }

    private void EnsureCameras()
    {
        if (_captureCamera == null)
        {
            _captureCamera = GetComponent<Camera>();
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
    }

    private void ApplyCaptureSettings()
    {
        if (_captureCamera == null)
        {
            return;
        }

        int bloomLayer = LayerMask.NameToLayer("BloomOnly");
        if (bloomLayer >= 0)
        {
            _captureCamera.cullingMask = 1 << bloomLayer;
        }

        _captureCamera.clearFlags = CameraClearFlags.SolidColor;
        _captureCamera.backgroundColor = _clearColor;
        _captureCamera.depth = -100f;
        _captureCamera.enabled = true;

        UniversalAdditionalCameraData captureData = _captureCamera.GetUniversalAdditionalCameraData();
        if (captureData != null)
        {
            captureData.renderType = CameraRenderType.Base;
            captureData.renderPostProcessing = true;
            captureData.volumeLayerMask = 1;
        }

        AudioListener listener = _captureCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = false;
        }
    }

    private Camera GetSyncSourceCamera()
    {
        if (_runtimeSyncCamera != null)
        {
            return _runtimeSyncCamera;
        }

        return _mainCamera;
    }

    private void SyncCaptureFromMain()
    {
        Camera source = GetSyncSourceCamera();
        Camera target = _captureCamera;
        if (source == null || target == null)
        {
            return;
        }

        Transform sourceTransform = source.transform;
        Transform targetTransform = target.transform;
        targetTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

        target.orthographic = source.orthographic;
        target.orthographicSize = source.orthographicSize;
        target.fieldOfView = source.fieldOfView;
        target.nearClipPlane = source.nearClipPlane;
        target.farClipPlane = source.farClipPlane;
        target.rect = source.rect;
        target.allowHDR = source.allowHDR;
        target.allowMSAA = false;
    }

    private void EnsureRenderTexture()
    {
        if (_captureCamera == null)
        {
            return;
        }

        Camera syncSource = GetSyncSourceCamera();
        int width = Mathf.Max(1, syncSource != null ? syncSource.pixelWidth : Screen.width);
        int height = Mathf.Max(1, syncSource != null ? syncSource.pixelHeight : Screen.height);

        if (_bloomTexture != null && _lastWidth == width && _lastHeight == height)
        {
            return;
        }

        ReleaseRenderTexture();

        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.DefaultHDR, 0);
        descriptor.msaaSamples = 1;
        descriptor.useMipMap = false;
        descriptor.autoGenerateMips = false;
        descriptor.sRGB = false;

        _bloomTexture = new RenderTexture(descriptor)
        {
            name = "BloomOnlyCaptureRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        _bloomTexture.Create();

        _captureCamera.targetTexture = _bloomTexture;
        _lastWidth = width;
        _lastHeight = height;
    }

    private void ReleaseRenderTexture()
    {
        if (_captureCamera != null)
        {
            _captureCamera.targetTexture = null;
        }

        if (_bloomTexture != null)
        {
            _bloomTexture.Release();
            Destroy(_bloomTexture);
            _bloomTexture = null;
        }

        _lastWidth = 0;
        _lastHeight = 0;
    }

#if UNITY_EDITOR
    public void SetReferencesForEditor(Camera mainCamera, Camera captureCamera)
    {
        _mainCamera = mainCamera;
        _captureCamera = captureCamera;
    }
#endif
}
