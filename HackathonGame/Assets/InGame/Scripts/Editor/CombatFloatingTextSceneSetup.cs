#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene に戦闘 FloatingText 用 UI を追加する。
/// </summary>
public static class CombatFloatingTextSceneSetup
{
    private const string MenuPath = "InGame/Setup Combat Floating Text";
    private const string PrefabPath = "Assets/InGame/Prefabs/UI/CombatFloatingText.prefab";
    private const string PrefabDir = "Assets/InGame/Prefabs/UI";
    private const string WorldSpaceCanvasName = "WorldSpaceUI";
    private const string ContainerName = "CombatFloatingTextContainer";

    [MenuItem(MenuPath)]
    public static void SetupCombatFloatingText()
    {
        CombatFloatingTextView prefab = EnsurePrefab();
        if (prefab == null)
        {
            return;
        }

        Canvas worldCanvas = FindOrCreateWorldSpaceCanvas();
        Transform container = FindOrCreateContainer(worldCanvas.transform);
        CombatFloatingTextPresenter presenter = container.GetComponent<CombatFloatingTextPresenter>();
        if (presenter == null)
        {
            presenter = Undo.AddComponent<CombatFloatingTextPresenter>(container.gameObject);
        }

        Camera mainCamera = FindMainSceneCamera();
        SerializedObject serializedPresenter = new SerializedObject(presenter);
        serializedPresenter.FindProperty("_container").objectReferenceValue = container as RectTransform;
        serializedPresenter.FindProperty("_prefab").objectReferenceValue = prefab;
        SerializedProperty worldCameraProp = serializedPresenter.FindProperty("_worldCamera");
        if (worldCameraProp != null && mainCamera != null)
        {
            worldCameraProp.objectReferenceValue = mainCamera;
        }

        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty floatingTextProp = serializedManager.FindProperty("_floatingText");
            if (floatingTextProp != null)
            {
                floatingTextProp.objectReferenceValue = presenter;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[CombatFloatingTextSceneSetup] InGameManager._floatingText が見つかりません。");
            }
        }
        else
        {
            Debug.LogWarning("[CombatFloatingTextSceneSetup] InGameManager が見つかりません。");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[CombatFloatingTextSceneSetup] 戦闘 FloatingText のセットアップが完了しました。", presenter);
    }

    private static CombatFloatingTextView EnsurePrefab()
    {
        CombatFloatingTextView existing = AssetDatabase.LoadAssetAtPath<CombatFloatingTextView>(PrefabPath);
        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/InGame", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/InGame/Prefabs", "UI");
        }

        GameObject root = new GameObject("CombatFloatingText", typeof(RectTransform));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 48f);

        TextMeshProUGUI outlineTmp = CreateLabelChild(root.transform, "Outline", "0");
        TextMeshProUGUI faceTmp = CreateLabelChild(root.transform, "Face", "0");

        CombatFloatingTextView view = root.AddComponent<CombatFloatingTextView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_rectTransform").objectReferenceValue = rt;
        serializedView.FindProperty("_faceLabel").objectReferenceValue = faceTmp;
        serializedView.FindProperty("_outlineLabel").objectReferenceValue = outlineTmp;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        CombatFloatingTextView prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)
            .GetComponent<CombatFloatingTextView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static TextMeshProUGUI CreateLabelChild(Transform parent, string objectName, string defaultText)
    {
        GameObject labelGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(parent, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28f;
        tmp.text = defaultText;
        tmp.color = Color.white;
        return tmp;
    }

    private static Canvas FindOrCreateWorldSpaceCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.name == WorldSpaceCanvasName)
            {
                EnsureWorldSpaceCanvasSettings(canvases[i]);
                return canvases[i];
            }
        }

        Transform uiRoot = GameObject.Find("UI")?.transform;
        if (uiRoot == null)
        {
            Debug.LogError("[CombatFloatingTextSceneSetup] UI ルートが見つかりません。");
            return null;
        }

        GameObject canvasGo = new GameObject(WorldSpaceCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create WorldSpaceUI");
        canvasGo.layer = LayerMask.NameToLayer("WorldUI");
        if (canvasGo.layer < 0)
        {
            canvasGo.layer = 6;
        }

        canvasGo.transform.SetParent(uiRoot, false);
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        canvasRect.sizeDelta = new Vector2(100f, 100f);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        EnsureWorldSpaceCanvasSettings(canvas);
        return canvas;
    }

    private static void EnsureWorldSpaceCanvasSettings(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        Camera worldUiCamera = GameObject.Find("WorldUICamera")?.GetComponent<Camera>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldUiCamera;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        Transform canvasTransform = canvas.transform;
        if (canvasTransform.parent != null && canvasTransform.parent.name == "WorldUICamera")
        {
            canvasTransform.SetParent(GameObject.Find("UI")?.transform, false);
            canvasTransform.localPosition = Vector3.zero;
            canvasTransform.localRotation = Quaternion.identity;
            canvasTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }
    }

    private static Transform FindOrCreateContainer(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find(ContainerName);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject(ContainerName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create CombatFloatingTextContainer");
        go.transform.SetParent(canvasTransform, false);
        go.layer = canvasTransform.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return go.transform;
    }

    private static Camera FindMainSceneCamera()
    {
        GameObject mainCameraGo = GameObject.Find("Main Camera");
        if (mainCameraGo != null && mainCameraGo.TryGetComponent(out Camera mainCamera))
        {
            return mainCamera;
        }

        return Camera.main;
    }
}
#endif
