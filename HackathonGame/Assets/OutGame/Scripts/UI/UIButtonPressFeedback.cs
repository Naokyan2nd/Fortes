using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Press-down scale/color feedback for UI buttons, plus a short confirm animation on click.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float pressScale = 0.94f;
    [SerializeField] Color pressColor = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] float pressDownDuration = 0.05f;
    [SerializeField] float pressHoldDuration = 0.04f;
    [SerializeField] float pressReleaseDuration = 0.1f;

    Button _button;
    Image _targetImage;
    RectTransform _rect;
    Vector3 _restScale = Vector3.one;
    Color _restColor = Color.white;
    bool _isPointerDown;
    bool _isPlayingConfirm;
    Coroutine _visualCoroutine;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.transition = Selectable.Transition.None;
        }

        CacheReferences();
        ApplyRestVisualImmediate();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanReceivePress())
        {
            return;
        }

        _isPointerDown = true;
        StartVisualTransition(pressScale, pressColor, pressDownDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPointerDown || _isPlayingConfirm)
        {
            return;
        }

        _isPointerDown = false;
        StartVisualTransition(1f, _restColor, pressReleaseDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isPointerDown || _isPlayingConfirm)
        {
            return;
        }

        _isPointerDown = false;
        StartVisualTransition(1f, _restColor, pressReleaseDuration);
    }

    /// <summary>
    /// Clears Unity disabled tint and ensures full-opacity normal color on the target graphic.
    /// </summary>
    public static void RestoreNormalVisual(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;

        if (button.targetGraphic is Image image)
        {
            Color normal = button.colors.normalColor;
            image.color = new Color(normal.r, normal.g, normal.b, 1f);
        }

        UIButtonPressFeedback pressFeedback = button.GetComponent<UIButtonPressFeedback>();
        if (pressFeedback != null)
        {
            pressFeedback.RecacheRestVisualFromCurrent(preferButtonNormalColor: true);
        }
    }

    public IEnumerator PlayClickConfirm()
    {
        if (_rect == null)
        {
            CacheReferences();
        }

        if (_rect == null)
        {
            yield break;
        }

        _isPlayingConfirm = true;
        _isPointerDown = false;
        StopVisualTransition();

        yield return AnimateVisual(pressScale, pressColor, pressDownDuration);

        float hold = Mathf.Max(0f, pressHoldDuration);
        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }

        yield return AnimateVisual(1f, _restColor, pressReleaseDuration);
        _isPlayingConfirm = false;
    }

    void CacheReferences()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        _rect = transform as RectTransform;
        _targetImage = _button != null ? _button.targetGraphic as Image : GetComponent<Image>();
        _restScale = _rect != null ? _rect.localScale : Vector3.one;
        ResolveRestColor();
    }

    void ResolveRestColor()
    {
        if (_button != null)
        {
            _restColor = _button.colors.normalColor;
            _restColor.a = 1f;
            return;
        }

        if (_targetImage != null)
        {
            _restColor = _targetImage.color;
            _restColor.a = 1f;
            return;
        }

        _restColor = Color.white;
    }

    /// <summary>
    /// Re-read scale/color after external visual updates (e.g. Home ToScan presentation).
    /// When <paramref name="preferButtonNormalColor"/> is true, ignores disabled tint on the graphic.
    /// </summary>
    public void RecacheRestVisualFromCurrent(bool preferButtonNormalColor = false)
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_button != null)
        {
            _button.transition = Selectable.Transition.None;
        }

        _rect = transform as RectTransform;
        _targetImage = _button != null ? _button.targetGraphic as Image : GetComponent<Image>();
        _restScale = _rect != null ? _rect.localScale : Vector3.one;

        if (preferButtonNormalColor)
        {
            ResolveRestColor();
        }
        else if (_targetImage != null)
        {
            _restColor = _targetImage.color;
        }
        else
        {
            ResolveRestColor();
        }

        ApplyRestVisualImmediate();
    }

    void OnEnable()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_button != null)
        {
            _button.transition = Selectable.Transition.None;
            RecacheRestVisualFromCurrent(preferButtonNormalColor: true);
        }
    }

    bool CanReceivePress()
    {
        return _button != null && _button.interactable && _button.enabled && isActiveAndEnabled && !_isPlayingConfirm;
    }

    void ApplyRestVisualImmediate()
    {
        if (_rect != null)
        {
            _rect.localScale = _restScale;
        }

        if (_targetImage != null)
        {
            _targetImage.color = _restColor;
        }
    }

    void StartVisualTransition(float targetScale, Color targetColor, float duration)
    {
        StopVisualTransition();
        _visualCoroutine = StartCoroutine(AnimateVisual(targetScale, targetColor, duration));
    }

    void StopVisualTransition()
    {
        if (_visualCoroutine != null)
        {
            StopCoroutine(_visualCoroutine);
            _visualCoroutine = null;
        }
    }

    IEnumerator AnimateVisual(float targetScale, Color targetColor, float duration)
    {
        if (_rect == null)
        {
            yield break;
        }

        Vector3 startScale = _rect.localScale;
        Vector3 endScale = _restScale * targetScale;
        Color startColor = _targetImage != null ? _targetImage.color : targetColor;
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f)
        {
            _rect.localScale = endScale;
            if (_targetImage != null)
            {
                _targetImage.color = targetColor;
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _rect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            if (_targetImage != null)
            {
                _targetImage.color = Color.LerpUnclamped(startColor, targetColor, t);
            }

            yield return null;
        }

        _rect.localScale = endScale;
        if (_targetImage != null)
        {
            _targetImage.color = targetColor;
        }
    }
}
