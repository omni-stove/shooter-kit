using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EnemyBrainCoverExitTests
    {
        // 敵は原点の遮蔽点 A に立つ。A は +Z を遮り、B は +X を遮る。プレイヤーは +Z の 8m 先 (FireRange の内)。
        private static readonly Vector3 _player = new Vector3(0f, 0f, 8f);
        private static readonly Vector3 _far = new Vector3(0f, 0f, 15f);
        private static readonly Vector3 _flanker = new Vector3(12f, 0f, 0f);
        private static readonly CoverPoint _coverA = new CoverPoint(Vector3.Zero, Vector3.UnitZ, CoverHeight.Low);
        private static readonly CoverPoint _coverB = new CoverPoint(new Vector3(-3f, 0f, 0f), Vector3.UnitX, CoverHeight.High);

        // 視認 20m、攻撃 10m (FireRange も 10m)、見失って 2 秒、探して 6 秒、反応 1 秒、隠れて 1 秒、覗いて 1 秒。
        // 覗く回数 3・覗いてから撃つまで 0.3 秒・冷却 5 秒は既定値。
        private static EnemySpec Spec(
            float coverPeekReactionSeconds = EnemySpec.DefaultCoverPeekReactionSeconds,
            float coverCooldownSeconds = EnemySpec.DefaultCoverCooldownSeconds,
            int coverMaxPeeks = EnemySpec.DefaultCoverMaxPeeks) =>
            new EnemySpec(
                20f,
                10f,
                2f,
                6f,
                1f,
                coverHideSeconds: 1f,
                coverPeekSeconds: 1f,
                coverMaxPeeks: coverMaxPeeks,
                coverPeekReactionSeconds: coverPeekReactionSeconds,
                coverCooldownSeconds: coverCooldownSeconds);

        private static EnemyPerception Watches(Vector3 target, bool lineOfSight) =>
            new EnemyPerception(Vector3.Zero, target, lineOfSight, false, null);

        private static EnemyPerception Shot(Vector3 source, bool lineOfSight) =>
            new EnemyPerception(Vector3.Zero, source, lineOfSight, false, null, true, source, 0.9f);

        private static EnemyPerception Suppressed(Vector3 target) =>
            new EnemyPerception(Vector3.Zero, target, true, false, null, false, null, 0.9f, EnemyPerception.FullAwareness, true);

        private static EnemyPerception Nothing() => new EnemyPerception(Vector3.Zero, null, false, false, null);

        // プレイヤーに見えないところから撃たれ、A に着いて隠れ始めたところにする。
        private static EnemyBrain Hiding(EnemySpec spec, out CoverPointSelector selector, out List<EnemyState> changes)
        {
            var brain = new EnemyBrain(spec);
            brain.SetPatrolRoute(new[] { new Vector3(0f, 0f, -10f) });
            selector = new CoverPointSelector(new[] { _coverA, _coverB });
            brain.UseCover(selector);
            var log = new List<EnemyState>();
            brain.StateChanged += (_, next) => log.Add(next);
            changes = log;

            brain.Tick(0.1f, Shot(_player, false));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));
            Assert.That(brain.CoverPointIndex, Is.Zero);
            return brain;
        }

        // 隠れ終えて覗き、撃って隠れ直す。最後の 1 回は覗き終えた tick の決定を返す。
        private static EnemyDecision PeekTimes(EnemyBrain brain, int peeks, bool seen)
        {
            var last = EnemyDecision.None;
            for (var i = 0; i < peeks; i++)
            {
                brain.Tick(1f, Watches(_player, false));
                Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Peeking), $"peek {i + 1}");
                Assert.That(brain.Tick(0.5f, Watches(_player, seen)).Fire, Is.EqualTo(seen), $"peek {i + 1} fires when it sees");
                last = brain.Tick(0.5f, Watches(_player, seen));
                if (i < peeks - 1)
                {
                    Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover), $"peek {i + 1}");
                    Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding), $"peek {i + 1} hides again");
                }
            }

            return last;
        }

        [Test]
        public void ThreePeeksLeaveCoverAndKeepFiring()
        {
            var brain = Hiding(Spec(), out var selector, out var changes);

            var decision = PeekTimes(brain, 3, true);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(-1));
            Assert.That(selector.IsOccupied(0), Is.False, "点を放す");
            Assert.That(decision.LookAt, Is.EqualTo(_player));
            Assert.That(decision.Crouch, Is.False);
            Assert.That(decision.Fire, Is.True, "覗いて構えた銃口のまま撃ち続ける");
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover, EnemyState.Attack }));
        }

        [Test]
        public void MaxPeeksComeFromTheSpec()
        {
            var brain = Hiding(Spec(coverMaxPeeks: 1), out _, out _);

            PeekTimes(brain, 1, true);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
        }

        [Test]
        public void PeeksWithoutSeeingTheTargetLeaveCoverForWhereItWas()
        {
            var brain = Hiding(Spec(), out _, out var changes);

            var decision = PeekTimes(brain, 3, false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(decision.MoveTo, Is.EqualTo(_player), "撃たれた方向へ詰める");
            Assert.That(decision.Fire, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover, EnemyState.Chase }));
        }

        [Test]
        public void ATargetSeenBeyondFireRangeEndsCoverWhenThePeekStarts()
        {
            var brain = Hiding(Spec(), out var selector, out _);

            var decision = brain.Tick(1f, Watches(_far, true));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(selector.IsOccupied(0), Is.False);
            Assert.That(decision.MoveTo, Is.EqualTo(_far), "離れた相手を追う");
            Assert.That(decision.LookAt, Is.EqualTo(_far));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void AShotFromBeyondFireRangeEndsCoverAndGoesThere()
        {
            var brain = Hiding(Spec(), out _, out _);

            var decision = brain.Tick(1f, Shot(_far, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(_far));
        }

        [Test]
        public void ATargetMovingAwayUnseenDoesNotEndCover()
        {
            var brain = Hiding(Spec(), out _, out _);

            var decision = brain.Tick(1f, Watches(_far, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Peeking), "見えていない今の位置は知らない");
            Assert.That(decision.LookAt, Is.EqualTo(_player), "最後に知った位置を向いて覗く");
        }

        [Test]
        public void DoesNotGoBackToCoverWithinTheCooldown()
        {
            var brain = Hiding(Spec(), out _, out _);
            PeekTimes(brain, 3, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.CoverCooldownRemaining, Is.EqualTo(5f));

            for (var second = 1; second <= 4; second++)
            {
                brain.Tick(1f, Shot(_player, true));
                Assert.That(brain.State, Is.EqualTo(EnemyState.Attack), $"{second} 秒後に撃たれても");
                Assert.That(brain.CoverCooldownRemaining, Is.EqualTo(5f - second));
            }

            brain.Tick(1f, Shot(_player, true));

            Assert.That(brain.CoverCooldownRemaining, Is.Zero);
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover), "5 秒経ったら遮蔽へ戻る");
            Assert.That(brain.CoverPointIndex, Is.Zero);
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));

            brain.Tick(1f, Watches(_player, false));
            brain.Tick(1f, Watches(_player, true));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover), "覗いた回数は数え直す");
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));
        }

        [Test]
        public void CooldownComesFromTheSpec()
        {
            var brain = Hiding(Spec(coverCooldownSeconds: 2f), out _, out _);
            PeekTimes(brain, 3, true);

            brain.Tick(1f, Shot(_player, true));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            brain.Tick(1f, Shot(_player, true));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void ReturningToPatrolClearsTheCooldown()
        {
            var brain = Hiding(Spec(coverCooldownSeconds: 60f), out _, out _);
            PeekTimes(brain, 3, false);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));

            brain.Tick(2f, Nothing());
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));
            Assert.That(brain.CoverCooldownRemaining, Is.GreaterThan(0f), "探している間は残す");
            brain.Tick(6f, Nothing());
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(brain.CoverCooldownRemaining, Is.Zero);

            brain.Tick(0.1f, Shot(_player, false));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void StandingDownClearsTheCooldown()
        {
            var brain = Hiding(Spec(coverCooldownSeconds: 60f), out _, out _);
            PeekTimes(brain, 3, true);

            Assert.That(brain.StandDown(), Is.True);

            Assert.That(brain.CoverCooldownRemaining, Is.Zero);
            brain.Tick(0.1f, Shot(_player, false));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void RestartingClearsTheCooldown()
        {
            var brain = Hiding(Spec(coverCooldownSeconds: 60f), out _, out _);
            PeekTimes(brain, 3, true);

            brain.Restart();

            Assert.That(brain.CoverCooldownRemaining, Is.Zero);
            brain.Tick(0.1f, Shot(_player, false));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void FiresThreeTenthsOfASecondIntoThePeek()
        {
            var brain = Hiding(Spec(), out _, out _);
            brain.Tick(1f, Watches(_player, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Peeking));
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.3f).Within(1e-5f), "ReactionSeconds (1 秒) でなく覗きの構え時間");

            var early = brain.Tick(0.15f, Watches(_player, true));
            Assert.That(early.Fire, Is.False, "0.15 秒");
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.15f).Within(1e-5f));

            Assert.That(brain.Tick(0.15f, Watches(_player, true)).Fire, Is.True, "0.3 秒");
        }

        [Test]
        public void PeekReactionComesFromTheSpec()
        {
            var brain = Hiding(Spec(coverPeekReactionSeconds: 0.6f), out _, out _);
            brain.Tick(1f, Watches(_player, false));

            Assert.That(brain.Tick(0.25f, Watches(_player, true)).Fire, Is.False);
            Assert.That(brain.Tick(0.25f, Watches(_player, true)).Fire, Is.False, "0.5 秒");
            Assert.That(brain.Tick(0.25f, Watches(_player, true)).Fire, Is.True, "0.75 秒");
        }

        [Test]
        public void AnUnseenFlankDoesNotPickAnotherPoint()
        {
            var brain = Hiding(Spec(), out var selector, out _);

            var decision = brain.Tick(0.1f, Watches(_flanker, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));
            Assert.That(brain.CoverPointIndex, Is.Zero, "壁越しに回り込みを知らない");
            Assert.That(selector.IsOccupied(1), Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(_player));
            Assert.That(decision.Crouch, Is.True);

            brain.Tick(0.1f, Watches(_flanker, true));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(1), "見えたら選び直す");
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
        }

        [Test]
        public void AimedPeekKeepsFiringWhileMovingToAnotherPoint()
        {
            var brain = Hiding(Spec(), out _, out _);
            brain.Tick(1f, Watches(_player, false));
            Assert.That(brain.Tick(0.5f, Watches(_player, true)).Fire, Is.True, "覗いて構え終えている");

            var decision = brain.Tick(0.1f, Watches(new Vector3(8f, 0f, 0f), true));

            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(1));
            Assert.That(decision.Fire, Is.True, "移るときに構え直さない");
        }

        [Test]
        public void StaysInCoverPastTheMaxPeeksWhileSuppressed()
        {
            var brain = Hiding(Spec(), out _, out _);
            PeekTimes(brain, 2, true);
            brain.Tick(1f, Watches(_player, false));
            brain.Tick(0.5f, Watches(_player, true));

            brain.Tick(0.5f, Suppressed(_player));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));

            brain.Tick(1f, Watches(_player, false));
            brain.Tick(0.5f, Watches(_player, true));
            brain.Tick(0.5f, Watches(_player, true));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack), "制圧が解けたら次の覗きで出る");
        }
    }
}
