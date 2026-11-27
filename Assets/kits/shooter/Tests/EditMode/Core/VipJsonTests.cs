using System;
using GameKit.Core;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VipJsonTests
    {
        private const string _source = "vip.json";

        private static readonly string[] _expectedItemNames =
        {
            "vipMaxHealth", "roundSeconds", "winsToWin", "exitHoldSeconds", "swapEveryRounds",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = VipJson.Parse(
                "{\"vipMaxHealth\": 120, \"roundSeconds\": 90, \"winsToWin\": 2, \"exitHoldSeconds\": 0, \"swapEveryRounds\": 2}",
                _source);

            Assert.That(spec.VipMaxHealth, Is.EqualTo(120));
            Assert.That(spec.RoundSeconds, Is.EqualTo(90f));
            Assert.That(spec.WinsToWin, Is.EqualTo(2));
            Assert.That(spec.ExitHoldSeconds, Is.EqualTo(0f), "the VIP may escape the moment it steps in");
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(2));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = VipJson.Parse("{}", _source);

            Assert.That(spec.VipMaxHealth, Is.EqualTo(200));
            Assert.That(spec.RoundSeconds, Is.EqualTo(150f));
            Assert.That(spec.WinsToWin, Is.EqualTo(4));
            Assert.That(spec.ExitHoldSeconds, Is.EqualTo(3f));
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(1), "the sides swap every round");
            Assert.That(spec.SameAs(VipSpec.Default), Is.True);
        }

        [Test]
        public void ZeroSwapsKeepTheSides()
        {
            Assert.That(VipJson.Parse("{\"swapEveryRounds\": 0}", _source).SwapEveryRounds, Is.Zero);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(VipJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(VipJson.ResourcePath, Is.EqualTo("Tuning/vip"));
        }

        [Test]
        public void SameAsComparesEveryValue()
        {
            Assert.That(new VipSpec().SameAs(VipSpec.Default), Is.True);
            Assert.That(new VipSpec(vipMaxHealth: 201).SameAs(VipSpec.Default), Is.False);
            Assert.That(new VipSpec(roundSeconds: 151f).SameAs(VipSpec.Default), Is.False);
            Assert.That(new VipSpec(winsToWin: 5).SameAs(VipSpec.Default), Is.False);
            Assert.That(new VipSpec(exitHoldSeconds: 4f).SameAs(VipSpec.Default), Is.False);
            Assert.That(new VipSpec(swapEveryRounds: 0).SameAs(VipSpec.Default), Is.False);
            Assert.That(VipSpec.Default.SameAs(null), Is.False);
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var health = Reject("{\"vipMaxHealth\": 0}");
            var round = Reject("{\"roundSeconds\": 0}");
            var wins = Reject("{\"winsToWin\": 0}");
            var hold = Reject("{\"exitHoldSeconds\": -1}");
            var longHold = Reject("{\"exitHoldSeconds\": 60, \"roundSeconds\": 60}");
            var swap = Reject("{\"swapEveryRounds\": -1}");
            var fraction = Reject("{\"winsToWin\": 1.5}");
            var unknown = Reject("{\"vipCount\": 1}");

            Assert.That(health.Item, Is.EqualTo("vipMaxHealth"));
            Assert.That(health.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(round.Item, Is.EqualTo("roundSeconds"));
            Assert.That(wins.Item, Is.EqualTo("winsToWin"));
            Assert.That(hold.Item, Is.EqualTo("exitHoldSeconds"));
            Assert.That(longHold.Item, Is.EqualTo("exitHoldSeconds"));
            Assert.That(longHold.Message, Does.Contain("shorter than roundSeconds (60)"));
            Assert.That(swap.Item, Is.EqualTo("swapEveryRounds"));
            Assert.That(fraction.Item, Is.EqualTo("winsToWin"));
            Assert.That(unknown.Item, Is.EqualTo("vipCount"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => VipJson.Parse(json, _source));
        }
    }
}
