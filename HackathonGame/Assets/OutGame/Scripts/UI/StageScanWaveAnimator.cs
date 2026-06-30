using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reveals ScanWave (and optional ReadyStatus images) from screen center (UV clip).
/// MapDark uses a darken band synced to the same reveal.
/// </summary>
[DisallowMultipleComponent]
public class StageScanWaveAnimator : MonoBehaviour
{
    struct ReadyStatusGraphicState
    {
        public Graphic graphic;
        public bool wasEnabled;
        public float originalAlpha;
    }
    static readonly int RevealHalfId = Shader.PropertyToID("_RevealHalf");
    static readonly int ScanRevealHalfId = Shader.PropertyToID("_ScanRevealHalf");
    static readonly int CanvasRectId = Shader.PropertyToID("_CanvasRect");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int TextureSampleAddId = Shader.PropertyToID("_TextureSampleAdd");

    const string RevealShaderName = "OutGame/UI_VerticalCenterReveal";
    const string DarkenShaderName = "OutGame/UI_VerticalCenterDarken";
    const string RevealShaderResourcePath = "UI_VerticalCenterReveal";
    const string DarkenShaderResourcePath = "UI_VerticalCenterDarken";

    [SerializeField] Shader revealShaderReference;
    [SerializeField] Shader darkenShaderReference;

    [SerializeField] Image scanWaveImage;
    [SerializeField] Image mapDarkImage;
    [SerializeField] RectTransform readyStatusRoot;
    [SerializeField] string readyStatusFallbackName = "ReadyStatus";
    [SerializeField] float scanDuration = 1.4f;
    [SerializeField] [Range(0f, 0.5f)] float scanRevealEnd = 0.5f;
    [SerializeField] [Range(1f, 6f)] float easeOutPower = 2.5f;
    [Tooltip("When false, MapDark stays visible with its default image before and after the scan (StageScene).")]
    [SerializeField] bool hideMapDarkWhenIdle = true;
    [Tooltip("When true, ReadyStatus is hidden until PlayScan and reveals with ScanWave.")]
    [SerializeField] bool hideReadyStatusWhenIdle = true;

    Material _revealMaterial;
    Material _darkenMaterial;
    float _currentRevealHalf;
    bool _scanRevealAtEnd;
    readonly List<Image> _readyStatusRevealImages = new();
    readonly List<ReadyStatusGraphicState> _readyStatusDeferredGraphics = new();

    public float CurrentRevealHalf => _currentRevealHalf;
    public bool ScanRevealAtEnd => _scanRevealAtEnd;

    public void Configure(
        Image scanWave,
        Image mapDark,
        float duration,
        float revealEnd,
        float easeOut,
        bool hideMapDarkIdle = true,
        RectTransform readyStatus = null,
        bool hideReadyStatusIdle = true)
    {
        if (scanWave != null)
        {
            scanWaveImage = scanWave;
        }

        if (mapDark != null)
        {
            mapDarkImage = mapDark;
        }

        if (readyStatus != null)
        {
            readyStatusRoot = readyStatus;
        }

        scanDuration = Mathf.Max(0f, duration);
        scanRevealEnd = Mathf.Clamp(revealEnd, 0f, 0.5f);
        easeOutPower = Mathf.Max(1f, easeOut);
        hideMapDarkWhenIdle = hideMapDarkIdle;
        hideReadyStatusWhenIdle = hideReadyStatusIdle;
        ApplyIdleState();
    }

    void Awake()
    {
        EnsureReferences();
        ApplyIdleState();
    }

    public IEnumerator PlayScan()
    {
        EnsureReferences();
        _scanRevealAtEnd = false;
        SetupScanMaterials();
        if (scanWaveImage != null)
        {
            scanWaveImage.enabled = true;
        }

        if (mapDarkImage != null)
        {
            mapDarkImage.enabled = true;
        }

        PrepareReadyStatusForScan();
        SetReveal(0f);

        float duration = Mathf.Max(0f, scanDuration);
        float scanEnd = Mathf.Clamp(scanRevealEnd, 0f, 0.5f);
        if (duration <= 0f)
        {
            SetReveal(scanEnd);
            ApplyMapDarkAfterScan();
            FinishReadyStatusAfterScan();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, easeOutPower);
            float scanReveal = Mathf.Lerp(0f, scanEnd, eased);
            SetReveal(scanReveal);
            yield return null;
        }

