using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flies a UI rect from an off-screen anchor to its rest position (e.g. ReadyBanner, BackToStageButton).
/// </summary>
public class UIStageReadyBannerFlyIn : MonoBehaviour
{
    [SerializeField] RectTransform readyBannerRect;
    [SerializeField] string fallbackObjectName = "ReadyBanner";
    [SerializeField] Vector2 flyInFromPosition = new(1785f, 742f);
    [SerializeField] float flyInDuration = 0.52f;
    [SerializeField] [Range(1f, 6f)] float flyInEaseOutPower = 3f;
    [SerializeField] bool fadeInDuringFly = true;
    [SerializeField] bool bringToFrontOnShow = true;
    [Tooltip("Scene rest position. When zero, uses ReadyBanner's anchored position from the scene.")]
    [SerializeField] Vector2 flyInToPosition = new(-255f, 390f);

    Vector2 _restAnchoredPosition;
    float _restEulerZ;
    bool _restPositionCached;
    bool _restRotationCached;
    bool _useConfiguredRestPosition;
    bool _flyInCompleted;
    UITurntableDragRotator _turntableRotator;

    public bool FlyInCompleted => _flyInCompleted;
    public string TargetName => fallbackObjectName;

    public void Configure(
        RectTransform bannerRect,
        Vector2 fromPosition,
        float duration,
        float easeOutPower,
        bool useFadeIn)
    {
        Configure(bannerRect, fromPosition, flyInToPosition, duration, easeOutPower, useFadeIn, fallbackObjectName);
    }

    public void Configure(
        RectTransform targetRect,
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration,
        float easeOutPower,
        bool useFadeIn,
        string targetObjectName,
        bool bringToFront = true)
    {
        if (!string.IsNullOrEmpty(targetObjectName))
        {
            fallbackObjectName = targetObjectName;
        }

        readyBannerRect = targetRect;
        flyInFromPosition = fromPosition;
        flyInToPosition = toPosition;
        flyInDuration = duration;
        flyInEaseOutPower = easeOutPower;
        fadeInDuringFly = useFadeIn;
        bringToFrontOnShow = bringToFront;
        _useConfiguredRestPosition = true;
        _restPositionCached = false;
        _restRotationCached = false;
        _flyInCompleted = false;
        CacheRestPosition();
        CacheRestRotation();
    }

    public void EnsureBannerReference()
    {
        if (readyBannerRect != null)
        {
            return;
        }

        Transform searchRoot = ResolveCanvasRoot();
        GameObject bannerObject = FindChildByName(searchRoot, fallbackObjectName);
        if (bannerObject != null)
        {
            readyBannerRect = bannerObject.GetComponent<RectTransform>();
        }
    }

    public void PrepareHidden()
    {
        EnsureBannerReference();
        CacheRestPosition();
        CacheRestRotation();

        if (readyBannerRect == null)
        {
            return;
        }

        ApplyRestRotation();
        SetTurntableInteractionEnabled(false);
        readyBannerRect.gameObject.SetActive(false);
        _flyInCompleted = false;
    }

    public void ShowAtRest()
    {
        ShowAtRest(null, null);
    }

    public void ShowAtRest(RectTransform explicitRect, Vector2? explicitRestPosition)
    {
        if (explicitRect != null)
        {
            readyBannerRect = explicitRect;
            if (explicitRestPosition.HasValue)
            {
                _restAnchoredPosition = explicitRestPosition.Value;
                flyInToPosition = explicitRestPosition.Value;
                _restPositionCached = true;
            }
            else
            {
                _restPositionCached = false;
            }
        }

        EnsureBannerReference();
        CacheRestPosition();
        CacheRestRotation();

        if (readyBannerRect == null)
        {
            return;
        }

        ApplyRestRotation();
        SetTurntableInteractionEnabled(false);
        readyBannerRect.gameObject.SetActive(true);
        readyBannerRect.anchoredPosition = _restAnchoredPosition;
        if (bringToFrontOnShow)
        {
            readyBannerRect.SetAsLastSibling();
        }

        RestoreGraphicAlphas(readyBannerRect);

        _flyInCompleted = true;
    }

