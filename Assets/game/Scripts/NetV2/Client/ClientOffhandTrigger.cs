using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public readonly struct OffhandPull
    {
        public OffhandPull(TriggerPull pull, FireDecision decision, bool pressed)
        {
            Pull = pull;
            Decision = decision;
            Pressed = pressed;
        }

        public TriggerPull Pull { get; }

        public FireDecision Decision { get; }

        // 遅らせた押しを渡したフレームも押したことにする。弾が無いときの空撃ちの音を、押したときだけ鳴らすため。
        public bool Pressed { get; }

        public int Shots => Decision == FireDecision.Fire ? Pull.Shots : 0;
    }

    // 二丁持ちの左手の引き金を回す。右手と同じフレームに押した左の押しは AkimboSpec.OffhandShotDelaySeconds だけ遅らせて渡す
    // (オフラインの WeaponController.PullTriggers と同じ)。同じ tick に左右の 2 発が届くと、1 発に重なって見えるため。
    public sealed class ClientOffhandTrigger
    {
        private ClientTrigger _trigger;
        private float _delayRemaining;

        public bool IsDelayed { get; private set; }

        public OffhandPull Pull(
            ClientTrigger offhand,
            float deltaSeconds,
            bool isPressed,
            bool wasPressedThisFrame,
            bool rightWasPressedThisFrame,
            SnapshotMessage latest,
            int playerId,
            in CharacterMotorState predicted,
            bool firesVehicleWeapon)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            // 持ち替えたら遅らせた押しを捨てる。持ち替え先の武器で、前の武器で押した分を撃たないように。
            if (!ReferenceEquals(offhand, _trigger))
            {
                _trigger?.Drop();
                _trigger = offhand;
                Reset();
            }

            if (offhand == null)
            {
                return default;
            }

            offhand.Tick(deltaSeconds);
            _delayRemaining = Math.Max(0f, _delayRemaining - deltaSeconds);
            var delay = offhand.Spec.IsAkimbo ? offhand.Spec.Akimbo.OffhandShotDelaySeconds : 0f;
            if (rightWasPressedThisFrame && wasPressedThisFrame && delay > 0f)
            {
                IsDelayed = true;
                _delayRemaining = delay;
            }

            if (IsDelayed)
            {
                // 遅らせている間に引き金を引くと、押しっぱなしで撃つ撃ち方が押した扱いを先に始めてしまう。
                if (_delayRemaining > 0f)
                {
                    return default;
                }

                IsDelayed = false;
                wasPressedThisFrame = true;
            }

            var pull = offhand.Pull(isPressed, wasPressedThisFrame);
            if (pull.Shots <= 0)
            {
                return new OffhandPull(pull, FireDecision.Hold, wasPressedThisFrame);
            }

            var decision = ClientFire.Decide(latest, playerId, predicted, offhand.Spec, firesVehicleWeapon, AkimboHand.Left);
            return new OffhandPull(pull, decision, wasPressedThisFrame);
        }

        public void Reset()
        {
            IsDelayed = false;
            _delayRemaining = 0f;
        }

        public void Forget()
        {
            _trigger?.Drop();
            _trigger = null;
            Reset();
        }
    }
}
