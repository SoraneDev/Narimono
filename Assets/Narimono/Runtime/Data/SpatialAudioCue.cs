using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// 3D音源の再生設定を持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialAudioCue", menuName = "Narimono/Spatial Audio Cue")]
    public sealed class SpatialAudioCue : AudioCueBase
    {
        [SerializeField] private AudioRolloff _rolloff;
        [SerializeField] [Min(0.01f)] [Tooltip("音量が減衰を始める距離")]
        private float _minDistance = 1f;
        [SerializeField] [Min(0.01f)] [Tooltip("音が聞こえなくなる距離")]
        private float _maxDistance = 30f;


        /// <summary>距離による音量の減衰方式。</summary>
        public AudioRolloff Rolloff => _rolloff;


        /// <summary>音量が減衰を始める距離。</summary>
        public float MinDistance => _minDistance;


        /// <summary>音が聞こえなくなる距離。</summary>
        public float MaxDistance => _maxDistance;
    }
}
