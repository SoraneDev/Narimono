using UnityEngine;
using UnityEngine.Audio;


namespace Narimono
{
    /// <summary>
    /// 音源のカテゴリごとの再生設定を持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCategory", menuName = "Narimono/Audio Category")]
    public sealed class AudioCategory : ScriptableObject
    {
        [SerializeField] private AudioMixerGroup _audioMixerGroup;
        [SerializeField] [Min(1)] [Tooltip("このカテゴリの同時再生数の上限")]
        private int _maxPlaybackCount = 1;
        [SerializeField] [Tooltip("このカテゴリの同時再生数の上限に達したときの挙動")]
        private AudioOverflowPolicy _overflowPolicy;
        [SerializeField] [Min(0f)] [Tooltip("音を自動でフェードアウトさせるときにかける秒数")]
        private float _fadeOutDuration = 0.2f;


        /// <summary>オーディオミキサーグループ。</summary>
        public AudioMixerGroup AudioMixerGroup => _audioMixerGroup;


        /// <summary>このカテゴリの同時再生数の上限。</summary>
        public int MaxPlaybackCount => _maxPlaybackCount;


        /// <summary>このカテゴリの同時再生数の上限に達したときの挙動。</summary>
        public AudioOverflowPolicy OverflowPolicy => _overflowPolicy;


        /// <summary>音を自動でフェードアウトさせるときにかける秒数。</summary>
        public float FadeOutDuration => _fadeOutDuration;
    }
}
