using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアル用：画面周囲を暗転し、指定 RectTransform の矩形だけ操作可能にする（4パネル穴あき）。
/// </summary>
public sealed class BattleTutorialCommandFocusOverlay : MonoBehaviour
{
    const int SortingOrder = 450;

    [SerializeField]
    private RectTransform _overlayRoot;

    [SerializeField]
    private RectTransform _topPanel;

    [SerializeField]
    private RectTransform _leftPanel;

    [SerializeField]
    private RectTransform _rightPanel;

    [SerializeField]
    private RectTransform _bottomPanel;

    [SerializeField]
    private float _dimAlpha = 0.65f;

    [SerializeField]
    private float _holePadding = 14f;

    private Canvas _canvas;
    private RectTransform _holeTarget;
    private bool _isVisible;

    public void Configure(
        RectTransform overlayRoot,
        RectTransform topPanel,
        RectTransform leftPanel,
        RectTransform rightPanel,
        RectTransform bottomPanel,
        float dimAlpha = 0.65f,
        float holePadding = 14f)
    {
        _overlayRoot = overlayRoot;
        _topPanel = topPanel;
        _leftPanel = leftPanel;
        _rightPanel = rightPanel;
        _bottomPanel = bottomPanel;
        _dimAlpha = dimAlpha;
        _holePadding = holePadding;
        EnsureCanvas();
        ApplyDimColor();
        HideImmediate();
    }

    public void SetHoleTarget(RectTransform holeTarget)
    {
        _holeTarget = holeTarget;
        if (_isVisible)
        {
            UpdatePanelLayout();
        }
    }

    public void Show(RectTransform holeTarget)
    {
        _holeTarget = holeTarget;
        _isVisible = true;
        gameObject.SetActive(true);
        UpdatePanelLayout();
    }

    public void Hide()
    {
        _isVisible = false;
        _holeTarget = null;
        HideImmediate();
    }

    void LateUpdate()
    {
        if (!_isVisible || _holeTarget == null)
        {
            return;
        }

        UpdatePanelLayout();
    }

    void HideImmediate()
    {
        gameObject.SetActive(false);
    }

    void EnsureCanvas()
    {
        if (_overlayRoot == null)
        {
            return;
        }

        _canvas = _overlayRoot.GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = _overlayRoot.gameObject.AddComponent<Canvas>();
        }

        _canvas.overrideSorting = true;
        _canvas.sortingOrder = SortingOrder;

        if (_overlayRoot.GetComponent<GraphicRaycaster>() == null)
        {
            _overlayRoot.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    void ApplyDimColor()
    {
        Color dim = new Color(0f, 0f, 0f, _dimAlpha);
        SetPanelColor(_topPanel, dim);
        SetPanelColor(_leftPanel, dim);
        SetPanelColor(_rightPanel, dim);
        SetPanelColor(_bottomPanel, dim);
    }

    static void SetPanelColor(RectTransform panel, Color color)
    {
        if (panel == null || !panel.TryGetComponent(out Image image))
        {
            return;
        }

        image.color = color;
        image.raycastTarget = true;
    }

    void UpdatePanelLayout()
    {
        if (_overlayRoot == null || _holeTarget == null)
        {
            return;
        }

        if (!TryGetHoleLocalRect(out float holeMinX, out float holeMaxX, out float holeMinY, out float holeMaxY))
        {
            return;
        }

        Rect parentRect = _overlayRoot.rect;
        float width = parentRect.width;
        float height = parentRect.height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        float normMinX = Mathf.Clamp01((holeMinX - parentRect.xMin) / width);
        float normMaxX = Mathf.Clamp01((holeMaxX - parentRect.xMin) / width);
        float normMinY = Mathf.Clamp01((holeMinY - parentRect.yMin) / height);
        float normMaxY = Mathf.Clamp01((holeMaxY - parentRect.yMin) / height);

        normMinX = Mathf.Min(normMinX, normMaxX);
        normMinY = Mathf.Min(normMinY, normMaxY);

        SetStretchPanel(_topPanel, 0f, normMaxY, 1f, 1f);
        SetStretchPanel(_bottomPanel, 0f, 0f, 1f, normMinY);
        SetStretchPanel(_leftPanel, 0f, normMinY, normMinX, normMaxY);
        SetStretchPanel(_rightPanel, normMaxX, normMinY, 1f, normMaxY);
    }

    bool TryGetHoleLocalRect(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0f;
        var worldCorners = new Vector3[4];
        _holeTarget.GetWorldCorners(worldCorners);

        Camera eventCamera = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = _canvas.worldCamera;
        }

        bool hasPoint = false;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _overlayRoot,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                continue;
            }

            if (!hasPoint)
            {
                minX = maxX = localPoint.x;
                minY = maxY = localPoint.y;
                hasPoint = true;
            }
            else
            {
                minX = Mathf.Min(minX, localPoint.x);
                maxX = Mathf.Max(maxX, localPoint.x);
                minY = Mathf.Min(minY, localPoint.y);
                maxY = Mathf.Max(maxY, localPoint.y);
            }
        }

        if (!hasPoint)
        {
            return false;
        }

        minX -= _holePadding;
        maxX += _holePadding;
        minY -= _holePadding;
        maxY += _holePadding;
        return true;
    }

    static void SetStretchPanel(RectTransform panel, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        if (panel == null)
        {
            return;
        }

        panel.anchorMin = new Vector2(anchorMinX, anchorMinY);
        panel.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.pivot = new Vector2(0.5f, 0.5f);
    }
}
