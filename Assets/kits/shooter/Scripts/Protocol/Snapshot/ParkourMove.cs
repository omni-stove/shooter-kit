namespace Shooter.Protocol
{
    // Core の ParkourMove と同じ番号にする。送る側と読む側はバイトの値で写し合う
    public enum ParkourMove : byte
    {
        None = 0,
        WallKick = 1,
        LandingRoll = 2,
        WallClimb = 3,
        LedgeHang = 4,
        LedgeShimmy = 5,
        LedgeClimbUp = 6,
    }
}
