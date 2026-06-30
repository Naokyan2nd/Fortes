---
description: インゲーム（浄化バトル）のルール、計算式、ステート遷移の厳密な仕様
---
# インゲームシステム仕様書 (InGameSpec)

## 1. コンセプト
[cite_start]ターン制コマンドバトルに、リズムゲーム的なQTEを融合させた2Dシステム 。
攻撃・回復の2コマンドと、ウェーブ制（1〜3ウェーブ）の戦闘を展開する 。
プレイヤー、敵、背景などすべてのオブジェクトは2D（ワールド空間のSprite）で構成し、UI要素のみCanvas（UI空間）で管理する。

## 2. リソースとデータ構造（Model層）
[cite_start]Model層のパラメータは `R3.ReactiveProperty` 等で公開すること 。

### スキルスロット（MVP仕様）
**現在（開発フェーズ）:**
スキルスロットは固定2枠（**0=攻撃 / 1=回復**）。`InGameManager` の `[SerializeField]` に `SkillDataSO`（Attack / Heal）を直接アサインする。

**装備（アウトゲーム）:**
装備によるスキル差し替えは行わない。装備はプレイヤーのステータス（HP等）と見た目のみ変更する想定。

### PlayerModel
- **HP:** 現在のHP値。0で敗北。
- **MaxHP:** 最大HP値。
- **Speed:** 行動順を決定する固定値。

※HPはウェーブ間で引き継ぐ。

### EnemyModel（1ウェーブにつき1〜3体）
- [cite_start]**HP:** 個体のHP。0で撃破 。
- **MaxHP:** 最大HP値。
- [cite_start]**Speed:** 行動順を決定する固定値 。
- **IsSelected:** 現在ターゲットとして選択されているか（R3で公開し、Viewのマーカー表示と連動）。

[cite_start]ウェーブ数および敵数は可変（動的拡張可能）とするため、`ScriptableObject` で管理する 。
[cite_start]※敵に「部位」の概念は存在しない。単体攻撃は敵1体を選択し、全体攻撃は全個体に当たる 。

### ScriptableObject一覧

| SO名 | 保存場所 | 主な用途 |
|------|----------|----------|
| `EnemyDataSO` | `InGame/Data/Enemies/` | HP・Speed・攻撃力・ViewPrefab（EnemyView）・Sprite（任意上書き） |
| `SkillDataSO` | `InGame/Data/Skills/` | 威力・回復力・QTE設定 |
| `WaveConfigSO` | `InGame/Data/Waves/` | 1ウェーブ分の出現敵の配列 |
| `StageConfigSO` | `InGame/Data/Stages/` | 1ステージ分の `WaveConfigSO[]` |
| `BattleSettingsSO` | `InGame/Data/` | プレイヤー基礎値・QTE倍率・AP回復ボーナス |

#### SkillDataSOのフィールド定義
- `SkillId`: string
- `Category`: `Attack` / `Heal`
- `Power`: int（攻撃威力。初期値 5 想定）
- `HealPower`: int（回復威力。初期値 5 想定）
- `QteVariants`: `SkillQteJingleVariant[]`（QTE 開始時にランダム抽選。2件以上ある場合は直前と同じ曲を除外）
- **`AttackTrailSettings`:** 攻撃のみ。Trail **1 本・直進** ＋ Hit Vfx。

#### SkillQteJingleVariant（1曲分）
- `QteCombinedClip`: AudioClip（BGM+ジングル合成済み。QTE 再生に使用）
- `NoteScrollDurationSeconds`: float（その曲の全音符共通スクロール秒数）
- `JingleTimelineOffsetSeconds`: float（その clip 用のタイムライン補正）
- `QteTimings`: QtePointData[]（各要素は `TimingInSeconds` のみ。攻撃・回復とも **1〜5ノート**）

#### EnemyDataSOのフィールド定義
- [cite_start]`EnemyId`: string 
- [cite_start]`MaxHp`: int 
- [cite_start]`Speed`: int 
- [cite_start]`AttackPower`: int 
- [cite_start]`Sprite`: Sprite（任意。設定時は生成後にプレハブのスプライトを上書き）
- `ViewPrefab`: EnemyView（スポーン用プレハブ。必須）

#### WaveConfigSOのフィールド定義
- [cite_start]`WaveIndex`: int 
- [cite_start]`Enemies`: EnemyDataSO[] 

#### StageConfigSOのフィールド定義
- `StageId`: string（将来 OutGame 連携用）
- `DisplayName`: string（任意）
- `Waves`: WaveConfigSO[]

#### ステージのシーン間受け渡し（`BattleStageSession`）
- 配置: `Shared/Scripts/Battle/BattleStageSession.cs`
- `SetPending(StageConfigSO)` → MainScene 遷移前にセット（将来 StageScene から呼ぶ）
- `TryConsume` → `InGameManager.Awake` で 1 回消費。未設定時は `InGameManager` の `_fallbackStage` を使用

