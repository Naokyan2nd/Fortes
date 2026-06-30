#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainScene に Battle Camera Rig と Cinemachine Brain を追加するセットアップ。
/// </summary>
public static class BattleCameraSceneSetup
{
    private const string MenuPath = "InGame/Setup Battle Camera Rig";

    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem(MenuPath)]
    public static void SetupBattleCameraRig()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[BattleCameraSceneSetup] Main Camera が見つかりません。");
            return;
        }

        CinemachineBrain brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);
        }

        brain.DefaultBlend.Time = 0.35f;

        BattleCameraController existingController = Object.FindFirstObjectByType<BattleCameraController>();
        if (existingController != null)
        {
            Debug.Log("[BattleCameraSceneSetup] 既存の BattleCameraRig を更新します。", existingController);
            SetupImpulseComponents(existingController);
            WireInGameManager(existingController);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return;
        }

        GameObject rigRoot = new GameObject("BattleCameraRig");
        Undo.RegisterCreatedObjectUndo(rigRoot, "Create BattleCameraRig");

        CinemachineCamera defaultCam = CreateVirtualCamera(
            rigRoot.transform,
            "CM_Default",
            new Vector3(0f, 0f, -10f),
            orthographicSize: 5f,
            addPositionComposer: false,
            priority: 10);

        CinemachineCamera playerCam = CreateVirtualCamera(
            rigRoot.transform,
            "CM_Player",
            new Vector3(0f, 0f, -10f),
            orthographicSize: 3.5f,
            addPositionComposer: true,
            priority: 0);

        CinemachineCamera[] enemyCams = new CinemachineCamera[3];
        for (int i = 0; i < enemyCams.Length; i++)
        {
            enemyCams[i] = CreateVirtualCamera(
                rigRoot.transform,
                $"CM_Enemy_{i}",
                new Vector3(0f, 0f, -10f),
                orthographicSize: 3.5f,
                addPositionComposer: true,
                priority: 0);
        }

        BattleCameraController controller = Undo.AddComponent<BattleCameraController>(rigRoot);
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("_defaultCamera").objectReferenceValue = defaultCam;
        serializedController.FindProperty("_playerCamera").objectReferenceValue = playerCam;
        SerializedProperty enemyArray = serializedController.FindProperty("_enemyCameras");
        enemyArray.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            enemyArray.GetArrayElementAtIndex(i).objectReferenceValue = enemyCams[i];
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SetupImpulseComponents(controller);
        WireInGameManager(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = rigRoot;
        Debug.Log("[BattleCameraSceneSetup] Battle Camera Rig のセットアップが完了しました。");
    }

    /// <summary>Unity バッチモード用（-executeMethod BattleCameraSceneSetup.RunBatchSetup）。</summary>
    public static void RunBatchSetup()
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"[BattleCameraSceneSetup] シーンが見つかりません: {MainScenePath}");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(MainScenePath);
        SetupBattleCameraRig();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static void SetupImpulseComponents(BattleCameraController controller)
    {
        if (controller == null)
        {
            return;
        }

        GameObject rigRoot = controller.gameObject;
        CinemachineImpulseSource impulseSource = rigRoot.GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
        {
            impulseSource = Undo.AddComponent<CinemachineImpulseSource>(rigRoot);
            impulseSource.DefaultVelocity = new Vector3(1f, 0f, 0f);
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("_impulseSource").objectReferenceValue = impulseSource;

        SerializedProperty defaultCamProp = serializedController.FindProperty("_defaultCamera");
        SerializedProperty playerCamProp = serializedController.FindProperty("_playerCamera");
        SerializedProperty enemyCamsProp = serializedController.FindProperty("_enemyCameras");

        if (defaultCamProp.objectReferenceValue is CinemachineCamera defaultCam)
        {
            EnsureImpulseListener(defaultCam);
        }

        if (playerCamProp.objectReferenceValue is CinemachineCamera playerCam)
        {
            EnsureImpulseListener(playerCam);
        }

        if (enemyCamsProp != null)
        {
            for (int i = 0; i < enemyCamsProp.arraySize; i++)
            {
                if (enemyCamsProp.GetArrayElementAtIndex(i).objectReferenceValue is CinemachineCamera enemyCam)
                {
                    EnsureImpulseListener(enemyCam);
                }
            }
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureImpulseListener(CinemachineCamera vcam)
    {
        if (vcam == null)
        {
            return;
        }

        CinemachineImpulseListener listener = vcam.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
        {
            listener = Undo.AddComponent<CinemachineImpulseListener>(vcam.gameObject);
        }

        listener.ChannelMask = 1;
        listener.Gain = 1f;
        listener.Use2DDistance = true;
    }

    private static CinemachineCamera CreateVirtualCamera(
        Transform parent,
        string name,
        Vector3 localPosition,
        float orthographicSize,
        bool addPositionComposer,
        int priority)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;

        CinemachineCamera vcam = Undo.AddComponent<CinemachineCamera>(go);
        LensSettings lens = vcam.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = orthographicSize;
        vcam.Lens = lens;

        PrioritySettings prioritySettings = vcam.Priority;
        prioritySettings.Enabled = true;
        prioritySettings.Value = priority;
        vcam.Priority = prioritySettings;

        if (addPositionComposer)
        {
            CinemachinePositionComposer composer = Undo.AddComponent<CinemachinePositionComposer>(go);
            composer.CameraDistance = 10f;
            composer.TargetOffset = new Vector3(0f, 0.5f, 0f);
            composer.Damping = new Vector3(0.5f, 0.5f, 0.5f);
        }

        EnsureImpulseListener(vcam);
        return vcam;
    }

    private static void WireInGameManager(BattleCameraController controller)
    {
        InGameManager manager = Object.FindFirstObjectByType<InGameManager>();
        if (manager == null)
        {
            Debug.LogWarning("[BattleCameraSceneSetup] InGameManager が見つかりません。手動で _battleCamera を割り当ててください。");
            return;
        }

        SerializedObject serializedManager = new SerializedObject(manager);
        SerializedProperty battleCameraProp = serializedManager.FindProperty("_battleCamera");
        if (battleCameraProp == null)
        {
            Debug.LogWarning("[BattleCameraSceneSetup] _battleCamera フィールドが見つかりません。");
            return;
        }

        battleCameraProp.objectReferenceValue = controller;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }
}
#endif
