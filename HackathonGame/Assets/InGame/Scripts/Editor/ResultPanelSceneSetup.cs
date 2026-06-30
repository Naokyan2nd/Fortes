#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene の ResultCanvas に Result パネル UI を構築する。
/// </summary>
public static class ResultPanelSceneSetup
{
    private const string MenuPath = "InGame/Setup Result Panel";
    private const string VictoryCutInMenuPath = "InGame/Setup Victory Cut-In";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string ItemSlotPrefabPath = "Assets/InGame/Prefabs/UI/ResultItemSlot.prefab";
    private const string PrefabDir = "Assets/InGame/Prefabs/UI";

    private struct PanelBuildResult
    {
        public Transform PanelRoot;
        public GameObject VictoryRoot;
        public GameObject DefeatRoot;
        public TMP_Text OutcomeText;
        public GameObject RewardsBlock;
        public TMP_Text ExpText;
        public Transform ItemListRoot;
        public Button ToTitleButton;
        public Button ToStageButton;
    }

    [MenuItem(VictoryCutInMenuPath)]
    public static void SetupVictoryCutIn()
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"[ResultPanelSceneSetup] シーンが見つかりません: {MainScenePath}");
            return;
        }

        EditorSceneManager.OpenScene(MainScenePath);
        WireVictoryCutIn();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem(MenuPath)]
    public static void SetupResultPanel()
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"[ResultPanelSceneSetup] シーンが見つかりません: {MainScenePath}");
            return;
        }

        EditorSceneManager.OpenScene(MainScenePath);
        RunSetup();
    }

    /// <summary>バッチモード用。</summary>
    public static void SetupResultPanelBatch()
    {
        EditorSceneManager.OpenScene(MainScenePath);
        RunSetup();
        EditorSceneManager.SaveOpenScenes();
    }

    private static void RunSetup()
    {
        Canvas resultCanvas = FindResultCanvas();
        if (resultCanvas == null)
        {
            Debug.LogError("[ResultPanelSceneSetup] ResultCanvas が見つかりません。");
            return;
        }

        resultCanvas.sortingOrder = 200;
        RectTransform canvasRect = resultCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
        }

        ResultItemSlotView itemSlotPrefab = BuildAndSaveItemSlotPrefab();
        if (itemSlotPrefab == null)
        {
            return;
        }

        PanelBuildResult built = BuildPanelHierarchy(resultCanvas.transform);
        ResultView resultView = built.PanelRoot.GetComponent<ResultView>();
        if (resultView == null)
        {
            resultView = Undo.AddComponent<ResultView>(built.PanelRoot.gameObject);
        }

        WireResultView(resultView, resultCanvas.gameObject, built);
        WireVictoryCutInOnResultView(resultView, resultCanvas);

        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("_resultView").objectReferenceValue = resultView;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[ResultPanelSceneSetup] Result パネルのセットアップが完了しました。", resultView);
    }

    private static Canvas FindResultCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.name == "ResultCanvas")
            {
                return canvases[i];
            }
        }

        return null;
    }

    private static PanelBuildResult BuildPanelHierarchy(Transform canvasTransform)
    {
        Transform panelRoot = canvasTransform.Find("ResultView");
        if (panelRoot != null)
        {
            for (int i = panelRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(panelRoot.GetChild(i).gameObject);
            }
        }
        else
        {
            GameObject viewGo = new GameObject("ResultView", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(viewGo, "Create ResultView");
            panelRoot = viewGo.transform;
            panelRoot.SetParent(canvasTransform, false);
        }

        StretchFull(panelRoot as RectTransform);

        GameObject dim = CreateUiImage(panelRoot, "DimOverlay", new Color(0f, 0f, 0f, 0.72f));
        StretchFull(dim.GetComponent<RectTransform>());

        GameObject victoryRoot = CreateUiImage(panelRoot, "VictoryRoot", new Color(0.15f, 0.55f, 0.35f, 0.35f));
        RectTransform victoryRect = victoryRoot.GetComponent<RectTransform>();
        victoryRect.anchorMin = new Vector2(0.1f, 0.55f);
        victoryRect.anchorMax = new Vector2(0.9f, 0.88f);
        victoryRect.offsetMin = Vector2.zero;
        victoryRect.offsetMax = Vector2.zero;

        GameObject defeatRoot = CreateUiImage(panelRoot, "DefeatRoot", new Color(0.55f, 0.12f, 0.12f, 0.35f));
        RectTransform defeatRect = defeatRoot.GetComponent<RectTransform>();
        defeatRect.anchorMin = new Vector2(0.1f, 0.55f);
        defeatRect.anchorMax = new Vector2(0.9f, 0.88f);
        defeatRect.offsetMin = Vector2.zero;
        defeatRect.offsetMax = Vector2.zero;
        defeatRoot.SetActive(false);

        TextMeshProUGUI outcomeText = CreateTmp(panelRoot, "OutcomeText", "勝利！", 80f, new Vector2(0.5f, 0.72f), new Vector2(900f, 120f));

        GameObject rewardsBlock = new GameObject("RewardsBlock", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rewardsBlock, "Create RewardsBlock");
        rewardsBlock.transform.SetParent(panelRoot, false);
        RectTransform rewardsRect = rewardsBlock.GetComponent<RectTransform>();
        rewardsRect.anchorMin = new Vector2(0.08f, 0.22f);
        rewardsRect.anchorMax = new Vector2(0.92f, 0.52f);
        rewardsRect.offsetMin = Vector2.zero;
        rewardsRect.offsetMax = Vector2.zero;

        TextMeshProUGUI expLabel = CreateTmp(rewardsBlock.transform, "ExpLabel", "獲得経験値", 28f, new Vector2(0.5f, 0.82f), new Vector2(400f, 48f));
        expLabel.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI expText = CreateTmp(rewardsBlock.transform, "ExpValue", "+120 EXP", 40f, new Vector2(0.5f, 0.58f), new Vector2(500f, 56f));
        expText.fontStyle = FontStyles.Bold;

        GameObject itemList = new GameObject(
            "ItemList",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        Undo.RegisterCreatedObjectUndo(itemList, "Create ItemList");
        itemList.transform.SetParent(rewardsBlock.transform, false);
        RectTransform itemListRect = itemList.GetComponent<RectTransform>();
        itemListRect.anchorMin = new Vector2(0.05f, 0.05f);
        itemListRect.anchorMax = new Vector2(0.95f, 0.45f);
        itemListRect.offsetMin = Vector2.zero;
        itemListRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = itemList.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = itemList.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject buttonsRoot = new GameObject(
            "Buttons",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(buttonsRoot, "Create Buttons");
        buttonsRoot.transform.SetParent(panelRoot, false);
        RectTransform buttonsRect = buttonsRoot.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.25f, 0.04f);
        buttonsRect.anchorMax = new Vector2(0.75f, 0.18f);
        buttonsRect.offsetMin = Vector2.zero;
        buttonsRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup buttonLayout = buttonsRoot.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 16f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;

        Button toTitleButton = CreateButton(buttonsRoot.transform, "ToTitleButton", "タイトルへ");
        Button toStageButton = CreateButton(buttonsRoot.transform, "ToStageButton", "ステージ選択へ");

        return new PanelBuildResult
        {
            PanelRoot = panelRoot,
            VictoryRoot = victoryRoot,
            DefeatRoot = defeatRoot,
            OutcomeText = outcomeText,
            RewardsBlock = rewardsBlock,
            ExpText = expText,
            ItemListRoot = itemList.transform,
            ToTitleButton = toTitleButton,
            ToStageButton = toStageButton,
        };
    }

    private static void WireResultView(
        ResultView resultView,
        GameObject canvasRoot,
        PanelBuildResult built)
    {
        SerializedObject serialized = new SerializedObject(resultView);
        serialized.FindProperty("_canvasRoot").objectReferenceValue = canvasRoot;
        serialized.FindProperty("_victoryRoot").objectReferenceValue = built.VictoryRoot;
        serialized.FindProperty("_defeatRoot").objectReferenceValue = built.DefeatRoot;
        serialized.FindProperty("_outcomeText").objectReferenceValue = built.OutcomeText;
        serialized.FindProperty("_rewardsBlock").objectReferenceValue = built.RewardsBlock;
        serialized.FindProperty("_toTitleButton").objectReferenceValue = built.ToTitleButton;
        serialized.FindProperty("_toStageButton").objectReferenceValue = built.ToStageButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireVictoryCutIn()
    {
        Canvas resultCanvas = FindResultCanvas();
        if (resultCanvas == null)
        {
            Debug.LogError("[ResultPanelSceneSetup] ResultCanvas が見つかりません。");
            return;
        }

        ResultView resultView = resultCanvas.GetComponentInChildren<ResultView>(true);
        if (resultView == null)
        {
            Debug.LogError("[ResultPanelSceneSetup] ResultView が見つかりません。");
            return;
        }

        WireVictoryCutInOnResultView(resultView, resultCanvas);
        Debug.Log("[ResultPanelSceneSetup] Victory Cut-In の参照配線が完了しました。", resultView);
    }

    private static void WireVictoryCutInOnResultView(ResultView resultView, Canvas resultCanvas)
    {
        Transform panelRoot = resultView.transform;
        VictoryCutInView cutInView = resultView.GetComponent<VictoryCutInView>();
        if (cutInView == null)
        {
            cutInView = Undo.AddComponent<VictoryCutInView>(resultView.gameObject);
        }

        CanvasGroup canvasGroup = resultCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = Undo.AddComponent<CanvasGroup>(resultCanvas.gameObject);
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Transform dimOverlay = panelRoot.Find("DimOverlay");
        Transform victoryRoot = panelRoot.Find("VictoryRoot");
        Transform victorySearchRoot = victoryRoot != null ? victoryRoot : panelRoot;
        Transform backPopCircle = victorySearchRoot.Find("BackPopCircle");
        Transform banner = victorySearchRoot.Find("Banner");

        Image screenDimImage = dimOverlay != null ? dimOverlay.GetComponent<Image>() : null;
        Transform miniLeft = backPopCircle != null ? backPopCircle.Find("MiniLeftPopCircle") : null;
        Transform miniRight = backPopCircle != null ? backPopCircle.Find("MiniRightPopCircle") : null;
        Transform bannerDim = banner != null ? banner.Find("DimOverlay") : null;
        Transform victoryText = banner != null ? banner.Find("VictoryText") : null;
        Transform popCircle = banner != null ? banner.Find("PopCircle") : null;

        SerializedObject cutInSerialized = new SerializedObject(cutInView);
        cutInSerialized.FindProperty("_screenDimImage").objectReferenceValue = screenDimImage;
        cutInSerialized.FindProperty("_backPopCircleRoot").objectReferenceValue = backPopCircle;
        cutInSerialized.FindProperty("_miniLeftPopCircle").objectReferenceValue =
            miniLeft != null ? miniLeft as RectTransform : null;
        cutInSerialized.FindProperty("_miniRightPopCircle").objectReferenceValue =
            miniRight != null ? miniRight as RectTransform : null;
        cutInSerialized.FindProperty("_bannerRect").objectReferenceValue =
            banner != null ? banner as RectTransform : null;
        cutInSerialized.FindProperty("_bannerDimImage").objectReferenceValue =
            bannerDim != null ? bannerDim.GetComponent<Image>() : null;
        cutInSerialized.FindProperty("_victoryTextRect").objectReferenceValue =
            victoryText != null ? victoryText as RectTransform : null;
        cutInSerialized.FindProperty("_victoryTextImage").objectReferenceValue =
            victoryText != null ? victoryText.GetComponent<Image>() : null;
        cutInSerialized.FindProperty("_popCircleRect").objectReferenceValue =
            popCircle != null ? popCircle as RectTransform : null;
        cutInSerialized.FindProperty("_popCircleImage").objectReferenceValue =
            popCircle != null ? popCircle.GetComponent<Image>() : null;
        cutInSerialized.FindProperty("_outroCanvasGroup").objectReferenceValue = canvasGroup;

        Transform defeatRoot = panelRoot.Find("DefeatRoot");
        if (defeatRoot != null)
        {
            Transform defeatBack = defeatRoot.Find("BackPopCircle");
            Transform defeatBanner = defeatRoot.Find("Banner");
            cutInSerialized.FindProperty("_defeatRoot").objectReferenceValue = defeatRoot.gameObject;
            cutInSerialized.FindProperty("_defeatBackPopCircleRoot").objectReferenceValue = defeatBack;
            Transform defeatMiniLeft = defeatBack != null ? defeatBack.Find("MiniLeftPopCircle") : null;
            Transform defeatMiniRight = defeatBack != null ? defeatBack.Find("MiniRightPopCircle") : null;
            cutInSerialized.FindProperty("_defeatMiniLeftPopCircle").objectReferenceValue =
                defeatMiniLeft != null ? defeatMiniLeft as RectTransform : null;
            cutInSerialized.FindProperty("_defeatMiniRightPopCircle").objectReferenceValue =
                defeatMiniRight != null ? defeatMiniRight as RectTransform : null;
            cutInSerialized.FindProperty("_defeatBannerRect").objectReferenceValue =
                defeatBanner != null ? defeatBanner as RectTransform : null;
            Transform defeatBannerDim = defeatBanner != null ? defeatBanner.Find("DimOverlay") : null;
            cutInSerialized.FindProperty("_defeatBannerDimImage").objectReferenceValue =
                defeatBannerDim != null ? defeatBannerDim.GetComponent<Image>() : null;
            Transform defeatOutcomeText = defeatBanner != null ? defeatBanner.Find("VictoryText") : null;
            if (defeatOutcomeText == null && defeatBanner != null)
            {
                defeatOutcomeText = defeatBanner.Find("DefeatText");
            }

            cutInSerialized.FindProperty("_defeatOutcomeTextRect").objectReferenceValue =
                defeatOutcomeText != null ? defeatOutcomeText as RectTransform : null;
            cutInSerialized.FindProperty("_defeatOutcomeTextImage").objectReferenceValue =
                defeatOutcomeText != null ? defeatOutcomeText.GetComponent<Image>() : null;
        }

        cutInSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject resultSerialized = new SerializedObject(resultView);
        resultSerialized.FindProperty("_victoryCutInView").objectReferenceValue = cutInView;
        resultSerialized.FindProperty("_defeatRoot").objectReferenceValue =
            defeatRoot != null ? defeatRoot.gameObject : null;
        resultSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ResultItemSlotView BuildAndSaveItemSlotPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/InGame", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/InGame/Prefabs", "UI");
        }

        GameObject root = new GameObject("ResultItemSlot", typeof(RectTransform), typeof(LayoutElement));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(140f, 160f);

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 140f;
        layoutElement.preferredHeight = 160f;

        GameObject iconGo = CreateUiImage(root.transform, "Icon", Color.white);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.45f);
        iconRect.anchorMax = new Vector2(0.5f, 0.45f);
        iconRect.sizeDelta = new Vector2(96f, 96f);
        iconRect.anchoredPosition = Vector2.zero;
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;

        TextMeshProUGUI label = CreateTmp(root.transform, "Label", "Item", 22f, new Vector2(0.5f, 0.12f), new Vector2(130f, 40f));
        label.alignment = TextAlignmentOptions.Center;

        ResultItemSlotView view = root.AddComponent<ResultItemSlotView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_iconImage").objectReferenceValue = iconImage;
        serializedView.FindProperty("_labelText").objectReferenceValue = label;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabRoot = PrefabUtility.SaveAsPrefabAsset(root, ItemSlotPrefabPath);
        Object.DestroyImmediate(root);
        return prefabRoot != null ? prefabRoot.GetComponent<ResultItemSlotView>() : null;
    }

    private static GameObject CreateUiImage(Transform parent, string objectName, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static TextMeshProUGUI CreateTmp(
        Transform parent,
        string objectName,
        string defaultText,
        float fontSize,
        Vector2 anchorCenter,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorCenter;
        rect.anchorMax = anchorCenter;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.text = defaultText;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string objectName, string label)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 72f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.2f, 0.25f, 0.35f, 0.95f);

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.35f, 0.42f, 0.55f, 1f);
        colors.pressedColor = new Color(0.15f, 0.18f, 0.28f, 1f);
        button.colors = colors;

        LayoutElement layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 72f;
        layoutElement.minHeight = 64f;

        CreateTmp(go.transform, "Label", label, 30f, new Vector2(0.5f, 0.5f), new Vector2(400f, 48f));
        return button;
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
#endif
