using UnityEngine;
using UnityEngine.Audio;


namespace Narimono
{
    /// <summary>
    /// 音源の再生設定のうち、2Dと3Dで共通する部分を持つスクリプタブルオブジェクトの基底クラス
    /// </summary>
    public abstract class AudioCueBase : ScriptableObject
    {
        [SerializeField] private AudioResource _audioResource;
        [SerializeField] private AudioCategory _category;
        [SerializeField] private bool _loop;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float _pitch = 1f;
        [SerializeField, Min(0f), Tooltip("秒")] private float _fadeInDuration;
        [SerializeField, Min(0f), Tooltip("秒")] private float _minInterval;
        [SerializeField] private bool _limitInstances;
        [SerializeField, Min(1)] private int _maxInstances = 1;
        [SerializeField] private AudioOverflowPolicy _instanceOverflowPolicy;


        // 2DのAudioCueと3DのSpatialAudioCue以外の派生を、アセンブリ外から作れないようにするためinternalにする。
        internal AudioCueBase()
        {
        }


        /// <summary>音源（AudioClipまたはAudioRandomContainer）</summary>
        public AudioResource AudioResource => _audioResource;


        /// <summary>所属カテゴリ</summary>
        public AudioCategory Category => _category;


        /// <summary>ループ再生するかどうか。</summary>
        public bool Loop => _loop;


        /// <summary>音量</summary>
        public float Volume => _volume;


        /// <summary>ピッチ</summary>
        public float Pitch => _pitch;


        /// <summary>再生開始時のフェードインにかける秒数</summary>
        public float FadeInDuration => _fadeInDuration;


        /// <summary>同じCueを、前回の再生開始から再び再生できるようになるまでの最短間隔（秒）</summary>
        public float MinInterval => _minInterval;


        /// <summary>このCueの同時再生数の上限</summary>
        public int MaxInstances => _limitInstances ? _maxInstances : int.MaxValue;


        /// <summary>このCueの同時再生数の上限に達したときの挙動</summary>
        public AudioOverflowPolicy InstanceOverflowPolicy => _instanceOverflowPolicy;
    }
}