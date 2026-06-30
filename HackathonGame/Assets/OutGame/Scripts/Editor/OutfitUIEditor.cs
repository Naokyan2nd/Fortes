#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Outfit 用 UI Prefab とシーン内 Panel のセットアップ。
/// メニュー: Hackathon → Outfit → Create Prefabs / Setup Panels In Open Scene
/// </summary>
public static class OutfitUIEditor
{
    private const string PrefabFolder = "Assets/OutGame/Prefabs";
    private const string ItemSlotPrefabPath = PrefabFolder + "/ItemSlot.prefab";
    private const string ItemPanelPrefabPath = PrefabFolder + "/OutfitItemPanel.prefab";

    [MenuItem("Hackathon/Outfit/Create Prefabs")]
    public static void CreatePrefabs()
    {
        EnsureFolder(PrefabFolder);

        var slotPrefab = BuildItemSlotPrefab();
        var panelPrefab = BuildOutfitItemPanelPrefab(slotPrefab);

        PrefabUtility.SaveAsPrefabAsset(slotPrefab.gameObject, ItemSlotPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(panelPrefab, ItemPanelPrefabPath);

        Object.DestroyImmediate(slotPrefab.gameObject);
        Object.DestroyImmediate(panelPrefab);

        AssetDatabase.Refresh();
        Debug.Log("Created: " + ItemSlotPrefabPath + ", " + ItemPanelPrefabPath);
    }

    [MenuItem("Hackathon/Outfit/Setup Outfit Scene")]
    public static void SetupAllOutfitScenes()
    {
        SetupScene("Assets/Scenes/OutfitScene.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("OutfitScene の Panel セットアップ完了。");
    }

    public static void SetupAllOutfitScenesBatch()
    {
        SetupAllOutfitScenes();
    }

    private static void SetupScene(string scenePath)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        if (!scene.IsValid())
        {
            Debug.LogError("シーンを開けません: " + scenePath);
            return;
        }
        SetupPanelsInOpenScene();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Hackathon/Outfit/Setup Panels In Open Scene")]
    public static void SetupPanelsInOpenScene()
    {
        CreatePrefabs();

        var slotPrefab = AssetDatabase.LoadAssetAtPath<ItemSlotView>(ItemSlotPrefabPath);
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPanelPrefabPath);
        if (slotPrefab == null || panelPrefab == null)
        {
            Debug.LogError("Prefab の読み込みに失敗しました。先に Create Prefabs を実行してください。");
            return;
        }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("シーンに Canvas がありません。");
            return;
        }

        var manager = Object.FindFirstObjectByType<OutfitSceneManager>();
        if (manager == null)
        {
            Debug.LogError("シーンに OutfitSceneManager がありません。");
            return;
        }

        var top = CreateOrGetPanel(canvas.transform, panelPrefab, slotPrefab, "ItemTopPanel", ItemType.Top);
        var bottom = CreateOrGetPanel(canvas.transform, panelPrefab, slotPrefab, "ItemBottomPanel", ItemType.Bottom);
        var cd = CreateOrGetPanel(canvas.transform, panelPrefab, slotPrefab, "CDPanel", ItemType.CD);

        WireManager(manager, top, bottom, cd);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Outfit panels を Canvas 配下に配置しました。Hierarchy で位置・サイズを自由に調整できます。");
    }

    private static GameObject CreateOrGetPanel(
        Transform canvas,
        GameObject panelPrefab,
        ItemSlotView slotPrefab,
        string panelName,
        ItemType type)
    {
        var existing = canvas.Find(panelName);
        if (existing != null)
        {
            ConfigurePanel(existing.gameObject, slotPrefab, type);
            return existing.gameObject;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvas);
        instance.name = panelName;
        ConfigurePanel(instance, slotPrefab, type);
        instance.SetActive(false);
        return instance;
    }

