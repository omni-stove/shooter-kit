using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        private const float _jamClearSeconds = 0.5f;

        private static readonly ShotRejection[] _oneJammedRejection = { ShotRejection.Jammed };

        [Test]
        public void AJammedRifleRefusesTheNextShotBeforeTheFireRateCheck()
        {
            Start(JamOn(), rifle: AlwaysJammingRifle());
            Join(1);
            Join(2);
            Step(0.2f);
            var rejected = new List<ShotRejection>();
            _signals.ShotRejected += (_, reason) => rejected.Add(reason);

            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " jammed sustained=0 chance=1.000"));
            Assert.That(Snapshot(1).Owner.Weapon.IsJammed, Is.True);
            Assert.That(Snapshot(1).Owner.Weapon.JamClearEndsAt, Is.Zero, "not clearing yet");

            // 連射の間隔より前に撃ち直す。詰まりの検査が先なので、連射の間隔ではなく詰まりで断る
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));

            Assert.That(rejected, Is.EqualTo(_oneJammedRejection));
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=1 reason=Jammed"));
            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(29), "the refused shot uses no round");
        }

        [Test]
        public void TheReloadRequestClearsTheJamAndTheRifleFiresAgain()
        {
            Start(JamOn(), rifle: AlwaysJammingRifle());
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));

            RequestReload(1);

            var clearing = Snapshot(1).Owner.Weapon;
            Assert.That(clearing.IsJammed, Is.True);
            Assert.That(clearing.IsReloading, Is.False, "the reload request clears the jam instead of reloading");
            Assert.That(clearing.JamClearEndsAt, Is.GreaterThan(0d));
            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " clear started seconds=0.50"));

            Step(_jamClearSeconds + 0.1f);

            var cleared = Snapshot(1).Owner.Weapon;
            Assert.That(cleared.IsJammed, Is.False);
            Assert.That(cleared.JamClearEndsAt, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Combat] player=1 [Jam] weapon=" + TestRules.RifleId + " cleared"));

            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));

            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(28));
            Assert.That(Lines(_log).Count(line => line.EndsWith(" jammed sustained=0 chance=1.000", System.StringComparison.Ordinal)), Is.EqualTo(2));
        }

        [Test]
        public void WithTheJamRuleOffTheRifleNeverJams()
        {
            Start(Rules(), rifle: AlwaysJammingRifle());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 5; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
                Step(0.2f);
            }

            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(25));
            Assert.That(Snapshot(1).Owner.Weapon.IsJammed, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Jam]"));
        }

        private static GameRules JamOn()
        {
            var rules = Rules();
            return rules.WithActions(rules.Actions.WithJam(WeaponJam.On));
        }

        // 確率 1 なので、どの乱数の値でも撃った発が必ず詰まる
        private static WeaponSpec AlwaysJammingRifle()
        {
            var rifle = TestRules.Rifle();
            return new WeaponSpec(
                rifle.Id,
                rifle.NameKey,
                rifle.Category,
                rifle.Trigger,
                new AmmoSpec(30, 90, 2f, jam: new JamSpec(1f, 0f, 0f, 1f, _jamClearSeconds)),
                rifle.Round,
                rifle.Impact,
                rifle.Aim,
                rifle.AmmoTypes,
                rifle.ShotNoiseRadius);
        }
    }
}
