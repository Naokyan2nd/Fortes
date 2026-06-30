#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// MainScene の Player に 4 装備プレハブを配置し、InGame_Bone_compressed を削除する。
/// プレハブはアセット既定の Transform / Sorting / Bone をそのまま使う（bone からはコピーしない）。
/// </summary>
public static class PlayerInGameOutfitSceneSetup
{
    const string SpritesPrefabDirectory = "Assets/InGame/Sprites/";
    const string LegacyBoneName = "InGame_Bone_compressed";

    static readonly string[] VariantPrefabNames =
    {
        "Top_Starter_Bottom_Starter",
        "Top_Starter_Bottom_Better",
        "Top_Better_Bottom_Starter",
        "Top_Better_Bottom_Better",
    };

    [MenuItem("Hackathon/Setup MainScene Player Outfit Variants")]
    public static void SetupMainScenePlayerOutfitVariants()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[PlayerInGameOutfitSceneSetup] シーンに Player が見つかりません。");
            return;
        }

        Transform playerTransform = player.transform;
        EnsurePlayerViewOnPlayer(player);
        RemoveLegacyBone(playerTransform);
        RemoveExistingVariantInstances(playerTransform);

        string defaultVariant = OutfitItemVisualHelper.GetInGameCharacterVariantNameFromLoadout();

        for (int i = 0; i < VariantPrefabNames.Length; i++)
        {
            string prefabName = VariantPrefabNames[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SpritesPrefabDirectory}{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[PlayerInGameOutfitSceneSetup] プレハブが見つかりません: {SpritesPrefabDirectory}{prefabName}.prefab");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, playerTransform);
            instance.name = prefabName;
            instance.SetActive(prefabName == defaultVariant);
        }

        PlayerInGameOutfitVisual outfitVisual = player.GetComponent<PlayerInGameOutfitVisual>();
        if (outfitVisual == null)
        {
            outfitVisual = player.AddComponent<PlayerInGameOutfitVisual>();
        }

        SerializedObject serializedOutfitVisual = new SerializedObject(outfitVisual);
        serializedOutfitVisual.FindProperty("_variantsRoot").objectReferenceValue = playerTransform;
        serializedOutfitVisual.ApplyModifiedPropertiesWithoutUndo();

        Transform activeVariant = OutfitItemVisualHelper.FindInGameCharacterVariantByName(
            playerTransform,
            defaultVariant,
            includeInactive: true);
        PlayerView playerView = player.GetComponent<PlayerView>();
        if (activeVariant != null && playerView != null)
        {
            playerView.BindVisualRoot(activeVariant.gameObject);
        }

        InGameManager inGameManager = Object.FindAnyObjectByType<InGameManager>();
        if (inGameManager != null && playerView != null)
        {
            SerializedObject serializedManager = new SerializedObject(inGameManager);
            serializedManager.FindProperty("_playerView").objectReferenceValue = playerView;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log(
            "[PlayerInGameOutfitSceneSetup] InGame_Bone_compressed を削除し、4 装備プレハブを Player 直下に配置しました（プレハブ既定の見た目のまま）。",
            player);
    }

    static void RemoveLegacyBone(Transform playerTransform)
    {
        for (int i = playerTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = playerTransform.GetChild(i);
            if (child.name == LegacyBoneName)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static void EnsurePlayerViewOnPlayer(GameObject player)
    {
        PlayerView playerView = player.GetComponent<PlayerView>();
        if (playerView == null)
        {
            playerView = player.GetComponentInChildren<PlayerView>(true);
        }

        if (playerView != null && playerView.transform != player.transform)
        {
            if (player.GetComponent<PlayerView>() == null)
            {
                if (ComponentUtility.CopyComponent(playerView))
                {
                    ComponentUtility.PasteComponentAsNew(player);
                }
            }

            BoxCollider2D oldBox = playerView.GetComponent<BoxCollider2D>();
            if (oldBox != null && player.GetComponent<BoxCollider2D>() == null)
            {
                if (ComponentUtility.CopyComponent(oldBox))
                {
                    ComponentUtility.PasteComponentAsNew(player);
                }
            }

            Object.DestroyImmediate(playerView);
            if (oldBox != null)
            {
                Object.DestroyImmediate(oldBox);
            }

            playerView = player.GetComponent<PlayerView>();
        }

        if (playerView == null)
        {
            playerView = player.AddComponent<PlayerView>();
        }

        if (player.GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D box = player.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.0001f, 0.0001f);
        }
    }

    static void RemoveExistingVariantInstances(Transform playerTransform)
    {
        for (int i = playerTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = playerTransform.GetChild(i);
            if (OutfitItemVisualHelper.TryParseInGameCharacterVariantKey(child.name, out _))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
#endif
