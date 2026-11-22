using System.Numerics;

namespace Shooter.Core
{
    // 壁を駆け上がる。ふちに届いたら VaultProbe.EvaluateMantle で上に乗れるかを調べ、乗れるなら乗り越えの状態 (CharacterVaultState) で上がる。
    // よじ登りを乗り越えの状態に載せるのは、スナップショットが軌跡の頂点を運ぶので、予測のやり直しでもサーバーと同じ道をたどれるため。
    public sealed partial class CharacterMotorSimulation
    {
        private CharacterFrontWallSensor _frontWallSensor;

        // ふちに届いた tick に始めたよじ登り。TryMoveParkour の引数に乗り越えの状態を足すと、ほかの動きの入り口まで書き換えるので、Step が受け取って移す
        private CharacterVaultState _wallClimbMantle;

        public CharacterFrontWallSensor FrontWallSensor => _frontWallSensor ??= new CharacterFrontWallSensor(Body);

        private bool TryStepWallClimb(
            WallClimbTuning tuning,
            CharacterMotorState state,
            Vector2 move,
            float yaw,
            bool jump,
            bool allowed,
            ref CharacterParkourState parkour,
            out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            var climbing = parkour.Move == ParkourMove.WallClimb;

            // 始められない tick は壁を調べない (使わないゲームと、跳んでいない tick に Raycast を払わせない)
            if (!climbing && (!tuning.Enabled || !allowed || !jump || !state.IsGrounded || parkour.IsActive))
            {
                return false;
            }

            // 倒れた・カバーに入った tick は壁を見失ったとして落とす (跳び離れさせない)
            var wall = allowed ? FrontWallSensor.Probe(Body.Position, yaw, _motor.Spec, tuning) : FrontWallContact.None;
            var result = WallClimbStep.Step(
                parkour, wall, state.Velocity, jump && allowed, state.IsGrounded, Body.Stance, _motor.Spec, tuning, TickSeconds);
            parkour = result.State;
            switch (result.Motion)
            {
                case WallClimbMotion.None:
                    return false;
                case WallClimbMotion.ReachedLedge:
                    velocity = MantleFromWallClimb(yaw);
                    return true;
                case WallClimbMotion.PushedOff:
                    // 跳んだ知らせ (Jumped) を地上のジャンプと同じ 1 か所から出す (壁走りの跳び離れと同じ)
                    _motor.JumpWith(result.Velocity);
                    break;
            }

            var contacts = Body.Move(result.Displacement);
            velocity = result.Velocity;
            if ((contacts & CapsuleContacts.Above) != 0 && velocity.Y > 0f)
            {
                velocity = new Vector3(velocity.X, 0f, velocity.Z);
            }

            return true;
        }

        // 速さを MantleSpeed にせず VaultSpeed で軌跡を作るのは、乗り越えの状態が速さを持たず CharacterVaultState.ToPath が VaultSpeed で戻すので、
        // 作り直したときに速さが替わって予測とサーバーの位置が食い違うため。
        private Vector3 MantleFromWallClimb(float yaw)
        {
            var probe = _vaultSensor.ProbeMantle(Body.Position, yaw, _motor.Spec);
            if (!probe.CanMantle)
            {
                return FallFromWall();
            }

            var vault = CharacterVaultState.Following(probe.Path, 0f);
            _vaultPath = vault.ToPath(_motor.Spec.VaultSpeed);
            var velocity = FollowVault(ref vault);
            _wallClimbMantle = vault;
            return velocity;
        }

        // 上に立てないふちでは、WallClimbStep が登り終えたときと同じく、上向きの速さを捨てて 0 から落ち始める
        private Vector3 FallFromWall()
        {
            var verticalEnd = -_motor.Spec.Gravity * TickSeconds;
            Body.Move(new Vector3(0f, verticalEnd * 0.5f * TickSeconds, 0f));
            return new Vector3(0f, verticalEnd, 0f);
        }

        private CharacterVaultState TakeWallClimbMantle(CharacterVaultState vault)
        {
            var mantle = _wallClimbMantle;
            _wallClimbMantle = CharacterVaultState.None;
            return mantle.IsVaulting ? mantle : vault;
        }
    }
}
