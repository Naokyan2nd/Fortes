#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configures BloomOnly capture camera + Volume Bloom RT pipeline and the composite renderer feature.
/// </summary>
public static class BloomOnlyRendererSetup
{
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string MenuPath = "InGame/Setup Bloom Only Layer Rendering";
    private const string AssignLayerMenuPath = "InGame/Assign BloomOnly Layer To Selection";
    private const string CreateTestGlowMenuPath = "InGame/Create BloomOnly Test Glow";
    private const string RendererFeatureMap = "c9a790ef353d646c91028473";
    private const string TestGlowObjectName = "BloomOnly_TestGlow";

    [MenuItem(MenuPath, false, 200)]
    public static void Setup()
    {
        SetupRendererFeature();
        FixMainSceneCameras();
        EnsureBloomOnlyTestGlow();
        AssetDatabase.SaveAssets();
        Debug.Log(
            "[BloomOnlyRendererSetup] Setup complete. " +
            "Play mode: use Frame Debugger to verify Capture camera Bloom and 'BloomOnly Composite' pass.");
    }

    [MenuItem(MenuPath, true)]
    public static bool ValidateSetup()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(AssignLayerMenuPath, false, 201)]
    public static void AssignBloomOnlyLayerToSelection()
    {
        int bloomLayer = LayerMask.NameToLayer("BloomOnly");
        if (bloomLayer < 0)
        {
            Debug.LogError("[BloomOnlyRendererSetup] BloomOnly layer is not defined in Tags and Layers.");
            return;
        }

        GameObject[] objects = Selection.gameObjects;
        if (objects == null || objects.Length == 0)
        {
            Debug.LogWarning("[BloomOnlyRendererSetup] Select one or more GameObjects first.");
            return;
        }

        Undo.RecordObjects(objects, "Assign BloomOnly Layer");
        foreach (GameObject go in objects)
        {
            go.layer = bloomLayer;
            EditorUtility.SetDirty(go);
        }

        Debug.Log($"[BloomOnlyRendererSetup] Assigned BloomOnly layer to {objects.Length} object(s).");
    }

    [MenuItem(AssignLayerMenuPath, true)]
    public static bool ValidateAssignBloomOnlyLayer()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && Selection.gameObjects.Length > 0;
    }

    [MenuItem(CreateTestGlowMenuPath, false, 202)]
    public static void CreateTestGlowMenu()
    {
        EnsureBloomOnlyTestGlow(forceRecreate: true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem(CreateTestGlowMenuPath, true)]
    public static bool ValidateCreateTestGlowMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void SetupRendererFeature()
    {
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (rendererData == null)
        {
            Debug.LogError("[BloomOnlyRendererSetup] Renderer not found: " + RendererPath);
            return;
        }

        RemoveLegacyLayerFeature(rendererData);

        SerializedObject rendererSerialized = new SerializedObject(rendererData);
        SerializedProperty featureMap = rendererSerialized.FindProperty("m_RendererFeatureMap");
        if (featureMap != null)
        {
            featureMap.stringValue = RendererFeatureMap;
        }

        ScriptableRendererFeature compositeFeature = FindCompositeFeature(rendererData);
        if (compositeFeature == null)
        {
            Type featureType = typeof(BloomOnlyCompositeRendererFeature);
            compositeFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
            compositeFeature.name = "BloomOnlyCompositeRendererFeature";
            AssetDatabase.AddObjectToAsset(compositeFeature, rendererData);
            rendererData.rendererFeatures.Add(compositeFeature);
        }

        compositeFeature.SetActive(true);

        SerializedObject serialized = new SerializedObject(compositeFeature);
        SetProperty(serialized, "settings.renderPassEvent", (int)RenderPassEvent.BeforeRenderingPostProcessing);
        SetProperty(serialized, "settings.intensity", 1f);
        SetProperty(serialized, "settings.syncIntensityFromVolume", true);

        Shader compositeShader = Shader.Find("Hidden/InGame/BloomOnlyComposite");
        SetObjectReference(serialized, "compositeShader", compositeShader);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(compositeFeature);
    }

    private static void RemoveLegacyLayerFeature(ScriptableRendererData rendererData)
    {
        for (int i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
        {
            ScriptableRendererFeature feature = rendererData.rendererFeatures[i];
            if (feature == null || feature.GetType().Name != "BloomOnlyLayerRendererFeature")
            {
                continue;
            }

            rendererData.rendererFeatures.RemoveAt(i);
            UnityEngine.Object.DestroyImmediate(feature, true);
        }

        EditorUtility.SetDirty(rendererData);
    }

    private static ScriptableRendererFeature FindCompositeFeature(ScriptableRendererData rendererData)
    {
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is BloomOnlyCompositeRendererFeature)
            {
                return feature;
            }
        }

        return null;
    }

    private static void FixMainSceneCameras()
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(MainScenePath);
        Camera mainCamera = FindMainSceneCamera();
        GameObject bloomCameraGo = GameObject.Find("BloomOnlyCamera");

        if (mainCamera != null)
        {
            UniversalAdditionalCameraData mainData = mainCamera.GetUniversalAdditionalCameraData();
            if (mainData != null)
            {
                mainData.renderPostProcessing = false;
                Camera uiCamera = GameObject.Find("UICamera")?.GetComponent<Camera>();
                if (uiCamera != null && uiCamera != mainCamera)
                {
                    SetCameraStackSerialized(mainData, uiCamera);
                }
            }

            int bloomLayer = LayerMask.NameToLayer("BloomOnly");
            if (bloomLayer >= 0)
            {
                mainCamera.cullingMask &= ~(1 << bloomLayer);
            }
        }
        else
        {
            Debug.LogWarning("[BloomOnlyRendererSetup] Main Camera not found in MainScene.");
        }

        if (bloomCameraGo != null)
        {
            ConfigureCaptureCamera(bloomCameraGo, mainCamera);
        }
        else
        {
            Debug.LogWarning("[BloomOnlyRendererSetup] BloomOnlyCamera not found in MainScene.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCaptureCamera(GameObject bloomCameraGo, Camera mainCamera)
    {
        bloomCameraGo.SetActive(true);

        Camera captureCamera = bloomCameraGo.GetComponent<Camera>();
        if (captureCamera == null)
        {
            return;
        }

        int bloomLayer = LayerMask.NameToLayer("BloomOnly");
        if (bloomLayer >= 0)
        {
            captureCamera.cullingMask = 1 << bloomLayer;
        }

        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.black;
        captureCamera.depth = -100f;
        captureCamera.targetTexture = null;

        UniversalAdditionalCameraData captureData = captureCamera.GetUniversalAdditionalCameraData();
        if (captureData != null)
        {
            captureData.renderType = CameraRenderType.Base;
            captureData.renderPostProcessing = true;
            captureData.volumeLayerMask = 1;

            SerializedObject captureSerialized = new SerializedObject(captureData);
            SerializedProperty stackCameras = captureSerialized.FindProperty("m_Cameras");
            if (stackCameras != null)
            {
                stackCameras.ClearArray();
                captureSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        AudioListener listener = bloomCameraGo.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = false;
        }

        BloomOnlyBloomController controller = bloomCameraGo.GetComponent<BloomOnlyBloomController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<BloomOnlyBloomController>(bloomCameraGo);
        }

        SerializedObject controllerSerialized = new SerializedObject(controller);
        SetObjectReference(controllerSerialized, "_mainCamera", mainCamera);
        SetObjectReference(controllerSerialized, "_captureCamera", captureCamera);
        controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(bloomCameraGo);
    }

    private static void EnsureBloomOnlyTestGlow(bool forceRecreate = false)
    {
        int bloomLayer = LayerMask.NameToLayer("BloomOnly");
        if (bloomLayer < 0)
        {
            Debug.LogError("[BloomOnlyRendererSetup] BloomOnly layer is not defined in Tags and Layers.");
            return;
        }

        GameObject existing = GameObject.Find(TestGlowObjectName);
        if (existing != null && !forceRecreate)
        {
            return;
        }

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var glow = new GameObject(TestGlowObjectName);
        Undo.RegisterCreatedObjectUndo(glow, "Create BloomOnly Test Glow");
        glow.layer = bloomLayer;
        glow.transform.position = Vector3.zero;

        var renderer = glow.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateWhiteSprite();
        renderer.color = new Color(4f, 4f, 4f, 1f);
        renderer.sortingOrder = 1000;

        Shader hdrShader = Shader.Find("Eric/URP_AdditiveFlow_HDR");
        if (hdrShader != null)
        {
            var material = new Material(hdrShader);
            renderer.sharedMaterial = material;
        }

        EditorUtility.SetDirty(glow);
        Debug.Log(
            "[BloomOnlyRendererSetup] Created BloomOnly_TestGlow at origin. " +
            "Tune Bloom in MainScene Volume Profile; verify in Frame Debugger.");
    }

    private static Sprite CreateWhiteSprite()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private static void SetProperty(SerializedObject serialized, string path, float value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string path, bool value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string path, int value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetObjectReference(SerializedObject serialized, string path, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static Camera FindMainSceneCamera()
    {
        GameObject mainCameraGo = GameObject.Find("Main Camera");
        if (mainCameraGo != null)
        {
            Camera namedCamera = mainCameraGo.GetComponent<Camera>();
            if (namedCamera != null)
            {
                return namedCamera;
            }
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (camera.gameObject.name == "BloomOnlyCamera")
            {
                continue;
            }

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null && data.renderType == CameraRenderType.Base && camera.targetTexture == null)
            {
                return camera;
            }
        }

        return Camera.main;
    }

    private static void SetCameraStackSerialized(UniversalAdditionalCameraData cameraData, Camera uiCamera)
    {
        SerializedObject serialized = new SerializedObject(cameraData);
        SerializedProperty cameras = serialized.FindProperty("m_Cameras");
        if (cameras == null)
        {
            Debug.LogWarning("[BloomOnlyRendererSetup] m_Cameras property not found on UniversalAdditionalCameraData.");
            return;
        }

        cameras.ClearArray();
        cameras.InsertArrayElementAtIndex(0);
        cameras.GetArrayElementAtIndex(0).objectReferenceValue = uiCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
