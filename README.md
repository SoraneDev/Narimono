# Narimono（鳴物）

Unity 向けの軽量なサウンド再生ライブラリです。
鳴らす音を ScriptableObject のアセットとして定義し、同時再生数の制御・3D サウンドの距離カリング・フェードイン／フェードアウトを、再生時のアロケーションなしで行います。

*A lightweight, allocation-free sound playback library for Unity.*

### 名前の由来

**鳴物（なりもの）** は、歌舞伎の下座音楽のうち、太鼓や笛などの楽器で演奏されるものです。舞台下手の黒御簾（くろみす）の中で演奏され、場面を盛り上げる音楽だけでなく、大太鼓で風や雨の音を表すといった効果音も担います。
観客の目に触れない場所で、効果音と音楽を受け持つ裏方です。ゲームの裏側で効果音と BGM を鳴らし分けるこのライブラリの役割を、その姿に重ねて名付けました。

---

## 特徴

- **アセットで音を指定する**：鳴らす音は Cue（ScriptableObject）で定義します。文字列や enum の ID を管理する必要がなく、参照切れは Inspector で分かります。
- **AudioClip と AudioRandomContainer の両方に対応**：Cue には `AudioResource` を設定します。
- **AudioSource をプールして使い回す**：起動時に必要な数だけ生成し、再生時には GameObject もコンポーネントも作りません。
- **3 段階の同時再生数の制御**：Cue ごと・カテゴリごと・全体の 3 段階で上限を設定できます。上限に達したときは「新しい音を捨てる」「古い音を止める」「古い音をフェードアウトさせる」から選べます。
- **連打対策**：同じ Cue の最短再生間隔を設定できます。
- **3D サウンド**：位置を指定した再生と、Transform への追従再生に対応しています。リスナーから聞こえない距離の音は再生せず、再生中に離れた音はフェードアウトして解放します。
- **フェード**：Cue ごとのフェードインと、秒数を指定したフェードアウト停止に対応しています。BGM のクロスフェードはカテゴリの設定だけで実現できます。
- **ハンドルによる操作**：再生時に返るハンドルで、停止・一時停止・再開・状態の取得を行います。終了済みの音や別の音に使い回された枠を指すハンドルは、安全に無視されます。
- **計測機能**：同時再生数のピークや、再生しなかった回数とその理由を記録し、上限値の調整に使えます。記録は Editor と Development Build のみで、リリースビルドでは記録処理ごと取り除かれます。

## 動作環境

- Unity 6 以降
- 外部ライブラリへの依存はありません

---

## 導入

### Package Manager（Git URL）

`Window > Package Manager` の `+` ボタンから `Install package from git URL...` を選び、次の URL を入力します。

```
https://github.com/SoraneDev/Narimono.git?path=/Assets/Narimono
```

### 手動

このリポジトリをダウンロードし、プロジェクトの `Packages` 以下、または `Assets` 以下に配置します。

---

## クイックスタート

### 1. AudioSystem を配置する