        SetReveal(scanEnd);
        ApplyMapDarkAfterScan();
        FinishReadyStatusAfterScan();
        _scanRevealAtEnd = true;
    }

    /// <summary>
    /// Restores the visual state after PlayScan without replaying the animation (return from OutfitScene).
    /// </summary>
    public void RestorePostScanAppearance()
    {
        EnsureReferences();

        float scanEnd = Mathf.Clamp(scanRevealEnd, 0f, 0.5f);
        SetupScanMaterials();

        if (scanWaveImage != null)
        {
            SetScanWaveCullTransparentMesh(false);
            scanWaveImage.enabled = true;
        }

        if (mapDarkImage != null)
        {
            mapDarkImage.enabled = true;
        }

        PrepareReadyStatusForScan(initialRevealZero: false);
        SetReveal(scanEnd);
        FinishReadyStatusAfterScan();
        ApplyMapDarkAfterScan();
        _scanRevealAtEnd = true;
    }

    /// <summary>
    /// Mirror of <see cref="PlayScan"/>: reveal band and ReadyStatus shrink back to center and hide.
    /// </summary>
    public IEnumerator PlayScanReverse()
    {
        EnsureReferences();

        float scanEnd = Mathf.Clamp(scanRevealEnd, 0f, 0.5f);
        if (!_scanRevealAtEnd && _currentRevealHalf <= 0f)
        {
            ApplyIdleState();
            yield break;
        }

        float startReveal = Mathf.Max(_currentRevealHalf, scanEnd);

        SetupScanMaterials();

        if (scanWaveImage != null)
        {
            scanWaveImage.enabled = true;
        }

        if (mapDarkImage != null)
        {
            mapDarkImage.enabled = true;
        }

        PrepareReadyStatusForScan(initialRevealZero: false);
        SetReveal(startReveal);

        float duration = Mathf.Max(0f, scanDuration);
        if (duration <= 0f)
        {
            SetReveal(0f);
            FinishReadyStatusAfterScan();
            ApplyIdleState();
            _scanRevealAtEnd = false;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, easeOutPower);
            float scanReveal = Mathf.Lerp(startReveal, 0f, eased);
            SetReveal(scanReveal);
            yield return null;
        }

        SetReveal(0f);
        FinishReadyStatusAfterScan();
        ApplyIdleState();
        _scanRevealAtEnd = false;
    }

    void EnsureReferences()
    {
        if (scanWaveImage == null)
        {
            GameObject scanWaveObject = GameObject.Find("ScanWave");
            if (scanWaveObject != null)
            {
                scanWaveImage = scanWaveObject.GetComponent<Image>();
            }
        }

        if (mapDarkImage == null)
        {
            GameObject mapDarkObject = GameObject.Find("MapDark");
            if (mapDarkObject != null)
            {
                mapDarkImage = mapDarkObject.GetComponent<Image>();
            }
        }

        if (readyStatusRoot == null && !string.IsNullOrEmpty(readyStatusFallbackName))
        {
            GameObject readyStatusObject = GameObject.Find(readyStatusFallbackName);
            if (readyStatusObject != null)
            {
                readyStatusRoot = readyStatusObject.GetComponent<RectTransform>();
            }
        }
    }

    void SetupScanMaterials()
    {
        Shader revealShader = ResolveRevealShader();
        if (revealShader == null)
        {
            Debug.LogError("[StageScanWaveAnimator] Reveal shader not found (assign refs or add Resources shaders).", this);
            return;
        }

        if (_revealMaterial == null || _revealMaterial.shader != revealShader)
        {
            if (_revealMaterial != null)
            {
                Destroy(_revealMaterial);
            }

            _revealMaterial = new Material(revealShader);
        }

        if (scanWaveImage != null)
        {
            SetScanWaveCullTransparentMesh(false);
            scanWaveImage.material = _revealMaterial;
            SyncMaterialFromGraphic(scanWaveImage, _revealMaterial);
        }

        Shader darkenShader = ResolveDarkenShader();
        if (darkenShader == null)
        {
            Debug.LogError("[StageScanWaveAnimator] Darken shader not found (assign refs or add Resources shaders).", this);
            return;
        }

        if (_darkenMaterial == null || _darkenMaterial.shader != darkenShader)
        {
            if (_darkenMaterial != null)
            {
                Destroy(_darkenMaterial);
            }

            _darkenMaterial = new Material(darkenShader);
        }

        if (mapDarkImage != null)
        {
            mapDarkImage.material = _darkenMaterial;
            SyncMaterialFromGraphic(mapDarkImage, _darkenMaterial);
            UpdateDarkenCanvasRect();
        }
    }

    Shader ResolveRevealShader()
    {
        if (revealShaderReference != null)
        {
            return revealShaderReference;
        }

        Shader fromResources = Resources.Load<Shader>(RevealShaderResourcePath);
        if (fromResources != null)
        {
            return fromResources;
        }

        return Shader.Find(RevealShaderName);
    }

    Shader ResolveDarkenShader()
    {
        if (darkenShaderReference != null)
        {
            return darkenShaderReference;
        }

        Shader fromResources = Resources.Load<Shader>(DarkenShaderResourcePath);
        if (fromResources != null)
        {
            return fromResources;
        }

        return Shader.Find(DarkenShaderName);
    }

    static void SyncMaterialFromGraphic(Graphic graphic, Material material)
    {
        if (graphic == null || material == null)
        {
            return;
        }

        Texture mainTexture = graphic.mainTexture;
        if (mainTexture != null)
        {
            material.mainTexture = mainTexture;
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, graphic.color);
        }

        if (material.HasProperty(TextureSampleAddId))
        {
            material.SetColor(TextureSampleAddId, Color.clear);
        }
    }

    void UpdateDarkenCanvasRect()
    {
        if (_darkenMaterial == null || mapDarkImage == null)
        {
            return;
        }

        Canvas canvas = mapDarkImage.canvas;
        if (canvas == null)
        {
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        canvasRect.GetWorldCorners(corners);
        _darkenMaterial.SetVector(
            CanvasRectId,
            new Vector4(corners[0].x, corners[0].y, corners[2].x, corners[2].y));
    }

    void SetScanWaveCullTransparentMesh(bool cull)
    {
        if (scanWaveImage == null)
        {
            return;
        }

        CanvasRenderer renderer = scanWaveImage.canvasRenderer;
        if (renderer != null)
        {
            renderer.cullTransparentMesh = cull;
        }
    }

    void ApplyIdleState()
    {
        if (scanWaveImage != null)
        {
            SetScanWaveCullTransparentMesh(true);
            scanWaveImage.enabled = false;
            scanWaveImage.material = null;
        }

        if (mapDarkImage == null)
        {
            return;
        }

        if (hideMapDarkWhenIdle)
        {
            mapDarkImage.enabled = false;
            mapDarkImage.material = null;
        }
        else
        {
            ApplyMapDarkIdleVisual();
        }

        ApplyReadyStatusIdleState();
        SetReveal(0f);
        _scanRevealAtEnd = false;
    }

    void PrepareReadyStatusForScan(bool initialRevealZero = true)
    {
        if (readyStatusRoot == null)
        {
            return;
        }

        readyStatusRoot.gameObject.SetActive(true);
        CacheReadyStatusGraphics();

        for (int i = 0; i < _readyStatusRevealImages.Count; i++)
        {
            Image image = _readyStatusRevealImages[i];
            if (image == null)
            {
                continue;
            }

            image.enabled = true;
            if (_revealMaterial != null)
            {
                image.material = _revealMaterial;
                SyncMaterialFromGraphic(image, _revealMaterial);
            }
        }

        if (initialRevealZero)
        {
            ApplyReadyStatusTextAlpha(0f);
        }
    }

    void FinishReadyStatusAfterScan()
    {
        for (int i = 0; i < _readyStatusRevealImages.Count; i++)
        {
            Image image = _readyStatusRevealImages[i];
            if (image != null)
            {
                image.material = null;
            }
        }

        _readyStatusRevealImages.Clear();

        RestoreReadyStatusTextVisuals();

        _readyStatusDeferredGraphics.Clear();
    }

    void CacheReadyStatusGraphics()
    {
        _readyStatusRevealImages.Clear();
        _readyStatusDeferredGraphics.Clear();

        if (readyStatusRoot == null)
        {
            return;
        }

        Graphic[] graphics = readyStatusRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic is not Image image)
            {
                if (graphic != null && graphic != readyStatusRoot)
                {
                    _readyStatusDeferredGraphics.Add(new ReadyStatusGraphicState
                    {
                        graphic = graphic,
                        wasEnabled = graphic.enabled,
                        originalAlpha = GetGraphicAlpha(graphic),
                    });
                }

                continue;
            }

            _readyStatusRevealImages.Add(image);
        }
    }

    void ApplyReadyStatusIdleState()
    {
        ClearReadyStatusRevealMaterials();

        if (readyStatusRoot == null)
        {
            return;
        }

        if (hideReadyStatusWhenIdle)
        {
            readyStatusRoot.gameObject.SetActive(false);
        }
        else
        {
            readyStatusRoot.gameObject.SetActive(true);
        }
    }

    void ClearReadyStatusRevealMaterials()
    {
        for (int i = 0; i < _readyStatusRevealImages.Count; i++)
        {
            Image image = _readyStatusRevealImages[i];
            if (image != null)
            {
                image.material = null;
            }
        }

        _readyStatusRevealImages.Clear();
        _readyStatusDeferredGraphics.Clear();
    }

    void ApplyMapDarkIdleVisual()
    {
        if (mapDarkImage == null)
        {
            return;
        }

        mapDarkImage.enabled = true;
        mapDarkImage.material = null;
    }

    void ApplyMapDarkAfterScan()
    {
        if (hideMapDarkWhenIdle)
        {
            return;
        }

        ApplyMapDarkIdleVisual();
    }

    void SetReveal(float scanRevealHalf)
    {
        _currentRevealHalf = Mathf.Clamp(scanRevealHalf, 0f, 0.5f);

        if (_revealMaterial != null)
        {
            _revealMaterial.SetFloat(RevealHalfId, _currentRevealHalf);
        }

        if (_darkenMaterial != null)
        {
            _darkenMaterial.SetFloat(ScanRevealHalfId, _currentRevealHalf);
            UpdateDarkenCanvasRect();
        }

        float revealEnd = Mathf.Max(0.001f, scanRevealEnd);
        ApplyReadyStatusTextAlpha(Mathf.Clamp01(_currentRevealHalf / revealEnd));
    }

    static float GetGraphicAlpha(Graphic graphic)
    {
        if (graphic is TMP_Text tmpText)
        {
            return tmpText.alpha;
        }

        return graphic.color.a;
    }

    void ApplyReadyStatusTextAlpha(float normalizedReveal)
    {
        normalizedReveal = Mathf.Clamp01(normalizedReveal);

        for (int i = 0; i < _readyStatusDeferredGraphics.Count; i++)
        {
            ReadyStatusGraphicState state = _readyStatusDeferredGraphics[i];
            if (state.graphic == null)
            {
                continue;
            }

            state.graphic.enabled = state.wasEnabled;
            float alpha = state.originalAlpha * normalizedReveal;

            if (state.graphic is TMP_Text tmpText)
            {
                tmpText.alpha = alpha;
            }
            else
            {
                Color color = state.graphic.color;
                color.a = alpha;
                state.graphic.color = color;
            }
        }
    }

    void RestoreReadyStatusTextVisuals()
    {
        for (int i = 0; i < _readyStatusDeferredGraphics.Count; i++)
        {
            ReadyStatusGraphicState state = _readyStatusDeferredGraphics[i];
            if (state.graphic == null)
            {
                continue;
            }

            state.graphic.enabled = state.wasEnabled;

            if (state.graphic is TMP_Text tmpText)
            {
                tmpText.alpha = state.originalAlpha;
            }
            else
            {
                Color color = state.graphic.color;
                color.a = state.originalAlpha;
                state.graphic.color = color;
            }
        }
    }

    void OnDestroy()
    {
        if (scanWaveImage != null)
        {
            scanWaveImage.material = null;
        }

        if (mapDarkImage != null)
        {
            mapDarkImage.material = null;
        }

        ClearReadyStatusRevealMaterials();

        if (_revealMaterial != null)
        {
            Destroy(_revealMaterial);
            _revealMaterial = null;
        }

        if (_darkenMaterial != null)
        {
            Destroy(_darkenMaterial);
            _darkenMaterial = null;
        }
    }
}
