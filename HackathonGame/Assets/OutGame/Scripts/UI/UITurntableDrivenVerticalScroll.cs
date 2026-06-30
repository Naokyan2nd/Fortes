using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves a UI rect vertically from CD turntable rotation or vertical drag on its children.
/// </summary>
[DisallowMultipleComponent]
public class UITurntableDrivenVerticalScroll : MonoBehaviour
{
    [SerializeField] UITurntableDragRotator turntable;
    [SerializeField] RectTransform scrollTarget;
    [SerializeField] Canvas rootCanvas;
    [SerializeField] float pixelsPerDegree = 10f;
    [SerializeField] float minDragPixels = 2f;
    [SerializeField] float maxClickDragPixels = 18f;

    StageNoiseSlotFocus _scrollBounds;

    bool _isDragging;
    bool _didExceedClickThreshold;
    float _lastPointerScreenY;
    Vector2 _pressStartScreenPosition;
    RectTransform _pressedChild;
    bool _interactionEnabled = true;

    /// <summary>Fired when a vertical list drag touch begins.</summary>
    public event Action ScrollDragBegan;

    /// <summary>Fired when a vertical list drag touch ends.</summary>
    public event Action ScrollDragEnded;

    /// <summary>Fired on tap/click over a Noises child without dragging the list.</summary>
    public event Action<RectTransform> ScrollChildClicked;

    public void Configure(UITurntableDragRotator source, RectTransform target, float scrollPixelsPerDegree = 10f)
    {
        turntable = source;
        scrollTarget = target;
        pixelsPerDegree = scrollPixelsPerDegree;
        EnsureCanvas();
        Subscribe();
        ApplyScrollBoundsToCurrentPosition();
    }

    public void SetScrollBounds(StageNoiseSlotFocus scrollBounds)
    {
        _scrollBounds = scrollBounds;
        ApplyScrollBoundsToCurrentPosition();
    }

    void ApplyScrollBoundsToCurrentPosition()
    {
        if (scrollTarget == null || _scrollBounds == null)
        {
            return;
        }

        Vector2 position = scrollTarget.anchoredPosition;
        position.y = _scrollBounds.ClampScrollY(position.y);
        scrollTarget.anchoredPosition = position;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;

        if (!enabled)
        {
            EndDrag();
        }
    }

    void OnEnable()
    {
        EnsureReferences();
        Subscribe();
    }

    void OnDisable()
    {
        EndDrag();
        Unsubscribe();
    }

    void Update()
    {
        if (!_interactionEnabled || scrollTarget == null)
        {
            return;
        }

        UpdateFromPointerDrag();
    }

    void EnsureReferences()
    {
        if (turntable == null)
        {
            GameObject cdObject = GameObject.Find("CD");
            if (cdObject != null)
            {
                turntable = cdObject.GetComponent<UITurntableDragRotator>();
            }
        }

        if (scrollTarget == null)
        {
            GameObject noisesObject = GameObject.Find("Noises");
            if (noisesObject != null)
            {
                scrollTarget = noisesObject.GetComponent<RectTransform>();
            }
        }

        EnsureCanvas();
    }

    void EnsureCanvas()
    {
        if (rootCanvas == null && scrollTarget != null)
        {
            rootCanvas = scrollTarget.GetComponentInParent<Canvas>();
        }
    }

    void OnTurntableRotated(float deltaDegrees)
    {
        ScrollByPixels(-deltaDegrees * pixelsPerDegree, allowRubberBand: false);
    }

    float FilterTurntableRotationByScrollBounds(float deltaDegrees)
    {
        if (scrollTarget == null || _scrollBounds == null || Mathf.Abs(deltaDegrees) < 0.0001f)
        {
            return deltaDegrees;
        }

        float deltaPixels = -deltaDegrees * pixelsPerDegree;
        float currentScrollY = scrollTarget.anchoredPosition.y;
        float newScrollY = _scrollBounds.ApplyScrollDelta(currentScrollY, deltaPixels, allowRubberBand: false);
        float consumedPixels = newScrollY - currentScrollY;

        if (Mathf.Abs(consumedPixels) < 0.0001f)
        {
            return 0f;
        }

        return -consumedPixels / pixelsPerDegree;
    }

