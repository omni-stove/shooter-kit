using System;

namespace Shooter.Core
{
    // Func<float> でなく interface にするのは、テストが固定の値の列を返す実装を名前の付いた型として渡せるようにするため。
    public interface IJamRoll
    {
        // [0,1) の値を返す。
        float Draw();
    }

    public sealed class JamRoll : IJamRoll
    {
        private const long _modulus = 2147483647L;
        private const long _multiplier = 48271L;

        // 2^24。float の仮数に収まる 24 ビットで割るのは、(状態 - 1) / (_modulus - 1) を float にすると 1 に丸まることがあるため。
        private const float _scale = 16777216f;

        private long _state;

        public JamRoll(int seed)
        {
            // 状態が 0 だと乱数の列が 0 のまま進まなくなるので、種を 1 から 2147483646 へ寄せる (MapVote と同じ)。
            _state = ((uint)seed % (_modulus - 1L)) + 1L;
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、MapVote と同じ Lehmer の乗算合同法 (MINSTD) で決める (Unity と dotnet で同じ種から同じ結果にするため)。
        public float Draw()
        {
            _state = _state * _multiplier % _modulus;
            return ((_state - 1L) >> 7) / _scale;
        }

        public static JamRoll ForWeapon(string weaponId)
        {
            return new JamRoll(SeedFor(weaponId));
        }

        // string.GetHashCode は .NET で起動ごとに変わるので使わず、FNV-1a (32 ビット) で Id から種を決める。
        public static int SeedFor(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                throw new ArgumentException("weaponId must not be empty", nameof(weaponId));
            }

            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in weaponId)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
