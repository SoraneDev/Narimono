using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace Narimono
{
    /// <summary>
    /// 音の再生・停止などを管理する。
    /// </summary>
    /// <remarks>
    /// ほかのコンポーネントのAwakeからPlayが呼ばれてもボイスが生成済みであるように、<see cref="DefaultExecutionOrder" />で先に初期化する。
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Narimono/AudioSystem")]
    public sealed class AudioSystem : MonoBehaviour, IAudioSystem
    {
        [SerializeField] [Min(1)] [Tooltip("同時に再生できる音の総数（フェードアウト中の音も含む）")]
        private int _maxVoiceCount = 32;


        // リスナーが見つからないときに、探し直す間隔（秒）。
        private const float ListenerSearchInterval = 1f;


        private readonly Dictionary<AudioCueBase, double> _lastPlayTimes = new();
        private AudioVoice[] _voices;
        private Transform _listener;
        private float _nextListenerSearchTime;
        private long _nextStartOrder;
        private bool _hasWarnedNoFreeVoice;


        /// <summary>再生状況の計測結果。Awakeより前はnull。</summary>
        public AudioStats Stats { get; private set; }


        private void Awake()
        {
            Stats = new AudioStats(_maxVoiceCount);

            CreateVoices();
        }


        private void Update()
        {
            UpdateVoices();
        }


        /// <inheritdoc />
        public AudioPlayback Play(AudioCue cue)
        {
            if (!HasValidSettings(cue))
            {
                return AudioPlayback.None;
            }

            if (IsTooSoon(cue))
            {
                Stats.RecordDiscard(cue, AudioDiscardReason.MinInterval);
                return AudioPlayback.None;
            }

            if (!TryReserveVoice(cue, out AudioVoice voice))
            {
                return AudioPlayback.None;
            }

            voice.Play(cue, _nextStartOrder++);

            return FinishPlay(cue, voice);
        }


        /// <inheritdoc />
        public AudioPlayback Play(SpatialAudioCue cue, Vector3 position)
        {
            if (!HasValidSettings(cue) || !HasValidDistance(cue))
            {
                return AudioPlayback.None;
            }

            return PlaySpatial(cue, position, followTarget: null);
        }


        /// <inheritdoc />
        public AudioPlayback Play(SpatialAudioCue cue, Transform followTarget)
        {
            if (!HasValidSettings(cue) || !HasValidDistance(cue))
            {
                return AudioPlayback.None;
            }

            if (followTarget == null)
            {
                Debug.LogError($"引数： {nameof(followTarget)} がnullです。", cue);
                return AudioPlayback.None;
            }

            return PlaySpatial(cue, followTarget.position, followTarget);
        }


        /// <inheritdoc />
        public void Stop(AudioPlayback playback, float fadeOutDuration = 0f)
        {
            if (TryGetVoice(playback, out AudioVoice voice))
            {
                StopVoice(voice, fadeOutDuration);
            }
        }


        /// <inheritdoc />
        public void Pause(AudioPlayback playback)
        {
            if (TryGetVoice(playback, out AudioVoice voice))
            {
                voice.Pause();
            }
        }


        /// <inheritdoc />
        public void Resume(AudioPlayback playback)
        {
            if (TryGetVoice(playback, out AudioVoice voice))
            {
                voice.Resume();
            }
        }


        /// <inheritdoc />
        public AudioPlaybackState GetState(AudioPlayback playback)
        {
            // 再生が終わってから解放されるまでの間も停止中として扱う。
            if (!TryGetVoice(playback, out AudioVoice voice) || voice.IsFinished)
            {
                return AudioPlaybackState.Stopped;
            }

            return voice.IsPaused ? AudioPlaybackState.Paused : AudioPlaybackState.Playing;
        }


        /// <inheritdoc />
        public void StopAll(AudioCategory category, float fadeOutDuration = 0f)
        {
            if (!IsValidCategory(category))
            {
                return;
            }

            // フェードアウト中の音も対象に含める。
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category)
                {
                    StopVoice(voice, fadeOutDuration);
                }
            }
        }


        /// <inheritdoc />
        public void PauseAll(AudioCategory category)
        {
            if (!IsValidCategory(category))
            {
                return;
            }

            // フェードアウト中の音も対象に含める。
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category)
                {
                    voice.Pause();
                }
            }
        }


        /// <inheritdoc />
        public void ResumeAll(AudioCategory category)
        {
            if (!IsValidCategory(category))
            {
                return;
            }

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned && voice.Category == category)
                {
                    voice.Resume();
                }
            }
        }


        /// <inheritdoc />
        public void SetListener(Transform listener)
        {
            _listener = listener;

            // nullが渡された場合に次の判定ですぐ自動検出できるようにする。
            _nextListenerSearchTime = 0f;
        }


        // 秒数が0より大きい場合は、フェードアウトしてから停止する。
        private static void StopVoice(AudioVoice voice, float fadeOutDuration)
        {
            if (fadeOutDuration > 0f)
            {
                // すでにフェードアウト中の音は止まる途中のため何もしない。
                // ここで即座に止めるとフェードアウトの意味がなくなる。
                if (voice.IsActive)
                {
                    voice.BeginFadeOut(fadeOutDuration);
                }

                return;
            }

            // 0以下の場合は即座に停止する。NaNも上の比較を満たさないため、ここで停止する。
            voice.Stop();
        }


        // ボイスの数だけ、AudioSourceを持つ子オブジェクトを生成する。
        private void CreateVoices()
        {
            _voices = new AudioVoice[_maxVoiceCount];

            for (int i = 0; i < _voices.Length; i++)
            {
                var voiceObject = new GameObject($"AudioVoice_{i}");
                voiceObject.transform.SetParent(transform);

                AudioSource audioSource = voiceObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;

                _voices[i] = new AudioVoice(i, audioSource);
            }
        }


        // 全ボイスのフェードの進行、終了した音の解放、追従、聞こえない距離まで離れた音の停止を行う。
        private void UpdateVoices()
        {
            // Time.timeScaleが0の間もフェードが進むように、スケールされない時間を使う。
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
                if (!voice.IsSpatial || !voice.IsActive || voice.IsPaused)
                {
                    continue;
                }

                voice.UpdateFollow();

                // 聞こえない距離まで離れた音は、フェードアウトして解放する。
                if (hasListener && voice.IsActive && voice.IsOutOfRange(listenerPosition))
                {
                    voice.BeginFadeOut(voice.Category.FadeOutDuration);
                }
            }

            RecordVoiceUsage();
        }


        // 追従させない場合は、followTargetにnullを渡す。
        private AudioPlayback PlaySpatial(SpatialAudioCue cue, Vector3 position, Transform followTarget)
        {
            if (IsTooSoon(cue))
            {
                Stats.RecordDiscard(cue, AudioDiscardReason.MinInterval);
                return AudioPlayback.None;
            }

            if (IsOutOfRange(cue, position))
            {
                Stats.RecordDiscard(cue, AudioDiscardReason.OutOfRange);
                return AudioPlayback.None;
            }

            if (!TryReserveVoice(cue, out AudioVoice voice))
            {
                return AudioPlayback.None;
            }

            voice.Play(cue, _nextStartOrder++, position, followTarget);

            return FinishPlay(cue, voice);
        }


        // 再生を開始した後の記録を行い、再生中の音を指すハンドルを返す。
        private AudioPlayback FinishPlay(AudioCueBase cue, AudioVoice voice)
        {
            // 最短間隔が0のCueは判定が不要なため、再生時刻を記録しない。
            if (cue.MinInterval > 0f)
            {
                _lastPlayTimes[cue] = Time.unscaledTimeAsDouble;
            }

            Stats.RecordPlay(cue, CountActiveVoicesOfCue(cue), CountActiveVoicesOfCategory(cue.Category));
            RecordVoiceUsage();

            return voice.CreatePlayback();
        }


        // EditorとDevelopment Build以外では、呼び出し自体が取り除かれる。
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void RecordVoiceUsage()
        {
            int assignedCount = 0;
            int fadingOutCount = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned)
                {
                    assignedCount++;
                }

                if (voice.IsFadingOut)
                {
                    fadingOutCount++;
                }
            }

            Stats.RecordVoiceUsage(assignedCount, fadingOutCount);
        }


        // 再生に必要な設定がない場合は、エラーログを出してfalseを返す。
        private static bool HasValidSettings(AudioCueBase cue)
        {
            if (cue == null)
            {
                Debug.LogError($"引数： {nameof(cue)} がnullです。");
                return false;
            }

            if (cue.AudioResource == null)
            {
                Debug.LogError($"{cue.GetType().Name}： {cue.name} の {nameof(cue.AudioResource)} が設定されていません。", cue);
                return false;
            }

            if (cue.Category == null)
            {
                Debug.LogError($"{cue.GetType().Name}： {cue.name} の {nameof(cue.Category)} が設定されていません。", cue);
                return false;
            }

            return true;
        }


        // nullの場合は、エラーログを出してfalseを返す。
        private static bool IsValidCategory(AudioCategory category)
        {
            if (category != null)
            {
                return true;
            }

            Debug.LogError($"引数： {nameof(category)} がnullです。");

            return false;
        }


        // 最大距離が最小距離より小さい場合は、エラーログを出してfalseを返す。
        private static bool HasValidDistance(SpatialAudioCue cue)
        {
            if (cue.MaxDistance >= cue.MinDistance)
            {
                return true;
            }

            Debug.LogError($"{nameof(SpatialAudioCue)}： {cue.name} の {nameof(cue.MaxDistance)} が "
                + $"{nameof(cue.MinDistance)} より小さく設定されています。", cue);

            return false;
        }


        // 前回の再生開始から、Cueの最短間隔が経過していないかどうかを返す。
        private bool IsTooSoon(AudioCueBase cue)
        {
            // 最短間隔が0のCueは判定する必要がない。
            if (cue.MinInterval <= 0f)
            {
                return false;
            }

            return _lastPlayTimes.TryGetValue(cue, out double lastPlayTime)
                && Time.unscaledTimeAsDouble - lastPlayTime < cue.MinInterval;
        }


        // リスナーが見つからない場合は判定できないため、範囲内として扱う。
        private bool IsOutOfRange(SpatialAudioCue cue, Vector3 position)
        {
            if (!TryGetListenerPosition(out Vector3 listenerPosition))
            {
                return false;
            }

            return (position - listenerPosition).sqrMagnitude > cue.MaxDistance * cue.MaxDistance;
        }


        // リスナーが未設定または破棄されている場合は、シーン内のAudioListenerを探す。
        private bool TryGetListenerPosition(out Vector3 position)
        {
            // 見つからない場合に毎フレーム探さないよう、探す間隔を空ける。
            if (_listener == null && Time.unscaledTime >= _nextListenerSearchTime)
            {
                AudioListener found = FindFirstObjectByType<AudioListener>();

                if (found != null)
                {
                    _listener = found.transform;
                }
                else
                {
                    _nextListenerSearchTime = Time.unscaledTime + ListenerSearchInterval;
                }
            }

            if (_listener == null)
            {
                position = default;
                return false;
            }

            position = _listener.position;

            return true;
        }


        // Cueとカテゴリの同時再生数の上限もここで処理する。falseの場合は、新しい音を捨てる。
        private bool TryReserveVoice(AudioCueBase cue, out AudioVoice voice)
        {
            voice = null;

            // Cueの同時再生数が上限に達している場合は、Cueの設定に従って空きを作る。
            if (CountActiveVoicesOfCue(cue) >= cue.MaxInstances && !TryMakeRoomInCue(cue))
            {
                return false;
            }

            // カテゴリの同時再生数が上限に達している場合は、カテゴリの設定に従って空きを作る。
            // Cueの上限で空きを作った場合は同じカテゴリの音も1つ減っているため、ここでは上限に達していない。
            AudioCategory category = cue.Category;
            if (CountActiveVoicesOfCategory(category) >= category.MaxPlaybackCount && !TryMakeRoomInCategory(cue))
            {
                return false;
            }

            if (!TryFindFreeVoice(out voice))
            {
                ReportNoFreeVoice(cue);
                return false;
            }

            return true;
        }


        // フェードアウト中の音は数えない。
        // Cue用とカテゴリ用の検索処理は似ているが、デリゲートで共通化するとアロケーションが発生するため、あえて分ける。
        private int CountActiveVoicesOfCue(AudioCueBase cue)
        {
            int count = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsActive && voice.Cue == cue)
                {
                    count++;
                }
            }

            return count;
        }


        // フェードアウト中の音は数えない。
        private int CountActiveVoicesOfCategory(AudioCategory category)
        {
            int count = 0;

            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsActive && voice.Category == category)
                {
                    count++;
                }
            }

            return count;
        }


        private bool TryMakeRoomInCue(AudioCueBase cue)
        {
            return TryFindOldestActiveVoiceOfCue(cue, out AudioVoice oldestVoice)
                && TryApplyOverflowPolicy(
                    cue,
                    AudioLimitScope.Cue,
                    cue.InstanceOverflowPolicy,
                    cue.Category.FadeOutDuration,
                    oldestVoice);
        }


        private bool TryMakeRoomInCategory(AudioCueBase cue)
        {
            AudioCategory category = cue.Category;

            return TryFindOldestActiveVoiceOfCategory(category, out AudioVoice oldestVoice)
                && TryApplyOverflowPolicy(
                    cue,
                    AudioLimitScope.Category,
                    category.OverflowPolicy,
                    category.FadeOutDuration,
                    oldestVoice);
        }


        // 空きを作れた場合はtrue、新しい音を捨てる場合はfalseを返す。
        private bool TryApplyOverflowPolicy(AudioCueBase cue, AudioLimitScope scope, AudioOverflowPolicy policy,
            float fadeOutDuration, AudioVoice oldestVoice)
        {
            switch (policy)
            {
                case AudioOverflowPolicy.DiscardNew:
                    Stats.RecordDiscard(cue, ToDiscardReason(scope));
                    return false;
                case AudioOverflowPolicy.StopOldest:
                    oldestVoice.Stop();
                    Stats.RecordEviction(cue, scope);
                    return true;
                case AudioOverflowPolicy.FadeOutOldest:
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
                    Stats.RecordEviction(cue, scope);
                    return true;
                default:
                    Debug.LogError($"{nameof(AudioOverflowPolicy)}： {policy} には対応していません。");
                    return false;
            }
        }


        private static AudioDiscardReason ToDiscardReason(AudioLimitScope scope)
        {
            return scope switch
            {
                AudioLimitScope.Cue => AudioDiscardReason.CueLimit,
                _ => AudioDiscardReason.CategoryLimit,
            };
        }


        private bool TryFindOldestActiveVoiceOfCue(AudioCueBase cue, out AudioVoice oldestVoice)
        {
            oldestVoice = null;

            foreach (AudioVoice voice in _voices)
            {
                if (!voice.IsActive || voice.Cue != cue)
                {
                    continue;
                }

                if (oldestVoice != null && oldestVoice.StartOrder <= voice.StartOrder)
                {
                    continue;
                }

                oldestVoice = voice;
            }

            return oldestVoice != null;
        }


        private bool TryFindOldestActiveVoiceOfCategory(AudioCategory category, out AudioVoice oldestVoice)
        {
            oldestVoice = null;

            foreach (AudioVoice voice in _voices)
            {
                if (!voice.IsActive || voice.Category != category)
                {
                    continue;
                }

                if (oldestVoice != null && oldestVoice.StartOrder <= voice.StartOrder)
                {
                    continue;
                }

                oldestVoice = voice;
            }

            return oldestVoice != null;
        }


        private bool TryFindFreeVoice(out AudioVoice freeVoice)
        {
            foreach (AudioVoice voice in _voices)
            {
                if (voice.IsAssigned)
                {
                    continue;
                }

                freeVoice = voice;
                return true;
            }

            freeVoice = null;
            return false;
        }


        // 空きボイスがなく新しい音を捨てることを、計測結果と警告ログに記録する。
        private void ReportNoFreeVoice(AudioCueBase cue)
        {
            Stats.RecordDiscard(cue, AudioDiscardReason.NoFreeVoice);

            // 弾丸のように大量の音が続く場面で毎回ログを出すとそれ自体が負荷になるため、警告は最初の1回だけにする。
            if (_hasWarnedNoFreeVoice)
            {
                return;
            }

            _hasWarnedNoFreeVoice = true;

            Debug.LogWarning($"再生できるボイスが残っていません。{nameof(_maxVoiceCount)} を増やしてください。"
                + "フェードアウト中の音もボイスを使うため、カテゴリごとの最大再生数の合計より余裕が必要です。"
                + $"警告は最初の1回のみ表示します。捨てた回数は {nameof(Stats)} で確認できます。");
        }


        // 終了した音や、別の再生に使われているボイスを指すハンドルの場合はfalseを返す。
        private bool TryGetVoice(AudioPlayback playback, out AudioVoice voice)
        {
            voice = null;

            if (!playback.IsValid || playback.VoiceIndex >= _voices.Length)
            {
                return false;
            }

            AudioVoice candidate = _voices[playback.VoiceIndex];
            if (!candidate.Matches(playback))
            {
                return false;
            }

            voice = candidate;

            return true;
        }
    }
}
