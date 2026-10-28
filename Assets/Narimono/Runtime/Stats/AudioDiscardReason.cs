namespace Narimono
{
    /// <summary>
    /// 新しい音を再生しなかった理由を表す。
    /// </summary>
    internal enum AudioDiscardReason
    {
        /// <summary>前回の再生開始から、Cueの最短間隔が経過していなかった。</summary>
        MinInterval,

        /// <summary>リスナーから聞こえない距離にあった。</summary>
        OutOfRange,

        /// <summary>Cueの同時再生数の上限に達していた。</summary>
        CueLimit,

        /// <summary>カテゴリの同時再生数の上限に達していた。</summary>
        CategoryLimit,

        /// <summary>空きボイスがなかった。</summary>
        NoFreeVoice,
    }
}
