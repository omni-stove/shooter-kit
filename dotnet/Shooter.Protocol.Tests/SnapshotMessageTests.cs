using System;
using System.Numerics;
using GameKit.Net;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class SnapshotMessageTests
    {
        [Test]
        public void FullSnapshotRoundTrips()
        {
            var sent = Build(100, 0f);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.BaselineTick, Is.EqualTo(SnapshotMessage.NoBaseline));
            AssertSame(sent, got);
        }

        [Test]
        public void TheJumpRuleRoundTrips()
        {
            var sent = Build(100, 0f);
            sent.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithJump(Jump.Off)));

            var got = Send(sent, null);

            Assert.That(got.Match.Rules.Jump, Is.EqualTo(Jump.Off));
            Assert.That(Send(Build(100, 0f), null).Match.Rules.Jump, Is.EqualTo(Jump.On));
        }

        [Test]
        public void TheAimWhileReloadingBlockRoundTrips()
        {
            var sent = Build(100, 0f);
            sent.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithAimWhileReloading(AimWhileReloading.Off)));

            var got = Send(sent, null);

            Assert.That(got.Match.Rules.BlocksAimWhileReloading, Is.True);
            Assert.That(Send(Build(100, 0f), null).Match.Rules.BlocksAimWhileReloading, Is.False);
        }

        [Test]
        public void TheRoundPhaseRoundTrips()
        {
            var sent = Build(100, 0f);
            sent.Match.Round = RoundPhase.Live;

            var got = Send(sent, null);

            Assert.That(got.Match.HasRound, Is.True);
            Assert.That(got.Match.Round, Is.EqualTo(RoundPhase.Live));
        }

        [Test]
        public void AnEliminationMatchCarriesItsRoundWinsAsPoints()
        {
            var sent = Build(100, 0f);
            sent.Match.Objective = MatchObjective.Elimination;
            sent.Match.AlphaPoints = 2;
            sent.Match.BravoPoints = 1;

            var got = Send(sent, null);

            Assert.That(got.Match.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(got.Match.AlphaPoints, Is.EqualTo(2));
            Assert.That(got.Match.BravoPoints, Is.EqualTo(1));
        }

        [Test]
        public void AKillConfirmedMatchCarriesItsDeniedCountsBesideItsConfirms()
        {
            var sent = Build(100, 0f);
            sent.Match.Objective = MatchObjective.KillConfirmed;
            sent.Match.AlphaPoints = 4;
            sent.Match.BravoPoints = 2;
            sent.Match.AlphaDenied = 3;
            sent.Match.BravoDenied = 5;

            var got = Send(sent, null);

            Assert.That(got.Match.AlphaPoints, Is.EqualTo(4));
            Assert.That(got.Match.BravoPoints, Is.EqualTo(2));
            Assert.That(got.Match.AlphaDenied, Is.EqualTo(3));
            Assert.That(got.Match.BravoDenied, Is.EqualTo(5));
        }

        [Test]
        public void AChangedDeniedCountSendsTheObjectivePartAndResolves()
        {
            // 目的の部分は目的の種類 (1 バイト)、点 2 つと阻止の数 2 つ (4 バイトずつ)
            const int objectiveBytes = 1 + 4 + 4 + 4 + 4;
            var baseline = Build(100, 0f);
            var unchanged = Build(101, 0f);
            var current = Build(101, 0f);
            current.Match.BravoDenied = 1;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(objectiveBytes));
            Assert.That(got.TryResolve(received), Is.True);
            AssertSame(current, got);
        }

        [Test]
        public void AnUnchangedDeniedCountIsTakenFromTheBaseline()
        {
            var baseline = Build(100, 0f);
            baseline.Match.AlphaDenied = 2;
            var current = Build(101, 0f);
            current.Match.AlphaDenied = 2;
            current.Match.AlphaScore = 9;
            var received = Send(baseline, null);

            var got = Send(current, baseline);
            Assert.That(got.TryResolve(received), Is.True);

            Assert.That(got.Match.AlphaDenied, Is.EqualTo(2));
        }

        [Test]
        public void ANegativeDeniedCountIsNotRead()
        {
            var sent = Build(100, 0f);
            sent.Match.BravoDenied = -1;
            var writer = new MessageWriter();
            writer.WriteMessage(sent);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void AMatchWithoutRoundsCarriesNoRoundPhase()
        {
            var sent = Build(100, 0f);
            sent.Match.HasRound = false;
            sent.Match.Round = RoundPhase.Ended;

            var got = Send(sent, null);

            Assert.That(got.Match.HasRound, Is.False);
            Assert.That(got.Match.Round, Is.EqualTo(RoundPhase.Waiting));
        }

        [Test]
        public void AChangedRoundPhaseSendsOnlyItsTwoBytesAndResolves()
        {
            var baseline = Build(100, 0f);
            var unchanged = Build(101, 0f);
            var current = Build(101, 0f);
            current.Match.Round = RoundPhase.Live;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(2));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.TryResolve(received), Is.True);
            AssertSame(current, got);
        }

        [Test]
        public void AnUnchangedRoundPhaseIsTakenFromTheBaseline()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 1f);
            current.Match.AlphaScore = 7;
            var received = Send(baseline, null);

            var got = Send(current, baseline);
            Assert.That(got.TryResolve(received), Is.True);

            Assert.That(got.Match.HasRound, Is.True);
            Assert.That(got.Match.Round, Is.EqualTo(RoundPhase.Preparing));
        }

        [Test]
        public void DeltaCarriesOnlyChangedPartsAndResolvesToTheSameValues()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 1f);
            current.Players[1].Health = 40;
            current.Players[1].HasKiller = true;
            current.Players[1].KillerId = 1;
            current.Match.AlphaScore = 3;
            current.Owner.Weapon = new WeaponSync { AmmoInMagazine = 10, ReserveAmmo = 90, EquippedIndex = 1, WeaponCount = 2 };

            var full = Size(current, null);
            var deltaSize = Size(current, baseline);
            var received = Send(baseline, null);
            var got = Send(current, baseline);

            Assert.That(deltaSize, Is.LessThan(full / 2));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.BaselineTick, Is.EqualTo(100));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Motion));
            Assert.That(got.Players[1].Present, Is.EqualTo(PlayerFields.Motion | PlayerFields.Health | PlayerFields.Score));
            Assert.That(got.Grenades[0].Present, Is.EqualTo(GrenadeFields.Transform));
            Assert.That(got.Projectiles[0].Present, Is.EqualTo(ProjectileFields.Motion));
            Assert.That(got.ControlPoints[0].Present, Is.EqualTo(ControlPointFields.None));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.IsComplete, Is.True);
            AssertSame(current, got);
        }

        [Test]
        public void UnchangedSnapshotIsSmall()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);

            // tick・基準の tick・試合と持ち主の部分のビット・処理済みの入力・並びの数と、物ごとの Id と部分のビットだけになる。
            // 乗り物・目的の物・設置物は並びの数だけ。
            // 試合と持ち主の部分のビットは 2 バイトずつ。
            var expected = 2 + 1 + 4 + 4 + 2 + 1 + 2 + 4 + 4 + (2 * 6) + 6 + 6 + 6 + 1 + 1 + 1;
            Assert.That(Size(current, baseline), Is.EqualTo(expected));
        }

        [Test]
        public void AGrenadeThatSticksSendsOnlyItsState()
        {
            var baseline = Build(100, 0f);
            baseline.Grenades[0].IsStuck = false;
            var current = Build(101, 0f);

            var got = Send(current, baseline);

            Assert.That(got.Grenades[0].Present, Is.EqualTo(GrenadeFields.State));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Grenades[0].IsStuck, Is.True);
            Assert.That(got.Grenades[0].TypeId, Is.EqualTo("sticky"));
        }

        [Test]
        public void AGrenadeCarriesWhoItIsStuckTo()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Grenades[0].HasStuckTo = true;
            current.Grenades[0].StuckToId = 2;

            var got = Send(current, baseline);

            Assert.That(got.Grenades[0].Present, Is.EqualTo(GrenadeFields.State));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Grenades[0].IsStuck, Is.True);
            Assert.That(got.Grenades[0].HasStuckTo, Is.True);
            Assert.That(got.Grenades[0].StuckToId, Is.EqualTo(2));

            var released = Send(Build(102, 0f), current);
            Assert.That(released.Grenades[0].Present, Is.EqualTo(GrenadeFields.State));
            Assert.That(released.TryResolve(Send(current, null)), Is.True);
            Assert.That(released.Grenades[0].HasStuckTo, Is.False);
        }

        [Test]
        public void ThePouchCarriesWhatIsLeftOfEachGrenadeType()
        {
            var got = Send(Build(100, 0f), null);
            var pouch = got.Owner.Grenades;

            Assert.That(pouch.Enabled, Is.True);
            Assert.That(pouch.Count, Is.EqualTo(2));
            Assert.That(pouch.TypeIdAt(0), Is.EqualTo("frag"));
            Assert.That(pouch.RemainingAt(0), Is.EqualTo(2));
            Assert.That(pouch.TypeIdAt(1), Is.EqualTo("smoke"));
            Assert.That(pouch.RemainingOf("smoke"), Is.EqualTo(1));
            Assert.That(pouch.RemainingOf("flash"), Is.Zero);
            Assert.That(pouch.TotalRemaining, Is.EqualTo(3));
        }

        [Test]
        public void AChangeInOneGrenadeTypeIsSentAndResolved()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Owner.Grenades.Clear();
            current.Owner.Grenades.Enabled = true;
            current.Owner.Grenades.Add("frag", 1);
            current.Owner.Grenades.Add("smoke", 1);

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.Grenades.RemainingOf("frag"), Is.EqualTo(1));
            AssertSame(current, got);
        }

        [Test]
        public void AChangedCarrySpeedMultiplierIsSentAndAnUnchangedOneIsFilledFromTheBaseline()
        {
            var baseline = Build(100, 0f);
            var changed = Build(101, 0f);
            changed.Owner.CarrySpeedMultiplier = 0.6f;
            var unchanged = Build(102, 0f);

            var gotChanged = Send(changed, baseline);
            var gotUnchanged = Send(unchanged, baseline);

            Assert.That(gotChanged.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotChanged.Owner.CarrySpeedMultiplier, Is.EqualTo(0.6f));
            Assert.That(gotUnchanged.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotUnchanged.Owner.CarrySpeedMultiplier, Is.EqualTo(0.85f));
        }

        [Test]
        public void ANegativeCarrySpeedMultiplierIsUnreadable()
        {
            var current = Build(100, 0f);
            current.Owner.CarrySpeedMultiplier = -0.5f;
            var writer = new MessageWriter();
            writer.WriteMessage(current);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void ThePouchRejectsTheSameTypeTwice()
        {
            var pouch = new GrenadePouchSync();
            pouch.Add("frag", 1);

            Assert.Throws<ArgumentException>(() => pouch.Add("frag", 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => pouch.Add("smoke", -1));
        }

        [Test]
        public void WeaponHeatOutsideZeroToOneIsUnreadable()
        {
            var current = Build(100, 0f);
            var weapon = current.Owner.Weapon;
            weapon.Heat = 1.5f;
            current.Owner.Weapon = weapon;
            var writer = new MessageWriter();
            writer.WriteMessage(current);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void TheOffhandMagazineAndReloadRoundTrip()
        {
            var sent = Build(100, 0f);

            var got = Send(sent, null).Owner.Weapon;

            Assert.That(got.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(got.OffhandAmmoInMagazine, Is.EqualTo(4));
            Assert.That(got.IsOffhandReloading, Is.True);
            Assert.That(got.OffhandReloadStartedAt, Is.EqualTo(13.25));
        }

        [Test]
        public void ASingleWeaponSendsAnEmptyOffhand()
        {
            var single = Build(100, 0f);
            single.Owner.Weapon = new WeaponSync { AmmoInMagazine = 12, ReserveAmmo = 36, WeaponCount = 1 };

            var weapon = Send(single, null).Owner.Weapon;

            Assert.That(weapon.OffhandAmmoInMagazine, Is.Zero);
            Assert.That(weapon.IsOffhandReloading, Is.False);
            Assert.That(weapon.OffhandReloadStartedAt, Is.Zero);
        }

        [Test]
        public void AChangedOffhandSendsTheWeaponPartAndResolves()
        {
            // 武器の部分は弾倉・予備弾 (4 + 4)・リロード中 (1)・持っている番号と数 (1 + 1)・持ち替え中 (1)・リロードの始まり (8)・
            // リロードの秒数と窓の始まりと終わり (4 × 3)・熱 (4)・過熱 (1)・溜め (4)・弾の種類 (1)・回し (4)・
            // 左の手の弾倉 (4)・リロード中 (1)・リロードの始まり (8)・詰まり (1)・解除の終わり (8)
            const int weaponBytes = 4 + 4 + 1 + 1 + 1 + 1 + 8 + (4 * 3) + 4 + 1 + 4 + 1 + 4 + 4 + 1 + 8 + 1 + 8;
            var baseline = Build(100, 0f);
            var unchanged = Build(101, 0f);
            var current = Build(101, 0f);
            var weapon = current.Owner.Weapon;
            weapon.OffhandAmmoInMagazine = 3;
            current.Owner.Weapon = weapon;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(weaponBytes));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Owner.Weapon.OffhandAmmoInMagazine, Is.EqualTo(3));
            Assert.That(got.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
        }

        [Test]
        public void TheWeaponSyncComparesTheOffhand()
        {
            var a = new WeaponSync { AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 };

            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, OffhandAmmoInMagazine = 8 }), Is.True);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, OffhandAmmoInMagazine = 7 }), Is.False);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, OffhandAmmoInMagazine = 8, IsOffhandReloading = true }), Is.False);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, OffhandAmmoInMagazine = 8, OffhandReloadStartedAt = 1.0 }), Is.False);
        }

        [Test]
        public void TheJamAndItsClearEndRoundTrip()
        {
            var sent = Build(100, 0f);
            var weapon = sent.Owner.Weapon;
            weapon.IsJammed = true;
            weapon.JamClearEndsAt = 14.75;
            sent.Owner.Weapon = weapon;

            var got = Send(sent, null).Owner.Weapon;

            Assert.That(got.IsJammed, Is.True);
            Assert.That(got.JamClearEndsAt, Is.EqualTo(14.75));
            Assert.That(got.OffhandReloadStartedAt, Is.EqualTo(13.25), "the field before the jam still reads");
        }

        [Test]
        public void AWeaponThatIsNotJammedSendsNoClearEnd()
        {
            var weapon = Send(Build(100, 0f), null).Owner.Weapon;

            Assert.That(weapon.IsJammed, Is.False);
            Assert.That(weapon.JamClearEndsAt, Is.Zero);
        }

        [Test]
        public void AJamStartedAgainstTheBaselineIsSentAndResolves()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            var weapon = current.Owner.Weapon;
            weapon.IsJammed = true;
            weapon.JamClearEndsAt = 15.5;
            current.Owner.Weapon = weapon;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Owner.Weapon.IsJammed, Is.True);
            Assert.That(got.Owner.Weapon.JamClearEndsAt, Is.EqualTo(15.5));
            Assert.That(got.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
        }

        [Test]
        public void TheWeaponSyncComparesTheJam()
        {
            var a = new WeaponSync { AmmoInMagazine = 8, IsJammed = true, JamClearEndsAt = 2.5 };

            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, IsJammed = true, JamClearEndsAt = 2.5 }), Is.True);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, JamClearEndsAt = 2.5 }), Is.False);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, IsJammed = true }), Is.False);
            Assert.That(a.SameAs(new WeaponSync { AmmoInMagazine = 8, IsJammed = true, JamClearEndsAt = 3.0 }), Is.False);
        }

        [Test]
        public void ANegativeOffhandMagazineIsUnreadable()
        {
            var current = Build(100, 0f);
            var weapon = current.Owner.Weapon;
            weapon.OffhandAmmoInMagazine = -1;
            current.Owner.Weapon = weapon;
            var writer = new MessageWriter();
            writer.WriteMessage(current);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void AProjectileThatLeavesIsGoneFromTheResolvedSnapshot()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Projectiles.Clear();

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Projectiles.Count, Is.Zero);
        }

        [Test]
        public void ResolveFailsAgainstTheWrongBaselineAndLeavesTheSnapshotAsIs()
        {
            var baseline = Build(100, 0f);
            var other = Send(Build(99, 0f), null);
            var got = Send(Build(101, 1f), baseline);

            Assert.That(got.TryResolve(null), Is.False);
            Assert.That(got.TryResolve(other), Is.False);
            Assert.That(got.IsComplete, Is.False);
        }

        [Test]
        public void ResolveFailsWhenAPartialEntityIsMissingFromTheBaseline()
        {
            var baseline = Build(100, 0f);
            var received = Send(baseline, null);
            received.Players.Clear();
            var got = Send(Build(101, 1f), baseline);

            Assert.That(got.TryResolve(received), Is.False);
        }

        [Test]
        public void NewAndRemovedEntitiesFollowTheCurrentList()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players.Clear();
            ref var newcomer = ref current.Players.Add();
            newcomer.PlayerId = 9;
            newcomer.Team = Team.Bravo;
            newcomer.WeaponId = "pistol";
            newcomer.Health = 100;

            var got = Send(current, baseline);

            Assert.That(got.Players.Count, Is.EqualTo(1));
            Assert.That(got.Players[0].PlayerId, Is.EqualTo(9));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.All));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("pistol"));
        }

        [Test]
        public void SnapshotWithoutABaselineMustCarryEveryPart()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.Snapshot);
            writer.WriteByte(SnapshotMessage.CurrentVersion);
            writer.WriteInt32(5);
            writer.WriteInt32(SnapshotMessage.NoBaseline);
            writer.WriteUInt16((ushort)MatchFields.Status);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void BaselineMustBeOlder()
        {
            var current = Build(100, 0f);
            current.Baseline = Build(100, 0f);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(current));
        }

        [Test]
        public void DuplicatePlayerIsRejected()
        {
            var current = Build(100, 0f);
            current.Players[1].PlayerId = current.Players[0].PlayerId;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(current));
        }

        [Test]
        public void NonFinitePositionIsRejected()
        {
            var current = Build(100, 0f);
            current.Players[0].Position = new Vector3(float.NaN, 0f, 0f);
            var writer = new MessageWriter();
            writer.WriteMessage(current);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void CopyKeepsTheSnapshotWhenTheSourceIsReadAgain()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            writer.WriteMessage(Build(100, 0f));
            var read = (SnapshotMessage)table.Read(writer.AsSegment());
            var kept = new SnapshotMessage();
            kept.CopyFrom(read);

            writer.Reset();
            var next = Build(101, 5f);
            next.Owner.EquippedWeaponIds.Clear();
            writer.WriteMessage(next);
            table.Read(writer.AsSegment());

            AssertSame(Build(100, 0f), kept);
        }

        [Test]
        public void SteadyStateWriteAndReadDoNotAllocate()
        {
            var table = MatchMessages.CreateClientTable();
            var serverTable = MatchMessages.CreateServerTable();
            var writer = new MessageWriter();
            var history = new SnapshotMessage();
            var baseline = Build(0, 0f);
            var current = Build(1, 0f);
            var input = new InputMessage();
            var shot = new ShotMessage { WeaponId = "rifle", HasImpact = true, ImpactMaterialId = "concrete" };
            ref var pellet = ref shot.AddSegment();
            pellet.HasImpact = true;
            pellet.ImpactMaterialId = "metal";

            long allocated = 0;
            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var tick = 1; tick <= 50; tick++)
                {
                    current.Tick = baseline.Tick + 1;
                    current.Players[0].Position = new Vector3(tick, 0f, 0f);
                    current.Baseline = baseline;
                    writer.Reset();
                    writer.WriteMessage(current);
                    var read = (SnapshotMessage)table.Read(writer.AsSegment());
                    read.TryResolve(history.Tick == baseline.Tick ? history : null);

                    history.CopyFrom(current);
                    history.Baseline = null;
                    baseline.CopyFrom(current);

                    input.Clear();
                    input.AckedSnapshotTick = baseline.Tick;
                    input.Add(new InputFrame(tick + (round * 100), new Vector2(0f, 1f), 0.5f, InputButtons.Sprint));
                    writer.Reset();
                    writer.WriteMessage(input);
                    serverTable.Read(writer.AsSegment());

                    writer.Reset();
                    writer.WriteMessage(shot);
                    table.Read(writer.AsSegment());
                }

                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void LeanSwimWallRunAirJumpsAndAssistsRoundTrip()
        {
            var sent = Build(100, 0f);
            sent.Players[0].Lean = -1f;
            sent.Players[0].Swimming = true;
            sent.Players[0].WallRunSide = WallRunSide.Right;
            sent.Players[0].AirJumpsUsed = 2;
            sent.Players[0].Assists = 7;
            sent.Players[1].Lean = 1f;
            sent.Players[1].WallRunSide = WallRunSide.Left;
            sent.Players[1].JumpGraceLeft = 0.075f;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Players[0].Lean, Is.EqualTo(-1f));
            Assert.That(got.Players[0].Swimming, Is.True);
            Assert.That(got.Players[0].WallRunSide, Is.EqualTo(WallRunSide.Right));
            Assert.That(got.Players[0].AirJumpsUsed, Is.EqualTo(2));
            Assert.That(got.Players[0].JumpGraceLeft, Is.Zero);
            Assert.That(got.Players[0].Assists, Is.EqualTo(7));
            Assert.That(got.Players[1].Lean, Is.EqualTo(1f));
            Assert.That(got.Players[1].Swimming, Is.False);
            Assert.That(got.Players[1].WallRunSide, Is.EqualTo(WallRunSide.Left));
            Assert.That(got.Players[1].JumpGraceLeft, Is.EqualTo(0.075f));
            Assert.That(got.Players[1].Assists, Is.Zero);
            AssertSame(sent, got);
        }

        [Test]
        public void LeanIsSentInStepsOfOne127th()
        {
            var sent = Build(100, 0f);
            sent.Players[0].Lean = 0.5f;

            var got = Send(sent, null);

            Assert.That(got.Players[0].Lean, Is.EqualTo(64f / 127f));
            Assert.That(got.Players[0].ChangedBits(sent.Players[0]), Is.Zero);
        }

        [Test]
        public void AFullSnapshotLeavesOutTheNewPartsWhileTheyAreDefault()
        {
            var plain = Build(100, 0f);
            var leaning = Build(100, 0f);
            leaning.Players[0].Lean = 0.25f;
            var assisting = Build(100, 0f);
            assisting.Players[1].Assists = 1;

            // 体を傾ける・泳ぐ・壁走り・空中ジャンプの 4 バイトとジャンプの猶予の 4 バイト、パルクールの動きの 1 バイト、アシストの 4 バイト
            Assert.That(Size(leaning, null), Is.EqualTo(Size(plain, null) + 9));
            Assert.That(Size(assisting, null), Is.EqualTo(Size(plain, null) + 4));
            Assert.That(Send(plain, null).Players[0].Lean, Is.Zero);
        }

        [Test]
        public void AFullSnapshotResetsTheNewPartsLeftOutToDefault()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            var leaning = Build(100, 0f);
            leaning.Players[0].Lean = 1f;
            leaning.Players[0].Swimming = true;
            leaning.Players[0].Assists = 3;
            writer.WriteMessage(leaning);
            table.Read(writer.AsSegment());
            writer.Reset();
            writer.WriteMessage(Build(101, 0f));

            var got = (SnapshotMessage)table.Read(writer.AsSegment());

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.All));
            Assert.That(got.Players[0].Lean, Is.Zero);
            Assert.That(got.Players[0].Swimming, Is.False);
            Assert.That(got.Players[0].Assists, Is.Zero);
        }

        [Test]
        public void ADeltaCarriesTheNewPartsOnlyWhenTheyChange()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players[0].Swimming = true;
            current.Players[1].Assists = 2;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Traversal));
            Assert.That(got.Players[1].Present, Is.EqualTo(PlayerFields.Assists));
            Assert.That(got.TryResolve(received), Is.True);
            AssertSame(current, got);
        }

        [Test]
        public void ANewcomerInADeltaCarriesTheNewPartsEvenWhenDefault()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players[1].PlayerId = 9;

            var got = Send(current, baseline);

            Assert.That(got.Players[1].Present, Is.EqualTo(PlayerFields.All));
        }

        [TestCase(1.01f)]
        [TestCase(-1.5f)]
        [TestCase(float.NaN)]
        public void LeanOutsideMinusOneToOneIsNotWritten(float lean)
        {
            var sent = Build(100, 0f);
            sent.Players[0].Lean = lean;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void NegativeAssistsAreNotWritten()
        {
            var sent = Build(100, 0f);
            sent.Players[0].Assists = -1;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void HandWrittenNewPartsAreReadable()
        {
            var got = (SnapshotMessage)MatchMessages.CreateClientTable().Read(OnePlayerDelta(PlayerFields.Traversal | PlayerFields.Assists, w =>
            {
                w.WriteByte(unchecked((byte)(sbyte)-127));
                w.WriteBool(true);
                w.WriteByte((byte)WallRunSide.Left);
                w.WriteByte(1);
                w.WriteSingle(0.05f);
                w.WriteByte((byte)ParkourMove.LedgeShimmy);
                w.WriteInt32(4);
            }));

            Assert.That(got.Players[0].Lean, Is.EqualTo(-1f));
            Assert.That(got.Players[0].Swimming, Is.True);
            Assert.That(got.Players[0].WallRunSide, Is.EqualTo(WallRunSide.Left));
            Assert.That(got.Players[0].AirJumpsUsed, Is.EqualTo(1));
            Assert.That(got.Players[0].JumpGraceLeft, Is.EqualTo(0.05f));
            Assert.That(got.Players[0].ParkourMove, Is.EqualTo(ParkourMove.LedgeShimmy));
            Assert.That(got.Players[0].Assists, Is.EqualTo(4));
        }

        [Test]
        public void ParkourMovesRoundTrip()
        {
            var sent = Build(100, 0f);
            sent.Players[0].ParkourMove = ParkourMove.WallKick;
            sent.Players[1].ParkourMove = ParkourMove.LedgeClimbUp;

            var got = Send(sent, null);

            Assert.That(got.Players[0].ParkourMove, Is.EqualTo(ParkourMove.WallKick));
            Assert.That(got.Players[1].ParkourMove, Is.EqualTo(ParkourMove.LedgeClimbUp));
            AssertSame(sent, got);
        }

        [Test]
        public void ADeltaCarriesTheParkourMoveWhenOnlyItChanges()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players[0].ParkourMove = ParkourMove.LandingRoll;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(current.Players[0].ChangedBits(baseline.Players[0]), Is.EqualTo((int)PlayerFields.Traversal));
            Assert.That(current.Players[1].ChangedBits(baseline.Players[1]), Is.Zero);
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Traversal));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].ParkourMove, Is.EqualTo(ParkourMove.LandingRoll));
            AssertSame(current, got);
        }

        [Test]
        public void ADeltaWithoutTheTraversalPartKeepsTheParkourMoveOfTheBaseline()
        {
            var baseline = Build(100, 0f);
            baseline.Players[0].ParkourMove = ParkourMove.WallClimb;
            var current = Build(101, 0f);
            current.Players[0].ParkourMove = ParkourMove.WallClimb;
            current.Players[0].Assists = 1;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Assists));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].ParkourMove, Is.EqualTo(ParkourMove.WallClimb));
        }

        [Test]
        public void AFullSnapshotLeavesOutTheParkourMoveWhileItIsNone()
        {
            var plain = Build(100, 0f);
            var kicking = Build(100, 0f);
            kicking.Players[0].ParkourMove = ParkourMove.WallKick;

            // 体を傾ける・泳ぐ・壁走り・空中ジャンプの 4 バイトとジャンプの猶予の 4 バイト、パルクールの動きの 1 バイト
            Assert.That(Size(kicking, null), Is.EqualTo(Size(plain, null) + 9));
            Assert.That(Send(plain, null).Players[0].ParkourMove, Is.EqualTo(ParkourMove.None));
        }

        [Test]
        public void AnUndefinedParkourMoveIsNotWritten()
        {
            var sent = Build(100, 0f);
            sent.Players[0].ParkourMove = (ParkourMove)7;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnUndefinedParkourMoveIsRejected()
        {
            var data = OnePlayerDelta(PlayerFields.Traversal, w =>
            {
                w.WriteByte(0);
                w.WriteBool(false);
                w.WriteByte((byte)WallRunSide.None);
                w.WriteByte(0);
                w.WriteSingle(0f);
                w.WriteByte(7);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void TheParkourMoveNumbersMatchTheCoreOnes()
        {
            var names = Enum.GetNames<ParkourMove>();

            Assert.That(names, Is.EqualTo(Enum.GetNames<Shooter.Core.ParkourMove>()));
            foreach (var name in names)
            {
                Assert.That(
                    (byte)Enum.Parse<ParkourMove>(name),
                    Is.EqualTo((byte)Enum.Parse<Shooter.Core.ParkourMove>(name)),
                    name);
            }
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AJumpGraceThatIsNegativeOrNotFiniteIsNotWritten(float grace)
        {
            var sent = Build(100, 0f);
            sent.Players[0].JumpGraceLeft = grace;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void AJumpGraceThatIsNegativeOrNotFiniteIsRejected(float grace)
        {
            var data = OnePlayerDelta(PlayerFields.Traversal, w =>
            {
                w.WriteByte(0);
                w.WriteBool(false);
                w.WriteByte((byte)WallRunSide.None);
                w.WriteByte(0);
                w.WriteSingle(grace);
                w.WriteByte((byte)ParkourMove.None);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void ADeltaCarriesTheJumpGraceWhenOnlyItChanges()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players[0].JumpGraceLeft = 0.1f;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Traversal));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].JumpGraceLeft, Is.EqualTo(0.1f));
            AssertSame(current, got);
        }

        [TestCase(0x80, 0, 0)]
        [TestCase(0, 3, 0)]
        [TestCase(0, 0, 2)]
        public void UnreadableTraversalIsRejected(int lean, int wallRunSide, int swimming)
        {
            var data = OnePlayerDelta(PlayerFields.Traversal, w =>
            {
                w.WriteByte((byte)lean);
                w.WriteByte((byte)swimming);
                w.WriteByte((byte)wallRunSide);
                w.WriteByte(0);
                w.WriteSingle(0f);
                w.WriteByte((byte)ParkourMove.None);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void NegativeAssistsAreUnreadable()
        {
            var data = OnePlayerDelta(PlayerFields.Assists, w => w.WriteInt32(-1));

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [TestCase(16, true)]
        [TestCase(16 | 1, true)]
        [TestCase(8, false)]
        public void TheParachuteIsReadFromTheMotionFlags(int flags, bool parachuting)
        {
            var got = (SnapshotMessage)MatchMessages.CreateClientTable().Read(OnePlayerDelta(PlayerFields.Motion, w =>
            {
                w.WriteVector3(new Vector3(0f, 80f, 0f));
                w.WriteVector3(new Vector3(0f, -5f, 0f));
                w.WriteSingle(0f);
                w.WriteByte((byte)flags);
            }));

            Assert.That(got.Players[0].IsParachuting, Is.EqualTo(parachuting));
            Assert.That(got.Players[0].Position, Is.EqualTo(new Vector3(0f, 80f, 0f)));
        }

        [Test]
        public void AnUnknownMotionFlagIsRejected()
        {
            var data = OnePlayerDelta(PlayerFields.Motion, w =>
            {
                w.WriteVector3(Vector3.Zero);
                w.WriteVector3(Vector3.Zero);
                w.WriteSingle(0f);
                w.WriteByte(32);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void AFullSnapshotMustStillCarryTheOlderParts()
        {
            var data = OnePlayer(null, PlayerFields.Optional, w =>
            {
                w.WriteByte(0);
                w.WriteBool(false);
                w.WriteByte(0);
                w.WriteByte(0);
                w.WriteInt32(0);
                w.WriteByte(0);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void AFreshPlayerIsDrawnWithNothingLowered()
        {
            var got = Send(Build(100, 0f), null);

            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
            Assert.That(got.Players[0].HolsterLowering, Is.Zero);
        }

        [TestCase(HolsterPhase.Holstering, 0.5f)]
        [TestCase(HolsterPhase.Drawing, 0.25f)]
        public void AMovingHolsterRoundTripsWithItsLoweringInSteps(HolsterPhase phase, float lowering)
        {
            var sent = Build(100, 0f);
            sent.Players[0].HolsterPhase = phase;
            sent.Players[0].HolsterLowering = lowering;

            var got = Send(sent, null);

            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(phase));
            Assert.That(got.Players[0].HolsterLowering, Is.EqualTo(lowering).Within(1f / 255f));
            Assert.That(got.Players[1].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
            AssertSame(sent, got);
        }

        [Test]
        public void AHolsteredPlayerIsReadFullyLowered()
        {
            var sent = Build(100, 0f);
            sent.Players[0].HolsterPhase = HolsterPhase.Holstered;
            sent.Players[0].HolsterLowering = 1f;

            var got = Send(sent, null);

            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Holstered));
            Assert.That(got.Players[0].HolsterLowering, Is.EqualTo(1f));
        }

        // 武器の部分は Id の文字列 (長さ 2 + "rifle" 5)・状態 (1)。動いている間だけ下げ具合 (1) が足される
        [Test]
        public void ChangingOnlyTheHolsterSendsTheWeaponPartAndOnlyAMovingOneCarriesTheLowering()
        {
            const int stoppedWeaponBytes = 2 + 5 + 1;
            var baseline = Build(100, 0f);
            var unchanged = Build(101, 0f);
            var holstered = Build(101, 0f);
            holstered.Players[0].HolsterPhase = HolsterPhase.Holstered;
            holstered.Players[0].HolsterLowering = 1f;
            var holstering = Build(101, 0f);
            holstering.Players[0].HolsterPhase = HolsterPhase.Holstering;
            holstering.Players[0].HolsterLowering = 0.4f;
            var received = Send(baseline, null);

            var got = Send(holstering, baseline);

            Assert.That(Size(holstered, baseline) - Size(unchanged, baseline), Is.EqualTo(stoppedWeaponBytes));
            Assert.That(Size(holstering, baseline) - Size(unchanged, baseline), Is.EqualTo(stoppedWeaponBytes + 1));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Weapon));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Holstering));
            Assert.That(got.Players[0].HolsterLowering, Is.EqualTo(0.4f).Within(0.5f / 255f));
        }

        [Test]
        public void ALoweringChangeSmallerThanAStepIsNotSent()
        {
            var baseline = Build(100, 0f);
            baseline.Players[0].HolsterPhase = HolsterPhase.Drawing;
            baseline.Players[0].HolsterLowering = 0.5f;
            var current = Build(101, 0f);
            current.Players[0].HolsterPhase = HolsterPhase.Drawing;
            current.Players[0].HolsterLowering = 0.5f + (0.1f / 255f);

            Assert.That(current.Players[0].ChangedBits(baseline.Players[0]), Is.Zero);

            current.Players[0].HolsterLowering = 0.5f + (2f / 255f);
            Assert.That(current.Players[0].ChangedBits(baseline.Players[0]), Is.EqualTo((int)PlayerFields.Weapon));
        }

        [TestCase(HolsterPhase.Drawn, 0.5f)]
        [TestCase(HolsterPhase.Holstered, 0.5f)]
        [TestCase(HolsterPhase.Holstering, 1.5f)]
        [TestCase(HolsterPhase.Drawing, -0.1f)]
        [TestCase(HolsterPhase.Drawing, float.NaN)]
        public void ALoweringThatDoesNotFitThePhaseIsNotWritten(HolsterPhase phase, float lowering)
        {
            var sent = Build(100, 0f);
            sent.Players[0].HolsterPhase = phase;
            sent.Players[0].HolsterLowering = lowering;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnUndefinedHolsterPhaseIsNotWritten()
        {
            var sent = Build(100, 0f);
            sent.Players[0].HolsterPhase = (HolsterPhase)Enum.GetValues<HolsterPhase>().Length;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnUndefinedHolsterPhaseIsNotRead()
        {
            var data = OnePlayerDelta(PlayerFields.Weapon, w =>
            {
                w.WriteString("rifle");
                w.WriteByte((byte)Enum.GetValues<HolsterPhase>().Length);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void AMovingHolsterWithoutItsLoweringIsNotRead()
        {
            var data = OnePlayerDelta(PlayerFields.Weapon, w =>
            {
                w.WriteString("rifle");
                w.WriteByte((byte)HolsterPhase.Drawing);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void AFreshPlayerHasTheWeaponLightOff()
        {
            var got = Send(Build(100, 0f), null);

            Assert.That(got.Players[0].WeaponLightOn, Is.False);
            Assert.That(got.Players[1].WeaponLightOn, Is.False);
        }

        [TestCase(HolsterPhase.Drawn, 0f)]
        [TestCase(HolsterPhase.Drawing, 0.5f)]
        public void TheWeaponLightRoundTripsWithTheHolster(HolsterPhase phase, float lowering)
        {
            var sent = Build(100, 0f);
            sent.Players[0].WeaponLightOn = true;
            sent.Players[0].HolsterPhase = phase;
            sent.Players[0].HolsterLowering = lowering;

            var got = Send(sent, null);

            Assert.That(got.Players[0].WeaponLightOn, Is.True);
            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(phase));
            Assert.That(got.Players[0].HolsterLowering, Is.EqualTo(lowering).Within(1f / 255f));
            Assert.That(got.Players[1].WeaponLightOn, Is.False);
            AssertSame(sent, got);
        }

        // 点灯はしまう状態のバイトの 1 ビットなので、点けても消しても武器の部分の大きさ (Id の文字列 2 + "rifle" 5・状態 1) は変わらない
        [Test]
        public void TurningTheLightOnSendsOnlyTheWeaponPartWithoutAnExtraByte()
        {
            const int weaponBytes = 2 + 5 + 1;
            var baseline = Build(100, 0f);
            var unchanged = Build(101, 0f);
            var lit = Build(101, 0f);
            lit.Players[0].WeaponLightOn = true;
            var received = Send(baseline, null);

            var got = Send(lit, baseline);

            Assert.That(lit.Players[0].ChangedBits(baseline.Players[0]), Is.EqualTo((int)PlayerFields.Weapon));
            Assert.That(Size(lit, baseline) - Size(unchanged, baseline), Is.EqualTo(weaponBytes));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Weapon));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].WeaponLightOn, Is.True);
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
        }

        [Test]
        public void AnUnchangedLightIsNotSentAgainAndTurningItOffIs()
        {
            var baseline = Build(100, 0f);
            baseline.Players[0].WeaponLightOn = true;
            var stillLit = Build(101, 0f);
            stillLit.Players[0].WeaponLightOn = true;
            var off = Build(101, 0f);
            var received = Send(baseline, null);

            Assert.That(stillLit.Players[0].ChangedBits(baseline.Players[0]), Is.Zero);
            Assert.That(off.Players[0].ChangedBits(baseline.Players[0]), Is.EqualTo((int)PlayerFields.Weapon));

            var keptLit = Send(stillLit, baseline);
            Assert.That(keptLit.TryResolve(received), Is.True);
            Assert.That(keptLit.Players[0].WeaponLightOn, Is.True, "a row without the weapon part keeps the light of the baseline");

            var turnedOff = Send(off, baseline);
            Assert.That(turnedOff.TryResolve(received), Is.True);
            Assert.That(turnedOff.Players[0].WeaponLightOn, Is.False);
        }

        [Test]
        public void ALitLightWithoutAWeaponIsNotWritten()
        {
            var sent = Build(100, 0f);
            sent.Players[0].WeaponId = "";
            sent.Players[0].WeaponLightOn = true;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void ALitLightWithoutAWeaponIsNotRead()
        {
            var data = OnePlayerDelta(PlayerFields.Weapon, w =>
            {
                w.WriteString("");
                w.WriteByte(0x10);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        // 0x10 より上のビットは使っていないので、立っていれば知らない状態として断る
        [Test]
        public void AnUnusedBitOfTheHolsterByteIsNotRead()
        {
            var data = OnePlayerDelta(PlayerFields.Weapon, w =>
            {
                w.WriteString("rifle");
                w.WriteByte(0x20);
            });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(data));
        }

        [Test]
        public void ALitLightIsReadFromTheHolsterByte()
        {
            var data = OnePlayerDelta(PlayerFields.Weapon, w =>
            {
                w.WriteString("rifle");
                w.WriteByte(0x10 | (byte)HolsterPhase.Drawn);
            });

            var got = (SnapshotMessage)MatchMessages.CreateClientTable().Read(data);

            Assert.That(got.Players[0].WeaponLightOn, Is.True);
            Assert.That(got.Players[0].HolsterPhase, Is.EqualTo(HolsterPhase.Drawn));
        }

        // Build と同じ試合の部分に、player 1 の bits の部分だけを parts で手で書き足した、持ち主も他の物も無いスナップショット
        private static ArraySegment<byte> OnePlayerDelta(PlayerFields bits, Action<MessageWriter> parts)
        {
            return OnePlayer(Build(4, 0f), bits, parts);
        }

        private static ArraySegment<byte> OnePlayer(SnapshotMessage baseline, PlayerFields bits, Action<MessageWriter> parts)
        {
            const int listCount = 7;
            var empty = Build(5, 0f);
            empty.HasOwner = false;
            empty.Players.Clear();
            empty.Grenades.Clear();
            empty.ControlPoints.Clear();
            empty.Projectiles.Clear();
            empty.Vehicles.Clear();
            empty.ObjectiveItems.Clear();
            empty.Deployables.Clear();
            empty.Baseline = baseline;
            var head = new MessageWriter();
            head.WriteMessage(empty);
            var bytes = head.ToArray();

            var writer = new MessageWriter();
            writer.WriteBytes(new ArraySegment<byte>(bytes, 0, bytes.Length - listCount));
            writer.WriteByte(1);
            writer.WriteInt32(1);
            writer.WriteUInt16((ushort)bits);
            parts(writer);
            for (var i = 1; i < listCount; i++)
            {
                writer.WriteByte(0);
            }

            return new ArraySegment<byte>(writer.ToArray());
        }

        private static SnapshotMessage Build(int tick, float moved)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.HasWinner = false;
            match.AlphaScore = 1;
            match.BravoScore = 2;
            match.RemainingSeconds = 300;
            match.NextWaveSeconds = 5;
            match.Rules = MatchRulesSync.From(GameRules.Default);
            match.Objective = MatchObjective.Domination;
            match.AlphaPoints = 10;
            match.BravoPoints = 20;
            match.HasRound = true;
            match.Round = RoundPhase.Preparing;

            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = tick - 2;
            snapshot.Owner.Weapon = new WeaponSync
            {
                AmmoInMagazine = 30,
                ReserveAmmo = 90,
                IsReloading = true,
                EquippedIndex = 0,
                WeaponCount = 2,
                ReloadStartedAt = 12.5,
                ReloadSeconds = 2f,
                ActiveReloadWindowStart = 0.4f,
                ActiveReloadWindowEnd = 0.6f,
                Heat = 0.75f,
                ChargeFraction = 0.5f,
                AmmoTypeIndex = 2,
                SpinUpFraction = 0.25f,
                OffhandAmmoInMagazine = 4,
                IsOffhandReloading = true,
                OffhandReloadStartedAt = 13.25,
            };
            snapshot.Owner.Grenades.Enabled = true;
            snapshot.Owner.Grenades.Add("frag", 2);
            snapshot.Owner.Grenades.Add("smoke", 1);
            snapshot.Owner.EquippedWeaponIds.Add("rifle");
            snapshot.Owner.EquippedWeaponIds.Add("pistol");
            snapshot.Owner.CarrySpeedMultiplier = 0.85f;

            for (var i = 0; i < 2; i++)
            {
                ref var player = ref snapshot.Players.Add();
                player.PlayerId = i + 1;
                player.Team = i == 0 ? Team.Alpha : Team.Bravo;
                player.IsBot = i == 1;
                player.Position = new Vector3(i + moved, 1f, 2f);
                player.Velocity = new Vector3(0f, -2f, 3f);
                player.Yaw = 0.75f;
                player.IsGrounded = true;
                player.IsCrouched = i == 1;
                player.IsAiming = true;
                player.Cover = new CoverSync(
                    new CoverSnapshot(CoverPhase.Peeking, CoverHeight.Low, new Vector3(0f, 0f, 1f), 0.5f, 0.1f, 0.2f, -1f),
                    1.2f,
                    CoverEdge.Left | CoverEdge.Right,
                    0.4f);
                player.Dodge = new DodgeSnapshot(true, new Vector2(1f, 0f), 0.1f, 0.5f);
                player.Vault = new VaultSync(true, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), new Vector3(0f, 0f, 2f), 0.3f);
                player.Health = 80;
                player.LastHitDirection = new Vector3(0f, 0f, -1f);
                player.Downed = new DownedSync(i == 1, 12, 40);
                player.WeaponId = "rifle";
                player.Kills = 3;
                player.Deaths = 1;
                player.HasKiller = i == 0;
                player.KillerId = i == 0 ? 2 : 0;
                player.SpawnProtectionSeconds = 2;
            }

            ref var grenade = ref snapshot.Grenades.Add();
            grenade.GrenadeId = 5;
            grenade.HasThrower = true;
            grenade.ThrowerId = 1;
            grenade.WeaponId = "frag";
            grenade.Position = new Vector3(moved, 2f, 0f);
            grenade.Rotation = Quaternion.CreateFromYawPitchRoll(moved, 0f, 0f);
            grenade.TypeId = "sticky";
            grenade.IsStuck = true;

            ref var projectile = ref snapshot.Projectiles.Add();
            projectile.ProjectileId = 11;
            projectile.HasShooter = true;
            projectile.ShooterId = 2;
            projectile.WeaponId = "crossbow";
            projectile.Position = new Vector3(moved, 1.5f, 4f);
            projectile.Velocity = new Vector3(0f, -1f, 60f);

            ref var point = ref snapshot.ControlPoints.Add();
            point.Index = 0;
            point.HasOwner = true;
            point.Owner = Team.Bravo;
            point.ControlPercent = -60;
            point.Contested = true;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment()));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.Length;
        }

        private static void AssertSame(SnapshotMessage expected, SnapshotMessage actual)
        {
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
            var em = expected.Match;
            var am = actual.Match;
            Assert.That(am.State, Is.EqualTo(em.State));
            Assert.That(am.HasWinner, Is.EqualTo(em.HasWinner));
            Assert.That(am.AlphaScore, Is.EqualTo(em.AlphaScore));
            Assert.That(am.BravoScore, Is.EqualTo(em.BravoScore));
            Assert.That(am.RemainingSeconds, Is.EqualTo(em.RemainingSeconds));
            Assert.That(am.NextMatchSeconds, Is.EqualTo(em.NextMatchSeconds));
            Assert.That(am.NextWaveSeconds, Is.EqualTo(em.NextWaveSeconds));
            Assert.That(am.Rules.SameAs(em.Rules), Is.True);
            Assert.That(am.Objective, Is.EqualTo(em.Objective));
            Assert.That(am.AlphaPoints, Is.EqualTo(em.AlphaPoints));
            Assert.That(am.BravoPoints, Is.EqualTo(em.BravoPoints));
            Assert.That(am.AlphaDenied, Is.EqualTo(em.AlphaDenied));
            Assert.That(am.BravoDenied, Is.EqualTo(em.BravoDenied));
            Assert.That(am.HasRound, Is.EqualTo(em.HasRound));
            Assert.That(am.Round, Is.EqualTo(em.Round));
            Assert.That(am.HasActivePoint, Is.EqualTo(em.HasActivePoint));
            Assert.That(am.ActivePointIndex, Is.EqualTo(em.ActivePointIndex));
            Assert.That(am.HasBomb, Is.EqualTo(em.HasBomb));
            Assert.That(am.Bomb, Is.EqualTo(em.Bomb));
            Assert.That(am.ObjectiveRemainingSeconds, Is.EqualTo(em.ObjectiveRemainingSeconds));

            Assert.That(actual.HasOwner, Is.EqualTo(expected.HasOwner));
            Assert.That(actual.Owner.LastProcessedInputTick, Is.EqualTo(expected.Owner.LastProcessedInputTick));
            Assert.That(actual.Owner.Weapon.SameAs(expected.Owner.Weapon), Is.True);
            Assert.That(actual.Owner.Grenades.SameAs(expected.Owner.Grenades), Is.True);
            Assert.That(actual.Owner.EquippedWeaponIds.SameAs(expected.Owner.EquippedWeaponIds), Is.True);
            Assert.That(actual.Owner.CarrySpeedMultiplier, Is.EqualTo(expected.Owner.CarrySpeedMultiplier));

            Assert.That(actual.Players.Count, Is.EqualTo(expected.Players.Count));
            for (var i = 0; i < expected.Players.Count; i++)
            {
                ref var e = ref expected.Players[i];
                ref var a = ref actual.Players[i];
                Assert.That(a.PlayerId, Is.EqualTo(e.PlayerId));
                Assert.That(a.Present, Is.EqualTo(PlayerFields.All));
                Assert.That(a.ChangedBits(e), Is.Zero, "player " + e.PlayerId);
            }

            Assert.That(actual.Grenades.Count, Is.EqualTo(expected.Grenades.Count));
            for (var i = 0; i < expected.Grenades.Count; i++)
            {
                Assert.That(actual.Grenades[i].GrenadeId, Is.EqualTo(expected.Grenades[i].GrenadeId));
                Assert.That(actual.Grenades[i].ChangedBits(expected.Grenades[i]), Is.Zero);
            }

            Assert.That(actual.Projectiles.Count, Is.EqualTo(expected.Projectiles.Count));
            for (var i = 0; i < expected.Projectiles.Count; i++)
            {
                Assert.That(actual.Projectiles[i].ProjectileId, Is.EqualTo(expected.Projectiles[i].ProjectileId));
                Assert.That(actual.Projectiles[i].ChangedBits(expected.Projectiles[i]), Is.Zero);
            }

            Assert.That(actual.ControlPoints.Count, Is.EqualTo(expected.ControlPoints.Count));
            for (var i = 0; i < expected.ControlPoints.Count; i++)
            {
                Assert.That(actual.ControlPoints[i].Index, Is.EqualTo(expected.ControlPoints[i].Index));
                Assert.That(actual.ControlPoints[i].ChangedBits(expected.ControlPoints[i]), Is.Zero);
            }

            Assert.That(actual.ObjectiveItems.Count, Is.EqualTo(expected.ObjectiveItems.Count));
            for (var i = 0; i < expected.ObjectiveItems.Count; i++)
            {
                Assert.That(actual.ObjectiveItems[i].ItemId, Is.EqualTo(expected.ObjectiveItems[i].ItemId));
                Assert.That(actual.ObjectiveItems[i].Present, Is.EqualTo(ObjectiveItemFields.All));
                Assert.That(actual.ObjectiveItems[i].ChangedBits(expected.ObjectiveItems[i]), Is.Zero);
            }

            Assert.That(actual.Deployables.Count, Is.EqualTo(expected.Deployables.Count));
            for (var i = 0; i < expected.Deployables.Count; i++)
            {
                Assert.That(actual.Deployables[i].DeployableId, Is.EqualTo(expected.Deployables[i].DeployableId));
                Assert.That(actual.Deployables[i].Present, Is.EqualTo(DeployableFields.All));
                Assert.That(actual.Deployables[i].ChangedBits(expected.Deployables[i]), Is.Zero);
            }
        }
    }
}
