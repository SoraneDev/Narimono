using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// 再生状況の計測結果。上限の設定を調整するために使う。
    /// </summary>
    /// <remarks>
    /// 計測はEditorとDevelopment Buildでのみ行う。それ以外のビルドでは記録処理の呼び出し自体がコンパイル時に取り除かれる。
    /// </remarks>
    public sealed class AudioStats
    {
        private readonly Dictionary<AudioCategory, AudioCategoryStats> _categoryStats = new();
        private readonly Dictionary<AudioCueBase, AudioCueStats> _cueStats = new();


        internal AudioStats(int maxVoiceCount)
        {
            MaxVoiceCount = maxVoiceCount;
        }


        /// <summary>ボイスの総数</summary>
        public int MaxVoiceCount { get; }


        /// <summary>使用中のボイス数のピーク。フェードアウト中のボイスも含む。</summary>
        public int PeakAssignedVoiceCount { get; private set; }


        /// <summary>フェードアウト中のボイス数のピーク。ボイス数の余裕（ヘッドルーム）の見積もりに使う。</summary>
        public int PeakFadingOutVoiceCount { get; private set; }


        /// <summary>空きボイスがなく新しい音を捨てた回数。0でない場合はボイスの総数が足りない。</summary>
        public int NoFreeVoiceCount { get; private set; }


        /// <summary>カテゴリごとの計測結果</summary>
        public IReadOnlyDictionary<AudioCategory, AudioCategoryStats> Categories => _categoryStats;


        /// <summary>Cueごとの計測結果</summary>
        public IReadOnlyDictionary<AudioCueBase, AudioCueStats> Cues => _cueStats;


        /// <summary>
        /// 計測結果をすべて消す。調整する場面だけを計測し直すときに使う。
        /// </summary>
        public void Clear()
        {
            PeakAssignedVoiceCount = 0;
            PeakFadingOutVoiceCount = 0;
            NoFreeVoiceCount = 0;

            _categoryStats.Clear();
            _cueStats.Clear();
        }


        /// <summary>
        /// 計測結果を読みやすい文字列にまとめる。
        /// </summary>
        public string BuildReport()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var builder = new StringBuilder();

            builder.AppendLine("[Narimono Audio Stats]");
            builder.AppendLine($"Voices: peak {PeakAssignedVoiceCount} / {MaxVoiceCount} " +
                               $"(fading-out peak {PeakFadingOutVoiceCount}), discarded by no free voice: {NoFreeVoiceCount}");

            builder.AppendLine();
            builder.AppendLine("Categories (peak active / max, discarded by limit, evicted by limit)");

            foreach (KeyValuePair<AudioCategory, AudioCategoryStats> pair in _categoryStats)
            {
                AudioCategoryStats stats = pair.Value;

                builder.AppendLine($"  {pair.Key.name}: {stats.PeakActiveCount} / {pair.Key.MaxPlaybackCount}, " +
                                   $"discarded {stats.DiscardedCount}, evicted {stats.EvictedCount}");
            }

            builder.AppendLine();
            builder.AppendLine("Cues (plays, peak active / max, discarded: interval / range / cue limit / category limit / no voice, evicted)");

            foreach (KeyValuePair<AudioCueBase, AudioCueStats> pair in _cueStats)
            {
                AudioCueStats stats = pair.Value;
                int maxInstances = pair.Key.MaxInstances;
                string maxInstancesText = maxInstances == int.MaxValue ? "-" : maxInstances.ToString();

                builder.AppendLine($"  {pair.Key.name}: plays {stats.PlayCount}, peak {stats.PeakActiveCount} / {maxInstancesText}, " +
                                   $"discarded {stats.MinIntervalDiscardedCount} / {stats.OutOfRangeDiscardedCount} / " +
                                   $"{stats.CueLimitDiscardedCount} / {stats.CategoryLimitDiscardedCount} / {stats.NoFreeVoiceDiscardedCount}, " +
                                   $"evicted {stats.EvictedCount}");
            }

            return builder.ToString();
#else
            return "Audio stats are only recorded in the Editor and Development Builds.";
#endif
        }


        /// <summary>
        /// 音を再生したことを記録する。
        /// </summary>
        /// <param name="cue">再生した音</param>
        /// <param name="cueActiveCount">再生後のCueの同時再生数</param>
        /// <param name="categoryActiveCount">再生後のカテゴリの同時再生数</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void RecordPlay(AudioCueBase cue, int cueActiveCount, int categoryActiveCount)
        {
            AudioCueStats cueStats = GetCueStats(cue);
            cueStats.PlayCount++;
            cueStats.PeakActiveCount = Mathf.Max(cueStats.PeakActiveCount, cueActiveCount);

            AudioCategoryStats categoryStats = GetCategoryStats(cue.Category);
            categoryStats.PeakActiveCount = Mathf.Max(categoryStats.PeakActiveCount, categoryActiveCount);
        }


        /// <summary>
        /// 新しい音を再生しなかったことを記録する。
        /// </summary>
        /// <param name="cue">再生しなかった音</param>
        /// <param name="reason">再生しなかった理由</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void RecordDiscard(AudioCueBase cue, AudioDiscardReason reason)
        {
            GetCueStats(cue).RecordDiscard(reason);

            // それ以外の理由はCueごとの回数だけを数える。
            switch (reason)
            {
                case AudioDiscardReason.CategoryLimit:
                    GetCategoryStats(cue.Category).DiscardedCount++;
                    break;

                case AudioDiscardReason.NoFreeVoice:
                    NoFreeVoiceCount++;
                    break;
            }
        }


        /// <summary>
        /// 上限に達して古い音を止めた、またはフェードアウトさせたことを記録する。
        /// </summary>
        /// <param name="cue">新しく再生しようとした音</param>
        /// <param name="scope">どの範囲の上限に達していたか</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void RecordEviction(AudioCueBase cue, AudioLimitScope scope)
        {
            switch (scope)
            {
                case AudioLimitScope.Cue:
                    GetCueStats(cue).EvictedCount++;
                    break;

                case AudioLimitScope.Category:
                    GetCategoryStats(cue.Category).EvictedCount++;
                    break;
            }
        }


        /// <summary>
        /// ボイスの使用状況を記録する。
        /// </summary>
        /// <param name="assignedCount">使用中のボイス数。フェードアウト中のボイスも含む。</param>
        /// <param name="fadingOutCount">フェードアウト中のボイス数</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void RecordVoiceUsage(int assignedCount, int fadingOutCount)
        {
            PeakAssignedVoiceCount = Mathf.Max(PeakAssignedVoiceCount, assignedCount);
            PeakFadingOutVoiceCount = Mathf.Max(PeakFadingOutVoiceCount, fadingOutCount);
        }


        /// <summary>
        /// Cueの計測結果を取得する。まだない場合は作成する。
        /// </summary>
        private AudioCueStats GetCueStats(AudioCueBase cue)
        {
            if (!_cueStats.TryGetValue(cue, out AudioCueStats stats))
            {
                stats = new AudioCueStats();
                _cueStats.Add(cue, stats);
            }

            return stats;
        }


        /// <summary>
        /// カテゴリの計測結果を取得する。まだない場合は作成する。
        /// </summary>
        private AudioCategoryStats GetCategoryStats(AudioCategory category)
        {
            if (!_categoryStats.TryGetValue(category, out AudioCategoryStats stats))
            {
                stats = new AudioCategoryStats();
                _categoryStats.Add(category, stats);
            }

            return stats;
        }
    }
}