#### BattleSettingsSOのフィールド定義
- `PlayerMaxHp`: int
- `PlayerSpeed`: int
- `QtePerfectMultiplier`: float（既定 1.3）
- `QteGoodMultiplier`: float（既定 1.1）
- `QteMissMultiplier`: float（既定 1。Miss 1 ノートあたりの乗算倍率）
- `AllPerfectHealBonusMultiplier`: float（全Perfect回復の追加倍率。既定 2）

---

## 3. バトルステート（ラウンド制フロー）
[cite_start]`InGameManager` は以下のステートを持ち、`UniTask` を用いてシーケンシャルに制御する 。

1. **Initialize（初期化）:**
   - `BattleSettingsSO` および各 `SkillDataSO`（固定スロット2枠分）をロード。

2. **WaveStart（ウェーブ開始）:**
   - [cite_start]アクティブな `StageConfigSO` の現在ウェーブに対応する `WaveConfigSO` から敵を生成・配置（`EnemyPositions` の各座標オブジェクトに生成） 。
   - **狂暴化リセット:** `SpawnWaveEnemiesForTransition` 先頭で `_currentRoundCount = 0`、`_enrageMultiplier = 1`、`_enrageBuffStackCount = 0`。
   - **1ウェーブ目:** `WaveTransitionPresenter.PlayBattleStartAsync`（BattleStart バナー画像 → グリッチ中スポーン → WAVE 01 中央画像 → 復帰）。Presenter 未設定時は即スポーン。
   - **2ウェーブ目以降（中間ウェーブ全滅後）:** `WaveTransitionPresenter.PlayTransitionAsync`（下記 WaveTransition）。

3. **RoundStart（ラウンド開始）:**
   - **狂暴化カウント:** `_currentRoundCount` をインクリメント。インクリメント後が **3以上の奇数**（3, 5, 7…）かつ `_enrageBuffStackCount < 3` のときのみ `_enrageMultiplier` に `_enrageMultiplierStep`（既定 0.2）を加算し、段階を +1。実質ラウンド 3 / 5 / 7 開始時に各1段（攻撃力倍率のみ反映、UI アイコンなし）。
   - [cite_start]生存しているプレイヤーと全敵の `Speed` を取得し、降順でソートして「行動順キュー」を作成する 。
   - [cite_start]※スピードが同値の場合は、50%のランダム（乱数）で順番を決定する 。
   - **Inspector（InGameManager）:** Header `Enemy Enrage` — `Enrage Multiplier Step`（float、既定 0.2）、`Max Enrage Buff Stacks`（int、既定 3）。

4. **ProcessNextAction（行動者の取り出し）:**
   - キューの先頭からキャラクターを取り出す。キューが空なら `3. [cite_start]RoundStart` へ戻る 。
   - [cite_start]取り出したキャラクターが既に死亡（HP0）している場合はスキップし、次のキャラを取り出す 。
   - [cite_start]生存していれば、プレイヤーなら `5` へ、敵なら `9`（まとめ攻撃）へ遷移 。

5. **PlayerTurnStart（プレイヤーターン開始）:**
   - コマンド選択へ（SP自然回復なし）。

6. **PlayerCommandSelect（コマンド選択）:**
   - コマンド入力を待機（攻撃・回復の2ボタン。SPコストなし）。
   - **攻撃入力方式（`InGameManager._attackCommandInputMode`）:**
     - **TwoStep（既定）:** 下記の2段階入力＋ターゲット選択。
     - **QuickConfirm:** 攻撃ボタン1回で確定。ターゲットは常に配列インデックス最小の生存敵（最左）。敵寄りカメラ＋`FocusEnemyAsync` 完了後に QTE 表示。敵タップ・2回目タップは不要。回復は TwoStep と同じ。
   - **2段階入力（選択 → 確定）※TwoStep 時:**
     - 使用可能なスキルボタンを1回目タップ: その枠を **Selected** にする（スキル表示のまま）。`CommandPanelView` がスケールアップ（例: 1.2倍、`DOTween` 0.15秒）で選択中を表示する。**もう一方の使用可能枠は戻るボタン表示**（戻るアイコン＋戻る用枠色、Normal スケール）に切り替わる。
     - **戻るボタンをタップ:** 選択を解除し、両枠をスキル表示・未選択に戻す。`UiCommandBack` SE を再生。カメラは全体（`CM_Default`）。敵・プレイヤーのターゲットマーカーがあれば解除。非選択側の NamePanel ラベルは **「Back」** 表示（戻るアイコン・枠色と併用）。
     - スキルの切り替えは **必ず戻るを経由** する（戻る → 未選択 → 別スキルを1回目タップ）。
     - **同じ枠を2回目タップ: 確定。**
       - 回復: ターゲットフェーズをスキップし `8. QTEPhase` へ（`targetSlot = -1`）。
       - 攻撃: `7. TargetSelect` へ。攻撃ボタンは Selected 表示を維持する。
   - ターン開始時は `ClearSelection()` で全枠を Normal に戻し、**スキルは未選択**（初期選択枠なし）。`BattleCameraController` は全体カメラ（`CM_Default`）。
   - 攻撃 **1回目** タップで `CM_Enemy_*` に寄せ、回復 **1回目** で `CM_Player` に寄せる。**攻撃**はターゲット確定後も寄りカメラ＋ステージフォーカスを維持したまま QTE へ。**回復**は QTE 直前に全体カメラへ戻し `ResetStageAsync` する。
   - **ターゲットマーカー（コマンド選択中）:** 攻撃 **1回目** タップ時は生存敵のデフォルトターゲットに敵頭上の TargetMarker を表示（`EnemyView` / `EnemyModel.IsSelected`）。回復 **1回目** タップ時はプレイヤー頭上の TargetMarker（`Player` 直下・`PlayerView`）を表示。攻撃へ切替・未選択・戻る・QTE 直前で敵・プレイヤー双方のマーカーを解除する。
   - QTE 直前にも選択表示をクリアする。

