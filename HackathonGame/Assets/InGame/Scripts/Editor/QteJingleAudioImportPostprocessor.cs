#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// QTE ジングル用 AudioClip の Import 設定（Preload Audio Data）を自動適用する。
/// </summary>
public sealed class QteJingleAudioImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/InGame/Audio/"))
        {
            return;
        }

        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (fileName.IndexOf("jingle", System.StringComparison.OrdinalIgnoreCase) < 0
            && fileName.IndexOf("ジングル", System.StringComparison.Ordinal) < 0
            && fileName.IndexOf("和太鼓", System.StringComparison.Ordinal) < 0
            && fileName.IndexOf("combined", System.StringComparison.OrdinalIgnoreCase) < 0
            && fileName.IndexOf("qte_mix", System.StringComparison.OrdinalIgnoreCase) < 0
            && fileName.IndexOf("合成", System.StringComparison.Ordinal) < 0)
        {
            return;
        }

        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.preloadAudioData)
        {
            return;
        }

        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }
}
#endif
