using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchParkourSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static readonly ParkourTuning _tuned = new ParkourTuning(
            new WallKickTuning(true, 7f, 8f),
            new LandingRollTuning(true, 0.25f, 0.4f, 0.7f, 5f),
            new WallClimbTuning(true, 3f, 6f, 0.8f, 3f, 4f, false),
            new LedgeHangTuning(true, 0.75f, 2f, 2.5f, 0.4f),
            new MomentumTuning(1.5f, 0.8f));

        private static NetMatchFile File(ParkourTuning parkour)
        {
            var defaults = NetMotorSettings.Default;
            var motor = new NetMotorSettings(
                defaults.Motor,
                defaults.Stance,
                defaults.Mover,
                defaults.Prone,
                defaults.Swim,
                defaults.WallRun,
                defaults.Parachute,
                defaults.Slide,
                defaults.Zipline,
                parkour);
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "TeamDeathmatch",
                GameRules.Default,
                NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null,
                motor);
        }

        private static string Text(string motor)
        {
            return "{\"motor\": " + motor
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultMotorHasTheDefaultParkourTuning()
        {
            Assert.That(NetMotorSettings.Default.Parkour, Is.SameAs(ParkourTuning.Default));
            Assert.Throws<System.ArgumentNullException>(() => File(null));
        }

        [Test]
        public void WrittenValuesReadBackTheSame()
        {
            var text = NetMatchFile.Write(File(_tuned));
            var read = NetMatchFile.Parse(text, "parkour.json").Motor.Parkour;

            Assert.That(read.WallKick.Enabled, Is.True);
            Assert.That(read.WallKick.UpSpeed, Is.EqualTo(7f));
            Assert.That(read.WallKick.AwaySpeed, Is.EqualTo(8f));
            Assert.That(read.LandingRoll.Enabled, Is.True);
            Assert.That(read.LandingRoll.WindowSeconds, Is.EqualTo(0.25f));
            Assert.That(read.LandingRoll.FallSpeedScale, Is.EqualTo(0.4f));
            Assert.That(read.LandingRoll.RollSeconds, Is.EqualTo(0.7f));
            Assert.That(read.LandingRoll.MinFallSpeed, Is.EqualTo(5f));
            Assert.That(read.WallClimb.Enabled, Is.True);
            Assert.That(read.WallClimb.MinSpeed, Is.EqualTo(3f));
            Assert.That(read.WallClimb.ClimbSpeed, Is.EqualTo(6f));
            Assert.That(read.WallClimb.MaxSeconds, Is.EqualTo(0.8f));
            Assert.That(read.WallClimb.MaxHeight, Is.EqualTo(3f));
            Assert.That(read.WallClimb.PushOffSpeed, Is.EqualTo(4f));
            Assert.That(read.WallClimb.MantleAtLedge, Is.False);
            Assert.That(read.LedgeHang.Enabled, Is.True);
            Assert.That(read.LedgeHang.GrabReach, Is.EqualTo(0.75f));
            Assert.That(read.LedgeHang.ShimmySpeed, Is.EqualTo(2f));
            Assert.That(read.LedgeHang.ClimbUpSpeed, Is.EqualTo(2.5f));
            Assert.That(read.LedgeHang.RegrabSeconds, Is.EqualTo(0.4f));
            Assert.That(read.Momentum.SlideJumpBoost, Is.EqualTo(1.5f));
            Assert.That(read.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.8f));
            Assert.That(read.SameAs(_tuned), Is.True);
            Assert.That(text, Does.Contain("\"" + NetMatchFile.WallKickEnabledItem + "\": true"));
            Assert.That(text, Does.Contain("\"" + NetMatchFile.LedgeGrabReachItem + "\": 0.75"));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "parkour.json")), Is.EqualTo(text), "writing what was read gives the same file");
        }

        [Test]
        public void DefaultParkourValuesAreLeftOutOfTheFile()
        {
            var text = NetMatchFile.Write(File(ParkourTuning.Default));

            foreach (var item in new[]
            {
                NetMatchFile.WallKickEnabledItem, NetMatchFile.LandingRollEnabledItem, NetMatchFile.WallClimbEnabledItem,
                NetMatchFile.LedgeHangEnabledItem, NetMatchFile.SlideJumpBoostItem, NetMatchFile.VaultExitSpeedKeepItem,
            })
            {
                Assert.That(text, Does.Not.Contain("\"" + item + "\""), item);
            }

            Assert.That(NetMatchFile.Parse(text, "default.json").Motor.Parkour.SameAs(ParkourTuning.Default), Is.True);
        }

        [Test]
        public void OnlyTheMovesThatDifferFromTheDefaultAreWritten()
        {
            var text = NetMatchFile.Write(File(new ParkourTuning(wallKick: new WallKickTuning(true))));

            Assert.That(text, Does.Contain("\"" + NetMatchFile.WallKickEnabledItem + "\": true"));
            Assert.That(text, Does.Contain("\"" + NetMatchFile.WallKickUpSpeedItem + "\": 6"));
            Assert.That(text, Does.Not.Contain("\"" + NetMatchFile.LandingRollEnabledItem + "\""));
            Assert.That(text, Does.Not.Contain("\"" + NetMatchFile.SlideJumpBoostItem + "\""));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"wallKickEnabled\": true, \"vaultExitSpeedKeep\": 0.5}"), "partial.json").Motor.Parkour;

            Assert.That(read.WallKick.Enabled, Is.True);
            Assert.That(read.WallKick.UpSpeed, Is.EqualTo(WallKickTuning.DefaultUpSpeed));
            Assert.That(read.WallKick.AwaySpeed, Is.EqualTo(WallKickTuning.DefaultAwaySpeed));
            Assert.That(read.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.5f));
            Assert.That(read.Momentum.SlideJumpBoost, Is.EqualTo(MomentumTuning.DefaultSlideJumpBoost));
            Assert.That(read.LandingRoll.SameAs(LandingRollTuning.Default), Is.True);
            Assert.That(read.WallClimb.SameAs(WallClimbTuning.Default), Is.True);
            Assert.That(read.WallClimb.MantleAtLedge, Is.True);
            Assert.That(read.LedgeHang.SameAs(LedgeHangTuning.Default), Is.True);
        }

        [Test]
        public void TurningOnlyTheMantleAtTheLedgeOffIsWrittenAndReadBack()
        {
            var noMantle = new ParkourTuning(wallClimb: new WallClimbTuning(mantleAtLedge: false));
            var text = NetMatchFile.Write(File(noMantle));

            Assert.That(text, Does.Contain("\"" + NetMatchFile.WallClimbMantleAtLedgeItem + "\": false"));
            Assert.That(NetMatchFile.Parse(text, "parkour.json").Motor.Parkour.WallClimb.MantleAtLedge, Is.False);
        }

        [TestCase("{\"wallKickUpSpeed\": -1}", "motor.wallKickUpSpeed")]
        [TestCase("{\"wallKickAwaySpeed\": -1}", "motor.wallKickAwaySpeed")]
        [TestCase("{\"landingRollWindowSeconds\": -1}", "motor.landingRollWindowSeconds")]
        [TestCase("{\"landingRollFallSpeedScale\": 2}", "motor.landingRollFallSpeedScale")]
        [TestCase("{\"landingRollSeconds\": 0}", "motor.landingRollSeconds")]
        [TestCase("{\"landingRollMinFallSpeed\": -1}", "motor.landingRollMinFallSpeed")]
        [TestCase("{\"wallClimbMinSpeed\": -1}", "motor.wallClimbMinSpeed")]
        [TestCase("{\"wallClimbSpeed\": 0}", "motor.wallClimbSpeed")]
        [TestCase("{\"wallClimbMaxSeconds\": 0}", "motor.wallClimbMaxSeconds")]
        [TestCase("{\"wallClimbMaxHeight\": 0}", "motor.wallClimbMaxHeight")]
        [TestCase("{\"wallClimbPushOffSpeed\": -1}", "motor.wallClimbPushOffSpeed")]
        [TestCase("{\"wallClimbMantleAtLedge\": 1}", "motor.wallClimbMantleAtLedge")]
        [TestCase("{\"ledgeGrabReach\": 0}", "motor.ledgeGrabReach")]
        [TestCase("{\"ledgeShimmySpeed\": -1}", "motor.ledgeShimmySpeed")]
        [TestCase("{\"ledgeClimbUpSpeed\": 0}", "motor.ledgeClimbUpSpeed")]
        [TestCase("{\"ledgeRegrabSeconds\": -1}", "motor.ledgeRegrabSeconds")]
        [TestCase("{\"slideJumpBoost\": -1}", "motor.slideJumpBoost")]
        [TestCase("{\"vaultExitSpeedKeep\": 1.5}", "motor.vaultExitSpeedKeep")]
        public void ABadValueNamesItsItem(string motor, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(motor), "bad.json"));

            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void TheCommittedRulesKeepTheParkourOff()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

                Assert.That(file.Motor.Parkour.SameAs(ParkourTuning.Default), Is.True, path);
                Assert.That(file.Motor.Parkour.WallKick.Enabled, Is.False, path);
                Assert.That(file.Motor.Parkour.LandingRoll.Enabled, Is.False, path);
                Assert.That(file.Motor.Parkour.WallClimb.Enabled, Is.False, path);
                Assert.That(file.Motor.Parkour.LedgeHang.Enabled, Is.False, path);
            }
        }
    }
}
