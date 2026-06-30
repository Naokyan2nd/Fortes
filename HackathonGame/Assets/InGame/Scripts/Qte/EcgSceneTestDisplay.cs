using UnityEngine;

/// <summary>
/// Main Camera の子に固定し、LineRenderer の Positions[0]/[1] を ECG 始点・終点として使う。
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EcgWaveformRenderer))]
public sealed class EcgSceneTestDisplay : MonoBehaviour
{
    [SerializeField]
    private Camera _targetCamera;

    private bool _attachedToCamera;

    private void Awake()
    {
        EcgUiWorldAnchor uiAnchor = GetComponent<EcgUiWorldAnchor>();
        if (uiAnchor != null)
        {
            uiAnchor.enabled = false;
        }

        EcgWaveformRenderer ecgRenderer = GetComponent<EcgWaveformRenderer>();
        if (ecgRenderer != null)
        {
            ecgRenderer.SetSceneTestMode(true);
        }

        AttachToCamera();
    }

    private void AttachToCamera()
    {
        if (_attachedToCamera)
        {
            return;
        }

        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return;
        }

        transform.SetParent(camera.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        _attachedToCamera = true;
    }

    private Camera ResolveCamera()
    {
        if (_targetCamera != null)
        {
            return _targetCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.name == "Main Camera")
            {
                return camera;
            }
        }

        return Camera.main;
    }
}
