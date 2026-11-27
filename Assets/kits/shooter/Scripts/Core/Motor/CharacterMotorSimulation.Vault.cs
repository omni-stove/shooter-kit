using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        private readonly CharacterVaultSensor _vaultSensor;

        // 状態の頂点から作った軌跡の使い回し。同じ頂点の間は毎 tick 作り直さない
        private VaultPath _vaultPath;

        public CharacterVaultSensor VaultSensor => _vaultSensor;

        public VaultProbeResult LastVaultProbe { get; private set; }

        // 前方を調べ、乗り越えられれば進み 0 の状態を返す。乗り越えられなければ None (Jump はふつうのジャンプになる)
        private CharacterVaultState TryStartVault(float yaw)
        {
            LastVaultProbe = _vaultSensor.Probe(Body.Position, yaw, _motor.Spec);
            if (!LastVaultProbe.CanVault)
            {
                return CharacterVaultState.None;
            }

            _vaultPath = LastVaultProbe.Path;
            return CharacterVaultState.Following(_vaultPath, 0f);
        }

        // 軌跡を 1 tick たどる。たどり終えたら乗り越えをやめ、越えた向きの水平の速さだけを MomentumTuning.VaultExitSpeedKeep の割合で残す (PlayerController と同じ)
        private Vector3 FollowVault(ref CharacterVaultState vault)
        {
            if (_vaultPath == null || !vault.HasSamePath(_vaultPath))
            {
                _vaultPath = vault.ToPath(_motor.Spec.VaultSpeed);
            }

            var before = vault.Elapsed;
            var after = Math.Min(_vaultPath.Duration, before + TickSeconds);
            var displacement = _vaultPath.Displacement(Math.Min(before, after), after);
            Body.Move(displacement);

            if (after >= _vaultPath.Duration)
            {
                vault = CharacterVaultState.None;
                return MomentumStep.VaultExitVelocity(_vaultPath, _parkour.Momentum);
            }

            vault = vault.WithElapsed(after);

            // 射撃のばらつきなど速度を読む側のために、たどっている速度を入れておく
            return displacement / TickSeconds;
        }
    }
}
