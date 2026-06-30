using UnityEngine;

/// <summary>
/// UI RectTransform（MultiplierAnchor）の左右端を EcgWaveformRenderer のワールドスパンへ反映する。
/// </summary>
public sealed class EcgUiWorldAnchor : MonoBehaviour
{
    [SerializeField]
    private RectTransform _uiAnchor;

    [SerializeField]
    private EcgWaveformRenderer _ecgRenderer;

    [SerializeField]
    [Range(0.5f, 1f)]
    private float _widthFillRatio = 0.92f;

    private readonly Vector3[] _corners = new Vector3[4];

    public void Configure(RectTransform uiAnchor, EcgWaveformRenderer ecgRenderer)
    {
        _uiAnchor = uiAnchor;
        _ecgRenderer = ecgRenderer;
        SyncNow();
    }

    private void LateUpdate()
    {
        SyncNow();
    }

    private void SyncNow()
    {
        if (_uiAnchor == null || _ecgRenderer == null)
        {
            return;
        }

        _uiAnchor.GetWorldCorners(_corners);

        Vector3 left = Vector3.Lerp(_corners[0], _corners[1], 0.5f);
        Vector3 right = Vector3.Lerp(_corners[2], _corners[3], 0.5f);
        Vector3 center = (left + right) * 0.5f;
        Vector3 up = (_corners[1] - _corners[0]).normalized;

        if (up.sqrMagnitude < 0.0001f)
        {
            up = _uiAnchor.up;
        }

        float halfWidth = Vector3.Distance(left, right) * 0.5f * _widthFillRatio;
        Vector3 direction = (right - left).normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = _uiAnchor.right;
        }

        left = center - direction * halfWidth;
        right = center + direction * halfWidth;

        Canvas canvas = _uiAnchor.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null ? canvas.worldCamera : null;
        _ecgRenderer.SetWorldSpan(left, right, up, uiCamera);
    }
}
