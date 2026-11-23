namespace Shooter.Core
{
    public readonly struct JuggernautChange
    {
        public JuggernautChange(int juggernautId, int? previousId, JuggernautChangeReason reason)
        {
            JuggernautId = juggernautId;
            PreviousId = previousId;
            Reason = reason;
        }

        public int JuggernautId { get; }

        // 最初に選んだときと、抜けた後に残りがだれもいなくて空いていたときには前の人がいない。
        public int? PreviousId { get; }

        public JuggernautChangeReason Reason { get; }
    }
}
