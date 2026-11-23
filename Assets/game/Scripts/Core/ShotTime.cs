using System;

namespace Shooter.Core
{
    // ネットワークから受け取った値をそのまま入れられるよう、コンストラクタでは検査しない (IsValid で確かめる)。
    public readonly struct ShotTime
    {
        private const float _largestAlpha = 0.99999994f;

        public ShotTime(int tick, float alpha)
        {
            Tick = tick;
            Alpha = alpha;
        }

        public int Tick { get; }

        public float Alpha { get; }

        public bool IsValid => Tick >= 0 && Alpha >= 0f && Alpha < 1f;

        // 端数を float にすると 1 に丸まることがあり、1 は IsValid が断るので、1 未満で最大の float にする。
        public static ShotTime FromTick(double tick)
        {
            var whole = Math.Floor(tick);
            var alpha = (float)(tick - whole);
            return new ShotTime((int)whole, alpha < 1f ? alpha : _largestAlpha);
        }

        // float で返すと長く動かしたサーバーの tick (2^23 以上) で Alpha が消える。
        public double ToTick()
        {
            return Tick + (double)Alpha;
        }
    }
}
