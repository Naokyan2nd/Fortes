# インゲーム実装プラン (ImplementationPlan)

## 進行方針

各Phaseは以下の基準で「連続実行」と「一時停止」を分けている。
- **連続実行:** Unityエディタ作業が不要なPhase（コード定義のみ）
- **一時停止:** Phaseの完了後に人間のUnityエディタ作業が必要なPhase

---

## 🟢 BLOCK A｜連続実行（Phase 1〜3）
> Unityエディタ作業不要。Agentは以下を止まらず順番に実装すること。

### Phase 1 ― データ層
依存関係がなく最初に定義する必要があるクラス群。

| ファイル | 配置場所 | 備考 |
|----------|----------|------|
| `QtePointData.cs` | `InGame/Scripts/` | QteTimings用のデータクラス（Pure C#） |
| `BattleSettingsSO.cs` | `InGame/Data/` | プレイヤー基礎値・QTE倍率・AP回復ボーナス |
| `EnemyDataSO.cs` | `InGame/Data/Enemies/` | 敵パラメータ・Sprite |
| `SkillDataSO.cs` | `InGame/Data/Skills/` | QtePointData[]を含む |
| `WaveConfigSO.cs` | `InGame/Data/Waves/` | EnemyDataSO[]を含む |
| `StageConfigSO.cs` | `InGame/Data/Stages/` | WaveConfigSO[]を含む |
| `BattleStageSession.cs` | `Shared/Scripts/Battle/` | StageScene→MainScene のステージ受け渡し |

### Phase 2 ― Model層
HP/SP/SpeedなどをReactivePropertyで公開するPure C#クラス。

| ファイル | 配置場所 | 備考 |
|----------|----------|------|
| `PlayerModel.cs` | `InGame/Scripts/` | HP・SpeedをReactivePropertyで公開 |
| `EnemyModel.cs` | `InGame/Scripts/` | HP・Speed・IsSelectedをReactivePropertyで公開 |

### Phase 3 ― InGameManagerステートマシン骨格
Enum + switch + UniTaskでステートを定義する。
各ステートの処理メソッドはスタブ（空実装）でよい。肉付けはPhase 5で行う。

| ファイル | 配置場所 | 備考 |
|----------|----------|------|
| `InGameManager.cs` | `InGame/Scripts/` | MonoBehaviour。ステート定義と遷移フローのみ |

---

### 🛑 BLOCK A 完了後 → 人間のエディタ作業

```text
□ InGame.unityシーンを開き、ワールド空間（Sprite）とUI空間（Canvas）を厳密に分離した以下のHierarchyを構築する：

InGame (Scene Root)
├── Managers
│   └── InGameManager [InGameManager.cs] <-- 各SOをここにアサイン
├── Environment (背景・環境レイヤー)
│   ├── FarLayer [SpriteRenderer]
│   ├── MidLayer [SpriteRenderer]
│   └── NearLayer [SpriteRenderer]
├── Characters (戦闘キャラクター群)
│   ├── Player [PlayerView.cs, BoxCollider2D, SpriteRenderer]
│   └── EnemyPositions [TargetSelectView.cs]
│       ├── Position_0 [EnemyView.cs, BoxCollider2D, SpriteRenderer]
│       ├── Position_1 [EnemyView.cs, BoxCollider2D, SpriteRenderer]
│       └── Position_2 [EnemyView.cs, BoxCollider2D, SpriteRenderer]
└── UI (Canvas)
    ├── HUD [HudView.cs]
    └── CommandPanel [CommandPanelView.cs]

□ InGame/Data/ 以下に以下のSOアセットを作成する：
  - BattleSettingsSO（1個）
  - SkillDataSO（3個：SP回復 / 単体浄化 / HP回復）
  - EnemyDataSO（テスト用に1個以上）
  - WaveConfigSO（テスト用に1個以上）
  - StageConfigSO（テスト用に1個。Waves に WaveConfigSO を並べる）

□ InGameManagerのInspectorに上記SOをアサインする（Fallback Stage に StageConfigSO を指定）。
```

---

## 🟡 BLOCK B｜一時停止あり（Phase 4〜6）
> 各Phase完了ごとにAgentは必ず停止し、人間へのエディタ作業指示を出すこと。
> 作業指示は rules-architecture.md に従い、視認性の高いツリー形式で出力すること。

### Phase 4 ― QTEシステム
ジングル再生・タイミング制御・判定ロジック・収縮リング演出。

