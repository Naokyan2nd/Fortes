using System;
using UnityEngine;

/// <summary>
/// Auto-spins a UI element and lets the player drag to rotate it (same feel as Home CD).
/// </summary>
[DisallowMultipleComponent]
public class UITurntableDragRotator : MonoBehaviour
{
    [SerializeField] RectTransform rotateTarget;
    [SerializeField] Canvas rootCanvas;
    [SerializeField] bool enableAutoSpin = false;
    [SerializeField] float rotateDegreesPerSecond = 45f;
    [SerializeField] bool rotateClockwise = true;
    [SerializeField] float releaseSpeedPerWoundDegree = 2f;
    [SerializeField] float maxReleaseAngularVelocity = 540f;
    [SerializeField] float returnToBaseSpeed = 80f;
    [SerializeField] float minDragDeltaDegrees = 0.25f;

    float _angularVelocity;
    float _dragAngularVelocityDegreesPerSecond;
    float _woundDegrees;
    float _lastPointerAngleDegrees;
    bool _isTouching;
    bool _interactionEnabled = true;

    /// <summary>Fired whenever the turntable rotates (drag or auto-spin), in degrees.</summary>
    public event Action<float> RotatedDegrees;

    /// <summary>Can reduce or zero rotation before it is applied (e.g. scroll bounds).</summary>
    public event Func<float, float> FilterRotationDelta;

    /// <summary>Fired when a CD drag touch begins on the turntable.</summary>
    public event Action DragBegan;

    /// <summary>Fired when a CD drag touch ends.</summary>
    public event Action DragEnded;

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureReferences();

        if (enableAutoSpin)
        {
            _angularVelocity = GetBaseAngularVelocity();
        }
    }

    void Update()
    {
        if (!_interactionEnabled || rotateTarget == null)
        {
            return;
        }

        if (UpdateFromPointer())
        {
            return;
        }

        if (!enableAutoSpin)
        {
            return;
        }

        float baseVelocity = GetBaseAngularVelocity();
        _angularVelocity = Mathf.MoveTowards(_angularVelocity, baseVelocity, returnToBaseSpeed * Time.deltaTime);
        ApplyRotation(_angularVelocity * Time.deltaTime);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;

        if (!enabled)
        {
            EndTouch();
        }
    }

    public void SetAutoSpinEnabled(bool enabled)
    {
        enableAutoSpin = enabled;

        if (enabled && !_isTouching)
        {
            _angularVelocity = GetBaseAngularVelocity();
        }
    }

    void EnsureReferences()
    {
        if (rotateTarget == null)
        {
            rotateTarget = transform as RectTransform;
        }

        if (rootCanvas == null && rotateTarget != null)
        {
            rootCanvas = rotateTarget.GetComponentInParent<Canvas>();
        }
    }

    bool UpdateFromPointer()
    {
        if (!TryGetPrimaryPointer(out Vector2 screenPosition, out bool isPressed, out bool beganPress))
        {
            EndTouch();
            return false;
        }

        if (beganPress && IsPointerOverTarget(screenPosition))
        {
            BeginTouch(screenPosition);
        }

        if (!isPressed)
        {
            EndTouch();
            return false;
        }

        if (!_isTouching)
        {
            return false;
        }

        ApplyDrag(screenPosition);
        return true;
    }

    void BeginTouch(Vector2 screenPosition)
    {
        _isTouching = true;
        DragBegan?.Invoke();
        _woundDegrees = 0f;
        _angularVelocity = 0f;
        _dragAngularVelocityDegreesPerSecond = 0f;
        _lastPointerAngleDegrees = GetPointerAngleDegrees(screenPosition);
    }

    void EndTouch()
    {
        if (!_isTouching)
        {
            return;
        }

        _isTouching = false;
        _dragAngularVelocityDegreesPerSecond = 0f;
        DragEnded?.Invoke();

        if (!enableAutoSpin)
        {
            return;
        }

        float baseVelocity = GetBaseAngularVelocity();
        float windSign = Mathf.Sign(baseVelocity);
        float releaseBonus = _woundDegrees * releaseSpeedPerWoundDegree * windSign;
        _angularVelocity = Mathf.Clamp(baseVelocity + releaseBonus, -maxReleaseAngularVelocity, maxReleaseAngularVelocity);
        _woundDegrees = 0f;
    }

    void ApplyDrag(Vector2 screenPosition)
    {
        float angleDegrees = GetPointerAngleDegrees(screenPosition);
        float deltaDegrees = Mathf.DeltaAngle(_lastPointerAngleDegrees, angleDegrees);
        _lastPointerAngleDegrees = angleDegrees;

        if (Mathf.Abs(deltaDegrees) < minDragDeltaDegrees)
        {
            return;
        }

        ApplyRotation(deltaDegrees);

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        _dragAngularVelocityDegreesPerSecond = deltaDegrees / deltaTime;

        float windSign = Mathf.Sign(GetBaseAngularVelocity());
        float woundDelta = deltaDegrees * windSign;

        if (woundDelta > 0f)
        {
            _woundDegrees += woundDelta;
        }
        else
        {
            _woundDegrees = Mathf.Max(0f, _woundDegrees + woundDelta);
        }
    }

    void ApplyRotation(float deltaDegrees)
    {
        if (rotateTarget == null || Mathf.Abs(deltaDegrees) < 0.0001f)
        {
            return;
        }

        if (FilterRotationDelta != null)
        {
            deltaDegrees = FilterRotationDelta.Invoke(deltaDegrees);
        }

        if (Mathf.Abs(deltaDegrees) < 0.0001f)
        {
            return;
        }

        rotateTarget.Rotate(0f, 0f, deltaDegrees);
        RotatedDegrees?.Invoke(deltaDegrees);
    }

    float GetBaseAngularVelocity()
    {
        float sign = rotateClockwise ? -1f : 1f;
        return sign * rotateDegreesPerSecond;
    }

    Camera GetEventCamera()
    {
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rootCanvas.worldCamera;
        }

        return null;
    }

    Vector2 GetScreenCenter()
    {
        return RectTransformUtility.WorldToScreenPoint(GetEventCamera(), rotateTarget.position);
    }

    bool IsPointerOverTarget(Vector2 screenPosition)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            rotateTarget,
            screenPosition,
            GetEventCamera());
    }

    float GetPointerAngleDegrees(Vector2 screenPosition)
    {
        Vector2 direction = screenPosition - GetScreenCenter();

        if (direction.sqrMagnitude < 64f)
        {
            return _lastPointerAngleDegrees;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
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
