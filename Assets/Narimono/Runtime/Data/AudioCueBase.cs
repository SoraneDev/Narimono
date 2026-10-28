using UnityEngine;
using UnityEngine.Audio;


namespace Narimono
{
    /// <summary>
    /// Cueの基底クラス。
    /// 2Dと3Dで共通する再生設定を持つ。
    /// </summary>
    public abstract class AudioCueBase : ScriptableObject
    {
        [SerializeField] private AudioResource _audioResource;
        [SerializeField] private AudioCategory _category;
        [SerializeField] private bool _loop;
        [SerializeField] [Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] [Range(-3f, 3f)] private float _pitch = 1f;
        [SerializeField] [Min(0f)] [Tooltip("再生開始時のフェードインにかける秒数")]
        private float _fadeInDuration;
        [SerializeField] [Min(0f)] [Tooltip("前回の再生開始から、同じCueを再び再生できるようになるまでの最短の秒数")]
        private float _minInterval;
        [SerializeField] [Tooltip("このCueの同時再生数に上限を設けるかどうか")]
        private bool _limitInstances;
        [SerializeField] [Min(1)] [Tooltip("このCueの同時再生数の上限")]
        private int _maxInstances = 1;
        [SerializeField] [Tooltip("このCueの同時再生数の上限に達したときの挙動")]
        private AudioOverflowPolicy _instanceOverflowPolicy;


        /// <summary>
        /// 派生クラスを作れる範囲を、このアセンブリの中に限定する。
        /// </summary>
        /// <remarks>Cueの種類は2Dの<see cref="AudioCue" />と3Dの<see cref="SpatialAudioCue" />だけにするため、internalにする。</remarks>
        internal AudioCueBase()
        {
        }


        /// <summary>音源（<see cref="AudioClip" />または<see cref="AudioRandomContainer" />）。</summary>
        public AudioResource AudioResource => _audioResource;


        /// <summary>所属カテゴリ。</summary>
        public AudioCategory Category => _category;


        /// <summary>ループ再生するかどうか。</summary>
        public bool Loop => _loop;


        /// <summary>音量。</summary>
        public float Volume => _volume;


        /// <summary>ピッチ。</summary>
        public float Pitch => _pitch;


        /// <summary>再生開始時のフェードインにかける秒数。</summary>
        public float FadeInDuration => _fadeInDuration;


        /// <summary>前回の再生開始から、同じCueを再び再生できるようになるまでの最短の秒数。</summary>
        public float MinInterval => _minInterval;


        /// <summary>このCueの同時再生数の上限。上限を設けない場合は<see cref="int.MaxValue" />。</summary>
        public int MaxInstances => _limitInstances ? _maxInstances : int.MaxValue;


        /// <summary>このCueの同時再生数の上限に達したときの挙動。</summary>
        public AudioOverflowPolicy InstanceOverflowPolicy => _instanceOverflowPolicy;
    }
}
