#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// QTE ライブ倍率 UI のシーンセットアップ。
/// </summary>
public static class QteLiveMultiplierSceneSetup
{
    private const string MenuPath = "InGame/Setup QTE Live Multiplier";
    private const string LegacyMenuPath = "InGame/Setup QTE Result Summary";
    private const string AbsorbNoteIconPrefabPath = "Assets/InGame/Prefabs/UI/QteAbsorbNoteIcon.prefab";
    private const string AbsorbSparklePrefabPath = "Assets/InGame/Prefabs/UI/QteAbsorbSparkle.prefab";
    private const string SparkleSpritePath = "Assets/InGame/Sprites/UI/Sparkle.png";
    private const string EcgMaterialPath = "Assets/InGame/Materials/EcgHeartbeatNeon.mat";
    private const string EcgLineObjectName = "EcgHeartbeatLine";
    private const string QteFxRootName = "QteFxRoot";
    private const string NoteStockBarName = "NoteStockBar";
    private const string QteChargeOverlayName = "QteChargeOverlay";
    private const string MainCanvasName = "MainCanvas";

    /// <summary>バッチモード用（Unity -executeMethod QteLiveMultiplierSceneSetup.SetupQteLiveMultiplierBatch）。</summary>
    public static void SetupQteLiveMultiplierBatch()
    {
        SetupQteLiveMultiplier();
    }

