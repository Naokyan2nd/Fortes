#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ノートストック UI の Prefab・シーンセットアップ。
/// </summary>
public static class NoteStockSceneSetup
{
    private const string MenuPath = "InGame/Setup Note Stock UI";
    private const string PrefabDir = "Assets/InGame/Prefabs/UI";
    private const string SlotPrefabPath = PrefabDir + "/NoteStockSlot.prefab";
    private const string PerfectIconPrefabPath = PrefabDir + "/NoteStockIcon.prefab";
    private const string MissIconPrefabPath = PrefabDir + "/NoteStockMissIcon.prefab";
    private const string JudgmentMissPrefabPath = "Assets/InGame/Prefabs/QteJudgmentMiss.prefab";
    private const string NoteStockBarName = "NoteStockBar";
    private const string SlotContainerName = "SlotContainer";
    private const string FlyRootName = "NoteStockFlyRoot";

    /// <summary>バッチモード用（Unity -executeMethod NoteStockSceneSetup.SetupNoteStockUiBatch）。</summary>
    public static void SetupNoteStockUiBatch()
    {
        SetupNoteStockUi();
    }

    [MenuItem(MenuPath)]
    public static void SetupNoteStockUi()
    {
        QteTaikoView taikoView = Object.FindFirstObjectByType<QteTaikoView>(FindObjectsInactive.Include);
        if (taikoView == null)
        {
            Debug.LogError("[NoteStockSceneSetup] QteTaikoView が見つかりません。");
            return;
        }

        Transform layerRoot = taikoView.transform;
        NoteStockSlotView slotPrefab = EnsureSlotPrefab();
        NoteStockIconView perfectIconPrefab = EnsureIconPrefab(PerfectIconPrefabPath, "NoteStockIcon", new Color(1f, 1f, 1f, 1f));
        NoteStockIconView missIconPrefab = EnsureMissIconPrefab();

        RectTransform barRoot = EnsureNoteStockBar(layerRoot);
        RectTransform slotContainer = EnsureSlotContainer(barRoot);
        RectTransform flyRoot = EnsureFlyRoot(barRoot);

        NoteStockUIManager manager = barRoot.GetComponent<NoteStockUIManager>();
        if (manager == null)
        {
            manager = Undo.AddComponent<NoteStockUIManager>(barRoot.gameObject);
        }

        QteTaikoSettingsSO settings = FindTaikoSettings(taikoView);
        TryAssignMissStockSpriteFromJudgmentPrefab(settings);

        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("_settings").objectReferenceValue = settings;
        serializedManager.FindProperty("_slotContainer").objectReferenceValue = slotContainer;
        serializedManager.FindProperty("_iconFlyRoot").objectReferenceValue = flyRoot;
        serializedManager.FindProperty("_slotPrefab").objectReferenceValue = slotPrefab;
        serializedManager.FindProperty("_perfectIconPrefab").objectReferenceValue = perfectIconPrefab;
        serializedManager.FindProperty("_missIconPrefab").objectReferenceValue = missIconPrefab;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        TaikoScrollQteRunner runner = layerRoot.GetComponent<TaikoScrollQteRunner>();
        QteLiveMultiplierView liveMultiplier = layerRoot.GetComponentInChildren<QteLiveMultiplierView>(true);
        if (runner != null && liveMultiplier != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_liveMultiplier").objectReferenceValue = liveMultiplier;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (runner != null)
        {
            Debug.LogWarning(
                "[NoteStockSceneSetup] QteLiveMultiplierView がありません。InGame → Setup QTE Live Multiplier を実行してください。",
                runner);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[NoteStockSceneSetup] ノートストック UI のセットアップが完了しました。", manager);
    }

    private static RectTransform EnsureNoteStockBar(Transform layerRoot)
    {
        Transform existing = layerRoot.Find(NoteStockBarName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject barGo = new GameObject(
            NoteStockBarName,
            typeof(RectTransform),
            typeof(NoteStockUIManager));
        Undo.RegisterCreatedObjectUndo(barGo, "Create NoteStockBar");
        barGo.transform.SetParent(layerRoot, false);

        RectTransform barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -24f);
        barRt.sizeDelta = new Vector2(0f, 96f);
        return barRt;
    }

    private static RectTransform EnsureSlotContainer(RectTransform barRoot)
    {
        Transform existing = barRoot.Find(SlotContainerName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject containerGo = new GameObject(
            SlotContainerName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        Undo.RegisterCreatedObjectUndo(containerGo, "Create SlotContainer");
        containerGo.transform.SetParent(barRoot, false);

        RectTransform containerRt = containerGo.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.pivot = new Vector2(0.5f, 0.5f);
        containerRt.anchoredPosition = Vector2.zero;
        containerRt.sizeDelta = new Vector2(600f, 72f);

        HorizontalLayoutGroup layout = containerGo.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = containerGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return containerRt;
    }

    private static RectTransform EnsureFlyRoot(RectTransform barRoot)
    {
        Transform existing = barRoot.Find(FlyRootName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject flyGo = new GameObject(FlyRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(flyGo, "Create NoteStockFlyRoot");
        flyGo.transform.SetParent(barRoot, false);

        RectTransform flyRt = flyGo.GetComponent<RectTransform>();
        flyRt.anchorMin = Vector2.zero;
        flyRt.anchorMax = Vector2.one;
        flyRt.offsetMin = Vector2.zero;
        flyRt.offsetMax = Vector2.zero;
        flyRt.pivot = new Vector2(0.5f, 0.5f);
        return flyRt;
    }

    private static NoteStockSlotView EnsureSlotPrefab()
    {
        NoteStockSlotView existing = AssetDatabase.LoadAssetAtPath<NoteStockSlotView>(SlotPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsurePrefabFolder();

        GameObject root = new GameObject(
            "NoteStockSlot",
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(Image),
            typeof(NoteStockSlotView));

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 72f;
        layoutElement.preferredHeight = 72f;

        Image frameImage = root.GetComponent<Image>();
        frameImage.raycastTarget = false;
        frameImage.color = new Color(1f, 1f, 1f, 0.15f);
        frameImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        frameImage.type = Image.Type.Sliced;

        GameObject anchorGo = new GameObject("IconAnchor", typeof(RectTransform));
        anchorGo.transform.SetParent(root.transform, false);
        RectTransform anchorRt = anchorGo.GetComponent<RectTransform>();
        anchorRt.anchorMin = Vector2.zero;
        anchorRt.anchorMax = Vector2.one;
        anchorRt.offsetMin = Vector2.zero;
        anchorRt.offsetMax = Vector2.zero;

        NoteStockSlotView slotView = root.GetComponent<NoteStockSlotView>();
        SerializedObject serializedSlot = new SerializedObject(slotView);
        serializedSlot.FindProperty("_iconAnchor").objectReferenceValue = anchorRt;
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();

        NoteStockSlotView prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath)
            .GetComponent<NoteStockSlotView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static NoteStockIconView EnsureIconPrefab(string path, string objectName, Color tint)
    {
        NoteStockIconView existing = AssetDatabase.LoadAssetAtPath<NoteStockIconView>(path);
        if (existing != null)
        {
            return existing;
        }

        EnsurePrefabFolder();

        GameObject root = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(NoteStockIconView));

        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = tint;
        image.preserveAspect = true;

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64f, 64f);

        NoteStockIconView view = root.GetComponent<NoteStockIconView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_rectTransform").objectReferenceValue = rt;
        serializedView.FindProperty("_canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        serializedView.FindProperty("_iconImage").objectReferenceValue = image;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);

        NoteStockIconView prefab = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NoteStockIconView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static NoteStockIconView EnsureMissIconPrefab()
    {
        NoteStockIconView prefab = EnsureIconPrefab(
            MissIconPrefabPath,
            "NoteStockMissIcon",
            new Color(1f, 0.35f, 0.35f, 1f));

        Sprite missSprite = TryLoadMissSpriteFromJudgmentPrefab();
        if (missSprite != null)
        {
            Image image = prefab.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = missSprite;
                EditorUtility.SetDirty(prefab);
            }
        }

        return prefab;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/InGame", "Prefabs");
            }

            AssetDatabase.CreateFolder("Assets/InGame/Prefabs", "UI");
        }
    }

    private static QteTaikoSettingsSO FindTaikoSettings(QteTaikoView taikoView)
    {
        TaikoScrollQteRunner runner = taikoView.GetComponent<TaikoScrollQteRunner>();
        if (runner == null)
        {
            return null;
        }

        SerializedObject serializedRunner = new SerializedObject(runner);
        return serializedRunner.FindProperty("_settings").objectReferenceValue as QteTaikoSettingsSO;
    }

    private static void TryAssignMissStockSpriteFromJudgmentPrefab(QteTaikoSettingsSO settings)
    {
        if (settings == null)
        {
            return;
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        if (serializedSettings.FindProperty("_missStockSprite").objectReferenceValue != null)
        {
            return;
        }

        Sprite missSprite = TryLoadMissSpriteFromJudgmentPrefab();
        if (missSprite == null)
        {
            return;
        }

        serializedSettings.FindProperty("_missStockSprite").objectReferenceValue = missSprite;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static Sprite TryLoadMissSpriteFromJudgmentPrefab()
    {
        GameObject missPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JudgmentMissPrefabPath);
        if (missPrefab == null)
        {
            return null;
        }

        Image image = missPrefab.GetComponent<Image>();
        return image != null ? image.sprite : null;
    }
}
#endif
