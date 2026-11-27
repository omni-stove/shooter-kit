using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class LandingRollLog
    {
        public const string LogTag = "[LandingRoll]";

        public const string StartPrefix = LogTag + " start ";

        public const string FinishPrefix = LogTag + " finish ";

        public static string Start(Vector3 landingVelocity, LandingRollResult result, LandingRollTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (result.Motion != LandingRollMotion.Started)
            {
                throw new ArgumentOutOfRangeException(nameof(result), result.Motion, "only a started roll is logged as a start");
            }

            var speed = new Vector2(result.Velocity.X, result.Velocity.Z).Length();
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}fallSpeed={1:0.00} fallSpeedScale={2:0.00} speed={3:0.00} seconds={4:0.00}",
                StartPrefix,
                LandingRollStep.FallSpeed(landingVelocity),
                result.State.FallSpeedScale,
                speed,
                tuning.RollSeconds);
        }

        public static string Finish(float rolledSeconds)
        {
            if (!(rolledSeconds >= 0f) || float.IsInfinity(rolledSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(rolledSeconds), rolledSeconds, "rolledSeconds must be finite and not negative");
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}seconds={1:0.00}", FinishPrefix, rolledSeconds);
        }
    }
}
