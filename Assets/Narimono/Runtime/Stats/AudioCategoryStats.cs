namespace Narimono
{
    /// <summary>
    /// カテゴリごとの再生状況の計測結果を保持する。
    /// </summary>
    public sealed class AudioCategoryStats
    {
        /// <summary>同時再生数のピーク。フェードアウト中の音は数えない。</summary>
        public int PeakActiveCount { get; internal set; }


        /// <summary>カテゴリの上限に達して、新しい音を捨てた回数。</summary>
        public int DiscardedCount { get; internal set; }


        /// <summary>カテゴリの上限に達して、最も古い音を止めた、またはフェードアウトさせた回数。</summary>
        public int EvictedCount { get; internal set; }
    }
}
