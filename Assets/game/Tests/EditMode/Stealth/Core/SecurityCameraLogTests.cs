using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Stealth.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SecurityCameraLogTests
    {
        private static readonly Vector3 _where = new Vector3(1f, 0f, 2f);

        private static readonly string[] _expectedPhaseChangeLines =
        {
            "[SecurityCamera] camera=cam1 phase=Tracking from=Sweeping yaw=0.00 level=0.25",
            "[SecurityCamera] camera=cam1 phase=Spotted from=Tracking at=(1.00, 0.00, 2.00) level=1.00",
            "[SecurityCamera] camera=cam1 phase=Disabled from=Spotted seconds=30.00",
            "[SecurityCamera] camera=cam1 phase=Sweeping from=Disabled yaw=20.00 level=1.00",
            "[SecurityCamera] camera=cam1 phase=Destroyed from=Sweeping",
        };

        private static SecurityCameraTarget Running(float bearing) =>
            new SecurityCameraTarget(true, 2f, bearing, _where, AwarenessPosture.Standing, 1f, AwarenessSpec.DefaultFullMotionSpeed);

        [Test]
        public void EachPhaseHasItsLine()
        {
            Assert.That(
                SecurityCameraLog.Sweeping("cam1", SecurityCameraPhase.Tracking, -12.5f, 0.5f),
                Is.EqualTo("[SecurityCamera] camera=cam1 phase=Sweeping from=Tracking yaw=-12.50 level=0.50"));
            Assert.That(
                SecurityCameraLog.Tracking("cam1", SecurityCameraPhase.Sweeping, 5f, 0.25f),
                Is.EqualTo("[SecurityCamera] camera=cam1 phase=Tracking from=Sweeping yaw=5.00 level=0.25"));
            Assert.That(
                SecurityCameraLog.Spotted("cam1", SecurityCameraPhase.Tracking, new Vector3(1f, 0f, -2.5f), 1f),
                Is.EqualTo("[SecurityCamera] camera=cam1 phase=Spotted from=Tracking at=(1.00, 0.00, -2.50) level=1.00"));
            Assert.That(
                SecurityCameraLog.Disabled("cam1", SecurityCameraPhase.Spotted, 30f),
                Is.EqualTo("[SecurityCamera] camera=cam1 phase=Disabled from=Spotted seconds=30.00"));
            Assert.That(
                SecurityCameraLog.Destroyed("cam1", SecurityCameraPhase.Disabled),
                Is.EqualTo("[SecurityCamera] camera=cam1 phase=Destroyed from=Disabled"));
        }

        [Test]
        public void AMissingIdIsADash()
        {
            Assert.That(SecurityCameraLog.Destroyed(null, SecurityCameraPhase.Sweeping), Is.EqualTo("[SecurityCamera] camera=- phase=Destroyed from=Sweeping"));
            Assert.That(SecurityCameraLog.Destroyed(string.Empty, SecurityCameraPhase.Sweeping), Does.StartWith("[SecurityCamera] camera=- "));
        }

        [Test]
        public void TheLinesAreTheSameInEveryCulture()
        {
            var before = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.That(
                    SecurityCameraLog.Tracking("cam1", SecurityCameraPhase.Sweeping, 5f, 0.25f),
                    Is.EqualTo("[SecurityCamera] camera=cam1 phase=Tracking from=Sweeping yaw=5.00 level=0.25"));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = before;
            }
        }

        [Test]
        public void EveryPhaseChangeOfACameraMakesOneLine()
        {
            var camera = new SecurityCameraSweep(SecurityCameraSpec.Default);
            var lines = new List<string>();
            camera.PhaseChanged += (from, _) => lines.Add(SecurityCameraLog.Changed("cam1", camera, from));

            for (var i = 0; i < 4; i++)
            {
                camera.Tick(0.25f, Running(0f));
            }

            camera.Disable(30f);
            camera.Tick(31f, SecurityCameraTarget.None);
            camera.Destroy();

            Assert.That(lines, Is.EqualTo(_expectedPhaseChangeLines));
        }

        [Test]
        public void ChangedNeedsACamera()
        {
            Assert.Throws<ArgumentNullException>(() => SecurityCameraLog.Changed("cam1", null, SecurityCameraPhase.Sweeping));
        }
    }
}
