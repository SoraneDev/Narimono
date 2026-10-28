namespace Narimono
{
    /// <summary>
    /// Cueごとの再生状況の計測結果を保持する。
    /// </summary>
    public sealed class AudioCueStats
    {
        /// <summary>再生した回数。</summary>
        public int PlayCount { get; internal set; }


        /// <summary>同時再生数のピーク。フェードアウト中の音は数えない。</summary>
        public int PeakActiveCount { get; internal set; }


        /// <summary>最短間隔が経過しておらず、再生しなかった回数。</summary>
        public int MinIntervalDiscardedCount { get; internal set; }


        /// <summary>リスナーから聞こえない距離にあり、再生しなかった回数。</summary>
        public int OutOfRangeDiscardedCount { get; internal set; }


        /// <summary>Cueの上限に達して、新しい音を捨てた回数。</summary>
        public int CueLimitDiscardedCount { get; internal set; }


        /// <summary>カテゴリの上限に達して、新しい音を捨てた回数。</summary>
        public int CategoryLimitDiscardedCount { get; internal set; }


        /// <summary>空きボイスがなく、新しい音を捨てた回数。</summary>
        public int NoFreeVoiceDiscardedCount { get; internal set; }


        /// <summary>Cueの上限に達して、最も古い音を止めた、またはフェードアウトさせた回数。</summary>
        public int EvictedCount { get; internal set; }


        /// <summary>
        /// 再生しなかった理由に応じて該当する回数を増やす。
        /// </summary>
        /// <param name="reason">再生しなかった理由。</param>
        internal void RecordDiscard(AudioDiscardReason reason)
        {
            switch (reason)
            {
                case AudioDiscardReason.MinInterval:
                    MinIntervalDiscardedCount++;
                    break;
                case AudioDiscardReason.OutOfRange:
                    OutOfRangeDiscardedCount++;
                    break;
                case AudioDiscardReason.CueLimit:
                    CueLimitDiscardedCount++;
                    break;
                case AudioDiscardReason.CategoryLimit:
                    CategoryLimitDiscardedCount++;
                    break;
                case AudioDiscardReason.NoFreeVoice:
                    NoFreeVoiceDiscardedCount++;
                    break;
            }
        }
    }
}