    [MenuItem(MenuPath)]
    public static void SetupQteLiveMultiplier()
    {
        QteTaikoView taikoView = Object.FindFirstObjectByType<QteTaikoView>(FindObjectsInactive.Include);
        if (taikoView == null)
        {
            Debug.LogError("[QteLiveMultiplierSceneSetup] QteTaikoView が見つかりません。");
            return;
        }

        Transform layerRoot = taikoView.transform;
        TaikoScrollQteRunner runner = Object.FindFirstObjectByType<TaikoScrollQteRunner>(FindObjectsInactive.Include);

        RectTransform liveRoot = EnsureLiveMultiplierRoot(layerRoot);
        DisableNoteStockBar(layerRoot);

        QteLiveMultiplierView liveView = liveRoot.GetComponent<QteLiveMultiplierView>();
        if (liveView == null)
        {
            liveView = Undo.AddComponent<QteLiveMultiplierView>(liveRoot.gameObject);
        }

        RectTransform absorbIconRoot = EnsureAbsorbIconRoot(liveRoot);
        RectTransform multiplierAnchor = FindChildRect(liveRoot, "MultiplierAnchor") ?? CreateMultiplierAnchor(liveRoot);
        TMP_Text mainLabel = EnsureMainLabel(multiplierAnchor);
        QteAbsorbNoteIconView absorbPrefab = EnsureAbsorbNoteIconPrefab();
        QteAbsorbSparkleView sparklePrefab = EnsureAbsorbSparklePrefab();
        TMP_Text apBadge = EnsureApBadgeLabel(multiplierAnchor);
        RectTransform apStampRect = EnsureApStampBadge(multiplierAnchor);

        SerializedObject serializedLive = new SerializedObject(liveView);
        serializedLive.FindProperty("_multiplierAnchor").objectReferenceValue = multiplierAnchor;
        serializedLive.FindProperty("_mainMultiplierLabel").objectReferenceValue = mainLabel;
        serializedLive.FindProperty("_absorbIconRoot").objectReferenceValue = absorbIconRoot;
        serializedLive.FindProperty("_absorbNoteIconPrefab").objectReferenceValue = absorbPrefab;
        serializedLive.FindProperty("_absorbSparklePrefab").objectReferenceValue = sparklePrefab;
        serializedLive.FindProperty("_apBadgeLabel").objectReferenceValue = apBadge;
        serializedLive.FindProperty("_apBadgeRect").objectReferenceValue = apStampRect;
        CanvasGroup apStampGroup = apStampRect.GetComponent<CanvasGroup>();
        if (apStampGroup == null)
        {
            apStampGroup = Undo.AddComponent<CanvasGroup>(apStampRect.gameObject);
        }

        apStampGroup.blocksRaycasts = false;
        apStampGroup.interactable = false;
        serializedLive.FindProperty("_apBadgeGroup").objectReferenceValue = apStampGroup;
        CanvasGroup mainGroup = mainLabel.GetComponent<CanvasGroup>();
        if (mainGroup == null)
        {
            mainGroup = mainLabel.gameObject.AddComponent<CanvasGroup>();
        }

        serializedLive.FindProperty("_mainMultiplierGroup").objectReferenceValue = mainGroup;

        Material ecgMaterial = EnsureEcgMaterial();
        EcgWaveformRenderer ecgRenderer = EnsureEcgLine(multiplierAnchor, ecgMaterial);

        serializedLive.FindProperty("_ecgFxRoot").objectReferenceValue = ecgRenderer != null ? ecgRenderer.gameObject : null;
        serializedLive.FindProperty("_ecgRenderer").objectReferenceValue = ecgRenderer;
        RectTransform chargeOverlay = EnsureQteChargeOverlay();
        serializedLive.FindProperty("_chargeOverlayParent").objectReferenceValue = chargeOverlay;
        serializedLive.ApplyModifiedPropertiesWithoutUndo();

        QtePresenter presenter = Object.FindFirstObjectByType<QtePresenter>(FindObjectsInactive.Include);
        PlayerView playerView = Object.FindFirstObjectByType<PlayerView>(FindObjectsInactive.Include);
        if (presenter != null)
        {
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_liveMultiplier").objectReferenceValue = liveView;
            if (playerView != null)
            {
                serializedPresenter.FindProperty("_playerView").objectReferenceValue = playerView;
            }

            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }

        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_liveMultiplier").objectReferenceValue = liveView;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[QteLiveMultiplierSceneSetup] QTE ライブ倍率のセットアップが完了しました。", liveView);
    }

    [MenuItem(LegacyMenuPath)]
    public static void SetupLegacyRedirect()
    {
        SetupQteLiveMultiplier();
    }

    private static RectTransform EnsureQteChargeOverlay()
    {
        GameObject mainCanvasGo = GameObject.Find(MainCanvasName);
        if (mainCanvasGo == null)
        {
            Debug.LogWarning($"[QteLiveMultiplierSceneSetup] {MainCanvasName} が見つかりません。チャージオーバーレイをスキップします。");
            return null;
        }

        Transform existing = mainCanvasGo.transform.Find(QteChargeOverlayName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject overlayGo = new GameObject(QteChargeOverlayName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(overlayGo, "Create QteChargeOverlay");
        RectTransform overlay = overlayGo.GetComponent<RectTransform>();
        overlay.SetParent(mainCanvasGo.transform, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        overlay.pivot = new Vector2(0.5f, 0.5f);
        overlay.localScale = Vector3.one;
        overlay.SetAsLastSibling();
        return overlay;
    }

    private static void DisableNoteStockBar(Transform layerRoot)
    {
        Transform bar = layerRoot.Find(NoteStockBarName);
        if (bar != null)
        {
            bar.gameObject.SetActive(false);
        }
    }

    private static RectTransform EnsureLiveMultiplierRoot(Transform layerRoot)
    {
        Transform existing = layerRoot.Find("QteResultSummaryRoot");
        if (existing == null)
        {
            existing = layerRoot.Find("QteLiveMultiplierRoot");
        }

        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject rootGo = new GameObject("QteLiveMultiplierRoot", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rootGo, "Create QteLiveMultiplierRoot");
        rootGo.transform.SetParent(layerRoot, false);
        RectTransform rt = rootGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform EnsureAbsorbIconRoot(RectTransform parent)
    {
        Transform existing = parent.Find("MergeLabelsFront");
        RectTransform chipRoot;
        if (existing != null)
        {
            chipRoot = existing as RectTransform;
            Canvas canvas = chipRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                Undo.DestroyObjectImmediate(canvas);
            }

            GraphicRaycaster raycaster = chipRoot.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Undo.DestroyObjectImmediate(raycaster);
            }
        }
        else
        {
            GameObject go = new GameObject(
                "MergeLabelsFront",
                typeof(RectTransform),
                typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create MergeLabelsFront");
            go.transform.SetParent(parent, false);
            chipRoot = go.GetComponent<RectTransform>();
            chipRoot.anchorMin = Vector2.zero;
            chipRoot.anchorMax = Vector2.one;
            chipRoot.offsetMin = Vector2.zero;
            chipRoot.offsetMax = Vector2.zero;
        }

        CanvasGroup group = chipRoot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = Undo.AddComponent<CanvasGroup>(chipRoot.gameObject);
        }

        group.blocksRaycasts = false;
        group.interactable = false;
        chipRoot.SetAsLastSibling();
        return chipRoot;
    }

    private static RectTransform CreateMultiplierAnchor(RectTransform parent)
    {
        GameObject go = new GameObject("MultiplierAnchor", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create MultiplierAnchor");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(400f, 120f);
        return rt;
    }

    private static TMP_Text EnsureMainLabel(RectTransform parent)
    {
        Transform existing = parent.Find("FinalMultiplierLabel");
        if (existing == null)
        {
            existing = parent.Find("MainMultiplierLabel");
        }

        if (existing != null)
        {
            TMP_Text tmp = existing.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                existing.name = "MainMultiplierLabel";
                tmp.text = "×1";
                return tmp;
            }
        }

        GameObject go = new GameObject(
            "MainMultiplierLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create MainMultiplierLabel");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TMP_Text tmpNew = go.GetComponent<TextMeshProUGUI>();
        tmpNew.alignment = TextAlignmentOptions.Center;
        tmpNew.fontSize = 72f;
        tmpNew.text = "×1";
        tmpNew.raycastTarget = false;
        return tmpNew;
    }

    private static TMP_Text EnsureApBadgeLabel(RectTransform parent)
    {
        Transform existing = parent.Find("ApBadgeLabel");
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        GameObject go = new GameObject(
            "ApBadgeLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create ApBadgeLabel");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -48f);
        rt.sizeDelta = new Vector2(200f, 48f);

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36f;
        tmp.text = "×2";
        tmp.raycastTarget = false;
        go.SetActive(false);
        return tmp;
    }

    private static RectTransform EnsureApStampBadge(RectTransform parent)
    {
        Transform existingInAnchor = parent.Find("AllPerfect");
        if (existingInAnchor != null)
        {
            return existingInAnchor as RectTransform;
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate.name != "AllPerfect")
            {
                continue;
            }

            RectTransform rt = candidate.GetComponent<RectTransform>();
            if (rt == null)
            {
                continue;
            }

            Undo.SetTransformParent(rt, parent, "Reparent AllPerfect to MultiplierAnchor");
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            candidate.SetActive(false);
            TMP_Text tmp = candidate.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.raycastTarget = false;
            }

            return rt;
        }

        GameObject go = new GameObject(
            "AllPerfect",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create AllPerfect stamp");
        go.transform.SetParent(parent, false);
        RectTransform created = go.GetComponent<RectTransform>();
        created.anchorMin = new Vector2(0.5f, 0.5f);
        created.anchorMax = new Vector2(0.5f, 0.5f);
        created.pivot = new Vector2(0.5f, 0.5f);
        created.anchoredPosition = new Vector2(0f, -80f);
        created.sizeDelta = new Vector2(700f, 50f);

        TMP_Text stampText = go.GetComponent<TextMeshProUGUI>();
        stampText.alignment = TextAlignmentOptions.Center;
        stampText.fontSize = 72f;
        stampText.text = "ALL PERFECT!";
        stampText.raycastTarget = false;
        go.SetActive(false);
        return created;
    }

    private static void EnsureUiPrefabFolder()
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

    private static QteAbsorbNoteIconView EnsureAbsorbNoteIconPrefab()
    {
        QteAbsorbNoteIconView existing = AssetDatabase.LoadAssetAtPath<QteAbsorbNoteIconView>(AbsorbNoteIconPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureUiPrefabFolder();

        GameObject root = new GameObject(
            "QteAbsorbNoteIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(QteAbsorbNoteIconView));
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(96f, 96f);
        root.SetActive(false);

        QteAbsorbNoteIconView prefab = PrefabUtility
            .SaveAsPrefabAsset(root, AbsorbNoteIconPrefabPath)
            .GetComponent<QteAbsorbNoteIconView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static QteAbsorbSparkleView EnsureAbsorbSparklePrefab()
    {
        QteAbsorbSparkleView existing = AssetDatabase.LoadAssetAtPath<QteAbsorbSparkleView>(AbsorbSparklePrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureUiPrefabFolder();

        Sprite sparkleSprite = LoadSparkleSprite();
        if (sparkleSprite == null)
        {
            Debug.LogWarning(
                $"[QteLiveMultiplierSceneSetup] Sparkle スプライトが見つかりません: {SparkleSpritePath}");
        }

        GameObject root = new GameObject(
            "QteAbsorbSparkle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(QteAbsorbSparkleView));
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sparkleSprite;
        image.color = Color.white;
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40f, 40f);
        root.SetActive(false);

        QteAbsorbSparkleView prefab = PrefabUtility
            .SaveAsPrefabAsset(root, AbsorbSparklePrefabPath)
            .GetComponent<QteAbsorbSparkleView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Sprite LoadSparkleSprite()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SparkleSpritePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == "Sparkle_0")
            {
                return sprite;
            }
        }

        return null;
    }

    private static RectTransform FindChildRect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child as RectTransform;
    }

    private static Material EnsureEcgMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(EcgMaterialPath);
        if (existing != null)
        {
            ApplyEcgMaterialSettings(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/InGame/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/InGame", "Materials");
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[QteLiveMultiplierSceneSetup] URP Unlit シェーダーが見つかりません。");
            return null;
        }

        Material material = new Material(shader);
        material.name = "EcgHeartbeatNeon";
        ApplyEcgMaterialSettings(material);

        AssetDatabase.CreateAsset(material, EcgMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void ApplyEcgMaterialSettings(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetColor("_BaseColor", new Color(0f, 0.85f, 1f, 0.85f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Transform EnsureQteFxRoot()
    {
        GameObject existing = GameObject.Find(QteFxRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject uiRoot = GameObject.Find("UI");
        Transform parent = uiRoot != null ? uiRoot.transform : null;

        GameObject rootGo = new GameObject(QteFxRootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Create QteFxRoot");
        if (parent != null)
        {
            rootGo.transform.SetParent(parent, false);
        }

        rootGo.transform.localPosition = Vector3.zero;
        rootGo.transform.localRotation = Quaternion.identity;
        rootGo.transform.localScale = Vector3.one;
        return rootGo.transform;
    }

    private static EcgWaveformRenderer EnsureEcgLine(
        RectTransform multiplierAnchor,
        Material ecgMaterial)
    {
        if (multiplierAnchor == null)
        {
            return null;
        }

        Transform existing = multiplierAnchor.Find(EcgLineObjectName);
        GameObject lineGo;
        if (existing != null)
        {
            lineGo = existing.gameObject;
        }
        else
        {
            Transform legacy = GameObject.Find(QteFxRootName)?.transform?.Find(EcgLineObjectName);
            if (legacy != null)
            {
                lineGo = legacy.gameObject;
                Undo.SetTransformParent(lineGo.transform, multiplierAnchor, "Move EcgHeartbeatLine to MultiplierAnchor");
            }
            else
            {
                lineGo = new GameObject(EcgLineObjectName);
                Undo.RegisterCreatedObjectUndo(lineGo, "Create EcgHeartbeatLine");
                lineGo.transform.SetParent(multiplierAnchor, false);
            }
        }

        lineGo.transform.localPosition = Vector3.zero;
        lineGo.transform.localRotation = Quaternion.identity;
        lineGo.transform.localScale = Vector3.one;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            lineGo.layer = uiLayer;
        }

        LineRenderer lineRenderer = lineGo.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = Undo.AddComponent<LineRenderer>(lineGo);
        }

        if (ecgMaterial != null)
        {
            lineRenderer.sharedMaterial = ecgMaterial;
        }

        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        EcgWaveformRenderer ecgRenderer = lineGo.GetComponent<EcgWaveformRenderer>();
        if (ecgRenderer == null)
        {
            ecgRenderer = Undo.AddComponent<EcgWaveformRenderer>(lineGo);
        }

        EcgUiWorldAnchor anchor = lineGo.GetComponent<EcgUiWorldAnchor>();
        if (anchor == null)
        {
            anchor = Undo.AddComponent<EcgUiWorldAnchor>(lineGo);
        }

        anchor.Configure(multiplierAnchor, ecgRenderer);

        SerializedObject anchorSerialized = new SerializedObject(anchor);
        anchorSerialized.FindProperty("_uiAnchor").objectReferenceValue = multiplierAnchor;
        anchorSerialized.FindProperty("_ecgRenderer").objectReferenceValue = ecgRenderer;
        anchorSerialized.ApplyModifiedPropertiesWithoutUndo();

        lineGo.SetActive(false);

        EditorUtility.SetDirty(lineGo);
        return ecgRenderer;
    }
}
#endif