    private static void ConfigurePanel(GameObject panel, ItemSlotView slotPrefab, ItemType type)
    {
        var controller = panel.GetComponent<OutfitItemPanelController>();
        if (controller == null) return;

        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        var content = scroll != null ? scroll.content : null;
        var close = panel.transform.Find("PanelFrame/Header/CloseButton")?.GetComponent<Button>();

        var so = new SerializedObject(controller);
        so.FindProperty("filterType").enumValueIndex = (int)type;
        so.FindProperty("contentRoot").objectReferenceValue = content;
        so.FindProperty("scrollRect").objectReferenceValue = scroll;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireManager(OutfitSceneManager manager, GameObject top, GameObject bottom, GameObject cd)
    {
        var so = new SerializedObject(manager);
        so.FindProperty("itemTopPanel").objectReferenceValue = top;
        so.FindProperty("itemBottomPanel").objectReferenceValue = bottom;
        so.FindProperty("itemCDPanel").objectReferenceValue = cd;

        so.FindProperty("openTopButton").objectReferenceValue = FindButton("SelectTops");
        so.FindProperty("openBottomButton").objectReferenceValue = FindButton("SelectBottoms");
        so.FindProperty("openCDButton").objectReferenceValue = FindButton("SelectCD");
        so.FindProperty("backButton").objectReferenceValue = FindButton("BackToHomeButton");

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static ItemSlotView BuildItemSlotPrefab()
    {
        var root = CreateUIObject("ItemSlot", null);
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120, 120);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.55f, 0.55f, 0.55f, 1f);

        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(3, -3);
        outline.enabled = false;

        var button = root.AddComponent<Button>();
        button.targetGraphic = bg;

        var iconGo = CreateUIObject("Icon", root.transform);
        Stretch(iconGo);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.offsetMin = new Vector2(10, 10);
        iconRect.offsetMax = new Vector2(-10, -10);
        var icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var labelGo = CreateUIObject("Label", root.transform);
        Stretch(labelGo);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enabled = false;

        var slot = root.AddComponent<ItemSlotView>();
        var slotSo = new SerializedObject(slot);
        slotSo.FindProperty("backgroundImage").objectReferenceValue = bg;
        slotSo.FindProperty("iconImage").objectReferenceValue = icon;
        slotSo.FindProperty("labelText").objectReferenceValue = label;
        slotSo.FindProperty("selectionOutline").objectReferenceValue = outline;
        slotSo.FindProperty("button").objectReferenceValue = button;
        slotSo.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    private static GameObject BuildOutfitItemPanelPrefab(ItemSlotView slotPrefab)
    {
        var panel = CreateUIObject("OutfitItemPanel", null);
        Stretch(panel);
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        var frame = CreateUIObject("PanelFrame", panel.transform);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.08f, 0.12f);
        frameRect.anchorMax = new Vector2(0.92f, 0.88f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        frame.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);

        var header = CreateUIObject("Header", frame.transform);
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 72);
        headerRect.anchoredPosition = Vector2.zero;

        var titleGo = CreateUIObject("Title", header.transform);
        var titleRect = titleGo.GetComponent<RectTransform>();
        Stretch(titleGo);
        titleRect.offsetMin = new Vector2(24, 0);
        titleRect.offsetMax = new Vector2(-120, 0);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Items";
        title.fontSize = 36;
        title.color = Color.black;

        var closeGo = CreateUIObject("CloseButton", header.transform);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 0.5f);
        closeRect.anchorMax = new Vector2(1, 0.5f);
        closeRect.pivot = new Vector2(1, 0.5f);
        closeRect.sizeDelta = new Vector2(72, 56);
        closeRect.anchoredPosition = new Vector2(-12, 0);
        closeGo.AddComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeGo.GetComponent<Image>();
        var closeTextGo = CreateUIObject("Text", closeGo.transform);
        Stretch(closeTextGo);
        var closeText = closeTextGo.AddComponent<TextMeshProUGUI>();
        closeText.text = "X";
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.fontSize = 28;
        closeText.color = Color.black;

        var scrollGo = CreateUIObject("ScrollView", frame.transform);
        var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(16, 16);
        scrollRectTransform.offsetMax = new Vector2(-16, -88);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        var viewport = CreateUIObject("Viewport", scrollGo.transform);
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);

        var content = CreateUIObject("Content", viewport.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120, 120);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        var controller = panel.AddComponent<OutfitItemPanelController>();
        var panelSo = new SerializedObject(controller);
        panelSo.FindProperty("contentRoot").objectReferenceValue = content.transform;
        panelSo.FindProperty("scrollRect").objectReferenceValue = scroll;
        panelSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
        panelSo.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
