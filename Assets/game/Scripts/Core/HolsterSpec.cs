using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class HolsterSpec
    {
        public const float DefaultMoveSpeedMultiplier = 1f;

        public const bool DefaultArmedWhileHeldByOthers = false;

        public static IReadOnlyList<HolsterPhase> DefaultArmedPhases { get; } = Array.AsReadOnly(new[] { HolsterPhase.Drawn, HolsterPhase.Drawing });

        public static HolsterSpec Default { get; } = new HolsterSpec();

        private readonly bool[] _armed;

        public float MoveSpeedMultiplier { get; }

        public IReadOnlyList<HolsterPhase> ArmedPhases { get; }

        public bool ArmedWhileHeldByOthers { get; }

        public HolsterSpec(
            float moveSpeedMultiplier = DefaultMoveSpeedMultiplier,
            IReadOnlyList<HolsterPhase> armedPhases = null,
            bool armedWhileHeldByOthers = DefaultArmedWhileHeldByOthers)
        {
            MoveSpeedMultiplier = SpecGuard.NonNegativeFinite(moveSpeedMultiplier, nameof(moveSpeedMultiplier));
            var phases = armedPhases ?? DefaultArmedPhases;
            _armed = new bool[Enum.GetValues(typeof(HolsterPhase)).Length];
            var copy = new HolsterPhase[phases.Count];
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (!Enum.IsDefined(typeof(HolsterPhase), phase))
                {
                    throw new ArgumentOutOfRangeException(nameof(armedPhases), phase, "armedPhases has a value that is not a HolsterPhase");
                }

                if (_armed[(int)phase])
                {
                    throw new ArgumentException($"armedPhases lists {phase} twice", nameof(armedPhases));
                }

                _armed[(int)phase] = true;
                copy[i] = phase;
            }

            ArmedPhases = Array.AsReadOnly(copy);
            ArmedWhileHeldByOthers = armedWhileHeldByOthers;
        }

        public bool IsArmedPhase(HolsterPhase phase)
        {
            if (!Enum.IsDefined(typeof(HolsterPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "phase is not a HolsterPhase");
            }

            return _armed[(int)phase];
        }

        public bool IsArmed(HolsterPhase phase, bool heldByOthers)
        {
            var armed = IsArmedPhase(phase);
            return heldByOthers ? armed && ArmedWhileHeldByOthers : armed;
        }
    }
}
