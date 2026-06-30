#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// デフォルト SE Catalog アセットの作成・移行。
/// </summary>
public static class InGameSeCatalogSetup
{
    public const string CatalogPath = "Assets/InGame/Resources/DefaultInGameSeCatalog.asset";

    private const string CreateMenuPath = "InGame/Create Default SE Catalog";
    private const string SyncKeysMenuPath = "InGame/Sync SE Catalog Missing Keys";
    private const string MigrateMenuPath = "InGame/Migrate SE Catalog (Legacy Clips)";

    [MenuItem(SyncKeysMenuPath)]
    public static void SyncMissingKeysOnDefaultCatalog()
    {
        InGameSeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<InGameSeCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning($"[InGameSeCatalogSetup] {CatalogPath} が見つかりません。");
            return;
        }

        catalog.EnsureMissingKeys();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        Debug.Log("[InGameSeCatalogSetup] 不足している SE キーを Catalog に追加しました。");
    }

    [MenuItem(CreateMenuPath)]
    public static void CreateDefaultSeCatalog()
    {
        string directory = Path.GetDirectoryName(CatalogPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            EnsureFolderExists("Assets/InGame/Resources");
        }

        InGameSeCatalogSO existing = AssetDatabase.LoadAssetAtPath<InGameSeCatalogSO>(CatalogPath);
        if (existing != null)
        {
            existing.MigrateLegacyClips();
            existing.SeedDefaultsIfEmpty();
            existing.EnsureMissingKeys();
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"[InGameSeCatalogSetup] 既存の Catalog を選択しました: {CatalogPath}");
            return;
        }

        InGameSeCatalogSO catalog = ScriptableObject.CreateInstance<InGameSeCatalogSO>();
        catalog.SeedDefaultsIfEmpty();
        catalog.EnsureMissingKeys();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
        Debug.Log($"[InGameSeCatalogSetup] Catalog を作成しました: {CatalogPath}");
    }

    [MenuItem(MigrateMenuPath)]
    public static void MigrateSelectedCatalogs()
    {
        Object[] selected = Selection.objects;
        int migrated = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] is not InGameSeCatalogSO catalog)
            {
                continue;
            }

            catalog.MigrateLegacyClips();
            EditorUtility.SetDirty(catalog);
            migrated++;
        }

        if (migrated == 0)
        {
            InGameSeCatalogSO defaultCatalog = AssetDatabase.LoadAssetAtPath<InGameSeCatalogSO>(CatalogPath);
            if (defaultCatalog != null)
            {
                defaultCatalog.MigrateLegacyClips();
                EditorUtility.SetDirty(defaultCatalog);
                migrated = 1;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[InGameSeCatalogSetup] レガシー Clips の移行を完了しました（{migrated} 件）。");
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolderExists(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
