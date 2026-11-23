namespace Shooter.Core
{
    public sealed class NeutralGuardState
    {
        public NeutralGuardPhase Phase { get; private set; } = NeutralGuardPhase.Neutral;

        public bool IsHostile => Phase == NeutralGuardPhase.Hostile;

        public bool IsWarning => Phase == NeutralGuardPhase.Warning;

        // 戻り値は段が変わったか。敵対した後は、ゾーンの警告が解けても敵対のままにする。
        public bool Observe(bool zoneWarning, bool zoneHostile)
        {
            if (IsHostile)
            {
                return false;
            }

            var next = zoneHostile ? NeutralGuardPhase.Hostile : zoneWarning ? NeutralGuardPhase.Warning : NeutralGuardPhase.Neutral;
            if (next == Phase)
            {
                return false;
            }

            Phase = next;
            return true;
        }

        // 撃たれたら警告の段を飛ばしてすぐ敵対する。
        public bool Provoke()
        {
            if (IsHostile)
            {
                return false;
            }

            Phase = NeutralGuardPhase.Hostile;
            return true;
        }
    }
}
