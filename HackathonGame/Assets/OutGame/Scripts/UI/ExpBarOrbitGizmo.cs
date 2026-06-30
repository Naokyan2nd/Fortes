using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Draws a Scene-view orbit circle from this pivot to <see cref="bar"/> center (radius = distance).
/// Attach to ExpBarPivot; toggle <see cref="showOrbitCircle"/> to show/hide.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ExpBarOrbitGizmo : MonoBehaviour
{
    [SerializeField] private bool showOrbitCircle = true;
    [SerializeField] private RectTransform bar;
    [SerializeField] private Color circleColor = new(0f, 1f, 1f, 0.85f);
    [SerializeField] private Color radiusLineColor = new(1f, 0.92f, 0.2f, 0.9f);

    public bool ShowOrbitCircle
    {
        get => showOrbitCircle;
        set => showOrbitCircle = value;
    }

    void OnValidate()
    {
        if (bar == null && transform.childCount > 0)
        {
            bar = transform.GetChild(0) as RectTransform;
        }
    }

    void OnDrawGizmos()
    {
        if (!showOrbitCircle || bar == null)
        {
            return;
        }

        RectTransform pivot = transform as RectTransform;
        if (pivot == null)
        {
            return;
        }

        Vector3 center = pivot.position;
        Vector3 barCenter = bar.position;
        float radius = Vector3.Distance(center, barCenter);
        if (radius < 0.001f)
        {
            return;
        }

        Vector3 normal = GetOrbitPlaneNormal(pivot);

#if UNITY_EDITOR
        Handles.color = circleColor;
        Handles.DrawWireDisc(center, normal, radius);
        Handles.color = radiusLineColor;
        Handles.DrawLine(center, barCenter);
        Handles.DrawWireDisc(barCenter, normal, HandleUtility.GetHandleSize(barCenter) * 0.04f);
#else
        Gizmos.color = circleColor;
        DrawCircleGizmo(center, normal, radius, 48);
        Gizmos.color = radiusLineColor;
        Gizmos.DrawLine(center, barCenter);
#endif
    }

#if !UNITY_EDITOR
    static void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.Cross(normal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        Vector3 prev = center + tangent * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            Vector3 point = center + (tangent * Mathf.Cos(t) + bitangent * Mathf.Sin(t)) * radius;
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
    }
#endif

    static Vector3 GetOrbitPlaneNormal(RectTransform pivot)
    {
        Canvas canvas = pivot.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? Vector3.forward
                : canvas.transform.forward;
        }

        return pivot.forward.sqrMagnitude > 0.0001f ? pivot.forward : Vector3.forward;
    }
}
