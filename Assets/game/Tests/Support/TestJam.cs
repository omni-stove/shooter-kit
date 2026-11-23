using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public static class TestJam
    {
        public const float ClearSeconds = 1f;

        public static readonly GameRules JamOn = GameRules.Default.WithActions(ActionRules.Default.WithJam(WeaponJam.On));

        // 確率 0.5 で撃ち続けても上がらない。FixedJamRoll の 0 で詰まり、0.9 で詰まらない。
        public static JamSpec HalfChance() => new JamSpec(0.5f, 0f, 0f, 0.5f, ClearSeconds);

        public static WeaponDefinition JammingRifle(JamSpec jam)
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "JammingRifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            rifle.Jam.Set(jam ?? throw new ArgumentNullException(nameof(jam)));
            return rifle;
        }
    }

    public sealed class FixedJamRoll : IJamRoll
    {
        private readonly float[] _values;

        public FixedJamRoll(params float[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("a fixed roll needs at least one value", nameof(values));
            }

            _values = values;
        }

        public int Calls { get; private set; }

        // 列を使い切ったら最後の値を返し続ける。
        public float Draw()
        {
            var value = _values[Math.Min(Calls, _values.Length - 1)];
            Calls++;
            return value;
        }
    }
}