    void ScrollByPixels(float deltaPixels, bool allowRubberBand = true)
    {
        if (scrollTarget == null || Mathf.Abs(deltaPixels) < 0.0001f)
        {
            return;
        }

        Vector2 position = scrollTarget.anchoredPosition;
        if (_scrollBounds != null)
        {
            position.y = _scrollBounds.ApplyScrollDelta(position.y, deltaPixels, allowRubberBand);
        }
        else
        {
            position.y += deltaPixels;
        }

        scrollTarget.anchoredPosition = position;
    }

    void UpdateFromPointerDrag()
    {
        if (!TryGetPrimaryPointer(out Vector2 screenPosition, out bool isPressed, out bool beganPress))
        {
            EndDrag();
            return;
        }

        if (beganPress && TryGetChildUnderPointer(screenPosition, out RectTransform pressedChild))
        {
            BeginDrag(screenPosition, pressedChild);
        }

        if (!isPressed)
        {
            EndDrag();
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        if ((screenPosition - _pressStartScreenPosition).sqrMagnitude
            > maxClickDragPixels * maxClickDragPixels)
        {
            _didExceedClickThreshold = true;
        }

        float deltaPixels = screenPosition.y - _lastPointerScreenY;
        _lastPointerScreenY = screenPosition.y;

        if (Mathf.Abs(deltaPixels) < minDragPixels)
        {
            return;
        }

        _didExceedClickThreshold = true;
        ScrollByPixels(deltaPixels, allowRubberBand: true);
    }

    void BeginDrag(Vector2 screenPosition, RectTransform pressedChild)
    {
        _isDragging = true;
        _didExceedClickThreshold = false;
        _pressStartScreenPosition = screenPosition;
        _pressedChild = pressedChild;
        ScrollDragBegan?.Invoke();
        _lastPointerScreenY = screenPosition.y;
    }

    void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        bool isClick = !_didExceedClickThreshold && _pressedChild != null;
        RectTransform clickedChild = _pressedChild;

        _isDragging = false;
        _pressedChild = null;
        _didExceedClickThreshold = false;

        if (isClick)
        {
            ScrollChildClicked?.Invoke(clickedChild);
            return;
        }

        ScrollDragEnded?.Invoke();
    }

    bool TryGetChildUnderPointer(Vector2 screenPosition, out RectTransform childRect)
    {
        childRect = null;

        if (scrollTarget == null)
        {
            return false;
        }

        Camera eventCamera = GetEventCamera();

        for (int i = scrollTarget.childCount - 1; i >= 0; i--)
        {
            if (scrollTarget.GetChild(i) is not RectTransform candidate)
            {
                continue;
            }

            if (!candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsPointerOverNoiseItem(candidate, screenPosition, eventCamera))
            {
                continue;
            }

            childRect = candidate;
            return true;
        }

        return false;
    }

    static bool IsPointerOverNoiseItem(RectTransform itemRoot, Vector2 screenPosition, Camera eventCamera)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(itemRoot, screenPosition, eventCamera))
        {
            return true;
        }

        Graphic[] graphics = itemRoot.GetComponentsInChildren<Graphic>(false);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (!graphic.raycastTarget || !graphic.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    graphic.rectTransform,
                    screenPosition,
                    eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    Camera GetEventCamera()
    {
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rootCanvas.worldCamera;
        }

        return null;
    }

    void Subscribe()
    {
        if (turntable != null)
        {
            turntable.FilterRotationDelta += FilterTurntableRotationByScrollBounds;
            turntable.RotatedDegrees += OnTurntableRotated;
        }
    }

    void Unsubscribe()
    {
        if (turntable != null)
        {
            turntable.FilterRotationDelta -= FilterTurntableRotationByScrollBounds;
            turntable.RotatedDegrees -= OnTurntableRotated;
        }
    }

    static bool TryGetPrimaryPointer(out Vector2 screenPosition, out bool isPressed, out bool beganPress)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            isPressed = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            beganPress = touch.phase == TouchPhase.Began;
            return true;
        }

        screenPosition = Input.mousePosition;
        isPressed = Input.GetMouseButton(0);
        beganPress = Input.GetMouseButtonDown(0);
        return true;
    }
}
