namespace Narimono
{
    /// <summary>
    /// ハンドルが指す音の状態を表す。
    /// </summary>
    public enum AudioPlaybackState
    {
        /// <summary>停止中。</summary>
        Stopped,

        /// <summary>再生中。</summary>
        Playing,

        /// <summary>一時停止中。</summary>
        Paused,
    }
}
