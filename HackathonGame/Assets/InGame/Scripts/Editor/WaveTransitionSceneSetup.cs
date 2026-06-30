#if UNITY_EDITOR

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;

using UnityEngine.SceneManagement;



/// <summary>

/// MainScene のウェーブ遷移 UI 参照を配線する（Panel 参照は Inspector で手動設定）。

/// </summary>

public static class WaveTransitionSceneSetup

{

    private const string SetupMenuPath = "InGame/Setup Wave Transition";

    private const string MigrateMenuPath = "InGame/Migrate Wave Transition CutIn (Scene)";



    [MenuItem(SetupMenuPath)]

    public static void SetupWaveTransition()

    {

        Canvas qteCanvas = FindQteCanvas();

        if (qteCanvas == null)

        {

            Debug.LogError("[WaveTransitionSceneSetup] QTECanvas が見つかりません。");

            return;

        }



        Transform presenterRoot = FindOrCreatePresenterRoot(qteCanvas.transform);

        WaveTransitionPresenter presenter = presenterRoot.GetComponent<WaveTransitionPresenter>();

        if (presenter == null)

        {

            presenter = Undo.AddComponent<WaveTransitionPresenter>(presenterRoot.gameObject);

        }



        WaveTransitionCutInView cutInInstance = FindCutInView(presenterRoot);

        if (cutInInstance == null)

        {

            Debug.LogError(

                "[WaveTransitionSceneSetup] WaveTransitionCutInView が見つかりません。Presenter 配下に CutInView を配置してください。",

                presenter);

            return;

        }



        ValidateCutInViewReferences(cutInInstance);



        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);

        CameraGlitchManager glitchManager = Object.FindFirstObjectByType<CameraGlitchManager>(FindObjectsInactive.Include);



        SerializedObject serializedPresenter = new SerializedObject(presenter);

        serializedPresenter.FindProperty("_cutInView").objectReferenceValue = cutInInstance;

        serializedPresenter.FindProperty("_glitchManager").objectReferenceValue = glitchManager;

        serializedPresenter.FindProperty("_showWaveMarkAfterGlitch").boolValue = true;

        serializedPresenter.FindProperty("_cutInDelayAfterBeatSeconds").floatValue = 0f;

        serializedPresenter.FindProperty("_battleStartAfterBeatSeconds").floatValue = 0.25f;

        serializedPresenter.FindProperty("_battleStartCutInDelayAfterBeatSeconds").floatValue = 0f;

        serializedPresenter.FindProperty("_battleStartGlitchHoldSeconds").floatValue = 0.35f;

        serializedPresenter.FindProperty("_battleStartGlitchDecaySeconds").floatValue = 0.2f;

        serializedPresenter.FindProperty("_battleStartSpawnAtGlitchHoldNormalized").floatValue = 0.35f;

        serializedPresenter.FindProperty("_battleStartShowWaveMarkAfterGlitch").boolValue = true;



        if (manager != null)

        {

            serializedPresenter.FindProperty("_inGameManager").objectReferenceValue = manager;

        }



        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();



        if (manager != null)

