using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Line Renderer 上で減衰振動パルス（ECG 風）を左→右に伝播させる。
/// シーンテスト時は Positions[0]/[1] を始点・終点とし、その間に頂点を均等配置する（2D・ローカル XY）。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class EcgWaveformRenderer : MonoBehaviour
{
    private struct Pulse
    {
        public float StartTime;
        public float Amplitude;
    }

    [Header("Line")]
    [SerializeField]
    [Min(2)]
    private int _minVertexCount = 8;

    [SerializeField]
    [Min(2)]
    private int _maxVertexCount = 96;

    [SerializeField]
    [Min(0.01f)]
    private float _vertexSpacing = 0.15f;

    [SerializeField]
    [Range(0f, 0.45f)]
    private float _endMarginRatio = 0.1f;

    [Header("Wave")]
    [SerializeField]
    [Min(0.1f)]
    private float _waveSpeedSpanPerSecond = 1.5f;

    [SerializeField]
    [Min(0.01f)]
    private float _frequency = 18f;

    [SerializeField]
    [Min(0f)]
    private float _decay = 4f;

    [SerializeField]
    [Min(0.01f)]
    private float _pulseDuration = 1.2f;

    [Header("Line Renderer")]
    [SerializeField]
    private string _sortingLayerName = "Default";

    [SerializeField]
    private int _sortingOrder = 10;

    [Header("Scene test (Positions[0]=始点, [1]=終点)")]
    [SerializeField]
    private bool _sceneTestMode;

    [SerializeField]
    private Vector3 _endpointStart = new Vector3(-3f, 0.8f, 5f);

    [SerializeField]
    private Vector3 _endpointEnd = new Vector3(3f, 0.8f, 5f);

    [SerializeField]
    [Min(0.001f)]
    private float _sceneLineWidth = 0.08f;

    [SerializeField]
    [Min(0.01f)]
    private float _scenePulseAmplitude = 0.45f;

    [Header("UI sizing (when scene test off)")]
    [SerializeField]
    [Min(1f)]
    private float _lineWidthPixels = 5f;

    [SerializeField]
    [Min(1f)]
    private float _pulseAmplitudePixels = 36f;

    [Header("Test pulse")]
    [SerializeField]
    private bool _testAutoPulse = true;

    [SerializeField]
    [Min(0.1f)]
    private float _testPulseInterval = 0.75f;

    [SerializeField]
    [Range(0f, 2f)]
    private float _testPulseAmplitudeScale = 0.65f;

    private LineRenderer _line;
    private readonly List<Pulse> _pulses = new List<Pulse>();
    private float[] _normalizedPositions;
    private Vector3[] _positions;

    private Vector3 _lineLeft;
    private Vector3 _lineRight;
    private Vector3 _spanUp = Vector3.up;
    private float _lineLength = 1f;
    private float _pulseSpanLength = 1f;
    private float _marginLength;
    private float _spanScreenPixels = 1f;
    private float _effectiveWaveSpeed = 1f;
    private float _effectiveLineWidth = 0.08f;
    private float _effectivePulseAmplitude = 0.45f;
    private bool _hasSpan;
    private bool _useWorldSpace;
    private bool _endpointsInitialized;
    private float _nextAutoPulseTime;
    private Camera _uiCamera;

    public bool SceneTestMode => _sceneTestMode;

    public void SetSceneTestMode(bool enabled)
    {
        _sceneTestMode = enabled;
        if (!enabled)
        {
            return;
        }

        _endpointsInitialized = false;
        if (isActiveAndEnabled && _line != null)
        {
            TryInitializeSceneTestEndpoints();
        }
    }

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        EnsureLineMaterial();
        _useWorldSpace = !_sceneTestMode;
        ApplyLineRendererSettings();
        TryInitializeSceneTestEndpoints();
    }

    private void OnEnable()
    {
        TryInitializeSceneTestEndpoints();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_line == null)
        {
            _line = GetComponent<LineRenderer>();
        }

        if (_line == null || _line.positionCount < 2)
        {
            return;
        }

        Vector3 start = _line.GetPosition(0);
        Vector3 end = _line.GetPosition(1);
        if ((end - start).sqrMagnitude > 0.0001f)
        {
            _endpointStart = start;
            _endpointEnd = end;
        }
    }
