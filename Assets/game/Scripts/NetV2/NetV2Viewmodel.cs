using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // NetV2 の自分の体は Damageable も WeaponController も持たないので、オフラインの体が一人称の武器モデルを隠す場面 (死んだ・はしごで武器をしまった・自分の武器を撃てない席に乗った・自分でしまった) を
    // スナップショットと予測から同じ理由 (ViewmodelPresenter.Reason*) で決める。倒れている間は撃てない (ClientFire.Decide) ので、それも隠す。
    public static class NetV2Viewmodel
    {
        public static string HiddenReason(SnapshotMessage latest, int playerId, in CharacterMotorState predicted, bool seatHidesWeapon)
        {
            var own = latest != null ? latest.Players.IndexOf(playerId) : -1;
            if (own >= 0)
            {
                var row = latest.Players[own];
                if (row.Downed.IsDowned)
                {
                    return ViewmodelPresenter.ReasonDowned;
                }

                if (row.Health <= 0)
                {
                    return ViewmodelPresenter.ReasonDead;
                }
            }

            if (seatHidesWeapon)
            {
                return ViewmodelPresenter.ReasonSeated;
            }

            // しまう途中と構える途中は下げて見せるので隠さず、しまい切った間だけ隠す (オフラインの WeaponController.Holster と同じ)
            if (own >= 0 && latest.Players[own].HolsterPhase == HolsterPhase.Holstered)
            {
                return ViewmodelPresenter.ReasonHolstered;
            }

            return predicted.Ladder.IsClimbing ? ViewmodelPresenter.ReasonHolstered : null;
        }

        // 一人称の左手のモデル (ViewmodelPresenter.AkimboSource) を出すかと、手ごとにリロードで下げる深さを、本人のスナップショットの左右の弾倉・リロードから決める。
        // オフラインの体は WeaponController の手の組から読むが、NetV2 の自分の体は手の組を持たないため。
        public static AkimboHandsView? AkimboHands(ClientAkimboReloads reloads, OwnerSnapshot owner, string weaponId, WeaponSpec spec, double nowSeconds)
        {
            if (reloads == null)
            {
                throw new ArgumentNullException(nameof(reloads));
            }

            return reloads.Observe(owner, weaponId, spec, nowSeconds);
        }

        // オフラインの体 (ViewmodelMotion が CharacterController から読む) と同じく、宙にいる間は揺らさない。
        public static float HorizontalSpeed(in CharacterMotorState predicted)
        {
            if (!predicted.IsGrounded)
            {
                return 0f;
            }

            var velocity = predicted.Velocity;
            return MathF.Sqrt((velocity.X * velocity.X) + (velocity.Z * velocity.Z));
        }
    }
}