7. **TargetSelect（ターゲット選択フェーズ）:**
   - 攻撃スキルを **2回目タップで確定した後** のみこのフェーズに移行する（回復は入らない）。
   - **初期ターゲットマーカー表示ロジック:**
     - 前回選択した敵（生存している場合のみ）がいればその敵をデフォルトターゲットとする。
     - 前回選択した敵が死亡している、または今回が初回のターゲット選択である場合は、生存している敵のうち「最初に見つかった敵（配列インデックスが最も若い個体）」をデフォルトターゲットとする。
     - ターゲットとなった敵オブジェクトには、選択中であることを示す「ターゲットマーカー（Sprite）」を頭上等に表示する。
   - **ターゲットの切り替え（タップ検知）:**
     - 画面上の生存している敵オブジェクト（`BoxCollider2D` を所持）がタップされたことを `OnPointerDown` / `IPointerClickHandler` 等のイベントシステムで検知する。
     - 別の生存している敵がタップされた場合、即座にターゲットマーカーをその敵へと移動（切り替え）させ、視差スクロールを更新する。
   - **コマンド選択への復帰:**
     - **戻るボタン**をタップした場合: ターゲット選択を完全中止し、ステージを初期位置へ `ResetStageAsync`、全敵の選択マーカーを解除し、`6. PlayerCommandSelect` の **未選択状態** に戻る。`UiCommandBack` SE を再生。攻撃の Selected は維持しない。
   - **行動の確定（発動ロジック）:**
     - 以下のいずれかの条件を満たした瞬間にターゲットを確定し、`8. QTEPhase` へ遷移する。
       1. **現在マーカーが表示されている（選択中の）敵をもう一度タップする。**
       2. **選択中の攻撃スキルボタン（CommandPanel上）をもう一度押す。**
   - [cite_start]**[演出要求]:** ターゲット選択時、背景の複数レイヤーのX/Y座標を対象に合わせてずらす「視差効果（パララックス）」を `DOTween` で実行し、奥行きを表現すること [cite: 1][cite_start]。背景レイヤーは3枚構成（Far/Mid/Near）とし、それぞれ異なる移動量でパララックスを表現する 。

8. **QTEPhase & ActionExecute（QTEと行動実行）:**
   - [cite_start]コマンドに応じたQTE・UIを表示し入力を待機。結果を算出 。
   - **攻撃 QTE 終了時（倍率チャージ直前）:** `QtePresenter` 内で `BattleCameraController.FocusDefaultAsync`（`CM_Default`）と `TargetSelectView.ResetStageAsync` を実行してから倍率チャージ吸収へ進む（QTE 中は敵寄りカメラ＋ステージフォーカスを維持）。HUD 復帰はチャージ完了後。
   - **攻撃の ActionExecute（通常）:** 上記単体 Trail フロー。
   - **攻撃の ActionExecute（全ノート Perfect = AP）:** 生存全敵に同一ダメージを **同時** Trail / Hit（単体設定を流用）。Trail は並行発射し、飛行時間は **最遠ターゲット基準で全 Trail 統一**（同時着弾）。ヒットストップ・攻撃ヒット SE・カメラシェイクは **1回**、敵ごとの Hit Vfx・FloatingText・パンチは **全 Trail 到達後に一斉並列**（`CM_Default` のまま、敵スロットごとのカメラ／ステージ寄せは行わない）。
   - [cite_start]結果に基づきダメージ/回復を適用 [cite: 1][cite_start]。Viewの演出完了を待機し、`10` へ遷移 。

