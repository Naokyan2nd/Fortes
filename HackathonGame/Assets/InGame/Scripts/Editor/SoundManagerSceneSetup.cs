#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 開いているシーンに SoundManager をセットアップし、旧 BGM / InGameSePlayer を整理する。
/// </summary>
public static class SoundManagerSceneSetup
{
    private const string MenuPath = "InGame/Setup Sound Manager";

    [MenuItem(MenuPath)]
    public static void SetupSoundManager()
    {
        InGameSeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<InGameSeCatalogSO>(InGameSeCatalogSetup.CatalogPath);
        if (catalog == null)
        {
            Debug.LogError(
                $"[SoundManagerSceneSetup] {InGameSeCatalogSetup.CatalogPath} が見つかりません。先に InGame > Create Default SE Catalog を実行してください。");
            return;
        }

        InGameManager manager = Object.FindFirstObjectByType<InGameManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("[SoundManagerSceneSetup] InGameManager が見つかりません。");
            return;
        }

        Transform parent = manager.transform.parent != null ? manager.transform.parent : manager.transform;

        SoundManager soundManager = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
        if (soundManager == null)
        {
            GameObject soundManagerGo = new GameObject("SoundManager");
            Undo.RegisterCreatedObjectUndo(soundManagerGo, "Create SoundManager");
            soundManagerGo.transform.SetParent(parent, false);
            soundManager = Undo.AddComponent<SoundManager>(soundManagerGo);
        }

        MigrateLegacyBgmSettings(soundManager);

        SerializedObject serializedSound = new SerializedObject(soundManager);
        serializedSound.FindProperty("_catalog").objectReferenceValue = catalog;
        serializedSound.ApplyModifiedPropertiesWithoutUndo();

        CleanupLegacyBgmObject();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SoundManagerSceneSetup] SoundManager のセットアップが完了しました。", soundManager);
    }

    private static void MigrateLegacyBgmSettings(SoundManager soundManager)
    {
        GameObject legacyBgmGo = GameObject.Find("BGM");
        if (legacyBgmGo == null)
        {
            return;
        }

        AudioSource legacySource = legacyBgmGo.GetComponent<AudioSource>();
        if (legacySource == null)
        {
            return;
        }

        SerializedObject legacySerialized = new SerializedObject(legacySource);
        SerializedObject serializedSound = new SerializedObject(soundManager);

        AudioClip clip = legacySource.clip;
        if (clip == null)
        {
            clip = legacySerialized.FindProperty("m_Resource").objectReferenceValue as AudioClip;
        }

        if (clip != null)
        {
            serializedSound.FindProperty("_initialBgmClip").objectReferenceValue = clip;
        }

        Object resource = legacySerialized.FindProperty("m_Resource").objectReferenceValue;
        if (resource is AudioResource audioResource)
        {
            serializedSound.FindProperty("_initialBgmResource").objectReferenceValue = audioResource;
        }

        serializedSound.FindProperty("_bgmVolume").floatValue = legacySource.volume;
        serializedSound.FindProperty("_playBgmOnAwake").boolValue = legacySource.playOnAwake;
        serializedSound.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CleanupLegacyBgmObject()
    {
        GameObject legacyBgmGo = GameObject.Find("BGM");
        if (legacyBgmGo == null)
        {
            return;
        }

        AudioSource legacySource = legacyBgmGo.GetComponent<AudioSource>();
        if (legacySource != null)
        {
            Undo.DestroyObjectImmediate(legacySource);
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(legacyBgmGo);
    }
}
#endif
