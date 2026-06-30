#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ウェーブトランジション用 PNG を 1 ファイル 1 スプライト（全体バウンディング）に再インポートする。
/// </summary>
public static class WaveTransitionSpriteImportSetup
{
    private static readonly string[] TransitionSpritePaths =
    {
        "Assets/InGame/Sprites/UI/INGAME_text_nextwave.png",
        "Assets/InGame/Sprites/UI/INGAME_text_gamestart.png",
        "Assets/InGame/Sprites/UI/INGAME_text_nextwave1.png",
        "Assets/InGame/Sprites/UI/INGAME_text_nextwave2.png",
        "Assets/InGame/Sprites/UI/INGAME_text_nextwave3.png",
    };

    public static void ReimportAllTransitionSprites()
    {
        for (int i = 0; i < TransitionSpritePaths.Length; i++)
        {
            ReimportAsUnionSprite(TransitionSpritePaths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ReimportAsUnionSprite(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
        {
            Debug.LogWarning($"[WaveTransitionSpriteImportSetup] スプライトが見つかりません: {assetPath}");
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();

        Rect unionRect = ComputeUnionRect(assetPath);
        if (unionRect.width <= 0f || unionRect.height <= 0f)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                unionRect = new Rect(0f, 0f, texture.width, texture.height);
            }
        }

        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty spritesProp = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");
        spritesProp.arraySize = 1;

        SerializedProperty spriteElement = spritesProp.GetArrayElementAtIndex(0);
        spriteElement.FindPropertyRelative("m_Name").stringValue =
            Path.GetFileNameWithoutExtension(assetPath);

        SerializedProperty rectProp = spriteElement.FindPropertyRelative("m_Rect");
        rectProp.FindPropertyRelative("x").floatValue = unionRect.x;
        rectProp.FindPropertyRelative("y").floatValue = unionRect.y;
        rectProp.FindPropertyRelative("width").floatValue = unionRect.width;
        rectProp.FindPropertyRelative("height").floatValue = unionRect.height;

        spriteElement.FindPropertyRelative("m_Alignment").intValue = (int)SpriteAlignment.Center;
        spriteElement.FindPropertyRelative("m_Pivot").vector2Value = new Vector2(0.5f, 0.5f);

        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();

        Debug.Log(
            $"[WaveTransitionSpriteImportSetup] {assetPath} を Single スプライト ({unionRect.width}x{unionRect.height}) に再インポートしました。");
    }

    private static Rect ComputeUnionRect(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Rect union = default;
        bool hasRect = false;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not Sprite sprite)
            {
                continue;
            }

            if (!hasRect)
            {
                union = sprite.rect;
                hasRect = true;
                continue;
            }

            union = Union(union, sprite.rect);
        }

        return union;
    }

    private static Rect Union(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
#endif