9. **EnemyBatchTurn（敵まとめ攻撃）:**
   - **キュー収集:** `ProcessNextAction` で敵ユニットを dequeue したとき、キュー先頭が **敵かつ生存** である限り連続 dequeue し、1フェーズの `attackingSlots[]` とする（Speed 順は維持。プレイヤーが割り込む並びではバッチが分割される）。
   - **`InGameManager._useEnemyBatchAttack`:** `true`（既定）で上記の連続収集。`false` では先頭1体のみ（旧・1体ずつ＋`CheckResult` 毎回）。
   - **対象:** `attackingSlots` のうち生存かつ `EnemyView` が有効な個体のみ。0体ならスキップして `10` へ。
   - **予備動作（並行）:** 各敵の Y ジャンプ（`EnemyView.PlayAttackFxAsync`）。インデックス `i` ごとに `i × _enemyBatchHopStaggerSeconds`（既定 0.05s）遅延後に開始し、`UniTask.WhenAll` で完了待ち。敵1体でも同一コードパス（遅延 0）。
   - **Trail（並行）:** 生存敵全員（最大3）が **闇の球 Trail**（`VFX_Trail_Dark`）を `Attack Trail Emit Point` からプレイヤー `CombatHitPoint` へ直進（`EnemyView.PlayAttackTrailToPlayerAsync`、DOTween `DOMove`、`duration = distance / Attack Trail Speed`、Min/Max でクランプ）。`UniTask.WhenAll` で全 Trail 着弾待ち。
   - Trail プレハブ・速度は `EnemyView` のインスペクター（`Attack Trail Speed` 等）。出現位置は敵プレハブ子 `AttackTrailEmit`、着弾点はプレイヤー `PlayerView` 子 `CombatHitPoint`（未設定時は子名検索 → bounds 中心）。
   - **ダメージ・被弾演出は全 Trail 到達後に1回:** `totalDamage = Σ Floor(AttackPower × enrageMultiplier)`（敵ごとに切り捨て後合算。`enrageMultiplier` はウェーブ内で永続、ラウンド3/5/7開始時に最大3段まで加算）。防御なし。`totalDamage > 0` のときのみ: ヒットストップ → HP 反映（1回）→ `CombatFloatingText`（DamageToPlayer、**合計のみ**）→ `BattleCameraController.PlayDamageShake` → SE → `PlayerView.PlayDamageHitFxAsync`（パンチ＋赤点滅）→ 演出完了待ち → `10` へ遷移（バッチ全体で `CheckResult` 1回）。
   - **Inspector（InGameManager）:** Header `Enemy Batch Attack` — `Use Enemy Batch Attack`（bool、既定 true）、`Enemy Batch Hop Stagger Seconds`（float、既定 0.05）。

10. **CheckResult（結果判定）:**
    - [cite_start]`PlayerHP <= 0` ならば敗北、`ResultView` を表示（敗北演出） 。
    - [cite_start]ウェーブ内の敵が全滅 ＆ 最終ウェーブならクリア、`ResultView` を表示（クリア演出） 。
    - 勝敗確定後: カメラ・ステージ復帰 → **バトル BGM フェードアウト＋停止**（`SoundManager.FadeOutAndStopActiveBgmAsync`）と **HUD 退場**（`BattleHudSlideView.PlayHideAsync`）を並行 → 短い待ち後 **リザルト SE**（`ResultVictory` / `ResultDefeat`）→ 勝敗カットイン（`VictoryCutInView.PlayIntroAsync`）→ ホールド（`ResultView._autoTransitionHoldSeconds`、既定 0.6s）→ **退場フェードなし**で `ResultScene` へ遷移（遷移直前に BGM 停止のフォールバック）。入場演出は `ResultSceneManager` 側。再戦時は `InitializeBattleAsync` で `EnsureInitialBgmPlaying` によりバトル BGM を復帰。
    - **Inspector（InGameManager）:** Header `Result Presentation` — `Result Bgm Fade Seconds`（既定 0.9）、`Result Se Delay After Fade Start`（既定 0.15）。
    - 中間ウェーブ全滅時: `_waveIndex++` のあと **WaveTransition**（下記）を再生し、次ラウンドへ。最終ウェーブでは遷移なし。
    - 上記以外 → `4. ProcessNextAction` へ戻る。

#### WaveTransition（ウェーブ間・中間ウェーブのみ）

暗転なし。`WaveTransitionPresenter` がタイムラインを制御（セットアップ: **InGame → Setup Wave Transition**）。

| フェーズ | 既定秒数 | 内容 |
|----------|----------|------|
| 1 余韻 | 0.5 | 入力ブロック。`FlushPendingEnemyRemovals` 後、画面そのまま待機 |
| 2 SweepStrip | 可変 | `WaveTransitionVisualsSO` の Sweep Sprite を**左外→中央で待機→右外へ退場**（グリッチより先・直列） |
| 3 グリッチ | Hold 0.25 / Decay 0.2 | `CameraGlitchManager.PlayWaveTransitionGlitchAsync`。Hold の 35% 時点で `SpawnWaveEnemiesForTransition` |
| 4 WaveMark | 0.6 前後 | グリッチ後、画面中央に `waveMarkSprites[waveIndex]`（`_showWaveMarkAfterGlitch` で OFF 可） |
| 5 復帰 | — | `WaveStartPostSpawnAsync`（カメラ・ステージ・`BattleWaveStart` SE）→ 入力解除 |

