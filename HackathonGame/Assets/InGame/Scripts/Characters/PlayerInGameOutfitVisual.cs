using UnityEngine;

/// <summary>
/// MainScene：Player 直下の Top_*_Bottom_* プレハブを装備に合わせて表示切替のみ行う。
/// InGame_Bone_compressed は使わない（あれば常に非表示）。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerInGameOutfitVisual : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Top_*_Bottom_* が並ぶ親（通常は Player）。")]
    Transform _variantsRoot;

    void Awake()
    {
        ApplyFromLoadout();
    }

    public void ApplyFromLoadout()
    {
        Transform root = ResolveVariantsRoot();
        if (root == null)
        {
            return;
        }

        OutfitItemVisualHelper.ApplyInGameCharacterVariant(root);

        Transform activeVariant = OutfitItemVisualHelper.FindInGameCharacterVariant(root, includeInactive: false);
        if (activeVariant == null)
        {
            Debug.LogWarning(
                "[PlayerInGameOutfitVisual] Player 直下に Top_*_Bottom_* がありません。"
                + " メニュー Hackathon/Setup MainScene Player Outfit Variants を実行してください。",
                this);
            return;
        }

        PlayerView playerView = EnsurePlayerViewOnHost();
        playerView.BindVisualRoot(activeVariant.gameObject);
    }

    /// <summary>
    /// Player ルートに <see cref="PlayerView"/> を置く（装備プレハブ内の PlayerView は非アクティブ化で止まるため使わない）。
    /// </summary>
    PlayerView EnsurePlayerViewOnHost()
    {
        PlayerView onHost = GetComponent<PlayerView>();
        if (onHost != null)
        {
            return onHost;
        }

        PlayerView nested = GetComponentInChildren<PlayerView>(true);
        if (nested != null && nested.transform != transform)
        {
            Debug.LogWarning(
                "[PlayerInGameOutfitVisual] 装備プレハブ内の PlayerView は使いません。"
                + " Hackathon/Setup MainScene Player Outfit Variants で Player へ移してください。",
                nested);
        }

        onHost = gameObject.AddComponent<PlayerView>();
        if (GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.0001f, 0.0001f);
        }

        return onHost;
    }

    Transform ResolveVariantsRoot()
    {
        return _variantsRoot != null ? _variantsRoot : transform;
    }
}
