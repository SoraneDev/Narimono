namespace Narimono
{
    /// <summary>
    /// 同時再生数の上限に達したときの挙動を表す。
    /// </summary>
    public enum AudioOverflowPolicy
    {
        /// <summary>新しく再生しようとした音を捨てる。</summary>
        DiscardNew,

        /// <summary>上限に達したカテゴリ内、またはCue内で最も古い音を即座に止めて、新しい音を再生する。</summary>
        StopOldest,

        /// <summary>上限に達したカテゴリ内、またはCue内で最も古い音をフェードアウトさせて、新しい音を再生する。</summary>
        FadeOutOldest,
    }
}