- **Inspector（Presenter）:** Timeline 系 / **`WaveTransitionVisualsSO`** / `_preferTextCutIn` / Show Wave Mark After Glitch / Glitch Hold / Decay / Spawn At Glitch Hold Normalized
- **Inspector（Visuals SO）:** SweepStrip（Next Wave / Battle Start Sprite + テキスト fallback）/ WaveMark（Sprites[] + テキスト format）
- **Inspector（CameraGlitchManager）:** Wave Strength / Aberration（被弾用とは別）
- **Inspector（CutInView）:** `_sweepStrip` / `_waveMark`（各 `WaveTransitionCutInPanel` 参照）/ SweepMotionSettings / WaveMarkRevealSettings
- **Inspector（Panel）:** Image / TextFallback / CanvasGroup（各パネル root で完結）
- **Sprite 未設定時:** 該当フェーズ（SweepStrip / WaveMark）のみスキップし、以降のフローは継続（警告ログ）
- **画像アセット配置（推奨）:** `Assets/InGame/Art/UI/WaveTransition/`
  - `NextWave_Banner.png` — ウェーブ間 SweepStrip
  - `BattleStart_Banner.png` — 1ウェーブ目 SweepStrip
  - `Wave01_Center.png`, `Wave02_Center.png`, … — WaveMark 表示（`waveMarkSprites` の index 0 = Wave01）
- **セットアップ:** **InGame → Create Wave Transition Visuals** で SO 作成。**InGame → Setup Wave Transition** で SweepStrip / WaveMark Panel 構成プレハブを再生成し Presenter へ SO を割当

#### BattleStart（1ウェーブ目のみ）

MainScene 入場時、1ウェーブ目だけ `WaveTransitionPresenter.PlayBattleStartAsync` を再生する（`WaveTransition` と同系統の短尺版）。`WaveTransitionPresenter` 未設定時は従来どおり即スポーン。

| フェーズ | 既定秒数 | 内容 |
|----------|----------|------|
| 1 余韻 | 0.25 | 入力ブロック（`InitializeBattleAsync` から）。`WavePanelView` 非表示 |
| 2 SweepStrip | 可変 | Visuals SO の Battle Start Sweep Sprite を**左外→中央で待機→右外へ退場** |
| 3 グリッチ | Hold 0.35 / Decay 0.2 | `PlayWaveTransitionGlitchAsync`。Hold の 35% 時点で `SpawnWaveEnemiesForTransition` |
| 4 WaveMark | 0.6 前後 | グリッチ後、画面中央に `waveMarkSprites[0]`（`_battleStartShowWaveMarkAfterGlitch` で OFF 可） |
| 5 復帰 | — | `WaveStartPostSpawnAsync`（`BattleWaveStart` SE・`WavePanelView` 表示）→ 入力解除 |

- **Inspector（Presenter・Battle Start）:** Battle Start After Beat / Cut-in Delay / Glitch Hold / Decay / Spawn At Hold Normalized / Show Wave Mark After Glitch（Visuals SO で Battle Start 素材を管理）
- ウェーブ間遷移との差分: `FlushPendingEnemyRemovals` なし、After Beat・Glitch が短め、バナー Sprite が Battle Start 用
- **常時 HUD:** `WavePanelView`（`WAVE 1/3` テキスト）は今回の画像化対象外

### 3.X ターゲット選択時の画面スクロール・パララックス仕様
ターゲット選択中、選択された敵のインデックス（0〜2）に応じて背景レイヤーを DOTween で水平移動（パララックス）する。`TargetSelectView` の **Focus Mode**（Inspector）で次を切り替え可能:

| Focus Mode | 挙動 |
|------------|------|
| `ScrollEnemyAndParallax`（既定） | 敵列（`EnemyPositions`）を中央へスクロール **＋** 背景パララックス（従来） |
| `ParallaxOnly` | 敵のワールド位置は固定し、**背景パララックスのみ** |

#### 移動量の定義（例）
基準となるターゲット座標のオフセットを `TargetOffsetX` と定義する。
（例：中央に寄せたい量。インデックスが右にいくほど、舞台全体を左[-X]に引っ張る）

- **Characters/EnemyPositions (敵全体の親):** `ScrollEnemyAndParallax` のときのみ、`-1.0f * selectedIndex *` **`TargetSelectView` の Character Scroll Unit** 分、X座標を移動。`ParallaxOnly` では移動しない。
- **Environment の Far/Mid/Near:** `BattleParallaxBackgroundView` がキャラ列と同じ符号の `scrollSigned` に対し、レイヤーごとのパララックス係数（既定: Near 1.2 / Mid 0.5 / Far 0.1）と移動時間・イージングで `DOTween` する。

#### 演出の制約
- 移動アニメーションはすべて `DOTween` を使用する。キャラ列は `TargetSelectView`、背景は `BattleParallaxBackgroundView` のインスペクターで秒数・イージングを調整できる（仕様例: 0.3秒・Ease.OutCubic）。
- ターゲットが切り替わるたびに、現在位置からの差分ではなく「初期位置 + 選択インデックスに応じた絶対目標座標」へのTweenを上書き（リスタート）することで、挙動のブレを防止する。
- 別スキルでコマンド選択に戻る際は `ResetStageAsync` で初期位置（X = 0）に戻す。**単体攻撃**は QTE 前にはリセットしない（寄りのまま QTE）。QTE 後の ActionExecute で一度リセットし、ヒット演出のあと再度リセットする。
---