シーンに空の GameObject を作り、`AudioSystem` コンポーネントを追加します。
`AudioSystem` は起動時に AudioSource を子オブジェクトとして生成します。シーンをまたいで使う方法は、[AudioSystem の置き方と参照方法](#audiosystem-の置き方と参照方法)を参照してください。

### 2. カテゴリを作る

`Assets > Create > Narimono > Audio Category` でカテゴリを作ります。
効果音・ボイス・BGM など、同時再生数をまとめて管理したい単位で作ります。

### 3. Cue を作る

`Assets > Create > Narimono > Audio Cue`（2D）または `Spatial Audio Cue`（3D）で Cue を作り、`Audio Resource` と `Category` を設定します。

### 4. 再生する

```csharp
using Narimono;
using UnityEngine;

public sealed class Player : MonoBehaviour
{
    [SerializeField] private AudioSystem _audioSystem;
    [SerializeField] private AudioCue _jumpSound;
    [SerializeField] private SpatialAudioCue _footstepSound;


    private void Jump()
    {
        _audioSystem.Play(_jumpSound);
    }


    private void Step()
    {
        _audioSystem.Play(_footstepSound, transform.position);
    }
}
```

---

## AudioSystem の置き方と参照方法

`AudioSystem` はシングルトンではない、普通の MonoBehaviour です。自動で生成されることも、`DontDestroyOnLoad` でシーンをまたいで残ることもありません。どこに置き、どう参照するかは、プロジェクトの設計に合わせて選んでください。

- `AudioSystem` を複数置くと、それぞれが別々のボイスと同時再生数を持ちます。通常はゲーム全体で 1 つにします。
- `AudioSystem` が破棄されると、子の AudioSource ごと破棄され、鳴っている音はすべて止まります。
- 呼び出す側は `IAudioSystem` で受け取ると、テストなどで差し替えやすくなります。計測結果（`Stats`）だけは `AudioSystem` クラスから読みます。
- `AudioSystem` は `[DefaultExecutionOrder(-100)]` で先に初期化されるため、他のコンポーネントの `Awake` から `Play` を呼べます。

### 方法 1：Inspector で直接参照する

1 つのシーンで完結するゲームや、試作に向いています。[クイックスタート](#4-再生する)の例がこの方法です。

### 方法 2：DI コンテナで渡す

VContainer の場合、`AudioSystem` を `IAudioSystem` として登録すると、使う側にコンストラクタなどで注入できます。

```csharp
public sealed class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private AudioSystem _audioSystem;


    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponent(_audioSystem).As<IAudioSystem>();
    }
}
```

```csharp
public sealed class PlayerSound
{
    private readonly IAudioSystem _audioSystem;


    public PlayerSound(IAudioSystem audioSystem)
    {
        _audioSystem = audioSystem;
    }
}
```

シーンをまたいで使う場合は、プロジェクト全体の親となる LifetimeScope（VContainerSettings で指定するルート）に `AudioSystem` を含めて登録します。

### 方法 3：起動時に 1 つだけ生成して常駐させる

DI コンテナを使わない場合は、起動時に一度だけ生成し、静的なプロパティなどで公開します。

```csharp
public static class GameAudio
{
    public static IAudioSystem System { get; private set; }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Resources/AudioSystem.prefab に、設定済みの AudioSystem を置いておく
        AudioSystem prefab = Resources.Load<AudioSystem>("AudioSystem");
        AudioSystem instance = Object.Instantiate(prefab);
        Object.DontDestroyOnLoad(instance.gameObject);

        System = instance;
    }
}
```

シーンに置いた `AudioSystem` に `DontDestroyOnLoad` を使う場合は、そのシーンを読み込み直すたびに `AudioSystem` が増える点に注意してください。最初に一度だけ読み込むシーンに置くか、上の例のように一度だけ生成します。また、`DontDestroyOnLoad` はルートの GameObject にしか効かないため、`AudioSystem` はルートに置きます。

---

## 使い方

### 2D の再生

```csharp
AudioPlayback playback = audioSystem.Play(cue);
```

### 3D の再生

```csharp
// 指定した位置で鳴らす
audioSystem.Play(spatialCue, hitPosition);

// Transformに追従させて鳴らす。追従対象が破棄されると、音はフェードアウトして終了する
audioSystem.Play(spatialCue, enemy.transform);
```

距離の判定に使うリスナーは、シーン内の `AudioListener` を自動で探します。明示的に指定する場合は `SetListener` を使います。リスナーが見つからない間は、距離による制限を行いません。

```csharp
audioSystem.SetListener(playerCamera.transform);
```

### 停止・一時停止・再開

`Play` が返したハンドルを使います。

```csharp
AudioPlayback playback = audioSystem.Play(loopCue);

audioSystem.Pause(playback);
audioSystem.Resume(playback);

audioSystem.Stop(playback);                          // 即座に停止
audioSystem.Stop(playback, fadeOutDuration: 1.5f);   // 1.5秒かけてフェードアウトして停止
```

カテゴリ単位でまとめて操作することもできます。ポーズメニューを開いている間だけ効果音を止める、といった場面に使えます。

```csharp
audioSystem.PauseAll(sfxCategory);
audioSystem.ResumeAll(sfxCategory);

audioSystem.StopAll(sfxCategory);
audioSystem.StopAll(bgmCategory, fadeOutDuration: 2f);
```

- 再生できなかった場合、`Play` は `AudioPlayback.None` を返します。`playback.IsValid` で確認できます。
- 既に終了した音のハンドルや `AudioPlayback.None` を渡しても、何も起きません。
- フェードアウトで止め始めた音のハンドルは、その時点で無効になります。
- `StopAll` でフェードアウトを指定した場合、すでにフェードアウト中の音はそのまま続けます。即座に止める場合は、フェードアウト中の音も止めます。

### 状態を調べる

```csharp
switch (audioSystem.GetState(playback))
{
    case AudioPlaybackState.Playing: break;
    case AudioPlaybackState.Paused: break;
    case AudioPlaybackState.Stopped: break;   // 再生が終わった音・フェードアウト中の音・無効なハンドル
}
```

ハンドルは値として比較でき、`==` や Dictionary のキーにもアロケーションなしで使えます。

### フェードイン

Cue の `Fade In Duration` に秒数を設定すると、再生開始時に無音からフェードインします。
フェードインの途中で止めた場合は、その時点の音量からフェードアウトします。

### BGM の切り替え

BGM 用のカテゴリを次のように設定すると、新しい BGM を `Play` するだけで、前の BGM がフェードアウトします。BGM の Cue に `Fade In Duration` を設定しておけば、クロスフェードになります。

| 項目 | 値 |
|---|---|
| Max Playback Count | 1 |
| Overflow Policy | Fade Out Oldest |
| Fade Out Duration | 1〜2 秒など |

```csharp
audioSystem.Play(titleBgm);
audioSystem.Play(stageBgm);   // titleBgmがフェードアウトし、stageBgmがフェードインする
```

同じ BGM をもう一度 `Play` すると、頭から鳴り直します。同じ曲が鳴っているかどうかは、ハンドルと `GetState` で確認してください。

---

## 設定項目

### AudioSystem

| 項目 | 説明 | 初期値 |
|---|---|---|
| Max Voice Count | 同時に鳴らせる音の総数（生成する AudioSource の数） | 32 |

### AudioCategory

| 項目 | 説明 | 初期値 |
|---|---|---|
| Audio Mixer Group | 出力先のミキサーグループ。未設定の場合はミキサーを通さずに出力する | なし |
| Max Playback Count | カテゴリ内の最大同時再生数 | 1 |
| Overflow Policy | カテゴリの上限に達したときの挙動 | Discard New |
| Fade Out Duration | ライブラリが自動でフェードアウトさせるときの秒数（Fade Out Oldest、聞こえない距離への移動、追従対象の破棄） | 0.2 |

### AudioCue / SpatialAudioCue（共通）

| 項目 | 説明 | 初期値 |
|---|---|---|
| Audio Resource | 再生する AudioClip または AudioRandomContainer | なし |
| Category | 所属カテゴリ | なし |
| Loop | ループ再生するかどうか | オフ |
| Volume | 音量（0〜1） | 1 |
| Pitch | ピッチ（-3〜3） | 1 |
| Fade In Duration | 再生開始時のフェードインの秒数。0 でフェードインしない | 0 |
| Min Interval | 同じ Cue を再び再生できるまでの最短間隔（秒）。0 で制限なし | 0 |
| Limit Instances | Cue ごとの同時再生数を制限するかどうか | オフ |
| Max Instances | Cue ごとの最大同時再生数 | 1 |
| Instance Overflow Policy | Cue の上限に達したときの挙動 | Discard New |

### SpatialAudioCue（3D のみ）

| 項目 | 説明 | 初期値 |
|---|---|---|
| Rolloff | 距離による減衰方式（Logarithmic / Linear） | Logarithmic |
| Min Distance | 音量が減衰を始める距離 | 1 |
| Max Distance | 音が聞こえなくなる距離。これより遠い音は再生せず、再生中に離れた音はフェードアウトして解放する | 30 |

### 上限に達したときの挙動（AudioOverflowPolicy）

| 値 | 挙動 | 向いている音 |
|---|---|---|
| Discard New | 新しい音を捨てる | 連続して鳴る短い効果音 |
| Stop Oldest | 最も古い音を即座に止めて、新しい音を再生する | 途中で切れても目立たない短い音 |
| Fade Out Oldest | 最も古い音をフェードアウトさせて、新しい音を再生する | BGM・環境音など、途中で切れると目立つ音 |

---

## 同時再生数の制御

### 判定の順番

`Play` は次の順に判定し、条件を満たさなければ再生せずに `AudioPlayback.None` を返します。

1. 引数と Cue の設定が正しいか（Cue や追従対象が null の場合、Audio Resource や Category が未設定の場合、Max Distance が Min Distance より小さい場合は、エラーログを出す）
2. Min Interval が経過しているか
3. （3D のみ）リスナーから Max Distance 以内か
4. Cue の上限：達していれば Instance Overflow Policy に従う
5. カテゴリの上限：達していれば Overflow Policy に従う
6. 空いている枠（ボイス）があるか

### フェードアウト中の音の扱い

- フェードアウト中の音は、Cue とカテゴリの同時再生数に **数えません**。
- ただし、鳴り終わるまで枠（ボイス）は **使い続けます**。
- そのため Fade Out Oldest では、古い音のフェードアウト中に新しい音を鳴らすための空き枠が必要です。空き枠がない場合は、古い音を途中で切らずに新しい音を捨てます。古い音が一時停止中の場合や、フェードアウトの秒数が 0 の場合は、古い音をその場で止めるため空き枠は要りません。
- 一時停止中の音は同時再生数に数えます。

### 上限値の決め方

次の関係を満たすように設定するのが目安です。

```
Cue の Max Instances  ≦  カテゴリの Max Playback Count

すべてのカテゴリの Max Playback Count の合計 + フェードアウト中の分の余裕
    ≦  AudioSystem の Max Voice Count
    ≦  Unity の Max Real Voices（Project Settings > Audio）
```

- 1 つの Cue の上限は、カテゴリの上限の 1/3〜1/2 程度にすると、1 種類の音がカテゴリを占有しにくくなります。
- フェードアウト中の分の余裕は、全体の 10〜20% 程度が目安です。
- 実際の値は、次の「計測」で確認しながら調整します。

### 大量に発生する効果音（弾丸の着弾音など）

- Min Interval を短く設定し、同じフレームでの重複再生を間引きます（例：0.03〜0.05 秒）。
- Limit Instances をオンにして Max Instances を小さくし、Instance Overflow Policy を Discard New にします。
- 専用のカテゴリに分けて、他の重要な音を巻き込まないようにします。
- SpatialAudioCue にして、遠くの音は再生しないようにします。

---

## 計測（Stats）

上限値の調整のために、再生状況を記録します。

### 画面に表示する

1. 任意の GameObject に `AudioStatsOverlay` を追加し、`Audio System` を割り当てます。
2. 再生中に画面左上の `Audio Stats` ボタンでパネルを開きます。
3. `Reset` で記録を消し、調整したい場面をプレイしてから結果を確認します。`Log` でコンソールに出力します。

### コードから読む

`Stats` は `IAudioSystem` には含まれないため、`AudioSystem` クラスから読みます。

```csharp
AudioStats stats = audioSystem.Stats;

Debug.Log(stats.PeakAssignedVoiceCount);
Debug.Log(stats.BuildReport());
```

### 読み方

| 状況 | 対処 |
|---|---|
| `NoFreeVoiceCount` が 0 でない | Max Voice Count を増やす、またはカテゴリの上限を下げる |
| フェードアウト中のピークが大きい | その分を Max Voice Count の余裕に足す |
| カテゴリのピークが上限に張り付き、捨てた回数が多い | 重要な音なら上限を上げる。大量に発生する効果音なら許容する |
| Cue のピークが常に上限より小さい | Max Instances を下げて、枠を他に回せる |

記録は Editor と Development Build でのみ行います。リリースビルドでは記録処理の呼び出しがコンパイル時に取り除かれ、`AudioStatsOverlay` も自動で無効になります。

---

## 仕様と注意点

- **時間**：フェードと Min Interval は `Time.timeScale` の影響を受けません。`Time.timeScale = 0` のポーズ中でもフェードは進みます。
- **一時停止とフェード**：`Pause` / `PauseAll` で一時停止した音は、フェードの進行も止まり、再開すると続きから進みます。一時停止中の音にフェードアウトを指定した場合は、即座に停止します。
- **3D の減衰**：Logarithmic は Max Distance でも音量が 0 になりません。距離で止めるときにはフェードアウトしますが、気になる場合は Linear を使ってください。
- **ドップラー効果**：使用しません。
- **AudioRandomContainer**：Cue の Volume と Pitch は、Container 側の設定と合わせて適用されます。
- **空き枠がないとき**：警告ログは最初の 1 回だけ表示します。回数は計測で確認できます。
- **引数の検証**：Cue の設定不足や、null のカテゴリ・追従対象を渡した場合は、エラーログを出して何もしません。
- **初期化前の呼び出し**：`AudioSystem` の `Awake` が呼ばれる前（GameObject が非アクティブな場合など）に操作すると、例外が発生します。
- **アロケーション**：通常の再生・停止ではアロケーションは発生しません。ただし、Min Interval を設定した Cue を初めて再生したときは、内部の辞書が拡張されることがあります。Editor と Development Build では、計測のために Cue やカテゴリを初めて記録したときと、計測結果の文字列を作るときにも確保します。

---

## ライセンス

[MIT License](LICENSE)
