using System;

namespace Shooter.Core
{
    // stealth.json は KitTuning が 1 つの型でしか覚えないので、足音と気づきの値を 1 つにまとめて読む。
    public sealed class StealthTuningSpec
    {
        public static StealthTuningSpec Default { get; } = new StealthTuningSpec(StealthNoiseSpec.Default, null);

        public StealthTuningSpec(StealthNoiseSpec noise, float? armedGainFactor)
        {
            Noise = noise ?? throw new ArgumentNullException(nameof(noise));
            if (armedGainFactor.HasValue)
            {
                // 値の検査を AwarenessSpec と 1 か所にそろえる。
                AwarenessSpec.Default.WithArmedGainFactor(armedGainFactor.Value);
            }

            ArmedGainFactor = armedGainFactor;
        }

        public StealthNoiseSpec Noise { get; }

        // 書かれていないときは null にして、敵ごとの設定 (EnemyAwarenessSettings) をそのまま使わせる。
        public float? ArmedGainFactor { get; }

        public AwarenessSpec ApplyTo(AwarenessSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return ArmedGainFactor.HasValue ? spec.WithArmedGainFactor(ArmedGainFactor.Value) : spec;
        }
    }
}