## 4. スキルとQTE計算
[cite_start]端数処理はすべて切り捨て（`Mathf.FloorToInt`） 。
[cite_start]スキルは `InGameManager` に直接アサインされた `SkillDataSO` から参照する 。

### 4.1 スキル仕様（v2）

| スキル | QTE数 | 効果 |
| :--- | :--- | :--- |
| **攻撃** | 5 | 通常: 選択1体に `Floor(威力 × Π倍率)`。**全ノート Perfect（AP）:** 生存全敵に同ダメージを同時適用。 |
| **回復** | 5 | `Floor(回復力 × Π倍率)`。**AP時** さらに `× AllPerfectHealBonusMultiplier`（既定2）。MaxHP上限。 |

### 4.2 QTE判定倍率（`QteOutcomeCalculator`）

- **Perfect:** 倍率 1.3（`BattleSettingsSO.QtePerfectMultiplier`）
- **Good:** 倍率 1.1（`BattleSettingsSO.QteGoodMultiplier`。打ち消しには使わない）
- **Miss:** ノートごとに `× QteMissMultiplier`（既定 1）。**1 Miss につき 1 Perfect を打ち消す**（左から順にペアリング想定）

**集計ルール（`ComputeProductMultiplier`）:**
- `perfectBudget = max(0, Perfect数 − Miss数)`。Perfect の有効枠はこの値まで。
- 判定リストを **インデックス順** に走査: **Good** は `× QteGoodMultiplier`、**Miss** は `× QteMissMultiplier`、**Perfect** は `perfectLeft > 0` のときだけ `× QtePerfectMultiplier` して `perfectLeft--`。
- 例（`QteMissMultiplier = 1`）: Miss×5 のみ → `×1`、Perfect×1 + Miss×2 → Perfect 枠 0・Miss×2 → `×1`。
- ダメージ・回復は `Floor(威力 × product)` のみ（旧「Miss 1 つで即 0」ルールは廃止）。
- **AP（全ノート Perfect）:** 攻撃は全体化。回復は上式に `AllPerfectHealBonusMultiplier` を追加乗算。Good が 1 つでもあれば AP なし。
- 最低1ダメージ保証はなし（0あり。演出は現状維持、不発演出は後日）。

### 4.3 QTEシステム詳細仕様

#### 演出フロー
1. [cite_start]コマンド＆ターゲット確定後、BGM音量フェードダウンと QTE 準備（ループ BGM の即時スクラッチ戻し）を **並行** 。
2. [cite_start]スクラッチ直後に抽選されたバリアントの `QteCombinedClip` を `_bgmSource` で **1回のみ** 再生開始（スクラッチ SE 後の待機なし。ループ BGM の pitch スクラッチは並行）。
3. [cite_start]`QteTimings` に定義されたタイミングで音符をスクロール表示し、判定ライン中央到達を合わせる 。
4. [cite_start]全ノート判定完了後、ライブ倍率のフィナーレと五線譜退場を開始し、**並行**して QTE clip を終端まで再生。退場後は確定倍率 UI を `QteChargeOverlay` へ退避し、オーバーレイ上で **収縮＋フェード** したのち、プレイヤー上に **CombatFloatingText**（確定倍率 `×n`）と **チャージ VFX**（`MultiplierChargeAnchor` の one-shot）を **同時** 再生し、続けて **スケールパンチ** してから `ActionExecute`（攻撃 Trail / 回復）へ。攻撃時はチャージ直前に `FocusDefault` と `ResetStageAsync`（`QtePresenter`）。clip 終端後にループ BGM をクロスフェード復帰（事後サマリー集計フェーズなし）。

#### QTE（Taiko）の判定ロジック
- [cite_start]入力は画面上のタップに加え **Space キー**（`KeyCode.Space` の `GetKeyDown`）でも可。いずれも `QteTaikoView` 経由で同一の判定処理に入る。
- [cite_start]タップ時、ノート中心と **Zone_Perfect** / **Zone_Good** の重なりで Perfect / Good / Miss を判定する（時間窓ではない） 。
- [cite_start]未タップで Good 帯通過・左画面外は Miss 。
- **Perfect 連続ピッチ:** 連続 Perfect ごとに `QteTap`（タップ音）SE の pitch が `QteTaikoSettingsSO` の `PerfectStreakPitchStep` / `PerfectStreakPitchMax` に従い線形上昇（1 発目は SE Catalog のベース pitch）。Good / Miss で連続カウントはリセット。