| ファイル | 配置場所 | 備考 |
|----------|----------|------|
| `QtePresenter.cs` | `InGame/Scripts/` | ジングル再生・TaikoScroll QTE 委譲 |
| `TaikoScrollQteRunner.cs` | `InGame/Scripts/Qte/` | 太鼓風スクロール QTE・空間ゾーン判定 |

#### 実装上の注意:
- ジングルと同期するタイミング制御は `AudioSource.PlayScheduled` と `AudioSettings.dspTime` を使用し、フレームレートに依存しない高精度タイマーで制御すること。
- QTEボタンはObject Poolingを前提とした設計にすること（`Instantiate`/`Destroy`の直接呼び出し禁止）。
- 複数QTEの倍率は全判定の平均値を使用する。端数切り捨ては最終ダメージ計算時のみ。

#### 🛑 Phase 4 完了後 → 人間のエディタ作業
```text
□ 各SkillDataSOの Qte Variants 各要素に QteCombinedClip / NoteScrollDurationSeconds / JingleTimelineOffsetSeconds / QteTimings を Inspector で設定する（複数曲は Variants に追加）。
```

---

### Phase 5 ― バトルロジック（InGameManagerの肉付け）
Phase 3で作ったスタブに実際のロジックを実装する。

| 実装順 | 対象ステート |
|--------|--------------|
| 5-1    | Initialize / WaveStart |
| 5-2    | RoundStart / ProcessNextAction |
| 5-3    | PlayerTurnStart / PlayerCommandSelect / TargetSelect（※1） |
| 5-4    | QTEPhase & ActionExecute（Phase 4のQtePresenterと接続） |
| 5-5    | EnemyBatchTurn |
| 5-6    | CheckResult（→ ResultViewの呼び出し） |

#### （※1）TargetSelectステートに関する重要指示:
ターゲット選択フェーズにおける「前回選択ターゲットの維持 / 最前列デフォルト選択ロジック」「タップによるマーカー切り替え」「同一対象の再タップまたはスキルボタン再押下による発動確定ロジック」は、`InGameSpec.md` の仕様に従って厳密にステートマシンへ組み込むこと。

#### 🛑 Phase 5 完了後 → 人間のエディタ作業
```text
□ InGameManagerのInspectorで各ステートに必要な参照を確認・アサインする。
□ StageConfigSO の Waves に WaveConfigSO を設定し、各 WaveConfigSO の Enemies に EnemyDataSO を設定する。
```

---

### Phase 6 ― View層
各ViewはMonoBehaviour。状態を持たず表示更新と入力検知のみ行うこと。

| ファイル | 配置場所 | 対応GameObject |
|----------|----------|----------------|
| `HudView.cs` | `InGame/Scripts/UI/` | HUD |
| `CommandPanelView.cs` | `InGame/Scripts/UI/` | CommandPanel |
| `TargetSelectView.cs` | `InGame/Scripts/Characters/` | EnemyPositions |
| `EnemyView.cs` | `InGame/Scripts/Characters/` | EnemyPositions配下の各Positionオブジェクト |
| `PlayerView.cs` | `InGame/Scripts/Characters/` | Player |
| `ResultView.cs` | `InGame/Scripts/UI/` | UI配下に新規作成するResultPanel |

#### 実装上の注意:
- `TargetSelectView` はターゲット選択時に背景3レイヤー（Far/Mid/Near）を異なる移動量でDOTweenアニメーションさせるパララックス演出を含む。
- `EnemyView` は `BoxCollider2D` を備え、`OnPointerDown` / `IPointerClickHandler` 等を用いて自身のタップを検知し、`TargetSelectView` やマネージャー層に入力を通知する仕組みを実装すること。また、モデルの `IsSelected` を購読してターゲットマーカー（Sprite）の表示/非表示を切り替えること。
- `ResultView` は勝利・敗北の両演出を持ち、`InGameManager`の`CheckResult`から呼び出される。
- `TextMeshProUGUI` を使用すること（`UnityEngine.UI.Text` 禁止）。

#### 🛑 Phase 6 完了後 → 人間のエディタ作業
```text
□ 各Viewスクリプトを対応するGameObject（ワールド空間のSpriteオブジェクトおよびCanvas内のUIオブジェクト）にアタッチする。
□ 各ViewのInspectorに必要なUI参照（TextMeshProUGUI）やSpriteRenderer、マーカー用オブジェクト等の参照をアサインする。
□ Environment配下の3レイヤー（Far/Mid/Near）を TargetSelectViewの[SerializeField]にアサインする。
□ ResultView用のResultPanelをUI配下に作成し、ResultView.csをアタッチする。
```