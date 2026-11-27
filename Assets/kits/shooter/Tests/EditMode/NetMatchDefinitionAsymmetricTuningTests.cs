using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Juggernaut・VIP の定義アセットの欄はまだ無いので、サーバーへ書き出す調整値は入口 (GameContent) の欄か Resources/Tuning の JSON から読む
    public sealed class NetMatchDefinitionAsymmetricTuningTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private NetMatchDefinition _match;
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _match = Make<NetMatchDefinition>();
            _content = Make<GameContent>();
            GameContent.Use(_content);
            KitTuning.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            KitTuning.Reload();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void WithoutTuningFilesInTheGameContentTheBundledDefaultsAreCarried()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.Juggernaut.SameAs(JuggernautSpec.Default), Is.True);
            Assert.That(settings.Vip.SameAs(VipSpec.Default), Is.True);
        }

        [Test]
        public void TheTuningJsonOfTheGameContentIsCarried()
        {
            _content.JuggernautTuning = Track(new TextAsset("{\"juggernautMaxHealth\": 350, \"scoreLimit\": 20}"));
            _content.VipTuning = Track(new TextAsset("{\"vipMaxHealth\": 180, \"swapEveryRounds\": 0}"));
            KitTuning.Reload();

            var settings = _match.ToSettings();

            Assert.That(settings.Juggernaut.JuggernautMaxHealth, Is.EqualTo(350));
            Assert.That(settings.Juggernaut.ScoreLimit, Is.EqualTo(20));
            Assert.That(settings.Juggernaut.TimeLimitSeconds, Is.EqualTo(JuggernautSpec.DefaultTimeLimitSeconds));
            Assert.That(settings.Vip.VipMaxHealth, Is.EqualTo(180));
            Assert.That(settings.Vip.SwapEveryRounds, Is.Zero);
            Assert.That(settings.Vip.RoundSeconds, Is.EqualTo(VipSpec.DefaultRoundSeconds));
        }

        private T Make<T>()
            where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _objects.Add(created);
            return created;
        }
    }
}