#### ジングル再生タイミングの精度担保
- QTE 中は **BGM+ジングル合成済み clip**（`QteCombinedClip`）を `_bgmSource` で `loop=false` として1回再生。
- `LoadAudioData` 後に `PlayScheduled`（`dspTime + lead`）または即時 `Play`。全ノート判定直後に `RestoreLoopBgmAfterQteAsync` で保存済みループ clip へ **2ch クロスフェード** 復帰（QTE 中の音量を維持・`QteLoopRestoreFadeSeconds` 既定 0.25 秒）。ジングル clip が残っていてもループと重ねて無音を避ける。
- **スクラッチ同期:** コマンド確定直後に `PlayBgmScratchOnConfirmAsync` で `QteBgmScratch` SE と `PlayBgmLoopScratchAsync`（pitch・並行）。待機なしで QTE 本体（ジングル・ノーツ）を開始可能。HUD 非表示等と並行。ループ先頭への `time` シークは行わない。
- **Taiko UI:** QTE 確定直後に五線譜レイヤーを表示。`QteTaikoLayerIntroView` で入場（`QteTaikoVisualRoot` Y 0→1 → `BlackPanel` α 0→初期値）、入場完了まで音符スポーン・タップ無効。QTE 終了後はループ BGM 復帰のあと退場（Y 1→0 と黒フェードを同時）し、退場中も BGM は再生継続。
- **ノートのマスタークロック**は `_bgmSource.time`（再生中の QTE 用 clip 位置）。再生開始前は `AudioSettings.dspTime - playbackStartDsp` にフォールバック。
- `QteTimings` は **合成 / ジングル clip の t=0 からの秒数**（判定ライン中央到達）。
- clip 先頭と MIDI tick 0 の差は `JingleTimelineOffsetSeconds`（**抽選されたバリアントごと**）で補正。

#### SkillDataSOのQTE設定フィールド（Inspectorから編集可能）
各スキルの `SkillDataSO` に **Qte Variants** 配列を持たせる。QTE 開始時に有効な要素からランダム抽選（2件以上のとき直前と同じ index は除外）。戦闘開始時に抽選履歴をリセット。

```csharp
SkillDataSO:
  - QteVariants: SkillQteJingleVariant[]
      - QteCombinedClip: AudioClip
      - NoteScrollDurationSeconds: float  // 画面外 → 判定ライン中央までの秒数
      - JingleTimelineOffsetSeconds: float
      - QteTimings: QtePointData[]
          - TimingInSeconds: float
```

### 4.4 Taiko QTE（スクロール）

