using System.Collections.Generic;
using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// 音の再生・停止などを管理するクラス
    /// </summary>
    /// <remarks>
    /// 他のコンポーネントのAwakeからPlayが呼ばれてもボイスが生成済みであるように、<see cref="DefaultExecutionOrder"/>で先に初期化する。
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class AudioSystem : MonoBehaviour, IAudioSystem
    {
        [SerializeField, Min(1)] private int _maxVoiceCount = 32;


        // リスナーが見つからないときに、探し直す間隔（秒）
        private const float ListenerSearchInterval = 1f;


        private readonly Dictionary<AudioCueBase, double> _lastPlayTimes = new();


        private AudioVoice[] _voices;
        private AudioStats _stats;
        private Transform _listener;
        private float _nextListenerSearchTime;
        private long _nextStartOrder;
        private bool _hasWarnedNoFreeVoice;


        /// <summary>再生状況の計測結果。Awakeより前はnull</summary>
        public AudioStats Stats => _stats;


        private void Awake()
        {
            _stats = new AudioStats(_maxVoiceCount);

            CreateVoices();
        }


        private void Update()
        {
            UpdateVoices();
        }


        /// <inheritdoc cref="IAudioSystem.Play(AudioCue)"/>
        public AudioPlayback Play(AudioCue cue)
        {
            if (!HasValidSettings(cue)) return AudioPlayback.None;

            if (IsTooSoon(cue))
            {
                _stats.RecordDiscard(cue, AudioDiscardReason.MinInterval);
                return AudioPlayback.None;
            }

            if (!TryReserveVoice(cue, out AudioVoice voice)) return AudioPlayback.None;

            voice.Play(cue, _nextStartOrder++);

            return FinishPlay(cue, voice);
        }


        /// <inheritdoc cref="IAudioSystem.Play(SpatialAudioCue, Vector3)"/>
        public AudioPlayback Play(SpatialAudioCue cue, Vector3 position)
        {
            if (!HasValidSettings(cue) || !HasValidDistance(cue)) return AudioPlayback.None;

            return PlaySpatial(cue, position, null);
        }


        /// <inheritdoc cref="IAudioSystem.Play(SpatialAudioCue, Transform)"/>
        public AudioPlayback Play(SpatialAudioCue cue, Transform followTarget)
        {
            if (!HasValidSettings(cue) || !HasValidDistance(cue)) return AudioPlayback.None;

            if (followTarget == null)
            {
                Debug.LogError($"引数： {nameof(followTarget)} がnullです。", cue);
                return AudioPlayback.None;
            }

            return PlaySpatial(cue, followTarget.position, followTarget);
        }


        /// <inheritdoc cref="IAudioSystem.Stop"/>
        public void Stop(AudioPlayback playback, float fadeOutDuration = 0f)
        {
            if (TryGetVoice(playback, out AudioVoice voice)) StopVoice(voice, fadeOutDuration);
        }


        /// <inheritdoc cref="IAudioSystem.Pause"/>
        public void Pause(AudioPlayback playback)
        {
            if (TryGetVoice(playback, out AudioVoice voice)) voice.Pause();
        }


        /// <inheritdoc cref="IAudioSystem.Resume"/>
        public void Resume(AudioPlayback playback)
        {
            if (TryGetVoice(playback, out AudioVoice voice)) voice.Resume();
        }


        /// <inheritdoc cref="IAudioSystem.GetState"/>
        public AudioPlaybackState GetState(AudioPlayback playback)
        {
            // 再生が終わってから解放されるまでの間も停止中として扱う。
            if (!TryGetVoice(playback, out AudioVoice voice) || voice.IsFinished) return AudioPlaybackState.Stopped;

            return voice.IsPaused ? AudioPlaybackState.Paused : AudioPlaybackState.Playing;
        }


        /// <inheritdoc cref="IAudioSystem.StopAll"/>
        public void StopAll(AudioCategory category, float fadeOutDuration = 0f)
        {
            if (!IsValidCategory(category)) return;

            // フェードアウト中の音も対象に含める。
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category) StopVoice(voice, fadeOutDuration);
            }
        }


        /// <inheritdoc cref="IAudioSystem.PauseAll"/>
        public void PauseAll(AudioCategory category)
        {
            if (!IsValidCategory(category)) return;

            // フェードアウト中の音も対象に含める。
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category) voice.Pause();
            }
        }


        /// <inheritdoc cref="IAudioSystem.ResumeAll"/>
        public void ResumeAll(AudioCategory category)
        {
            if (!IsValidCategory(category)) return;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category) voice.Resume();
            }
        }


        /// <inheritdoc cref="IAudioSystem.SetListener"/>
        public void SetListener(Transform listener)
        {
            _listener = listener;

            // nullが渡された場合に次の判定ですぐ自動検出できるようにする。
            _nextListenerSearchTime = 0f;
        }


        /// <summary>
        /// ボイスの音を停止する。秒数が0より大きい場合はフェードアウトしてから停止する。
        /// </summary>
        /// <param name="voice">停止するボイス</param>
        /// <param name="fadeOutDuration">フェードアウトにかける秒数</param>
        private static void StopVoice(AudioVoice voice, float fadeOutDuration)
        {
            if (fadeOutDuration > 0f)
            {
                // すでにフェードアウト中の音は止まる途中のため何もしない。
                // ここで即座に止めるとフェードアウトの意味がなくなる。
                if (voice.IsActive) voice.BeginFadeOut(fadeOutDuration);
                return;
            }

            // 0以下の場合は即座に停止する。NaNも上の比較を満たさないため、ここで停止する。
            voice.Stop();
        }


        /// <summary>
        /// ボイスの数だけAudioSourceを持つ子オブジェクトを生成する。
        /// </summary>
        private void CreateVoices()
        {
            _voices = new AudioVoice[_maxVoiceCount];

            for (int i = 0; i < _voices.Length; i++)
            {
                var voiceObject = new GameObject($"AudioVoice_{i}");
                voiceObject.transform.SetParent(transform);

                var audioSource = voiceObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;

                _voices[i] = new AudioVoice(i, audioSource);
            }
        }


        /// <summary>
        /// 全ボイスのフェードアウトの進行、終了した音の解放、追従、聞こえない距離まで離れた音の停止を行う。
        /// </summary>
        private void UpdateVoices()
        {
            // ポーズ中（Time.timeScale = 0）でもフェードアウトが進むように、スケールされない時間を使う。
            float deltaTime = Time.unscaledDeltaTime;
            bool hasListener = TryGetListenerPosition(out Vector3 listenerPosition);

            foreach (AudioVoice voice in _voices)
            {
                voice.UpdateFade(deltaTime);

                if (voice.IsFinished)
                {
                    voice.Release();
                    continue;
                }

                // 一時停止中の音は聞こえていないため、距離の判定は再開後に行う。
                if (!voice.IsSpatial || !voice.IsActive || voice.IsPaused) continue;

                voice.UpdateFollow();

                // 聞こえない距離まで離れた音は、フェードアウトして解放する。
                if (hasListener && voice.IsActive && voice.IsOutOfRange(listenerPosition))
                {
                    voice.BeginFadeOut(voice.Category.FadeOutDuration);
                }
            }

            RecordVoiceUsage();
        }


        /// <summary>
        /// 3D音源を再生する。
        /// </summary>
        /// <param name="cue">再生する音源</param>
        /// <param name="position">音を鳴らすワールド座標</param>
        /// <param name="followTarget">追従させるTransform。追従させない場合はnull</param>
        /// <returns>再生中の音を指すハンドル。再生しなかった場合は<see cref="AudioPlayback.None"/>を返す。</returns>
        private AudioPlayback PlaySpatial(SpatialAudioCue cue, Vector3 position, Transform followTarget)
        {
            if (IsTooSoon(cue))
            {
                _stats.RecordDiscard(cue, AudioDiscardReason.MinInterval);
                return AudioPlayback.None;
            }

            if (IsOutOfRange(cue, position))
            {
                _stats.RecordDiscard(cue, AudioDiscardReason.OutOfRange);
                return AudioPlayback.None;
            }

            if (!TryReserveVoice(cue, out AudioVoice voice)) return AudioPlayback.None;

            voice.Play(cue, _nextStartOrder++, position, followTarget);

            return FinishPlay(cue, voice);
        }


        /// <summary>
        /// 再生を開始した後の記録を行い、再生中の音を指すハンドルを返す。
        /// </summary>
        private AudioPlayback FinishPlay(AudioCueBase cue, AudioVoice voice)
        {
            // 最短間隔が0のCueは判定が不要なため、再生時刻を記録しない。
            if (cue.MinInterval > 0f) _lastPlayTimes[cue] = Time.unscaledTimeAsDouble;

            _stats.RecordPlay(cue, CountActiveVoicesOfCue(cue), CountActiveVoicesOfCategory(cue.Category));
            RecordVoiceUsage();

            return voice.CreatePlayback();
        }


        /// <summary>
        /// ボイスの使用状況を計測結果に記録する。
        /// </summary>
        /// <remarks>EditorとDevelopment Build以外では呼び出し自体が取り除かれる。</remarks>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void RecordVoiceUsage()
        {
            int assignedCount = 0;
            int fadingOutCount = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned) assignedCount++;
                if (voice.IsFadingOut) fadingOutCount++;
            }

            _stats.RecordVoiceUsage(assignedCount, fadingOutCount);
        }


        /// <summary>
        /// Cueが再生に必要な設定を持っているかどうかを判定する。持っていない場合はエラーログを出す。
        /// </summary>
        private static bool HasValidSettings(AudioCueBase cue)
        {
            if (cue == null)
            {
                Debug.LogError($"引数： {nameof(cue)} がnullです。");
                return false;
            }

            if (cue.AudioResource == null)
            {
                Debug.LogError($"AudioCue： {cue.name} の {nameof(cue.AudioResource)} が設定されていません。", cue);
                return false;
            }

            if (cue.Category == null)
            {
                Debug.LogError($"AudioCue： {cue.name} の {nameof(cue.Category)} が設定されていません。", cue);
                return false;
            }

            return true;
        }


        /// <summary>
        /// 引数のカテゴリがnullでないかどうかを判定する。nullの場合はエラーログを出す。
        /// </summary>
        private static bool IsValidCategory(AudioCategory category)
        {
            if (category != null) return true;

            Debug.LogError($"引数： {nameof(category)} がnullです。");

            return false;
        }


        /// <summary>
        /// 3D音源の最小距離と最大距離が正しい大小関係で設定されているかどうかを判定する。正しくない場合はエラーログを出す。
        /// </summary>
        private static bool HasValidDistance(SpatialAudioCue cue)
        {
            if (cue.MaxDistance >= cue.MinDistance) return true;

            Debug.LogError($"AudioCue： {cue.name} の {nameof(cue.MaxDistance)} が {nameof(cue.MinDistance)} より小さく設定されています。", cue);

            return false;
        }


        /// <summary>
        /// 前回の再生開始からCueの最短間隔が経過していないかどうかを判定する。
        /// </summary>
        private bool IsTooSoon(AudioCueBase cue)
        {
            // 最短間隔が0のCueは判定する必要がない。
            if (cue.MinInterval <= 0f) return false;

            return _lastPlayTimes.TryGetValue(cue, out double lastPlayTime) &&
                   Time.unscaledTimeAsDouble - lastPlayTime < cue.MinInterval;
        }


        /// <summary>
        /// 音を鳴らす位置がリスナーから聞こえなくなる距離を超えて離れているかどうかを判定する。
        /// </summary>
        /// <remarks>リスナーが見つからない場合は判定できないため、falseを返す。</remarks>
        private bool IsOutOfRange(SpatialAudioCue cue, Vector3 position)
        {
            if (!TryGetListenerPosition(out Vector3 listenerPosition)) return false;

            return (position - listenerPosition).sqrMagnitude > cue.MaxDistance * cue.MaxDistance;
        }


        /// <summary>
        /// リスナーの位置を取得する。リスナーが未設定または破棄されている場合はシーン内のAudioListenerを探す。
        /// </summary>
        /// <param name="position">取得できた場合はリスナーのワールド座標</param>
        /// <returns>取得できたらtrue、できなかった場合はfalseを返す。</returns>
        private bool TryGetListenerPosition(out Vector3 position)
        {
            // 見つからない場合に毎フレーム探さないよう、探す間隔を空ける。
            if (_listener == null && Time.unscaledTime >= _nextListenerSearchTime)
            {
                AudioListener found = FindFirstObjectByType<AudioListener>();

                if (found != null) _listener = found.transform;
                else _nextListenerSearchTime = Time.unscaledTime + ListenerSearchInterval;
            }

            if (_listener == null)
            {
                position = default;
                return false;
            }

            position = _listener.position;

            return true;
        }


        /// <summary>
        /// 新しい音のためのボイスを確保する。Cueとカテゴリの同時再生数の上限もここで処理する。
        /// </summary>
        /// <param name="cue">再生する音源</param>
        /// <param name="voice">確保できた場合は空きボイス、できなかった場合はnullを返す。</param>
        /// <returns>確保できたらtrue、新しい音を捨てる場合はfalseを返す。</returns>
        private bool TryReserveVoice(AudioCueBase cue, out AudioVoice voice)
        {
            voice = null;

            // Cueの同時再生数が上限に達している場合は、Cueの設定に従って空きを作る。
            if (CountActiveVoicesOfCue(cue) >= cue.MaxInstances && !TryMakeRoomInCue(cue)) return false;

            // カテゴリの同時再生数が上限に達している場合は、カテゴリの設定に従って空きを作る。
            // Cueの上限で空きを作った場合は同じカテゴリの音も1つ減っているため、ここでは上限に達していない。
            AudioCategory category = cue.Category;
            if (CountActiveVoicesOfCategory(category) >= category.MaxPlaybackCount && !TryMakeRoomInCategory(cue)) return false;

            if (!TryFindFreeVoice(out voice))
            {
                ReportNoFreeVoice(cue);
                return false;
            }

            return true;
        }


        /// <summary>
        /// Cueの再生数を数える。フェードアウト中の音は数えない。
        /// </summary>
        /// <remarks>
        /// Cue用とカテゴリ用の検索処理は似ているが、デリゲートで共通化するとアロケーションが発生するため、あえて分ける。
        /// </remarks>
        private int CountActiveVoicesOfCue(AudioCueBase cue)
        {
            int count = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsActive && voice.Cue == cue) count++;
            }

            return count;
        }


        /// <summary>
        /// カテゴリの再生数を数える。フェードアウト中の音は数えない。
        /// </summary>
        private int CountActiveVoicesOfCategory(AudioCategory category)
        {
            int count = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsActive && voice.Category == category) count++;
            }

            return count;
        }


        /// <summary>
        /// Cueの上限に達しているときに、新しい音のための空きを作る。
        /// </summary>
        private bool TryMakeRoomInCue(AudioCueBase cue)
        {
            return TryFindOldestActiveVoiceOfCue(cue, out AudioVoice oldestVoice) &&
                   TryApplyOverflowPolicy(
                       cue,
                       AudioLimitScope.Cue,
                       cue.InstanceOverflowPolicy,
                       cue.Category.FadeOutDuration,
                       oldestVoice);
        }


        /// <summary>
        /// Cueが所属するカテゴリの上限に達しているときに、新しい音のための空きを作る。
        /// </summary>
        private bool TryMakeRoomInCategory(AudioCueBase cue)
        {
            AudioCategory category = cue.Category;

            return TryFindOldestActiveVoiceOfCategory(category, out AudioVoice oldestVoice) &&
                   TryApplyOverflowPolicy(
                       cue,
                       AudioLimitScope.Category,
                       category.OverflowPolicy,
                       category.FadeOutDuration,
                       oldestVoice);
        }


        /// <summary>
        /// 同時再生数の上限に達しているときに、設定された挙動に従って新しい音のための空きを作る。
        /// </summary>
        /// <param name="cue">新しく再生しようとしている音</param>
        /// <param name="scope">どの範囲の上限に達しているか</param>
        /// <param name="policy">上限に達したときの挙動</param>
        /// <param name="fadeOutDuration">フェードアウトさせる場合の秒数</param>
        /// <param name="oldestVoice">上限に達している範囲の中で最も古いボイス</param>
        /// <returns>空きを作れた場合はtrue、新しい音を捨てる場合はfalseを返す。</returns>
        private bool TryApplyOverflowPolicy(
            AudioCueBase cue,
            AudioLimitScope scope,
            AudioOverflowPolicy policy,
            float fadeOutDuration,
            AudioVoice oldestVoice)
        {
            switch (policy)
            {
                case AudioOverflowPolicy.DiscardNew:
                {
                    _stats.RecordDiscard(cue, ToDiscardReason(scope));
                    return false;
                }

                case AudioOverflowPolicy.StopOldest:
                {
                    oldestVoice.Stop();
                    _stats.RecordEviction(cue, scope);
                    return true;
                }

                case AudioOverflowPolicy.FadeOutOldest:
                {
                    // フェードアウト中の音はボイスを使い続けるため、新しい音には別の空きボイスが必要になる。
                    // 空きがない場合にフェードアウト中の音を切って枠を作ると音がブチッと途切れるため、
                    // フェードアウトを始めずに新しい音を捨てる。
                    // 一時停止中などで即座に停止する場合は、そのボイスが空くため空きボイスは要らない。
                    if (oldestVoice.CanFadeOut(fadeOutDuration) && !TryFindFreeVoice(out _))
                    {
                        ReportNoFreeVoice(cue);
                        return false;
                    }

                    oldestVoice.BeginFadeOut(fadeOutDuration);
                    _stats.RecordEviction(cue, scope);
                    return true;
                }

                default:
                {
                    Debug.LogError($"未対応の {nameof(AudioOverflowPolicy)} です： {policy}");
                    return false;
                }
            }
        }


        /// <summary>
        /// 上限に達した範囲を、再生しなかった理由に変換する。
        /// </summary>
        private static AudioDiscardReason ToDiscardReason(AudioLimitScope scope)
        {
            return scope switch
            {
                AudioLimitScope.Cue => AudioDiscardReason.CueLimit,
                _ => AudioDiscardReason.CategoryLimit,
            };
        }


        /// <summary>
        /// Cueの中で最も古い、フェードアウト中でないボイスを探す。
        /// </summary>
        private bool TryFindOldestActiveVoiceOfCue(AudioCueBase cue, out AudioVoice oldestVoice)
        {
            oldestVoice = null;

            foreach (AudioVoice voice in _voices)
            {
                if (!voice.IsActive || voice.Cue != cue) continue;
                if (oldestVoice != null && oldestVoice.StartOrder <= voice.StartOrder) continue;

                oldestVoice = voice;
            }

            return oldestVoice != null;
        }


        /// <summary>
        /// カテゴリの中で最も古い、フェードアウト中でないボイスを探す。
        /// </summary>
        private bool TryFindOldestActiveVoiceOfCategory(AudioCategory category, out AudioVoice oldestVoice)
        {
            oldestVoice = null;

            foreach (AudioVoice voice in _voices)
            {
                if (!voice.IsActive || voice.Category != category) continue;
                if (oldestVoice != null && oldestVoice.StartOrder <= voice.StartOrder) continue;

                oldestVoice = voice;
            }

            return oldestVoice != null;
        }


        /// <summary>
        /// 音が割り当てられていない空きボイスを探す。
        /// </summary>
        private bool TryFindFreeVoice(out AudioVoice freeVoice)
        {
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned) continue;

                freeVoice = voice;
                return true;
            }

            freeVoice = null;
            return false;
        }


        /// <summary>
        /// 空きボイスがなく新しい音を捨てることを、計測結果と警告ログに記録する。
        /// </summary>
        /// <param name="cue">捨てる音</param>
        private void ReportNoFreeVoice(AudioCueBase cue)
        {
            _stats.RecordDiscard(cue, AudioDiscardReason.NoFreeVoice);

            // 弾丸のように大量の音が続く場面で毎回ログを出すとそれ自体が負荷になるため、警告は最初の1回だけにする。
            if (_hasWarnedNoFreeVoice) return;

            _hasWarnedNoFreeVoice = true;

            Debug.LogWarning($"再生できるボイスが残っていません。{nameof(_maxVoiceCount)} を増やしてください。" +
                             "フェードアウト中の音もボイスを使うため、カテゴリごとの最大再生数の合計より余裕が必要です。" +
                             $"警告は最初の1回のみ表示します。捨てた回数は {nameof(Stats)} で確認できます。");
        }


        /// <summary>
        /// ハンドルが指している現在再生中のボイスを取得する。
        /// </summary>
        /// <param name="playback">取得するボイスのハンドル</param>
        /// <param name="voice">取得できた場合はAudioVoice、できなかった場合はnullを返す。</param>
        /// <returns>取得できたらtrue、できなかった場合はfalseを返す。</returns>
        private bool TryGetVoice(AudioPlayback playback, out AudioVoice voice)
        {
            voice = null;

            if (!playback.IsValid || playback.VoiceIndex >= _voices.Length) return false;

            AudioVoice candidate = _voices[playback.VoiceIndex];
            if (!candidate.Matches(playback)) return false;

            voice = candidate;

            return true;
        }
    }
}