        {

            SerializedObject serializedManager = new SerializedObject(manager);

            SerializedProperty waveTransitionProp = serializedManager.FindProperty("_waveTransition");

            if (waveTransitionProp != null)

            {

                waveTransitionProp.objectReferenceValue = presenter;

                serializedManager.ApplyModifiedPropertiesWithoutUndo();

            }

        }



        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(

            "[WaveTransitionSceneSetup] Presenter 配線完了。CutInView の Panel 参照は Inspector で手動アタッチしてください。",

            presenter);

    }



    [MenuItem(MigrateMenuPath)]

    public static void MigrateSceneCutInToInspectorRefs()

    {

        WaveTransitionCutInView cutInView = Object.FindFirstObjectByType<WaveTransitionCutInView>(FindObjectsInactive.Include);

        if (cutInView == null)

        {

            Debug.LogError("[WaveTransitionSceneSetup] WaveTransitionCutInView が見つかりません。");

            return;

        }



        UnpackCutInPrefabIfNeeded(cutInView.gameObject);

        MigrateCutInPanelComponents(cutInView);

        SetupWaveTransition();

    }



    /// <summary>Unity バッチモードから MainScene を開いて移行・配線する。</summary>

    public static void ExecuteBatchSetup()

    {

        const string mainScenePath = "Assets/Scenes/MainScene.unity";

        if (!System.IO.File.Exists(mainScenePath))

        {

            Debug.LogError("[WaveTransitionSceneSetup] MainScene が見つかりません。");

            EditorApplication.Exit(1);

            return;

        }



        EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Single);

        MigrateSceneCutInToInspectorRefs();

        EditorSceneManager.SaveOpenScenes();

        AssetDatabase.SaveAssets();

        Debug.Log("[WaveTransitionSceneSetup] バッチ移行・配線完了。");

        EditorApplication.Exit(0);

    }



    public static void ValidateCutInViewReferences(WaveTransitionCutInView cutInView)

    {

        if (cutInView == null)

        {

            return;

        }



        SerializedObject serializedView = new SerializedObject(cutInView);

        serializedView.Update();



        SerializedProperty battleStartProp = serializedView.FindProperty("_battleStartSweep");

        SerializedProperty nextWaveProp = serializedView.FindProperty("_nextWaveSweep");

        SerializedProperty waveMarksProp = serializedView.FindProperty("_waveMarks");



        if (battleStartProp.objectReferenceValue == null)

        {

            Debug.LogWarning(

                "[WaveTransitionSceneSetup] _battleStartSweep が未設定です。BattleStartImage に WaveTransitionCutInPanel を付け、Inspector で参照してください。",

                cutInView);

        }



        if (nextWaveProp.objectReferenceValue == null)

        {

            Debug.LogWarning(

                "[WaveTransitionSceneSetup] _nextWaveSweep が未設定です。NextWaveText に WaveTransitionCutInPanel を付け、Inspector で参照してください。",

                cutInView);

        }



        if (waveMarksProp.arraySize == 0)

        {

            Debug.LogWarning(

                "[WaveTransitionSceneSetup] _waveMarks が未設定です。WaveText_001 等に WaveTransitionCutInPanel を付け、Inspector で参照してください。",

                cutInView);

        }

    }



    private static void UnpackCutInPrefabIfNeeded(GameObject cutInRoot)

    {

        if (!PrefabUtility.IsPartOfPrefabInstance(cutInRoot))

        {

            return;

        }



        PrefabUtility.UnpackPrefabInstance(

            cutInRoot,

            PrefabUnpackMode.Completely,

            InteractionMode.AutomatedAction);

        Debug.Log("[WaveTransitionSceneSetup] WaveTransitionCutIn を Prefab からアンパックしました。", cutInRoot);

    }



    private static void MigrateCutInPanelComponents(WaveTransitionCutInView cutInView)

    {

        Transform root = cutInView.transform;



        WaveTransitionCutInPanel battleStart = EnsurePanelOnChild(root, "BattleStartImage");

        WaveTransitionCutInPanel nextWave = EnsurePanelOnChild(root, "NextWaveText");

        WaveTransitionCutInPanel[] waveMarks =

        {

            EnsurePanelOnChild(root, "WaveText_001"),

            EnsurePanelOnChild(root, "WaveText_002"),

            EnsurePanelOnChild(root, "WaveText_003"),

        };



        SerializedObject serializedView = new SerializedObject(cutInView);

        serializedView.FindProperty("_battleStartSweep").objectReferenceValue = battleStart;

        serializedView.FindProperty("_nextWaveSweep").objectReferenceValue = nextWave;



        SerializedProperty waveMarksProp = serializedView.FindProperty("_waveMarks");

        int markCount = 0;

        for (int i = 0; i < waveMarks.Length; i++)

        {

            if (waveMarks[i] != null)

            {

                markCount++;

            }

        }



        waveMarksProp.arraySize = markCount;

        int writeIndex = 0;

        for (int i = 0; i < waveMarks.Length; i++)

        {

            if (waveMarks[i] == null)

            {

                continue;

            }



            waveMarksProp.GetArrayElementAtIndex(writeIndex).objectReferenceValue = waveMarks[i];

            writeIndex++;

        }



        serializedView.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(cutInView);



        ValidateCutInViewReferences(cutInView);

        EditorSceneManager.MarkSceneDirty(cutInView.gameObject.scene);

    }



    private static WaveTransitionCutInPanel EnsurePanelOnChild(Transform root, string childName)

    {

        Transform child = root.Find(childName);

        if (child == null)

        {

            Debug.LogWarning($"[WaveTransitionSceneSetup] {childName} が見つかりません。", root);

            return null;

        }



        if (child.GetComponent<CanvasGroup>() == null)

        {

            Undo.AddComponent<CanvasGroup>(child.gameObject);

        }



        WaveTransitionCutInPanel panel = child.GetComponent<WaveTransitionCutInPanel>();

        if (panel == null)

        {

            panel = Undo.AddComponent<WaveTransitionCutInPanel>(child.gameObject);

        }



        return panel;

    }



    private static Canvas FindQteCanvas()

    {

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)

        {

            if (canvases[i].gameObject.name == "QTECanvas")

            {

                return canvases[i];

            }

        }



        return canvases.Length > 0 ? canvases[0] : null;

    }



    private static Transform FindOrCreatePresenterRoot(Transform canvasTransform)

    {

        Transform existing = canvasTransform.Find("WaveTransitionPresenter");

        if (existing != null)

        {

            return existing;

        }



        GameObject go = new GameObject("WaveTransitionPresenter", typeof(RectTransform));

        Undo.RegisterCreatedObjectUndo(go, "Create WaveTransitionPresenter");

        go.transform.SetParent(canvasTransform, false);

        RectTransform rt = go.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;

        rt.anchorMax = Vector2.one;

        rt.offsetMin = Vector2.zero;

        rt.offsetMax = Vector2.zero;

        return go.transform;

    }



    private static WaveTransitionCutInView FindCutInView(Transform presenterRoot)

    {

        WaveTransitionCutInView onRoot = presenterRoot.GetComponent<WaveTransitionCutInView>();

        if (onRoot != null)

        {

            return onRoot;

        }



        Transform existing = presenterRoot.Find("WaveTransitionCutIn");

        if (existing != null)

        {

            WaveTransitionCutInView onChild = existing.GetComponent<WaveTransitionCutInView>();

            if (onChild != null)

            {

                return onChild;

            }

        }



        return presenterRoot.GetComponentInChildren<WaveTransitionCutInView>(true);

    }

}

#endif