- **`QtePresenter`** は **TaikoScroll** のみ。戻り値は `IReadOnlyList<QteJudgment>` で、`InGameManager` の倍率・効果適用は共通。
- **`QteTimings[].TimingInSeconds`:** 抽選されたバリアントのジングル clip 先頭（t=0）から **音符が判定ライン中央に到達する秒数**。音符数は **1〜5 個**（配列の要素数）。スクロール進行は `AudioSource.time` + そのバリアントの `JingleTimelineOffsetSeconds` に同期する。
- **`NoteScrollDurationSeconds`:** 抽選されたバリアントごとに 1 つ。全音符の出現は `TimingInSeconds - NoteScrollDurationSeconds`、速度は `(画面外X − JudgmentLineX) / NoteScrollDurationSeconds` で **その曲の全ノート同一**。
- **音符間隔:** 中心到達タイミングが多少重なっても可。タップ時は **先にスポーンした音符** を判定対象とする。各 `TimingInSeconds` は **0.05 秒以上**かつ **`NoteScrollDurationSeconds` 以上**。
- **QteTaikoSettingsSO で設定するもの:** `JudgmentLineX`（通常 0・音符が中央到達する X）、`NoteLaneY`、`PostRollSeconds`、`MissFadeOutSeconds`（Good 帯通過 Miss 時のノートフェード秒）、`AttackNoteSprite` / `HealNoteSprite`（`SkillCategory` に応じた音符見た目）、`AttackHitEffectSprite` / `HealHitEffectSprite`（タップヒットエフェクト見た目）。
- **スクロール:** 五線譜・判定 UI は固定、**音符のみ** が右から左へ移動。
- **Taiko 判定（空間ゾーン）:** `TaikoJudgmentContainer` 配下の **Zone_Perfect** / **Zone_Good**（横長帯・判定ライン中心）と、**ノート中心**の重なりで Perfect / Good / Miss を決める。
- **画面外ノート:** `QteTaikoView` の **Note Parent** Rect 水平範囲外（右端手前で未進入・左端より外）のノートはタップ判定の対象にせず、右側画面外では自動ミスもしない。左端より外に出たノートは Miss とする（即プール返却）。
- **タップ時の対象ノート:** Good 帯内にいる未判定ノートがいれば **先にスポーンした音符**。いなければ **一番左（X 最小）の音符**で判定（早すぎ＝Good 帯の右外 → Miss）。
- **通過ミス:** タップなしでノート中心が Good 帯の左端を過ぎたら Miss 確定。Miss テキストをノート位置に表示し、以降タップ判定対象外。ノートは左スクロールを継続しつつ `MissFadeOutSeconds` でフェードアウト後にプール返却。
- **終了（案A）:** 全ノート判定＋吸収演出キュー完了で `RunAsync` は `judgments` を返す。直後に **ループ BGM 復帰**（`RestoreLoopBgmAfterQteAsync`・QTE ジングルとクロスフェード）を開始し、**フィナーレ拡大と並行**。フィナーレ後に五線譜退場。clip 全文の終端待ちは行わない（`WaitForPlaybackEndAsync` は最終ノート + `PostRollSeconds` 基準のみ・復帰トリガーには未使用）。
- **ライブ倍率 UI（v5・数値＋ノーツ吸収）:** `QteLiveMultiplierView` が QTE 入場完了時に中央 **`×1`**（積算 product = 1）を表示。各ノート判定確定時、`QteOutcomeCalculator.ComputeProductMultiplier` で **積算倍率を即時更新**（Miss は `QteMissMultiplier` を反映。`QteMissMultiplier = 0` のときのみ `×0` 等）。**Perfect** / **Good** 判定時は太鼓ノーツ画像のクローンを **MultiplierAnchor** へ吸収（縮小・フェード）。吸収の始点・終点は **判定確定時（ノーツプール返却前）に `TryComputeAbsorbAnchors` で `QteAbsorbFlightSnapshot` に保存**し、Tween では **スナップショット座標を優先**（未設定時のみ世界座標変換・最後手段座標）。**吸収フライはノーツ間隔に関係なく全 Perfect/Good で並列起動**し、省略しない。**×表示の合流ジャストは note index 順**に直列適用（並列完了順に依存しない）。到着時に積算表示を再適用し **合流ジャスト**（スカッシュ）。`Miss` は吸収なし・アンカーシェイクのみ。Miss により打ち消された Perfect は **倍率には乗らない**が、吸収・Sparkle 演出は通常の Perfect と同様に再生する。全ノート完了後、**フィナーレでラベル拡大**（~0.2s ホールド）と **並行**して AP 演出。**AP スタンプ（`AllPerfect` / `_apBadgeRect`）:** 攻撃・回復いずれも全 Perfect で表示。DOTween で α0→1 のフェードイン（縮小と同時、既定 0.25s）＋開始スケール 3→1（`Ease.OutBack`、既定 0.3s）＋着地パンチ／シェイク。**`ApBadgeLabel`（×2）:** 回復 AP のみ（`AllPerfectHealBonusMultiplier`）。`ResetForNextQte` でスタンプ・ラベルを非表示。戦闘ダメージ／回復の計算は従来どおり `QteOutcomeCalculator`。**ECG:** `EcgWaveformRenderer` 等はリポジトリに残すが、現行 QTE ランタイムでは未使用（再有効化時に `QteLiveMultiplierView` から接続）。セットアップ: **InGame → Setup QTE Live Multiplier**。
- **レイヤー構成:** `QteTaikoScrollLayer` 直下に `BlackPanel`（`CanvasGroup`・タップ用 Raycast）と `QteTaikoVisualRoot`（`Gosenfu` / `TaikoJudgmentContainer`）。セットアップ: **InGame → Setup Taiko Layer Intro**。
- **判定表示:** 判定確定フレームでノート位置に Perfect / Good / Miss 画像を表示（`Duration` 後にプール返却）。セットアップ: **InGame → Setup Taiko Judgment Display** / **Setup Taiko Judgment Zones**。
- **音符プール:** `QteTaikoView` が `RentNote` / `ReturnNote` で管理。`Awake` 時に `_notePoolPrewarmCount`（既定 5＝同時最大音符数）で Prewarm。
- **タップヒットエフェクト:** タップ成功時のみ `QteTaikoHitEffectDisplay` がプールから `QteTaikoHitEffect` を表示。`PlayHitEffect` で DOTween によりスケール拡大（既定 1.5 倍）と `CanvasGroup` フェード（1→0、既定 0.2 秒）を同時再生し、完了後にプール返却。セットアップ: **InGame → Setup Taiko Hit Effect**。
- **ノートストック UI:** v3 では未使用（シーン上 `NoteStockBar` は非表示のまま残してよい）。

### 4.5 戦闘 FloatingText（TMP・プール）

- **表示タイミング:** プレイヤー被弾・単体攻撃ヒット・HP 回復・SP 回復で、絶対値 `n` のみ表示（符号・種類による色変更なし）。数値適用と同フレームで表示。
- **実装:** `CombatFloatingTextPresenter`（プール）+ `CombatFloatingText.prefab`（`CombatFloatingTextView`）。セットアップ: **InGame → Setup Combat Floating Text**。
- **テキスト:** プレハブの **Face** / **Outline** の TMP に同じ文字列を設定。色はプレハブ側の設定を維持（コードでは触らない）。
- **移動:** `CombatFloatingTextView` が **DOTween** で演出（ランダム跳ね上がり → 重力風に落下）。完了時にプール返却。Presenter の `Return After Seconds` は未完了時のフォールバック。
- **Inspector（プレハブ `CombatFloatingTextView`）:** Face Label / Outline Label、Jump Height Min/Max、Drift X Range、Fall Distance、Rise/Fall Duration・Ease、Scale Pop（任意）。
- **Inspector（Presenter）:** Return After Seconds、World Offset、Pool Prewarm Count。
- **アンカー:** `GetFloatingTextWorldPosition()`（`Floating Text Anchor` 未設定時は `SpriteRenderer.bounds` の上端中央）。Presenter はワールド座標＋`World Offset` を UI 座標へ変換。