#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 敵攻撃の闇の球 Trail 用アンカーとプレハブ参照を設定する。
/// </summary>
public static class EnemyAttackTrailSetup
{
    private const string MenuPath = "InGame/Setup Combat VFX Anchors";

    private const string EnemyPrefabPath = "Assets/InGame/Prefabs/Enemy.prefab";

    private const string EnemyAnimatorControllerPath = "Assets/InGame/Animations/Enemy/Enemy.controller";

    private const string EliteAnimatorControllerPath = "Assets/InGame/Animations/Enemy/Elite/Elite.controller";

    private static readonly (string prefabPath, string animatorControllerPath)[] EnemyPrefabConfigs =
    {
        (EnemyPrefabPath, EnemyAnimatorControllerPath),
        ("Assets/InGame/Prefabs/WhiteEnemy.prefab", EnemyAnimatorControllerPath),
        ("Assets/InGame/Prefabs/GreenEnemy.prefab", EnemyAnimatorControllerPath),
        ("Assets/InGame/Prefabs/BlackEnemy.prefab", EnemyAnimatorControllerPath),
        ("Assets/InGame/Prefabs/EliteEnemy.prefab", EliteAnimatorControllerPath),
    };

    private const string TrailPrefabPath = "Assets/InGame/Prefabs/VFX/VFX_Trail_Dark.prefab";

    private const string EnemyEmitChildName = "AttackTrailEmit";

    private const string EnemyCombatHitChildName = "CombatHitPoint";

    private const string PlayerEmitChildName = "AttackTrailEmit";

    private const string PlayerCombatHitChildName = "CombatHitPoint";

    [MenuItem(MenuPath)]
    public static void SetupEnemyAttackTrail()
    {
        GameObject trailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrailPrefabPath);
        if (trailPrefab == null)
        {
            Debug.LogError($"[EnemyAttackTrailSetup] Trail プレハブが見つかりません: {TrailPrefabPath}");
            return;
        }

        for (int i = 0; i < EnemyPrefabConfigs.Length; i++)
        {
            (string prefabPath, string controllerPath) = EnemyPrefabConfigs[i];
            RuntimeAnimatorController enemyController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (enemyController == null)
            {
                Debug.LogError($"[EnemyAttackTrailSetup] Animator Controller が見つかりません: {controllerPath}");
                continue;
            }

            SetupEnemyPrefab(prefabPath, trailPrefab, enemyController);
        }

        SetupPlayerViewInActiveScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyAttackTrailSetup] 敵 Animator / AttackTrailEmit・CombatHitPoint / プレイヤー CombatHitPoint の設定が完了しました。");
    }

    private static void SetupEnemyPrefab(
        string prefabPath,
        GameObject trailPrefab,
        RuntimeAnimatorController enemyController)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[EnemyAttackTrailSetup] 敵プレハブを読み込めません（スキップ）: {prefabPath}");
            return;
        }

        try
        {
            EnemyView enemyView = root.GetComponent<EnemyView>();
            if (enemyView == null)
            {
                Debug.LogError($"[EnemyAttackTrailSetup] EnemyView がありません: {prefabPath}");
                return;
            }

            Animator animator = FindOrAssignEnemyCharacterAnimator(root, enemyController);

            Transform emit = FindOrCreateChild(root.transform, EnemyEmitChildName, new Vector3(-0.6f, 1f, 0f));
            Transform combatHit = FindOrCreateChild(root.transform, EnemyCombatHitChildName, new Vector3(0f, 0.5f, 0f));
            SerializedObject serializedEnemy = new SerializedObject(enemyView);
            serializedEnemy.FindProperty("_animator").objectReferenceValue = animator;
            serializedEnemy.FindProperty("_attackTrailEmitPoint").objectReferenceValue = emit;
            serializedEnemy.FindProperty("_enemyCombatHitPoint").objectReferenceValue = combatHit;
            serializedEnemy.FindProperty("_attackTrailPrefab").objectReferenceValue = trailPrefab;
            if (serializedEnemy.FindProperty("_attackTrailSpeed").floatValue < 0.1f)
            {
                serializedEnemy.FindProperty("_attackTrailSpeed").floatValue = 6f;
            }

            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupPlayerViewInActiveScene()
    {
        PlayerView playerView = Object.FindFirstObjectByType<PlayerView>(FindObjectsInactive.Include);
        if (playerView == null)
        {
            Debug.LogWarning("[EnemyAttackTrailSetup] アクティブシーンに PlayerView がありません。シーンを開いて再実行してください。");
            return;
        }

        RemoveMisplacedHitVfxFromPlayerRoot(playerView);
        Transform playerRoot = playerView.transform.parent != null ? playerView.transform.parent : playerView.transform;
        Transform hit = FindOrCreateChild(playerRoot, PlayerCombatHitChildName, Vector3.zero);
        Transform emit = FindOrCreateChild(playerView.transform, PlayerEmitChildName, new Vector3(0.6f, 0.5f, 0f));
        SerializedObject serializedPlayer = new SerializedObject(playerView);
        serializedPlayer.FindProperty("_combatHitPoint").objectReferenceValue = hit;
        serializedPlayer.FindProperty("_attackTrailEmitPoint").objectReferenceValue = emit;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void RemoveMisplacedHitVfxFromPlayerRoot(PlayerView playerView)
    {
        Transform playerRoot = playerView.transform.parent;
        if (playerRoot == null)
        {
            return;
        }

        for (int i = playerRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = playerRoot.GetChild(i);
            if (child == playerView.transform)
            {
                continue;
            }

            if (child.GetComponentInChildren<ParticleSystem>(true) != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static Animator FindOrAssignEnemyCharacterAnimator(GameObject root, RuntimeAnimatorController enemyController)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        Animator matchedController = null;
        Animator combatAnimator = null;
        Animator fallback = null;
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null)
            {
                continue;
            }

            if (EnemyAnimatorTriggers.HasAttackTrigger(candidate))
            {
                combatAnimator ??= candidate;
            }

            RuntimeAnimatorController controller = candidate.runtimeAnimatorController;
            if (controller == enemyController)
            {
                matchedController = candidate;
            }
            else if (controller != null && controller.name == enemyController.name)
            {
                matchedController ??= candidate;
            }

            fallback ??= candidate;
        }

        if (matchedController != null)
        {
            return matchedController;
        }

        if (combatAnimator != null)
        {
            combatAnimator.runtimeAnimatorController = enemyController;
            return combatAnimator;
        }

        if (fallback != null)
        {
            fallback.runtimeAnimatorController = enemyController;
            return fallback;
        }

        Animator rootAnimator = root.GetComponent<Animator>();
        if (rootAnimator == null)
        {
            rootAnimator = root.AddComponent<Animator>();
        }

        rootAnimator.runtimeAnimatorController = enemyController;
        return rootAnimator;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            existing.localPosition = localPosition;
            return existing;
        }

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }
}
#endif
