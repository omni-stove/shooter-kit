using System;

namespace Shooter.Core
{
    // stealth.json は KitTuning が 1 つの型でしか覚えないので、足音と気づきの値を 1 つにまとめて読む。
    public sealed class StealthTuningSpec
    {
        public static StealthTuningSpec Default { get; } = new StealthTuningSpec(StealthNoiseSpec.Default, null);

        public StealthTuningSpec(StealthNoiseSpec noise, float? armedGainFactor)
            : this(noise, armedGainFactor, null, null, null)
        {
        }

        public StealthTuningSpec(StealthNoiseSpec noise, float? armedGainFactor, float? litGainFactor)
            : this(noise, armedGainFactor, litGainFactor, null, null)
        {
        }

        public StealthTuningSpec(StealthNoiseSpec noise, float? armedGainFactor, SecurityCameraSpec camera, SecurityAlarmSpec alarm)
            : this(noise, armedGainFactor, null, camera, alarm)
        {
        }

        // カメラと警報を省いたら既定値にするのは、stealth.json にその項目を書かないゲームでも同じ値で動かすため。
        public StealthTuningSpec(
            StealthNoiseSpec noise, float? armedGainFactor, float? litGainFactor, SecurityCameraSpec camera, SecurityAlarmSpec alarm)
        {
            Noise = noise ?? throw new ArgumentNullException(nameof(noise));
            Camera = camera ?? SecurityCameraSpec.Default;
            Alarm = alarm ?? SecurityAlarmSpec.Default;

            // 値の検査を AwarenessSpec と 1 か所にそろえる。
            if (armedGainFactor.HasValue)
            {
                AwarenessSpec.Default.WithArmedGainFactor(armedGainFactor.Value);
            }

            if (litGainFactor.HasValue)
            {
                AwarenessSpec.Default.WithLitGainFactor(litGainFactor.Value);
            }

            ArmedGainFactor = armedGainFactor;
            LitGainFactor = litGainFactor;
        }

        public StealthNoiseSpec Noise { get; }

        // 倍率は書かれていないときは null にして、敵ごとの設定 (EnemyAwarenessSettings) をそのまま使わせる。
        public float? ArmedGainFactor { get; }

        public float? LitGainFactor { get; }

        public SecurityCameraSpec Camera { get; }

        public SecurityAlarmSpec Alarm { get; }

        public AwarenessSpec ApplyTo(AwarenessSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var applied = ArmedGainFactor.HasValue ? spec.WithArmedGainFactor(ArmedGainFactor.Value) : spec;
            return LitGainFactor.HasValue ? applied.WithLitGainFactor(LitGainFactor.Value) : applied;
        }
    }
}
