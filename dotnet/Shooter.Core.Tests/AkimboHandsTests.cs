using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AkimboHandsTests
    {
        private const string _id = "9a4e1c7b-2d5f-4b8a-9e3c-6f1d0a2b7c58";
        private const float _fireInterval = 0.25f;
        private const float _reloadSeconds = 1.5f;

        private static WeaponSpec Pistol(int reserveAmmo = 24, AkimboSpec akimbo = null, ReloadStyle style = ReloadStyle.Whole, float perRound = 0f)
        {
            return new WeaponSpec(
                _id,
                null,
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, _fireInterval),
                new AmmoSpec(8, reserveAmmo, _reloadSeconds, reloadStyle: style, reloadSecondsPerRound: perRound),
                akimbo: akimbo ?? AkimboSpec.Default);
        }

        // 撃つたびに撃ち方の間隔だけ進め、同じ手で続けて撃てるようにする。
        private static void Fire(AkimboHands hands, AkimboHand hand, int times)
        {
            for (var i = 0; i < times; i++)
            {
                Assert.That(hands.TryFire(hand), Is.True, hand + " shot " + (i + 1));
                hands.Tick(_fireInterval);
            }
        }

        [Test]
        public void FiringTheRightHandThreeTimesLeavesTheLeftMagazineFull()
        {
            var hands = new AkimboHands(Pistol());

            Fire(hands, AkimboHand.Right, 3);

            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(5));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(24));
        }

        [Test]
        public void TheCooldownIsPerHand()
        {
            var hands = new AkimboHands(Pistol());

            Assert.That(hands.TryFire(AkimboHand.Right), Is.True);
            Assert.That(hands.TryFire(AkimboHand.Right), Is.False);
            Assert.That(hands.TryFire(AkimboHand.Left), Is.True);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
        }

        [Test]
        public void TheReserveIsSharedNotDoubled()
        {
            var hands = new AkimboHands(Pistol());

            Assert.That(hands.ReserveAmmo, Is.EqualTo(24));
            Assert.That(hands.Right.ReserveAmmo, Is.EqualTo(24));
            Assert.That(hands.Left.ReserveAmmo, Is.EqualTo(24));
            Assert.That(hands.MaxReserveAmmo, Is.EqualTo(24));
        }

        [Test]
        public void ReloadStartsEveryHandThatIsNotFullAndBothDrawFromOneReserve()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 3);
            Fire(hands, AkimboHand.Left, 1);

            Assert.That(hands.TryStartReload(), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.True);

            hands.Tick(_reloadSeconds);

            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(20));
            Assert.That(hands.IsReloading, Is.False);
        }

        [Test]
        public void ReloadLeavesAFullHandAlone()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 1);

            Assert.That(hands.TryStartReload(), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False);
        }

        [Test]
        public void ReloadWithBothHandsFullStartsNothing()
        {
            var hands = new AkimboHands(Pistol());

            Assert.That(hands.TryStartReload(), Is.False);
            Assert.That(hands.IsReloading, Is.False);
        }

        [Test]
        public void ReloadingOneHandStartsOnlyWhenItIsEmpty()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 3);

            Assert.That(hands.TryStartReload(AkimboHand.Right), Is.False);

            Fire(hands, AkimboHand.Right, 5);

            Assert.That(hands.TryStartReload(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.IsHandReloading(AkimboHand.Left), Is.False);
        }

        [Test]
        public void AReloadingHandCannotFireButTheOtherCan()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 8);
            hands.TryStartReload(AkimboHand.Right);
            hands.Tick(_fireInterval);

            Assert.That(hands.TryFire(AkimboHand.Right), Is.False);
            Assert.That(hands.TryFire(AkimboHand.Left), Is.True);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
        }

        [Test]
        public void AnEmptyHandCannotFire()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Left, 8);

            Assert.That(hands.TryFire(AkimboHand.Left), Is.False);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.Zero);
        }

        [Test]
        public void TheReloadSecondsAreMultiplied()
        {
            var hands = new AkimboHands(Pistol(akimbo: new AkimboSpec(reloadSecondsMultiplier: 2f)));
            Fire(hands, AkimboHand.Right, 1);
            hands.TryStartReload();

            Assert.That(hands.ReloadSeconds, Is.EqualTo(3f));
            Assert.That(hands.Spec.ReloadSeconds, Is.EqualTo(_reloadSeconds));

            hands.Tick(1.5f);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.True);
            Assert.That(hands.ReloadProgress(AkimboHand.Right), Is.EqualTo(0.5f).Within(1e-5f));

            hands.Tick(1.5f);
            Assert.That(hands.IsHandReloading(AkimboHand.Right), Is.False);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
        }

        [Test]
        public void ThePerRoundSecondsAreMultipliedToo()
        {
            var hands = new AkimboHands(Pistol(akimbo: new AkimboSpec(reloadSecondsMultiplier: 2f), style: ReloadStyle.PerRound, perRound: 0.5f));

            Assert.That(hands.Right.Spec.Ammo.ReloadSecondsPerRound, Is.EqualTo(1f));
            Assert.That(hands.Left.Spec.Ammo.ReloadSecondsPerRound, Is.EqualTo(1f));
        }

        [Test]
        public void AWeaponWhoseMultipliedReloadIsFiniteCanBeHeldInBothHands()
        {
            var spec = Pistol(akimbo: new AkimboSpec(reloadSecondsMultiplier: 1e20f));

            var hands = new AkimboHands(spec);

            Assert.That(hands.ReloadSeconds, Is.EqualTo(1.5e20f).Within(1e15f));
            Assert.That(hands.Right.Spec.IsAkimbo, Is.False);
            Assert.That(hands.Left.Spec.IsAkimbo, Is.False);
            Assert.That(hands.Spec.IsAkimbo, Is.True);
        }

        [Test]
        public void WithoutAkimboDropsOnlyTheAkimboPart()
        {
            var spec = Pistol(akimbo: AkimboSpec.Default);

            var single = spec.WithoutAkimbo();

            Assert.That(single.Akimbo, Is.Null);
            Assert.That(single.Id, Is.EqualTo(_id));
            Assert.That(single.Ammo, Is.SameAs(spec.Ammo));
            Assert.That(single.Trigger, Is.SameAs(spec.Trigger));
            Assert.That(Pistol().WithoutAkimbo().Akimbo, Is.Null);
        }

        [Test]
        public void TheFirstHandToFinishTakesWhatIsLeftOfTheReserve()
        {
            var hands = new AkimboHands(Pistol(reserveAmmo: 5));
            Fire(hands, AkimboHand.Right, 8);
            Fire(hands, AkimboHand.Left, 8);

            Assert.That(hands.TryStartReload(), Is.True);
            hands.Tick(_reloadSeconds);

            // Tick は右の手から進めるので、右の手が先に込め終える。
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(5));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.Zero);
            Assert.That(hands.ReserveAmmo, Is.Zero);
            Assert.That(hands.TryStartReload(AkimboHand.Left), Is.False);
        }

        [Test]
        public void TheSavedCountsAreRestored()
        {
            var hands = new AkimboHands(Pistol(), 2, 6, 10, GameRules.Default);

            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(2));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(6));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(10));
            Assert.That(hands.MaxReserveAmmo, Is.EqualTo(24));
        }

        [Test]
        public void ASavedMagazineBeyondTheSizeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AkimboHands(Pistol(), 8, 9, 10, GameRules.Default));
        }

        [Test]
        public void TheReserveMultiplierIsAppliedOnce()
        {
            var hands = new AkimboHands(Pistol(), GameRules.Default.WithReserveAmmoMultiplier(0.5f));

            Assert.That(hands.ReserveAmmo, Is.EqualTo(12));
        }

        [Test]
        public void PickedUpAmmoFillsTheSharedReserveUpToItsMax()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 3);
            hands.TryStartReload();
            hands.Tick(_reloadSeconds);

            Assert.That(hands.ReserveAmmoRoom, Is.EqualTo(3));
            Assert.That(hands.AddReserveAmmo(10), Is.EqualTo(3));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(24));
        }

        [Test]
        public void AnAmmoPoolReceivesTheReserveAndFeedsBothHands()
        {
            var hands = new AkimboHands(Pistol());
            var pool = new TestPool(0, 100);

            Assert.That(hands.UseAmmoPool(pool), Is.True);
            Assert.That(hands.AmmoPool, Is.SameAs(pool));
            Assert.That(pool.Count, Is.EqualTo(24));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(24));

            Fire(hands, AkimboHand.Right, 2);
            Fire(hands, AkimboHand.Left, 3);
            hands.TryStartReload();
            hands.Tick(_reloadSeconds);

            Assert.That(pool.Count, Is.EqualTo(19));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
        }

        [Test]
        public void AnAmmoPoolThatAlreadyHoldsTheReserveIsNotFilledAgain()
        {
            var hands = new AkimboHands(Pistol());
            var pool = new TestPool(30, 100);

            hands.UseAmmoPool(pool);

            Assert.That(pool.Count, Is.EqualTo(30));
            Assert.That(hands.ReserveAmmo, Is.EqualTo(30));
        }

        [Test]
        public void ReleasingTheAmmoPoolLeavesNoReserve()
        {
            var hands = new AkimboHands(Pistol());
            hands.UseAmmoPool(new TestPool(0, 100));

            Assert.That(hands.UseAmmoPool(null), Is.False);
            Assert.That(hands.AmmoPool, Is.Null);
            Assert.That(hands.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void TheReserveChangeIsReportedOnce()
        {
            var hands = new AkimboHands(Pistol());
            var changes = 0;
            hands.ReserveAmmoChanged += () => changes++;
            Fire(hands, AkimboHand.Right, 1);
            hands.TryStartReload();

            hands.Tick(_reloadSeconds);

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void AnInfiniteReserveIsNotDrawnAndTakesNoPool()
        {
            var hands = new AkimboHands(Pistol(), GameRules.Default.WithInfiniteReserve(true));
            Fire(hands, AkimboHand.Right, 8);
            Fire(hands, AkimboHand.Left, 8);

            hands.TryStartReload();
            hands.Tick(_reloadSeconds);

            Assert.That(hands.HasInfiniteReserve, Is.True);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(8));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(8));
            Assert.That(hands.UseAmmoPool(new TestPool(0, 100)), Is.False);
        }

        [Test]
        public void FiringAndReloadingReportTheHand()
        {
            var hands = new AkimboHands(Pistol());
            var fired = new List<AkimboHand>();
            var reloaded = new List<AkimboHand>();
            hands.Fired += fired.Add;
            hands.Reloaded += reloaded.Add;

            Fire(hands, AkimboHand.Left, 1);
            Fire(hands, AkimboHand.Right, 1);
            hands.TryFire(AkimboHand.Right);
            hands.TryFire(AkimboHand.Right);
            hands.CancelReload();
            hands.TryStartReload(AkimboHand.Left);
            hands.TryStartReload();
            hands.Tick(_reloadSeconds);

            Assert.That(fired, Is.EqualTo(new[] { AkimboHand.Left, AkimboHand.Right, AkimboHand.Right }));
            Assert.That(reloaded, Is.EqualTo(new[] { AkimboHand.Right, AkimboHand.Left }));
        }

        [Test]
        public void CancelReloadStopsBothHands()
        {
            var hands = new AkimboHands(Pistol());
            Fire(hands, AkimboHand.Right, 1);
            Fire(hands, AkimboHand.Left, 1);
            hands.TryStartReload();

            hands.CancelReload();

            Assert.That(hands.IsReloading, Is.False);
            Assert.That(hands.AmmoInMagazine(AkimboHand.Right), Is.EqualTo(7));
            Assert.That(hands.AmmoInMagazine(AkimboHand.Left), Is.EqualTo(7));
        }

        [Test]
        public void AWeaponWithoutAkimboIsRejected()
        {
            var single = new WeaponSpec(_id, null, WeaponCategory.Handgun);

            Assert.Throws<ArgumentException>(() => new AkimboHands(single));
            Assert.Throws<ArgumentNullException>(() => new AkimboHands(null));
            Assert.Throws<ArgumentNullException>(() => new AkimboHands(Pistol(), null));
        }

        [Test]
        public void AnUndefinedHandIsRejected()
        {
            var hands = new AkimboHands(Pistol());

            Assert.Throws<ArgumentOutOfRangeException>(() => hands.TryFire((AkimboHand)2));
            Assert.Throws<ArgumentOutOfRangeException>(() => hands.TryStartReload((AkimboHand)2));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ABadTickIsRejected(float seconds)
        {
            var hands = new AkimboHands(Pistol());

            Assert.Throws<ArgumentOutOfRangeException>(() => hands.Tick(seconds));
        }

        [Test]
        public void AShieldOrMeleeWeaponCannotBeAkimbo()
        {
            Assert.Throws<ArgumentException>(
                () => new WeaponSpec(_id, null, WeaponCategory.Handgun, shield: RiotShieldSpec.Default, akimbo: AkimboSpec.Default));
        }

        [Test]
        public void TheLogLinesNameTheHand()
        {
            Assert.That(AkimboLog.Fire(AkimboHand.Right, 5), Is.EqualTo("[Combat] akimbo fire hand=right ammo=5"));
            Assert.That(AkimboLog.Fire(AkimboHand.Left, 8), Is.EqualTo("[Combat] akimbo fire hand=left ammo=8"));
            Assert.That(AkimboLog.Reload(AkimboHand.Left), Is.EqualTo("[Combat] akimbo reload hand=left"));
            Assert.That(AkimboLog.Reload(AkimboHand.Right), Is.EqualTo("[Combat] akimbo reload hand=right"));
        }

        private static readonly GameRules _jamOn = GameRules.Default.WithActions(ActionRules.Default.WithJam(WeaponJam.On));

        private static WeaponSpec JammingPistol()
        {
            return new WeaponSpec(
                _id,
                null,
                WeaponCategory.Handgun,
                new TriggerSpec(FireMode.Semi, _fireInterval),
                new AmmoSpec(8, 24, _reloadSeconds, jam: new JamSpec(0.5f, 0f, 0f, 0.5f, 1f)),
                akimbo: AkimboSpec.Default);
        }

        // 確率 0.5 で下回ったら詰まる。弾倉の 8 発の内で最初に詰まる発数 (詰まらなければ 0) を、同じ乱数の列から数える。
        private static int ExpectedFirstJam(JamRoll roll)
        {
            for (var shot = 1; shot <= 8; shot++)
            {
                if (roll.Draw() < 0.5f)
                {
                    return shot;
                }
            }

            return 0;
        }

        private static int FirstJam(AkimboHands hands, AkimboHand hand)
        {
            var weapon = hands.Hand(hand);
            for (var shot = 1; shot <= 8; shot++)
            {
                Assert.That(hands.TryFire(hand), Is.True, hand + " shot " + shot);
                if (weapon.IsJammed)
                {
                    return shot;
                }

                hands.Tick(_fireInterval);
            }

            return 0;
        }

        private sealed class ConstantJamRoll : IJamRoll
        {
            private readonly float _value;

            public ConstantJamRoll(float value)
            {
                _value = value;
            }

            public float Draw()
            {
                return _value;
            }
        }

        [Test]
        public void TheLeftHandDrawsJamsFromItsOwnSeedByDefault()
        {
            Assert.That(JamRoll.SeedFor(_id), Is.Not.EqualTo(JamRoll.SeedFor(_id + ":left")));

            var hands = new AkimboHands(JammingPistol(), 8, 8, 16, _jamOn);

            Assert.That(FirstJam(hands, AkimboHand.Right), Is.EqualTo(ExpectedFirstJam(JamRoll.ForWeapon(_id))));
            Assert.That(FirstJam(hands, AkimboHand.Left), Is.EqualTo(ExpectedFirstJam(JamRoll.ForWeapon(_id + ":left"))));
        }

        [Test]
        public void AGivenLeftJamRollIsUsedForTheLeftHandOnly()
        {
            var hands = new AkimboHands(JammingPistol(), 8, 8, 16, _jamOn, new ConstantJamRoll(0.9f), new ConstantJamRoll(0f));

            Assert.That(FirstJam(hands, AkimboHand.Left), Is.EqualTo(1));
            Assert.That(FirstJam(hands, AkimboHand.Right), Is.EqualTo(0));
        }

        private sealed class TestPool : IAmmoPool
        {
            private readonly int _max;

            public TestPool(int count, int max)
            {
                Count = count;
                _max = max;
            }

            public event Action Changed;

            public string Id => "pistol-ammo";

            public int Count { get; private set; }

            public int Room => _max - Count;

            public int Take(int count)
            {
                var taken = Math.Min(count, Count);
                Count -= taken;
                Changed?.Invoke();
                return taken;
            }

            public int Put(int count)
            {
                var put = Math.Min(count, Room);
                Count += put;
                Changed?.Invoke();
                return put;
            }
        }
    }
}
