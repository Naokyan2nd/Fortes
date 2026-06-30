using System.Collections;
using UnityEngine;

/// <summary>
/// Places a UI RectTransform off-screen, then slides it into its rest position.
/// </summary>
[DisallowMultipleComponent]
public class UIRectSlideInEntryAnimator : MonoBehaviour
{
    public enum SlideInAxis
    {
        FromRight,
        FromLeft,
        FromBottom,
    }

    [SerializeField] RectTransform targetRect;
    [SerializeField] string fallbackObjectName;
    [SerializeField] SlideInAxis slideInAxis = SlideInAxis.FromRight;
    [SerializeField] float slideInOffsetX = 1400f;
    [SerializeField] float slideInOffsetY = 800f;
    [SerializeField] float slideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float easeOutPower = 3f;
    [SerializeField] bool prepareOffScreenOnAwake;

    Vector2 _restAnchoredPosition;
    bool _restPositionCached;

    public RectTransform TargetRect => targetRect;

    void Awake()
    {
        EnsureRectReference();
        if (targetRect == null)
        {
            return;
        }

        CacheRestPosition();

        if (prepareOffScreenOnAwake)
        {
            PrepareOffScreen();
        }
    }

    public IEnumerator PlaySlideIn()
    {
        EnsureRectReference();
        if (targetRect == null)
        {
            yield break;
        }

        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        Vector2 startPosition = _restAnchoredPosition + GetSlideOffset();
        targetRect.anchoredPosition = startPosition;

        float duration = Mathf.Max(0f, slideInDuration);
        if (duration <= 0f)
        {
            targetRect.anchoredPosition = _restAnchoredPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration), easeOutPower);
            targetRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, _restAnchoredPosition, eased);
            yield return null;
        }

        targetRect.anchoredPosition = _restAnchoredPosition;
    }

    public IEnumerator PlaySlideOut(bool deactivateOnComplete = true)
    {
        EnsureRectReference();
        if (targetRect == null)
        {
            yield break;
        }

        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        if (!targetRect.gameObject.activeInHierarchy)
        {
            yield break;
        }

        Vector2 startPosition = _restAnchoredPosition;
        Vector2 endPosition = _restAnchoredPosition + GetSlideOffset();
        targetRect.anchoredPosition = startPosition;

        float duration = Mathf.Max(0f, slideInDuration);
        if (duration <= 0f)
        {
            targetRect.anchoredPosition = endPosition;
            if (deactivateOnComplete)
            {
                targetRect.gameObject.SetActive(false);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration), easeOutPower);
            targetRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
            yield return null;
        }

        targetRect.anchoredPosition = endPosition;
        if (deactivateOnComplete)
        {
            targetRect.gameObject.SetActive(false);
        }
    }

    public void PrepareOffScreen()
    {
        EnsureRectReference();
        CacheRestPosition();

        if (targetRect == null)
        {
            return;
        }

        targetRect.anchoredPosition = _restAnchoredPosition + GetSlideOffset();
    }

    public void PrepareOffScreenRight()
    {
        slideInAxis = SlideInAxis.FromRight;
        PrepareOffScreen();
    }

    public void PrepareOffScreenLeft()
    {
        slideInAxis = SlideInAxis.FromLeft;
        PrepareOffScreen();
    }

    public void PrepareOffScreenBottom()
    {
        slideInAxis = SlideInAxis.FromBottom;
        PrepareOffScreen();
    }

    public void Configure(float offsetX, float duration, float easeOutPower)
    {
        slideInAxis = SlideInAxis.FromRight;
        slideInOffsetX = offsetX;
        slideInDuration = duration;
        this.easeOutPower = easeOutPower;
    }

    public void ConfigureFromLeft(float offsetX, float duration, float easeOutPower)
    {
        slideInAxis = SlideInAxis.FromLeft;
        slideInOffsetX = offsetX;
        slideInDuration = duration;
        this.easeOutPower = easeOutPower;
    }

    public void ConfigureFromBottom(float offsetY, float duration, float easeOutPower)
    {
        slideInAxis = SlideInAxis.FromBottom;
        slideInOffsetY = offsetY;
        slideInDuration = duration;
        this.easeOutPower = easeOutPower;
    }

    public void SetTarget(RectTransform rect, string objectNameFallback = null)
    {
        bool targetChanged = targetRect != rect;
        targetRect = rect;
        if (!string.IsNullOrEmpty(objectNameFallback))
        {
            fallbackObjectName = objectNameFallback;
        }

        EnsureRectReference();

        // Re-cache only when the target changes. Caching while off-screen (after PrepareOffScreen)
        // would treat the off-screen position as "rest" and PlaySlideIn would never return on-screen.
        if (targetChanged)
        {
            _restPositionCached = false;
            CacheRestPosition();
        }
    }

    public void SnapToRest()
    {
        EnsureRectReference();
        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        if (targetRect == null)
        {
            return;
        }

        targetRect.anchoredPosition = _restAnchoredPosition;
    }

    void EnsureRectReference()
    {
        if (targetRect != null)
        {
            return;
        }

        if (string.IsNullOrEmpty(fallbackObjectName))
        {
            targetRect = transform as RectTransform;
            return;
        }

        GameObject targetObject = GameObject.Find(fallbackObjectName);
        if (targetObject != null)
        {
            targetRect = targetObject.transform as RectTransform;
        }
    }

    void CacheRestPosition()
    {
        if (_restPositionCached || targetRect == null)
        {
            return;
        }

        _restAnchoredPosition = targetRect.anchoredPosition;
        _restPositionCached = true;
    }

    Vector2 GetSlideOffset()
    {
        if (slideInAxis == SlideInAxis.FromBottom)
        {
            return Vector2.down * Mathf.Max(0f, slideInOffsetY);
        }

        if (slideInAxis == SlideInAxis.FromLeft)
        {
            return Vector2.left * Mathf.Max(0f, slideInOffsetX);
        }

        return Vector2.right * Mathf.Max(0f, slideInOffsetX);
    }

    static float EvaluateEaseOut(float normalizedTime, float power)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }
}