    static void RestoreGraphicAlphas(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = 1f;
            graphic.color = color;
        }
    }

    public IEnumerator PlayFlyIn()
    {
        if (_flyInCompleted)
        {
            yield break;
        }

        EnsureBannerReference();
        CacheRestPosition();

        if (readyBannerRect == null)
        {
            yield break;
        }

        Image bannerImage = readyBannerRect.GetComponent<Image>();
        Color restColor = bannerImage != null ? bannerImage.color : Color.white;

        CacheRestRotation();
        ApplyRestRotation();
        SetTurntableInteractionEnabled(false);

        readyBannerRect.gameObject.SetActive(true);
        if (bringToFrontOnShow)
        {
            readyBannerRect.SetAsLastSibling();
        }

        readyBannerRect.anchoredPosition = flyInFromPosition;

        if (fadeInDuringFly && bannerImage != null)
        {
            Color transparent = restColor;
            transparent.a = 0f;
            bannerImage.color = transparent;
        }

        float duration = Mathf.Max(0f, flyInDuration);
        if (duration <= 0f)
        {
            readyBannerRect.anchoredPosition = _restAnchoredPosition;
            if (bannerImage != null)
            {
                bannerImage.color = restColor;
            }

            _flyInCompleted = true;
            yield break;
        }

        Vector2 startPosition = flyInFromPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));

            readyBannerRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, _restAnchoredPosition, eased);

            if (fadeInDuringFly && bannerImage != null)
            {
                Color color = restColor;
                color.a = Mathf.Lerp(0f, restColor.a, eased);
                bannerImage.color = color;
            }

            yield return null;
        }

        readyBannerRect.anchoredPosition = _restAnchoredPosition;
        ApplyRestRotation();
        if (bannerImage != null)
        {
            bannerImage.color = restColor;
        }

        _flyInCompleted = true;
    }

    public IEnumerator PlayFlyOut()
    {
        EnsureBannerReference();
        CacheRestPosition();

        if (readyBannerRect == null || !readyBannerRect.gameObject.activeInHierarchy)
        {
            _flyInCompleted = false;
            yield break;
        }

        Image bannerImage = readyBannerRect.GetComponent<Image>();
        Color restColor = bannerImage != null ? bannerImage.color : Color.white;

        Vector2 startPosition = readyBannerRect.anchoredPosition;
        Vector2 endPosition = flyInFromPosition;

        float duration = Mathf.Max(0f, flyInDuration);
        if (duration <= 0f)
        {
            readyBannerRect.anchoredPosition = endPosition;
            if (bannerImage != null)
            {
                bannerImage.color = restColor;
            }

            readyBannerRect.gameObject.SetActive(false);
            _flyInCompleted = false;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));

            readyBannerRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);

            if (fadeInDuringFly && bannerImage != null)
            {
                Color color = restColor;
                color.a = Mathf.Lerp(restColor.a, 0f, eased);
                bannerImage.color = color;
            }

            yield return null;
        }

        readyBannerRect.anchoredPosition = endPosition;
        if (bannerImage != null)
        {
            bannerImage.color = restColor;
        }

        ApplyRestRotation();
        SetTurntableInteractionEnabled(false);
        readyBannerRect.gameObject.SetActive(false);
        _flyInCompleted = false;
    }

    void CacheRestRotation()
    {
        if (_restRotationCached || readyBannerRect == null)
        {
            return;
        }

        _restEulerZ = readyBannerRect.localEulerAngles.z;
        _restRotationCached = true;
    }

    void ApplyRestRotation()
    {
        if (readyBannerRect == null)
        {
            return;
        }

        Vector3 euler = readyBannerRect.localEulerAngles;
        euler.z = _restEulerZ;
        readyBannerRect.localEulerAngles = euler;
    }

    void SetTurntableInteractionEnabled(bool enabled)
    {
        if (_turntableRotator == null && readyBannerRect != null)
        {
            _turntableRotator = readyBannerRect.GetComponent<UITurntableDragRotator>();
        }

        if (_turntableRotator != null)
        {
            _turntableRotator.SetInteractionEnabled(enabled);
        }
    }

    void CacheRestPosition()
    {
        if (_restPositionCached)
        {
            return;
        }

        if (readyBannerRect == null)
        {
            return;
        }

        if (_useConfiguredRestPosition || flyInToPosition != Vector2.zero)
        {
            _restAnchoredPosition = flyInToPosition;
        }
        else
        {
            _restAnchoredPosition = readyBannerRect.anchoredPosition;
        }

        _restPositionCached = true;
    }

    static Transform ResolveCanvasRoot()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        return canvasObject != null ? canvasObject.transform : null;
    }

    static GameObject FindChildByName(Transform searchRoot, string objectName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    float EvaluateEaseOut(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        float power = Mathf.Max(1f, flyInEaseOutPower);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }
}
