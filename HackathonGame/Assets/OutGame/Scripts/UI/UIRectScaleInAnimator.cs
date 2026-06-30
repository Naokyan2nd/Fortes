using System.Collections;
using UnityEngine;

/// <summary>
/// Scales a UI RectTransform from zero to its rest localScale.
/// </summary>
[DisallowMultipleComponent]
public class UIRectScaleInAnimator : MonoBehaviour
{
    [SerializeField] RectTransform targetRect;
    [SerializeField] string fallbackObjectName;
    [SerializeField] float scaleInDuration = 0.35f;
    [SerializeField] [Range(1f, 6f)] float easeOutPower = 3f;

    Vector3 _restLocalScale = Vector3.one;
    bool _restScaleCached;

    public RectTransform TargetRect => targetRect;

    void Awake()
    {
        EnsureRectReference();
        CacheRestScale();
    }

    public IEnumerator PlayScaleIn()
    {
        EnsureRectReference();
        if (targetRect == null)
        {
            yield break;
        }

        if (!_restScaleCached)
        {
            CacheRestScale();
        }

        Vector3 startScale = Vector3.zero;
        Vector3 endScale = _restLocalScale;
        targetRect.localScale = startScale;

        float duration = Mathf.Max(0f, scaleInDuration);
        if (duration <= 0f)
        {
            targetRect.localScale = endScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration), easeOutPower);
            targetRect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        targetRect.localScale = endScale;
    }

    public void Configure(float duration, float easeOutPower)
    {
        scaleInDuration = duration;
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

        // Re-cache only when the target changes. Caching while at zero scale (after Prepare)
        // would treat zero as "rest" and PlayScaleIn would not enlarge.
        if (targetChanged)
        {
            _restScaleCached = false;
            CacheRestScale();
        }
    }

    public void SnapToRest()
    {
        EnsureRectReference();
        if (!_restScaleCached)
        {
            CacheRestScale();
        }

        if (targetRect == null)
        {
            return;
        }

        targetRect.localScale = _restLocalScale;
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

    void CacheRestScale()
    {
        if (_restScaleCached || targetRect == null)
        {
            return;
        }

        _restLocalScale = targetRect.localScale;
        _restScaleCached = true;
    }

    static float EvaluateEaseOut(float normalizedTime, float power)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }
}
