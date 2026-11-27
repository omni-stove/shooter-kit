using System;
using System.Numerics;

namespace Shooter.Core
{
    // 手元のプレイヤーが今グラップルを撃ったら掛かるかと、掛かる先。どう見せるか (文・照準・掛かる点の印) は受け取る UI が決める。
    public readonly struct GrappleAimStatus : IEquatable<GrappleAimStatus>
    {
        public GrappleAimStatus(
            int slot,
            GrappleMiss miss,
            AbilityUseBlock slotBlock,
            bool hasSurface,
            Vector3 point,
            Vector3 normal,
            float distance,
            bool snappedToEdge)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            Slot = slot;
            Miss = miss;
            SlotBlock = slotBlock;
            HasSurface = hasSurface;
            Point = hasSurface ? point : Vector3.Zero;
            Normal = hasSurface ? normal : Vector3.Zero;
            Distance = hasSurface ? distance : 0f;
            SnappedToEdge = hasSurface && snappedToEdge;
        }

        // 0 から数えた能力の枠。
        public int Slot { get; }

        public GrappleMiss Miss { get; }

        // 狙いとは別に、回数・待ち時間・使用中で今は撃てないか。
        public AbilityUseBlock SlotBlock { get; }

        // 射程の中で Ray が何かに当たったか。当たったなら掴めなくても Point に当たった所を持つ。
        public bool HasSurface { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public bool SnappedToEdge { get; }

        public bool IsGrabbable => Miss == GrappleMiss.None;

        public bool CanGrapple => Miss == GrappleMiss.None && SlotBlock == AbilityUseBlock.None;

        public bool Equals(GrappleAimStatus other)
        {
            return Slot == other.Slot
                && Miss == other.Miss
                && SlotBlock == other.SlotBlock
                && HasSurface == other.HasSurface
                && Point.Equals(other.Point)
                && Normal.Equals(other.Normal)
                && Distance.Equals(other.Distance)
                && SnappedToEdge == other.SnappedToEdge;
        }

        public override bool Equals(object obj)
        {
            return obj is GrappleAimStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Slot, Miss, SlotBlock, HasSurface, Point, Normal, Distance, SnappedToEdge);
        }

        public static bool operator ==(GrappleAimStatus left, GrappleAimStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GrappleAimStatus left, GrappleAimStatus right)
        {
            return !left.Equals(right);
        }
    }
}
