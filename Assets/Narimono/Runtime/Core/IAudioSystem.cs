using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// 音の再生、停止などの機能を提供するためのインターフェース
    /// </summary>
    public interface IAudioSystem
    {
        /// <summary>
        /// 2D音源を再生する。
        /// </summary>
        /// <param name="cue">再生する音源</param>
        /// <returns>
        /// 停止・一時停止に使うハンドル。
        /// 再生間隔の制限や同時再生数の上限などで再生しなかった場合は<see cref="AudioPlayback.None"/>を返す。
        /// </returns>
        AudioPlayback Play(AudioCue cue);


        /// <summary>
        /// 3D音源を再生する。
        /// </summary>
        /// <param name="cue">再生する音源</param>
        /// <param name="position">音を鳴らすワールド座標</param>
        /// <returns>
        /// 停止・一時停止に使うハンドル。
        /// リスナーから聞こえない距離にある場合や、再生間隔の制限、同時再生数の上限などで再生しなかった場合は
        /// <see cref="AudioPlayback.None"/>を返す。
        /// </returns>
        AudioPlayback Play(SpatialAudioCue cue, Vector3 position);


        /// <summary>
        /// 指定したTransformに追従させて3D音源を再生する。追従対象が破棄されると、音はフェードアウトして終了する。
        /// </summary>
        /// <param name="cue">再生する音源</param>
        /// <param name="followTarget">音を追従させるTransform</param>
        /// <returns>
        /// 停止・一時停止に使うハンドル。
        /// リスナーから聞こえない距離にある場合や、再生間隔の制限、同時再生数の上限などで再生しなかった場合は
        /// <see cref="AudioPlayback.None"/>を返す。
        /// </returns>
        AudioPlayback Play(SpatialAudioCue cue, Transform followTarget);


        /// <summary>
        /// 音を停止する。既に終了している音や無効なハンドルを渡した場合は何もしない。
        /// </summary>
        /// <param name="playback">停止する音のハンドル</param>
        /// <param name="fadeOutDuration">フェードアウトにかける秒数。0以下の場合は即座に停止する。</param>
        /// <remarks>
        /// フェードアウトを指定すると、フェードアウトの開始と同時にハンドルが無効になる。
        /// そのため、フェードアウト中の音にStop・Pause・Resumeを呼んでも何もしない。
        /// 一時停止中の音は、秒数を指定しても即座に停止する。
        /// </remarks>
        void Stop(AudioPlayback playback, float fadeOutDuration = 0f);


        /// <summary>
        /// 音を一時停止する。既に終了している音や無効なハンドルを渡した場合は何もしない。
        /// </summary>
        /// <param name="playback">一時停止する音のハンドル</param>
        void Pause(AudioPlayback playback);


        /// <summary>
        /// 音の一時停止を解除する。既に終了している音や無効なハンドルを渡した場合は何もしない。
        /// </summary>
        /// <param name="playback">一時停止を解除する音のハンドル</param>
        void Resume(AudioPlayback playback);


        /// <summary>
        /// ハンドルが指す音の状態を取得する。
        /// </summary>
        /// <param name="playback">状態を取得する音のハンドル</param>
        /// <returns>
        /// 音の状態を返す。再生が終わった音、フェードアウト中の音、無効なハンドルの場合は<see cref="AudioPlaybackState.Stopped"/>を返す。
        /// </returns>
        AudioPlaybackState GetState(AudioPlayback playback);


        /// <summary>
        /// 指定カテゴリの音をすべて停止する。
        /// </summary>
        /// <param name="category">停止する音のカテゴリ</param>
        /// <param name="fadeOutDuration">フェードアウトにかける秒数。0以下の場合は即座に停止する。</param>
        /// <remarks>
        /// 即座に停止する場合は、フェードアウト中の音も含めてすべて停止する。
        /// フェードアウトを指定した場合は、すでにフェードアウト中の音はそのまま続ける。
        /// </remarks>
        void StopAll(AudioCategory category, float fadeOutDuration = 0f);


        /// <summary>
        /// 指定カテゴリの音を、フェードアウト中の音も含めてすべて一時停止する。
        /// </summary>
        /// <param name="category">一時停止する音のカテゴリ</param>
        void PauseAll(AudioCategory category);


        /// <summary>
        /// 指定カテゴリの一時停止中の音をすべて再開する。
        /// </summary>
        /// <param name="category">再開する音のカテゴリ</param>
        void ResumeAll(AudioCategory category);


        /// <summary>
        /// 3Dサウンドの距離判定に使うリスナーを設定する。未設定の場合はシーン内のAudioListenerを自動で探す。
        /// </summary>
        /// <param name="listener">AudioListenerのTransform。nullを渡すと自動で探す状態に戻る。</param>
        void SetListener(Transform listener);
    }
}