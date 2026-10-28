using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// <see cref="AudioSource" />と再生状態をまとめた、同時再生の1枠を表す。
    /// </summary>
    internal sealed class AudioVoice
    {
        private readonly AudioSource _audioSource;
        private readonly Transform _transform;
        private AudioVolumeFade _fade;
        private Transform _followTarget;
        private bool _isFollowing;
        private float _cullSqrDistance;


        /// <summary>
        /// 再生に使う<see cref="AudioSource" />を受け取り、空き状態のボイスを作成する。
        /// </summary>
        /// <param name="index">ボイスの番号。</param>
        /// <param name="audioSource">再生に使う<see cref="AudioSource" />。</param>
        public AudioVoice(int index, AudioSource audioSource)
        {
            Index = index;
            _audioSource = audioSource;
            _transform = audioSource.transform;
        }


        /// <summary>ボイスの番号。</summary>
        public int Index { get; }


        /// <summary>再生を開始するたびと、フェードアウトを開始するたびに進む世代番号。</summary>
        /// <remarks>
        /// ボイスは再利用されるため、ハンドルが指す再生と現在の再生を世代番号で区別する。
        /// 0は一度も再生していないことを表す。
        /// </remarks>
        public int Generation { get; private set; }


        /// <summary>音が割り当てられているかどうか。一時停止中・フェードアウト中も割り当て済みとして扱う。</summary>
        public bool IsAssigned { get; private set; }


        /// <summary>一時停止中かどうか。</summary>
        public bool IsPaused { get; private set; }


        /// <summary>フェードアウト中かどうか。</summary>
        public bool IsFadingOut { get; private set; }


        /// <summary>3Dサウンドとして再生しているかどうか。</summary>
        public bool IsSpatial { get; private set; }


        /// <summary>割り当てられているCue。割り当てられていない場合はnull。</summary>
        public AudioCueBase Cue { get; private set; }


        /// <summary>割り当てられている音のカテゴリ。割り当てられていない場合はnull。</summary>
        public AudioCategory Category { get; private set; }


        /// <summary>再生を開始した順番。小さいほど古い。</summary>
        public long StartOrder { get; private set; }


        /// <summary>割り当て済みで、フェードアウト中でもないかどうか。</summary>
        public bool IsActive => IsAssigned && !IsFadingOut;


        /// <summary>再生が自然に終了したかどうか。</summary>
        /// <remarks><see cref="AudioSource.isPlaying" />は一時停止中もfalseになるため、一時停止中は終了とみなさない。</remarks>
        public bool IsFinished => IsAssigned && !IsPaused && !_audioSource.isPlaying;


        /// <summary>
        /// 位置を持たない2D音源を割り当てて再生を開始する。
        /// </summary>
        /// <param name="cue">再生する音源。</param>
        /// <param name="startOrder">再生開始の順番。</param>
        /// <remarks>空きボイスに対して呼ぶこと。前回の再生の状態は<see cref="Release" />で初期化済みであることを前提とする。</remarks>
        public void Play(AudioCue cue, long startOrder)
        {
            Assign(cue, startOrder);

            // ボイスは再利用されるため、前回の3D設定が残らないように毎回設定し直す。
            _audioSource.spatialBlend = 0f;

            _audioSource.Play();
        }


        /// <summary>
        /// 位置を持つ3D音源を割り当てて再生を開始する。
        /// </summary>
        /// <param name="cue">再生する音源。</param>
        /// <param name="startOrder">再生開始の順番。</param>
        /// <param name="position">音を鳴らすワールド座標。</param>
        /// <param name="followTarget">追従させる<see cref="Transform" />。追従させない場合はnull。</param>
        /// <remarks>空きボイスに対して呼ぶこと。前回の再生の状態は<see cref="Release" />で初期化済みであることを前提とする。</remarks>
        public void Play(SpatialAudioCue cue, long startOrder, Vector3 position, Transform followTarget)
        {
            Assign(cue, startOrder);

            IsSpatial = true;
            _followTarget = followTarget;
            _isFollowing = followTarget != null;
            _cullSqrDistance = cue.MaxDistance * cue.MaxDistance;

            _transform.position = position;
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = ToRolloffMode(cue.Rolloff);
            _audioSource.minDistance = cue.MinDistance;
            _audioSource.maxDistance = cue.MaxDistance;

            // 弾丸のような高速な音源でピッチが変化しないように、ドップラー効果は使わない。
            _audioSource.dopplerLevel = 0f;

            _audioSource.Play();
        }


        /// <summary>
        /// 再生を停止して、ボイスを空き状態に戻す。
        /// </summary>
        public void Stop()
        {
            _audioSource.Stop();
            Release();
        }


        /// <summary>
        /// 一時停止する。すでに一時停止中の場合は何もしない。
        /// </summary>
        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _audioSource.Pause();
            IsPaused = true;
        }


        /// <summary>
        /// 一時停止を解除する。一時停止中でない場合は何もしない。
        /// </summary>
        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            _audioSource.UnPause();
            IsPaused = false;
        }


        /// <summary>
        /// 指定した秒数でフェードアウトできるかどうかを判定する。
        /// </summary>
        /// <param name="duration">フェードアウトにかける秒数。</param>
        /// <returns>
        /// フェードアウトできる場合はtrueを返す。
        /// 一時停止中の場合と秒数が0以下の場合は、フェードする意味がないためfalseを返す。
        /// </returns>
        public bool CanFadeOut(float duration)
        {
            // NaNも比較を満たさないため、フェードアウトできないものとして扱う。
            return !IsPaused && duration > 0f;
        }


        /// <summary>
        /// フェードアウトを開始する。フェードアウトが終わると、ボイスは空き状態に戻る。
        /// </summary>
        /// <param name="duration">フェードアウトにかける秒数。</param>
        /// <remarks>
        /// 世代番号を進めるため、再生開始時に発行したハンドルとは一致しなくなる。
        /// <see cref="CanFadeOut" />がfalseになる場合は即座に停止する。
        /// </remarks>
        public void BeginFadeOut(float duration)
        {
            if (!CanFadeOut(duration))
            {
                Stop();
                return;
            }

            Generation++;
            IsFadingOut = true;

            // フェードイン中の場合も、その時点の音量から下げる。
            _fade.Begin(_audioSource.volume, 0f, duration);
        }


        /// <summary>
        /// フェードインまたはフェードアウトを進める。フェードアウトが終わった場合は停止する。
        /// </summary>
        /// <param name="deltaTime">前回からの経過秒数。</param>
        /// <remarks>フェード中でない場合と一時停止中の場合は何もしない。</remarks>
        public void UpdateFade(float deltaTime)
        {
            if (!_fade.IsRunning || IsPaused)
            {
                return;
            }

            _audioSource.volume = _fade.Advance(deltaTime);

            if (IsFadingOut && !_fade.IsRunning)
            {
                Stop();
            }
        }


        /// <summary>
        /// 追従対象の位置にボイスを移動する。追従対象が破棄されていた場合は、フェードアウトを開始する。
        /// </summary>
        /// <remarks>追従していない場合や、フェードアウト中の場合は何もしない。</remarks>
        public void UpdateFollow()
        {
            if (!_isFollowing || !IsActive)
            {
                return;
            }

            // Unityのオブジェクトは破棄されるとnullと等価になる。
            // 最初から追従していない場合と区別するため、追従中かどうかは_isFollowingで別に持つ。
            if (_followTarget == null)
            {
                BeginFadeOut(Category.FadeOutDuration);
                return;
            }

            _transform.position = _followTarget.position;
        }


        /// <summary>
        /// リスナーから聞こえなくなる距離を超えて離れているかどうかを判定する。
        /// </summary>
        /// <param name="listenerPosition">リスナーのワールド座標。</param>
        /// <returns>3Dサウンドで、かつ最大距離を超えて離れている場合はtrue、そうでない場合はfalseを返す。</returns>
        public bool IsOutOfRange(Vector3 listenerPosition)
        {
            return IsSpatial && (_transform.position - listenerPosition).sqrMagnitude > _cullSqrDistance;
        }


        /// <summary>
        /// ボイスを空き状態に戻す。<see cref="AudioSource" />の再生自体は止めない。
        /// </summary>
        public void Release()
        {
            IsAssigned = false;
            IsPaused = false;
            IsFadingOut = false;
            IsSpatial = false;
            Cue = null;
            Category = null;
            _fade.Cancel();
            _followTarget = null;
            _isFollowing = false;
        }


        /// <summary>
        /// ハンドルがこのボイスの現在の再生を指しているかどうかを判定する。
        /// </summary>
        /// <param name="playback">判定するハンドル。</param>
        /// <returns>現在の再生を指している場合はtrue、終了した音・フェードアウト中の音・別の再生を指している場合はfalseを返す。</returns>
        public bool Matches(AudioPlayback playback)
        {
            return IsAssigned && Index == playback.VoiceIndex && Generation == playback.Generation;
        }


        /// <summary>
        /// 現在の再生を指すハンドルを生成する。
        /// </summary>
        public AudioPlayback CreatePlayback()
        {
            return new AudioPlayback(Index, Generation);
        }


        // 2Dと3Dで共通する、再生開始時の状態とAudioSourceの設定を行う。
        private void Assign(AudioCueBase cue, long startOrder)
        {
            Generation++;
            IsAssigned = true;
            IsPaused = false;
            IsFadingOut = false;
            Cue = cue;
            Category = cue.Category;
            StartOrder = startOrder;

            _audioSource.resource = cue.AudioResource;
            _audioSource.outputAudioMixerGroup = cue.Category.AudioMixerGroup;
            _audioSource.loop = cue.Loop;
            _audioSource.pitch = cue.Pitch;

            // フェードインする場合は無音から始める。
            if (cue.FadeInDuration > 0f)
            {
                _audioSource.volume = 0f;
                _fade.Begin(0f, cue.Volume, cue.FadeInDuration);
            }
            else
            {
                _audioSource.volume = cue.Volume;
            }
        }


        private static AudioRolloffMode ToRolloffMode(AudioRolloff rolloff)
        {
            return rolloff switch
            {
                AudioRolloff.Linear => AudioRolloffMode.Linear,
                _ => AudioRolloffMode.Logarithmic,
            };
        }
    }
}
