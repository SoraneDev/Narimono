using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// 音量を一定時間かけて目標の値まで変化させる構造体
    /// </summary>
    internal struct AudioVolumeFade
    {
        private float _fromVolume;
        private float _toVolume;
        private float _duration;
        private float _elapsed;


        /// <summary>音量を変化させている途中かどうか。</summary>
        public bool IsRunning { get; private set; }


        /// <summary>
        /// 音量の変化を開始する。
        /// </summary>
        /// <param name="fromVolume">開始時の音量</param>
        /// <param name="toVolume">目標の音量</param>
        /// <param name="duration">変化にかける秒数。0より大きい値</param>
        public void Begin(float fromVolume, float toVolume, float duration)
        {
            _fromVolume = fromVolume;
            _toVolume = toVolume;
            _duration = duration;
            _elapsed = 0f;
            IsRunning = true;
        }


        /// <summary>
        /// 音量の変化を進める。目標の音量に達すると変化を終える。
        /// </summary>
        /// <param name="deltaTime">前回からの経過秒数</param>
        /// <returns>進めた後の音量を返す。</returns>
        public float Advance(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed >= _duration)
            {
                IsRunning = false;
                return _toVolume;
            }

            return Mathf.Lerp(_fromVolume, _toVolume, _elapsed / _duration);
        }


        /// <summary>
        /// 音量の変化を途中でやめる。
        /// </summary>
        public void Cancel()
        {
            IsRunning = false;
        }
    }
}