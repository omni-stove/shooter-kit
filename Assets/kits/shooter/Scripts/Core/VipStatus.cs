using System;

namespace Shooter.Core
{
    // ModeStatus は出した後も UI が持つので、Vip を指さずに出した時点の値を写して持つ。
    public sealed class VipStatus
    {
        // 対戦のクライアントは Vip を持たないので、届いた様子から同じ形を作れるよう値で受け取る。
        public VipStatus(int? vipId, float exitFraction, Side defenderSide, bool? isLocalVip)
        {
            if (!(exitFraction >= 0f) || exitFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(exitFraction), exitFraction, "exitFraction must be from 0 to 1");
            }

            if (!defenderSide.IsTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(defenderSide), defenderSide, "defenderSide must be Alpha or Bravo");
            }

            VipId = vipId;
            ExitFraction = vipId.HasValue ? exitFraction : 0f;
            DefenderSide = defenderSide;
            IsLocalVip = isLocalVip;
        }

        public int? VipId { get; }

        public float ExitFraction { get; }

        public Side DefenderSide { get; }

        // 操作しているプレイヤーがいない (bot だけの) ときは null。
        public bool? IsLocalVip { get; }

        public static VipStatus Of(Vip game, int? localPlayerId)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            bool? local = null;
            if (localPlayerId.HasValue && game.HasPlayer(localPlayerId.Value))
            {
                local = game.IsVip(localPlayerId.Value);
            }

            return new VipStatus(game.VipId, game.ExitFraction, game.Defender, local);
        }
    }
}
