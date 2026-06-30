#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// QTE 五線譜レイヤー入退場（BlackPanel / VisualRoot / QteTaikoLayerIntroView）のシーンセットアップ。
/// </summary>
public static class QteTaikoLayerIntroSceneSetup
{
    private const string MenuPath = "InGame/Setup Taiko Layer Intro";
    private const string VisualRootName = "QteTaikoVisualRoot";
    private const string BlackPanelName = "BlackPanel";

    [MenuItem(MenuPath)]
    public static void SetupTaikoLayerIntro()
    {
        QteTaikoView taikoView = Object.FindFirstObjectByType<QteTaikoView>(FindObjectsInactive.Include);
        if (taikoView == null)
        {
            Debug.LogError("[QteTaikoLayerIntroSceneSetup] QteTaikoView が見つかりません。");
            return;
        }

        Transform layerRoot = taikoView.transform;
        Undo.RegisterFullObjectHierarchyUndo(layerRoot.gameObject, "Setup Taiko Layer Intro");

        CanvasGroup blackPanel = EnsureBlackPanel(layerRoot, taikoView);
        RectTransform visualRoot = EnsureVisualRoot(layerRoot);
        ReparentVisualChildren(layerRoot, visualRoot);

        QteTaikoLayerIntroView introView = layerRoot.GetComponent<QteTaikoLayerIntroView>();
        if (introView == null)
        {
            introView = Undo.AddComponent<QteTaikoLayerIntroView>(layerRoot.gameObject);
        }

        SerializedObject serializedIntro = new SerializedObject(introView);
        serializedIntro.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
        serializedIntro.FindProperty("_blackPanel").objectReferenceValue = blackPanel;
        serializedIntro.FindProperty("_qteTaikoView").objectReferenceValue = taikoView;
        serializedIntro.ApplyModifiedPropertiesWithoutUndo();

        TaikoScrollQteRunner runner = layerRoot.GetComponent<TaikoScrollQteRunner>();
        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_layerIntro").objectReferenceValue = introView;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }

        QtePresenter presenter = Object.FindFirstObjectByType<QtePresenter>(FindObjectsInactive.Include);
        if (presenter != null)
        {
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_layerIntro").objectReferenceValue = introView;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[QteTaikoLayerIntroSceneSetup] レイヤー入退場のセットアップが完了しました。");
    }

    private static CanvasGroup EnsureBlackPanel(Transform layerRoot, QteTaikoView taikoView)
    {
        Transform existing = layerRoot.Find(BlackPanelName);
        if (existing != null)
        {
            CanvasGroup group = existing.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(existing.gameObject);
            }

            WireRaycastCatcher(taikoView, existing.GetComponent<Image>());
            return group;
        }

        Image rootImage = layerRoot.GetComponent<Image>();
        GameObject blackGo;
        if (rootImage != null)
        {
            blackGo = new GameObject(BlackPanelName, typeof(RectTransform), typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(blackGo, "Create BlackPanel");
            blackGo.transform.SetParent(layerRoot, false);

            RectTransform blackRt = blackGo.GetComponent<RectTransform>();
            StretchFull(blackRt);

            Image blackImage = Undo.AddComponent<Image>(blackGo);
            CopyImageSettings(rootImage, blackImage);
            Undo.DestroyObjectImmediate(rootImage);
        }
        else
        {
            blackGo = new GameObject(
                BlackPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Undo.RegisterCreatedObjectUndo(blackGo, "Create BlackPanel");
            blackGo.transform.SetParent(layerRoot, false);
            StretchFull(blackGo.GetComponent<RectTransform>());

            Image blackImage = blackGo.GetComponent<Image>();
            blackImage.color = new Color(0f, 0f, 0f, 0.6313726f);
            blackImage.raycastTarget = true;
        }

        blackGo.transform.SetAsFirstSibling();

        CanvasGroup canvasGroup = Undo.AddComponent<CanvasGroup>(blackGo);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        WireRaycastCatcher(taikoView, blackGo.GetComponent<Image>());
        return canvasGroup;
    }

    private static void WireRaycastCatcher(QteTaikoView taikoView, Image image)
    {
        if (taikoView == null || image == null)
        {
            return;
        }

        SerializedObject serializedView = new SerializedObject(taikoView);
        serializedView.FindProperty("_raycastCatcher").objectReferenceValue = image;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform EnsureVisualRoot(Transform layerRoot)
    {
        Transform existing = layerRoot.Find(VisualRootName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject visualGo = new GameObject(VisualRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(visualGo, "Create VisualRoot");
        visualGo.transform.SetParent(layerRoot, false);
        visualGo.transform.SetAsLastSibling();

        RectTransform visualRt = visualGo.GetComponent<RectTransform>();
        StretchFull(visualRt);
        visualGo.transform.localScale = new Vector3(1f, 0f, 1f);

        return visualRt;
    }

    private static void ReparentVisualChildren(Transform layerRoot, RectTransform visualRoot)
    {
        Transform gosenfu = FindChild(layerRoot, "Gosenfu");
        Transform judgment = FindChild(layerRoot, "TaikoJudgmentContainer");

        if (gosenfu != null && gosenfu.parent != visualRoot)
        {
            Undo.SetTransformParent(gosenfu, visualRoot, "Reparent Gosenfu");
        }

        if (judgment != null && judgment.parent != visualRoot)
        {
            Undo.SetTransformParent(judgment, visualRoot, "Reparent TaikoJudgmentContainer");
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    private static void CopyImageSettings(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.material = source.material;
        target.raycastTarget = source.raycastTarget;
        target.maskable = source.maskable;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
#endif
