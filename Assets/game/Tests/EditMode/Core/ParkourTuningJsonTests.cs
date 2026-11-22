using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ParkourTuningJsonTests
    {
        [Test]
        public void TheDefaultsAreOffAndKeepTodaysMovement()
        {
            var parkour = TraversalSpec.Default.Parkour;

            Assert.That(parkour, Is.SameAs(ParkourTuning.Default));
            Assert.That(parkour.WallKick.Enabled, Is.False);
            Assert.That(parkour.WallKick.UpSpeed, Is.EqualTo(6f));
            Assert.That(parkour.WallKick.AwaySpeed, Is.EqualTo(6f));
            Assert.That(parkour.LandingRoll.Enabled, Is.False);
            Assert.That(parkour.LandingRoll.WindowSeconds, Is.EqualTo(0.3f));
            Assert.That(parkour.LandingRoll.FallSpeedScale, Is.EqualTo(0.5f));
            Assert.That(parkour.LandingRoll.RollSeconds, Is.EqualTo(0.6f));
            Assert.That(parkour.LandingRoll.MinFallSpeed, Is.EqualTo(6f));
            Assert.That(parkour.WallClimb.Enabled, Is.False);
            Assert.That(parkour.WallClimb.MinSpeed, Is.EqualTo(4f));
            Assert.That(parkour.WallClimb.ClimbSpeed, Is.EqualTo(5f));
            Assert.That(parkour.WallClimb.MaxSeconds, Is.EqualTo(0.6f));
            Assert.That(parkour.WallClimb.MaxHeight, Is.EqualTo(2.5f));
            Assert.That(parkour.WallClimb.PushOffSpeed, Is.EqualTo(5f));
            Assert.That(parkour.LedgeHang.Enabled, Is.False);
            Assert.That(parkour.LedgeHang.GrabReach, Is.EqualTo(0.6f));
            Assert.That(parkour.LedgeHang.ShimmySpeed, Is.EqualTo(1.5f));
            Assert.That(parkour.LedgeHang.ClimbUpSpeed, Is.EqualTo(3f));
            Assert.That(parkour.LedgeHang.RegrabSeconds, Is.EqualTo(0.5f));
            Assert.That(parkour.Momentum.SlideJumpBoost, Is.Zero);
            Assert.That(parkour.Momentum.VaultExitSpeedKeep, Is.Zero);
        }

        [Test]
        public void ReadsEveryParkourItem()
        {
            var spec = TraversalJson.Parse(
                "{\"wallKick\": {\"enabled\": true, \"upSpeed\": 7, \"awaySpeed\": 8},"
                + " \"landingRoll\": {\"enabled\": true, \"windowSeconds\": 0.25, \"fallSpeedScale\": 0.4, \"rollSeconds\": 0.7, \"minFallSpeed\": 5},"
                + " \"wallClimb\": {\"enabled\": true, \"minSpeed\": 3, \"climbSpeed\": 6, \"maxSeconds\": 0.8, \"maxHeight\": 3, \"pushOffSpeed\": 4},"
                + " \"ledgeHang\": {\"enabled\": true, \"grabReach\": 0.75, \"shimmySpeed\": 2, \"climbUpSpeed\": 2.5, \"regrabSeconds\": 0.4},"
                + " \"momentum\": {\"slideJumpBoost\": 1.5, \"vaultExitSpeedKeep\": 0.8}}",
                "traversal.json");

            var parkour = spec.Parkour;
            Assert.That(parkour.WallKick.Enabled, Is.True);
            Assert.That(parkour.WallKick.UpSpeed, Is.EqualTo(7f));
            Assert.That(parkour.WallKick.AwaySpeed, Is.EqualTo(8f));
            Assert.That(parkour.LandingRoll.Enabled, Is.True);
            Assert.That(parkour.LandingRoll.WindowSeconds, Is.EqualTo(0.25f));
            Assert.That(parkour.LandingRoll.FallSpeedScale, Is.EqualTo(0.4f));
            Assert.That(parkour.LandingRoll.RollSeconds, Is.EqualTo(0.7f));
            Assert.That(parkour.LandingRoll.MinFallSpeed, Is.EqualTo(5f));
            Assert.That(parkour.WallClimb.Enabled, Is.True);
            Assert.That(parkour.WallClimb.MinSpeed, Is.EqualTo(3f));
            Assert.That(parkour.WallClimb.ClimbSpeed, Is.EqualTo(6f));
            Assert.That(parkour.WallClimb.MaxSeconds, Is.EqualTo(0.8f));
            Assert.That(parkour.WallClimb.MaxHeight, Is.EqualTo(3f));
            Assert.That(parkour.WallClimb.PushOffSpeed, Is.EqualTo(4f));
            Assert.That(parkour.LedgeHang.Enabled, Is.True);
            Assert.That(parkour.LedgeHang.GrabReach, Is.EqualTo(0.75f));
            Assert.That(parkour.LedgeHang.ShimmySpeed, Is.EqualTo(2f));
            Assert.That(parkour.LedgeHang.ClimbUpSpeed, Is.EqualTo(2.5f));
            Assert.That(parkour.LedgeHang.RegrabSeconds, Is.EqualTo(0.4f));
            Assert.That(parkour.Momentum.SlideJumpBoost, Is.EqualTo(1.5f));
            Assert.That(parkour.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.8f));
            Assert.That(spec.WallRun, Is.SameAs(WallRunTuning.Default), "the other groups keep their defaults");
        }

        [Test]
        public void OmittedParkourItemsAndGroupsUseTheDefaults()
        {
            var spec = TraversalJson.Parse("{\"wallKick\": {\"enabled\": true}, \"momentum\": {\"vaultExitSpeedKeep\": 0.5}}", "traversal.json");

            Assert.That(spec.Parkour.WallKick.Enabled, Is.True);
            Assert.That(spec.Parkour.WallKick.UpSpeed, Is.EqualTo(WallKickTuning.DefaultUpSpeed));
            Assert.That(spec.Parkour.WallKick.AwaySpeed, Is.EqualTo(WallKickTuning.DefaultAwaySpeed));
            Assert.That(spec.Parkour.Momentum.SlideJumpBoost, Is.EqualTo(MomentumTuning.DefaultSlideJumpBoost));
            Assert.That(spec.Parkour.Momentum.VaultExitSpeedKeep, Is.EqualTo(0.5f));
            Assert.That(spec.Parkour.LandingRoll, Is.SameAs(LandingRollTuning.Default));
            Assert.That(spec.Parkour.WallClimb, Is.SameAs(WallClimbTuning.Default));
            Assert.That(spec.Parkour.LedgeHang, Is.SameAs(LedgeHangTuning.Default));

            var empty = TraversalJson.Parse("{}", "traversal.json");
            Assert.That(empty.Parkour.WallKick, Is.SameAs(WallKickTuning.Default));
            Assert.That(empty.Parkour.Momentum, Is.SameAs(MomentumTuning.Default));
        }

        [TestCase("{\"wallKick\": {\"upSpeed\": -1}}", "wallKick.upSpeed")]
        [TestCase("{\"wallKick\": {\"awaySpeed\": -2}}", "wallKick.awaySpeed")]
        [TestCase("{\"wallKick\": {\"enabled\": 1}}", "wallKick.enabled")]
        [TestCase("{\"landingRoll\": {\"windowSeconds\": -0.1}}", "landingRoll.windowSeconds")]
        [TestCase("{\"landingRoll\": {\"fallSpeedScale\": 1.5}}", "landingRoll.fallSpeedScale")]
        [TestCase("{\"landingRoll\": {\"rollSeconds\": 0}}", "landingRoll.rollSeconds")]
        [TestCase("{\"landingRoll\": {\"minFallSpeed\": -1}}", "landingRoll.minFallSpeed")]
        [TestCase("{\"wallClimb\": {\"minSpeed\": -1}}", "wallClimb.minSpeed")]
        [TestCase("{\"wallClimb\": {\"climbSpeed\": 0}}", "wallClimb.climbSpeed")]
        [TestCase("{\"wallClimb\": {\"maxSeconds\": 0}}", "wallClimb.maxSeconds")]
        [TestCase("{\"wallClimb\": {\"maxHeight\": 0}}", "wallClimb.maxHeight")]
        [TestCase("{\"wallClimb\": {\"pushOffSpeed\": -1}}", "wallClimb.pushOffSpeed")]
        [TestCase("{\"ledgeHang\": {\"grabReach\": 0}}", "ledgeHang.grabReach")]
        [TestCase("{\"ledgeHang\": {\"shimmySpeed\": -1}}", "ledgeHang.shimmySpeed")]
        [TestCase("{\"ledgeHang\": {\"climbUpSpeed\": 0}}", "ledgeHang.climbUpSpeed")]
        [TestCase("{\"ledgeHang\": {\"regrabSeconds\": -1}}", "ledgeHang.regrabSeconds")]
        [TestCase("{\"momentum\": {\"slideJumpBoost\": -1}}", "momentum.slideJumpBoost")]
        [TestCase("{\"momentum\": {\"vaultExitSpeedKeep\": 1.1}}", "momentum.vaultExitSpeedKeep")]
        [TestCase("{\"momentum\": {\"enabled\": true}}", "momentum.enabled")]
        [TestCase("{\"wallKick\": {\"jumpOffSpeed\": 3}}", "wallKick.jumpOffSpeed")]
        public void ABadParkourValueNamesItsItem(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TraversalJson.Parse(text, "traversal.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.SourceName, Is.EqualTo("traversal.json"));
        }

        [Test]
        public void TheParkourTuningTypesRejectBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallKickTuning(upSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallKickTuning(awaySpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandingRollTuning(fallSpeedScale: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandingRollTuning(fallSpeedScale: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LandingRollTuning(rollSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallClimbTuning(climbSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LedgeHangTuning(grabReach: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MomentumTuning(vaultExitSpeedKeep: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MomentumTuning(slideJumpBoost: float.PositiveInfinity));
        }

        [Test]
        public void TheEdgesOfTheFractionsAreAccepted()
        {
            Assert.That(new LandingRollTuning(fallSpeedScale: 0f).FallSpeedScale, Is.Zero);
            Assert.That(new LandingRollTuning(fallSpeedScale: 1f).FallSpeedScale, Is.EqualTo(1f));
            Assert.That(new MomentumTuning(vaultExitSpeedKeep: 1f).VaultExitSpeedKeep, Is.EqualTo(1f));
        }
    }
}
