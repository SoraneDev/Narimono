using UnityEngine;
using UnityEngine.Audio;


namespace Narimono
{
    /// <summary>
    /// 音源のカテゴリごとの再生設定を持つスクリプタブルオブジェクト
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCategory", menuName = "Narimono/Audio Category")]
    public sealed class AudioCategory : ScriptableObject
    {
        [SerializeField] private AudioMixerGroup _audioMixerGroup;
        [SerializeField, Min(1)] private int _maxPlaybackCount = 1;
        [SerializeField] private AudioOverflowPolicy _overflowPolicy;
        [SerializeField, Min(0f), Tooltip("秒")] private float _fadeOutDuration = 0.2f;


        /// <summary>オーディオミキサーグループ</summary>
        public AudioMixerGroup AudioMixerGroup => _audioMixerGroup;


        /// <summary>最大同時再生数</summary>
        public int MaxPlaybackCount => _maxPlaybackCount;


        /// <summary>同時再生数の上限に達したときの挙動</summary>
        public AudioOverflowPolicy OverflowPolicy => _overflowPolicy;


        /// <summary>音をフェードアウトで止めるときにかける秒数</summary>
        public float FadeOutDuration => _fadeOutDuration;
    }
}