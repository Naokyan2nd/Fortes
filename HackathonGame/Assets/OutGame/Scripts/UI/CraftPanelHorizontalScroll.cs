using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves a craft panel horizontally from drag on its children (Noises vertical scroll, but on X).
/// </summary>
[DisallowMultipleComponent]
public class CraftPanelHorizontalScroll : MonoBehaviour
{
    [SerializeField] RectTransform scrollTarget;
    [SerializeField] Canvas rootCanvas;
    [SerializeField] float minDragPixels = 2f;
    [SerializeField] float maxClickDragPixels = 18f;

    CraftPanelSlotFocus _scrollBounds;

    bool _isDragging;
    bool _didExceedClickThreshold;
    float _lastPointerScreenX;
    Vector2 _pressStartScreenPosition;
    RectTransform _pressedChild;
    bool _interactionEnabled = true;

    public event Action ScrollDragBegan;
    public event Action ScrollDragEnded;
    public event Action<RectTransform> ScrollChildClicked;

    public void Configure(RectTransform target)
    {
        scrollTarget = target;
        EnsureCanvas();
        ApplyScrollBoundsToCurrentPosition();
    }

    public void SetScrollBounds(CraftPanelSlotFocus scrollBounds)
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
        position.x = _scrollBounds.ClampScrollX(position.x);
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

    void OnDisable()
    {
        EndDrag();
    }

    void Update()
    {
        if (!_interactionEnabled || scrollTarget == null)
        {
            return;
        }

        UpdateFromPointerDrag();
    }

    void EnsureCanvas()
    {
        if (rootCanvas == null && scrollTarget != null)
        {
            rootCanvas = scrollTarget.GetComponentInParent<Canvas>();
        }
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
            position.x = _scrollBounds.ApplyScrollDelta(position.x, deltaPixels, allowRubberBand);
        }
        else
        {
            position.x += deltaPixels;
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

        float deltaPixels = screenPosition.x - _lastPointerScreenX;
        _lastPointerScreenX = screenPosition.x;

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
        _lastPointerScreenX = screenPosition.x;
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

            if (candidate.Find("Selected") == null)
            {
                continue;
            }

            if (!IsPointerOverCraftItem(candidate, screenPosition, eventCamera))
            {
                continue;
            }

            childRect = candidate;
            return true;
        }

        return false;
    }

    static bool IsPointerOverCraftItem(RectTransform itemRoot, Vector2 screenPosition, Camera eventCamera)
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
