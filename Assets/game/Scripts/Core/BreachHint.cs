using System;

namespace Shooter.Core
{
    // 文でなく値で持つのは、文にするのが UI だから。品の名前も UI が ChargeItemId から引く。
    public readonly struct BreachHint : IEquatable<BreachHint>
    {
        public BreachHint(bool canKick, bool canPlant, string chargeItemId, string inputActionPath)
        {
            CanKick = canKick;
            CanPlant = canPlant;
            ChargeItemId = SpecGuard.ReferenceId(chargeItemId);
            InputActionPath = inputActionPath ?? string.Empty;
        }

        public bool CanKick { get; }

        public bool CanPlant { get; }

        public string ChargeItemId { get; }

        public string InputActionPath { get; }

        // 品が要るのに持っていないときだけ真。品が要らなければ、仕掛けられないのは品のせいではない。
        public bool LacksCharge => !CanPlant && ChargeItemId.Length > 0;

        public bool Equals(BreachHint other)
        {
            return CanKick == other.CanKick
                && CanPlant == other.CanPlant
                && string.Equals(ChargeItemId, other.ChargeItemId, StringComparison.Ordinal)
                && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BreachHint other && Equals(other);
        }

        public static bool operator ==(BreachHint left, BreachHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BreachHint left, BreachHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CanKick, CanPlant, ChargeItemId, InputActionPath);
        }

        public override string ToString()
        {
            return $"breach kick={CanKick} plant={CanPlant} charge={(ChargeItemId.Length > 0 ? ChargeItemId : "-")} input={InputActionPath}";
        }
    }
}
