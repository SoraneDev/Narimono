namespace Narimono
{
    /// <summary>
    /// 3Dサウンドの距離による音量の減衰方式の列挙型
    /// </summary>
    public enum AudioRolloff
    {
        /// <summary>対数減衰。現実の音に近いが、最大距離を超えても音量が0にならない。</summary>
        Logarithmic,

        /// <summary>線形減衰。最小距離から最大距離にかけて、音量が直線的に0まで下がる。</summary>
        Linear,
    }
}