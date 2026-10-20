using System;


namespace Narimono
{
    /// <summary>
    /// 再生中の音を指すハンドル
    /// </summary>
    public readonly struct AudioPlayback : IEquatable<AudioPlayback>
    {
        /// <summary>再生できなかった場合に返される無効なハンドル</summary>
        public static AudioPlayback None => default;


        /// <summary>再生に使われているボイスの番号</summary>
        internal int VoiceIndex { get; }


        /// <summary>ハンドルを発行したときのボイスの世代番号。0は無効なハンドルを表す。</summary>
        internal int Generation { get; }


        /// <summary>有効なハンドルかどうか。ハンドルが指す音が再生中かどうかは表さない。</summary>
        public bool IsValid => Generation != 0;


        internal AudioPlayback(int voiceIndex, int generation)
        {
            VoiceIndex = voiceIndex;
            Generation = generation;
        }


        /// <summary>
        /// 同じ再生を指すハンドルかどうかを判定する。
        /// </summary>
        /// <param name="other">比較するハンドル</param>
        /// <returns>ボイスの番号と世代番号が両方とも一致する場合はtrueを返す。</returns>
        public bool Equals(AudioPlayback other)
        {
            return VoiceIndex == other.VoiceIndex && Generation == other.Generation;
        }


        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is AudioPlayback other && Equals(other);
        }


        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(VoiceIndex, Generation);
        }


        public static bool operator ==(AudioPlayback left, AudioPlayback right) => left.Equals(right);


        public static bool operator !=(AudioPlayback left, AudioPlayback right) => !left.Equals(right);
    }
}