# プロジェクトディレクトリ構成 (Final Version)

2名体制（インゲーム担当・アウトゲーム担当）での完全な分業と、DIなし環境での視認性を最大化するため、各ドメインに必要な資産を機能・種類ごとにフォルダで分離する「パッケージ型構成」を採用する。

## 1. 構成ツリー
Assets/
├── Shared/                     # 【共有領域】InGame/
│   ├── Scripts/                # 汎用Utility、定数など（コード系）
│   ├── Prefabs/                # 共通ボタン、ダイアログなど（UI系）
│   ├── Audio/                  # 共通のシステム音など
│   └── Sprites/                # 共通のUI素材など
│
├── InGame/                     # ★インゲーム担当（あなた）の完全隔離領域
│   ├── Scripts/                # インゲーム専用ロジック (例: PlayerView.cs)
│   ├── Prefabs/                # インゲーム専用プレハブ (例: Player.prefab)
│   ├── Audio/                  # インゲーム専用SE/BGM (例: Attack.wav)。SE の音量・ピッチは Resources/DefaultInGameSeCatalog の各エントリで設定
│   ├── Sprites/                # インゲーム専用画像 (例: HpBar.png)
│   ├── Animations/             # インゲーム専用アニメーション
│   └── Data/                   # 敵データ等の ScriptableObject
│
├── OutGame/                    # 〇アウトゲーム担当（相方）の完全隔離領域
│   ├── Scripts/                # アウトゲーム専用ロジック
│   ├── Prefabs/                # アウトゲーム専用プレハブ
│   ├── Audio/                  # アウトゲーム専用SE/BGM
│   ├── Sprites/                # アウトゲーム専用画像
│   ├── Animations/             # アウトゲーム専用アニメーション
│   └── Data/                   # ガチャ・ショップ等の ScriptableObject
│
├── Scenes/                     # Unityシーンファイル
│   ├── InGame.unity            
│   └── OutGame.unity           
└── Plugins/                    # 外部ライブラリ（DOTween）
※R3, UniTaskはGithub経由でPackagesフォルダにインポート済み。


## 2. 設計・運用ルール
- **「俺の島」の不可侵条約**:
    ファイルの配置場所（InGame/OutGame等）が既に明確に分類されているため、
    ファイル名に `IG_` や `SE_` などの冗長な接頭辞は使用せず、自然で簡潔な名前をつけること。
- **共有化のタイミング**:
    最初から何でも `Shared/` に入れようとせず、「OutGameでも使いたい」という機能・アセットが出たタイミングで担当者同士で合意し、`Shared/` へ移動させる。
- **DIなしの依存解決**:
    依存性の注入は `[SerializeField]` を用いたインスペクター上での手動紐付け、またはコード内での明示的な初期化によって行う。

## 3. UnityHierarchy構造（シーン: `Assets/Scenes/InGame.unity`、ルート名 `InGame`）
InGame (Scene Root)
├── Managers
│   ├── InGameManager [InGameManager.cs] <-- 各SO / View / BGM をここにアサイン
│   └── QtePresenter [QtePresenter.cs] ← ジングル用 AudioSource・QTEボタンプレハブをアサイン
├── Environment（背景レイヤー）
│   ├── ParallaxRoot [BattleParallaxBackgroundView.cs] ← Far/Mid/Near Transform、係数・Tween時間はここで調整
│   ├── FarLayer [SpriteRenderer]
│   ├── MidLayer [SpriteRenderer]
│   └── NearLayer [SpriteRenderer]
├── Characters（ワールド／SpriteRenderer）
│   ├── Player [PlayerView.cs, BoxCollider2D, SpriteRenderer]
│   └── EnemyPositions [Transform] ← 水平スクロールの親。TargetSelectView._enemyGroup およびスポーン点の親
│       ├── Spawn_0 [Transform のみ — 敵プレハブの親付け先]
│       ├── Spawn_1
│       └── Spawn_2
└── UI (Canvas: Screen Space、GraphicRaycaster。敵タップ用に Main Camera に Physics2DRaycaster）
    ├── HUD [HudView.cs] — `HpReactiveSliderBinder`（Player HP）+ TMP（SP）
    ├── CommandPanel [CommandPanelView.cs]（スキル Button ×3、スキル名・SP の TMP ×各2系統推奨）
    ├── ResultPanel [ResultView.cs]
    └── QteButtonAnchor [RectTransform] ← QtePresenter._buttonParent。QTEボタンプレハブの親

**敵プレハブ:** `EnemyView`（本体）＋子に World Space `Canvas` と `Slider`（表示のみ。Graphic の Raycast Target はオフ推奨で本体の `BoxCollider2D` にタップが届くようにする）。`Slider` 同オブジェクトに `HpReactiveSliderBinder` を付け、`EnemyView` の `_hpSliderBinder` へ割り当て。

**メモ:** 仕様書の「Background」とコード上の `Environment` は同じ役割。`BattleParallaxBackgroundView` に3レイヤーを渡し、`TargetSelectView` には `EnemyPositions` の親 Transform を渡すこと。

**TargetSelectView** は `_enemyGroup`（`EnemyPositions` の Transform）と任意で `_parallaxBackground`（上記 `BattleParallaxBackgroundView`）をアサインする。キャラ列のスクロール量・Tweenは `TargetSelectView`、背景のパララックス係数・速度は `BattleParallaxBackgroundView` で制御する。
