#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainScene に戦闘ヒットストップ用 Presenter を追加する。
/// </summary>
public static class CombatHitStopSceneSetup
{
    private const string MenuPath = "InGame/Setup Combat Hit Stop";

    [MenuItem(MenuPath)]
    public static void SetupCombatHitStop()
    {
        CombatHitStopPresenter presenter = Object.FindFirstObjectByType<CombatHitStopPresenter>(FindObjectsInactive.Include);
        if (presenter == null)
        {
            InGameManager managerForParent = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
            GameObject host = managerForParent != null ? managerForParent.gameObject : new GameObject("CombatHitStop");
            if (managerForParent == null)
            {
                Undo.RegisterCreatedObjectUndo(host, "Create CombatHitStop");
            }

            presenter = Undo.AddComponent<CombatHitStopPresenter>(host);
        }

        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty hitStopProp = serializedManager.FindProperty("_hitStop");
            if (hitStopProp != null)
            {
                hitStopProp.objectReferenceValue = presenter;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[CombatHitStopSceneSetup] InGameManager._hitStop が見つかりません。");
            }
        }
        else
        {
            Debug.LogWarning("[CombatHitStopSceneSetup] InGameManager が見つかりません。");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[CombatHitStopSceneSetup] ヒットストップのセットアップが完了しました。", presenter);
    }
}
#endif
