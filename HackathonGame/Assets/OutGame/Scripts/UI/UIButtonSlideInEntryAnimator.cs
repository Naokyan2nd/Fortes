using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Places a UI button off-screen to the right, then slides it into its rest position.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSlideInEntryAnimator : MonoBehaviour
{
    [SerializeField] Button targetButton;
    [SerializeField] string fallbackObjectName;
    [SerializeField] float slideInOffsetX = 1400f;
    [SerializeField] float slideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float easeOutPower = 3f;
    [SerializeField] bool slideFromLeft;
    [SerializeField] bool prepareOffScreenOnAwake;
    [SerializeField] bool enableInteractableAfterSlide = true;

    Vector2 _restAnchoredPosition;
    bool _restPositionCached;

    public Button TargetButton => targetButton;

    void Awake()
    {
        EnsureButtonReference();
        if (targetButton == null)
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
        EnsureButtonReference();

        RectTransform rect = GetButtonRect();
        if (rect == null)
        {
            yield break;
        }

        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        float offsetX = Mathf.Max(0f, slideInOffsetX);
        Vector2 startPosition = _restAnchoredPosition + GetSlideDirection() * offsetX;
        rect.anchoredPosition = startPosition;
        PrepareButtonBeforeSlide();
        SetButtonInteractable(false);

        float duration = Mathf.Max(0f, slideInDuration);
        if (duration <= 0f)
        {
            rect.anchoredPosition = _restAnchoredPosition;
            SetButtonInteractable(enableInteractableAfterSlide);
            RefreshPressFeedbackRestVisual();
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, _restAnchoredPosition, eased);
            yield return null;
        }

        rect.anchoredPosition = _restAnchoredPosition;
        SetButtonInteractable(enableInteractableAfterSlide);
        RefreshPressFeedbackRestVisual();
    }

    public IEnumerator PlaySlideOut(bool deactivateOnComplete = true)
    {
        EnsureButtonReference();

        RectTransform rect = GetButtonRect();
        if (rect == null)
        {
            yield break;
        }

        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        if (!rect.gameObject.activeInHierarchy)
        {
            yield break;
        }

        float offsetX = Mathf.Max(0f, slideInOffsetX);
        Vector2 startPosition = _restAnchoredPosition;
        Vector2 endPosition = _restAnchoredPosition + GetSlideDirection() * offsetX;
        rect.anchoredPosition = startPosition;
        SetButtonInteractable(false);

        float duration = Mathf.Max(0f, slideInDuration);
        if (duration <= 0f)
        {
            rect.anchoredPosition = endPosition;
            if (deactivateOnComplete)
            {
                rect.gameObject.SetActive(false);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
            yield return null;
        }

        rect.anchoredPosition = endPosition;
        if (deactivateOnComplete)
        {
            rect.gameObject.SetActive(false);
        }
    }

    public void PrepareOffScreen()
    {
        EnsureButtonReference();
        CacheRestPosition();

        RectTransform rect = GetButtonRect();
        if (rect == null)
        {
            return;
        }

        float offsetX = Mathf.Max(0f, slideInOffsetX);
        rect.anchoredPosition = _restAnchoredPosition + GetSlideDirection() * offsetX;
        SetButtonInteractable(false);
    }

    public void PrepareOffScreenRight()
    {
        slideFromLeft = false;
        PrepareOffScreen();
    }

    public void PrepareOffScreenLeft()
    {
        slideFromLeft = true;
        PrepareOffScreen();
    }

    public void ConfigureSlideFromLeft(
        float offsetX,
        float duration,
        float easeOutPower,
        bool fromLeft = true)
    {
        slideFromLeft = fromLeft;
        slideInOffsetX = offsetX;
        slideInDuration = duration;
        this.easeOutPower = easeOutPower;
    }

    public void SetTarget(Button button, string objectNameFallback = null)
    {
        targetButton = button;
        if (!string.IsNullOrEmpty(objectNameFallback))
        {
            fallbackObjectName = objectNameFallback;
        }

        _restPositionCached = false;
        EnsureButtonReference();
        CacheRestPosition();
    }

    public void SnapToRest()
    {
        EnsureButtonReference();
        if (!_restPositionCached)
        {
            CacheRestPosition();
        }

        RectTransform rect = GetButtonRect();
        if (rect == null)
        {
            return;
        }

        rect.anchoredPosition = _restAnchoredPosition;
        SetButtonInteractable(true);
    }

    void EnsureButtonReference()
    {
        if (targetButton != null)
        {
            return;
        }

        if (string.IsNullOrEmpty(fallbackObjectName))
        {
            return;
        }

        GameObject buttonObject = GameObject.Find(fallbackObjectName);
        if (buttonObject != null)
        {
            targetButton = buttonObject.GetComponent<Button>();
        }
    }

    void CacheRestPosition()
    {
        if (_restPositionCached)
        {
            return;
        }

        RectTransform rect = GetButtonRect();
        if (rect == null)
        {
            return;
        }

        _restAnchoredPosition = rect.anchoredPosition;
        _restPositionCached = true;
    }

    RectTransform GetButtonRect()
    {
        return targetButton != null ? targetButton.transform as RectTransform : null;
    }

    void PrepareButtonBeforeSlide()
    {
        if (targetButton == null)
        {
            return;
        }

        UIButtonPressFeedbackSceneBootstrap.EnsureOnButton(targetButton);
        UIButtonPressFeedback.RestoreNormalVisual(targetButton);
    }

    void SetButtonInteractable(bool interactable)
    {
        if (targetButton == null)
        {
            return;
        }

        targetButton.transition = Selectable.Transition.None;
        targetButton.interactable = interactable;
        UIButtonPressFeedback.RestoreNormalVisual(targetButton);
    }

    void RefreshPressFeedbackRestVisual()
    {
        if (targetButton == null)
        {
            return;
        }

        UIButtonPressFeedback.RestoreNormalVisual(targetButton);
    }

    Vector2 GetSlideDirection()
    {
        return slideFromLeft ? Vector2.left : Vector2.right;
    }

    float EvaluateEaseOut(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, easeOutPower);
    }
}