#endif

    private void Update()
    {
        if (!_hasSpan || _positions == null || _normalizedPositions == null)
        {
            return;
        }

        UpdateTestAutoPulse();

        float now = Time.unscaledTime;
        PruneExpiredPulses(now);

        for (int i = 0; i < _positions.Length; i++)
        {
            float t = _normalizedPositions[i];
            Vector3 basePosition = Vector3.Lerp(_lineLeft, _lineRight, t);
            float waveOffset = 0f;
            float distanceFromLineStart = t * _lineLength;

            if (distanceFromLineStart >= _marginLength
                && distanceFromLineStart <= _lineLength - _marginLength)
            {
                float distanceFromPulseOrigin = distanceFromLineStart - _marginLength;

                for (int p = 0; p < _pulses.Count; p++)
                {
                    Pulse pulse = _pulses[p];
                    float tLocal = now - pulse.StartTime - (distanceFromPulseOrigin / _effectiveWaveSpeed);

                    if (tLocal < 0f || tLocal > _pulseDuration)
                    {
                        continue;
                    }

                    waveOffset += pulse.Amplitude
                        * Mathf.Sin(_frequency * tLocal)
                        * Mathf.Exp(-_decay * tLocal);
                }
            }

            _positions[i] = basePosition + GetWaveOffset(waveOffset);
        }

        _line.SetPositions(_positions);
    }

    private void TryInitializeSceneTestEndpoints()
    {
        if (!_sceneTestMode || _endpointsInitialized || _line == null)
        {
            return;
        }

        InitializeSpanFromLineRendererEndpoints();
        ResetBaseline();
        _nextAutoPulseTime = 0f;
        _endpointsInitialized = true;
    }

    /// <summary>LineRenderer の Positions[0]/[1] を 2D 始点・終点として読み込む。</summary>
    public void InitializeSpanFromLineRendererEndpoints()
    {
        _useWorldSpace = false;

        Vector3 left = _endpointStart;
        Vector3 right = _endpointEnd;

        if (_line.positionCount >= 2)
        {
            Vector3 fromLineStart = _line.GetPosition(0);
            Vector3 fromLineEnd = _line.GetPosition(1);
            if ((fromLineEnd - fromLineStart).sqrMagnitude > 0.0001f)
            {
                left = fromLineStart;
                right = fromLineEnd;
                _endpointStart = left;
                _endpointEnd = right;
            }
        }

        float baselineY = (left.y + right.y) * 0.5f;
        float depthZ = (left.z + right.z) * 0.5f;
        left = new Vector3(left.x, baselineY, depthZ);
        right = new Vector3(right.x, baselineY, depthZ);
        _spanUp = Vector3.up;

        ApplySpan(left, right, _sceneLineWidth, _scenePulseAmplitude);
    }

    /// <summary>シーンテスト用。親 Transform ローカル座標で固定スパン（互換用）。</summary>
    public void ConfigureSceneTestSpanLocal(
        Vector3 left,
        Vector3 right,
        Vector3 up,
        float lineWidthWorld,
        float pulseAmplitudeWorld)
    {
        _useWorldSpace = false;

        float baselineY = (left.y + right.y) * 0.5f;
        float depthZ = (left.z + right.z) * 0.5f;
        left = new Vector3(left.x, baselineY, depthZ);
        right = new Vector3(right.x, baselineY, depthZ);
        _spanUp = Vector3.up;

        ApplySpan(left, right, lineWidthWorld, pulseAmplitudeWorld);
    }

    /// <summary>UI アンカーのワールド端からスパンを更新する。</summary>
    public void SetWorldSpan(Vector3 left, Vector3 right, Vector3 up, Camera uiCamera)
    {
        if (_sceneTestMode)
        {
            return;
        }

        _useWorldSpace = true;
        _uiCamera = uiCamera;
        RecomputeUiDerivedValues(left, right, up);
        EnsureTopology();
        ApplyBaseline();
    }

    public void ResetBaseline()
    {
        ClearPulses();
        ScheduleNextTestPulse();
        ApplyBaseline();
    }

    public void AddPulse(float amplitudeScale)
    {
        if (amplitudeScale <= 0f || !_hasSpan)
        {
            return;
        }

        _pulses.Add(new Pulse
        {
            StartTime = Time.unscaledTime,
            Amplitude = _effectivePulseAmplitude * amplitudeScale
        });
    }

    public void ClearPulses()
    {
        _pulses.Clear();
    }

    private Vector3 GetWaveOffset(float waveOffset)
    {
        if (_useWorldSpace)
        {
            return _spanUp * waveOffset;
        }

        return new Vector3(0f, waveOffset, 0f);
    }

    private void ApplySpan(
        Vector3 left,
        Vector3 right,
        float lineWidthWorld,
        float pulseAmplitudeWorld)
    {
        _lineLeft = left;
        _lineRight = right;
        _lineLength = Mathf.Abs(right.x - left.x);
        if (_lineLength < 0.0001f)
        {
            _lineLength = Vector3.Distance(left, right);
        }

        _hasSpan = _lineLength > 0.0001f;
        RecomputeMarginMetrics();
        _effectiveLineWidth = Mathf.Max(lineWidthWorld, 0.001f);
        _effectivePulseAmplitude = Mathf.Max(pulseAmplitudeWorld, 0.01f);
        _effectiveWaveSpeed = Mathf.Max(_pulseSpanLength * _waveSpeedSpanPerSecond, 0.01f);

        ApplyLineRendererSettings();
        EnsureTopology();
        ApplyBaseline();
    }

    private void RecomputeMarginMetrics()
    {
        float marginRatio = Mathf.Clamp(_endMarginRatio, 0f, 0.45f);
        _marginLength = _lineLength * marginRatio;
        _pulseSpanLength = _lineLength * Mathf.Max(1f - 2f * marginRatio, 0.1f);
    }

    private void UpdateTestAutoPulse()
    {
        if (!_testAutoPulse || !_hasSpan)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now < _nextAutoPulseTime)
        {
            return;
        }

        AddPulse(_testPulseAmplitudeScale);
        ScheduleNextTestPulse(now);
    }

    private void ScheduleNextTestPulse(float now = -1f)
    {
        if (now < 0f)
        {
            now = Time.unscaledTime;
        }

        _nextAutoPulseTime = now + _testPulseInterval;
    }

    private void RecomputeUiDerivedValues(Vector3 left, Vector3 right, Vector3 up)
    {
        _lineLeft = left;
        _lineRight = right;
        _spanUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        _lineLength = Vector3.Distance(left, right);
        _hasSpan = _lineLength > 0.0001f;
        RecomputeMarginMetrics();

        _spanScreenPixels = MeasureSpanScreenPixels();
        if (_spanScreenPixels < 1f)
        {
            _spanScreenPixels = 1f;
        }

        float worldPerPixel = _lineLength / _spanScreenPixels;
        _effectiveLineWidth = Mathf.Max(worldPerPixel * _lineWidthPixels, 0.0005f);
        _effectivePulseAmplitude = worldPerPixel * _pulseAmplitudePixels;
        _effectiveWaveSpeed = Mathf.Max(_pulseSpanLength * _waveSpeedSpanPerSecond, 0.01f);
        ApplyLineRendererSettings();
    }

    private float MeasureSpanScreenPixels()
    {
        if (_uiCamera == null || !_hasSpan)
        {
            return 1f;
        }

        Vector3 screenLeft = _uiCamera.WorldToScreenPoint(_lineLeft);
        Vector3 screenRight = _uiCamera.WorldToScreenPoint(_lineRight);
        return Vector2.Distance(
            new Vector2(screenLeft.x, screenLeft.y),
            new Vector2(screenRight.x, screenRight.y));
    }

    private void PruneExpiredPulses(float now)
    {
        float maxLifetime = _pulseDuration + (_pulseSpanLength / _effectiveWaveSpeed);

        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            if (now - _pulses[i].StartTime > maxLifetime)
            {
                _pulses.RemoveAt(i);
            }
        }
    }

    private void EnsureTopology()
    {
        if (!_hasSpan)
        {
            return;
        }

        int vertexCount = ResolveVertexCount();
        if (_positions != null && _positions.Length == vertexCount)
        {
            return;
        }

        _line.positionCount = vertexCount;
        _normalizedPositions = new float[vertexCount];
        _positions = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            _normalizedPositions[i] = vertexCount > 1 ? i / (float)(vertexCount - 1) : 0f;
        }
    }

    private int ResolveVertexCount()
    {
        if (!_hasSpan)
        {
            return _minVertexCount;
        }

        int spacingCount = Mathf.CeilToInt(_lineLength / _vertexSpacing) + 1;
        return Mathf.Clamp(spacingCount, _minVertexCount, _maxVertexCount);
    }

    private void ApplyBaseline()
    {
        if (_positions == null || _normalizedPositions == null || _line == null || !_hasSpan)
        {
            return;
        }

        for (int i = 0; i < _positions.Length; i++)
        {
            _positions[i] = Vector3.Lerp(_lineLeft, _lineRight, _normalizedPositions[i]);
        }

        _line.SetPositions(_positions);
    }

    private void ApplyLineRendererSettings()
    {
        _line.useWorldSpace = _useWorldSpace;
        _line.loop = false;
        _line.widthMultiplier = _effectiveLineWidth;
        _line.numCornerVertices = 2;
        _line.numCapVertices = 2;
        _line.alignment = _useWorldSpace ? LineAlignment.View : LineAlignment.TransformZ;
        _line.textureMode = LineTextureMode.Stretch;
        _line.sortingLayerName = _sortingLayerName;
        _line.sortingOrder = _sortingOrder;
        _line.shadowCastingMode = ShadowCastingMode.Off;
        _line.receiveShadows = false;
    }

    private void EnsureLineMaterial()
    {
        Material material = _line.sharedMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return;
            }

            material = new Material(shader);
            material.name = "EcgHeartbeatLine (Runtime)";
            _line.sharedMaterial = material;
        }

        ApplyLineMaterialSettings(material);
    }

    private static void ApplyLineMaterialSettings(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetColor("_BaseColor", new Color(0f, 0.85f, 1f, 0.85f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
