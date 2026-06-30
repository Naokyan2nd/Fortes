using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UISkew : BaseMeshEffect
{
    [Header("傾きの強さ")]
    [Tooltip("正の値で右傾き、負の値で左傾きになります")]
    public float skewX = 0.5f;

    // UIのメッシュ（頂点）が生成されるときに呼ばれる関数
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        UIVertex vertex = new UIVertex();
        
        // 全ての頂点をループ処理
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            // 頂点情報を取得
            vh.PopulateUIVertex(ref vertex, i);
            
            // Y座標の高さに応じて、X座標をスライドさせる（これが平行四辺形の歪みになります）
            vertex.position.x += vertex.position.y * skewX;
            
            // 変更した頂点情報を戻す
            vh.SetUIVertex(vertex, i);
        }
    }
}