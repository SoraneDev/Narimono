using System;


namespace Narimono
{
    /// <summary>
    /// 再生した音を指すハンドルを表す。
    /// </summary>
    public readonly struct AudioPlayback : IEquatable<AudioPlayback>
    {
        /// <summary>
        /// ボイスの番号と世代番号から、ハンドルを作成する。
        /// </summary>
        /// <param name="voiceIndex">再生に使われているボイスの番号。</param>
        /// <param name="generation">ハンドルを発行したときのボイスの世代番号。</param>
        internal AudioPlayback(int voiceIndex, int generation)
        {
            VoiceIndex = voiceIndex;
            Generation = generation;
        }


        /// <summary>再生できなかった場合に返される無効なハンドル。</summary>
        public static AudioPlayback None => default;


        /// <summary>有効なハンドルかどうか。ハンドルが指す音が再生中かどうかは表さない。</summary>
        public bool IsValid => Generation != 0;


        /// <summary>再生に使われているボイスの番号。</summary>
        internal int VoiceIndex { get; }


        /// <summary>ハンドルを発行したときのボイスの世代番号。0は無効なハンドルを表す。</summary>
        internal int Generation { get; }


        /// <summary>
        /// 同じ再生を指すハンドルかどうかを判定する。
        /// </summary>
        /// <param name="other">比較するハンドル。</param>
        /// <returns>ボイスの番号と世代番号が両方とも一致する場合はtrue、そうでない場合はfalseを返す。</returns>
        public bool Equals(AudioPlayback other)
        {
            return VoiceIndex == other.VoiceIndex && Generation == other.Generation;
        }


        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is AudioPlayback other && Equals(other);
        }


        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(VoiceIndex, Generation);
        }


        /// <summary>
        /// 2つのハンドルが同じ再生を指しているかどうかを判定する。
        /// </summary>
        /// <param name="left">比較するハンドル。</param>
        /// <param name="right">比較するハンドル。</param>
        /// <returns>同じ再生を指している場合はtrue、そうでない場合はfalseを返す。</returns>
        public static bool operator ==(AudioPlayback left, AudioPlayback right)
        {
            return left.Equals(right);
        }


        /// <summary>
        /// 2つのハンドルが別の再生を指しているかどうかを判定する。
        /// </summary>
        /// <param name="left">比較するハンドル。</param>
        /// <param name="right">比較するハンドル。</param>
        /// <returns>別の再生を指している場合はtrue、同じ再生を指している場合はfalseを返す。</returns>
        public static bool operator !=(AudioPlayback left, AudioPlayback right)
        {
            return !left.Equals(right);
        }
    }
}
