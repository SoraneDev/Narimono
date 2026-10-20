namespace Narimono
{
    /// <summary>
    /// 同時再生数の上限が適用される範囲の列挙型
    /// </summary>
    internal enum AudioLimitScope
    {
        /// <summary>Cueごとの上限</summary>
        Cue,

        /// <summary>カテゴリごとの上限</summary>
        Category,
    }
}