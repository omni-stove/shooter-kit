using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed partial class CharacterMotorSimulation
    {
        private SlideTuning _slideTuning = SlideTuning.Default;

        // 状態から毎 tick 戻して使う作業用。tick をまたいで中身を持ち越さない
        private SlideMotion _slide = NewSlide(SlideTuning.Default);

        // この tick の頭の滑り。どの道筋で終えても Finish で終えた理由を残すため
        private CharacterSlideState _slideBefore;

        // 予測とサーバーで同じ値を渡す。滑り出す速さや減速が違うと、滑った距離が毎 tick 食い違う。
        public SlideTuning Slide
        {
            get => _slideTuning;
            set
            {
                _slideTuning = value ?? throw new ArgumentNullException(nameof(value));
                _slide = NewSlide(value);
            }
        }

        // 滑り出した tick だけ IsSliding。ログと見た目は呼び手が読んで出す (シミュレーションは予測のやり直しでも呼ばれるため)
        public CharacterSlideState LastSlideStart { get; private set; }

        // 滑り終えた tick だけ None 以外。速さ・秒数・距離は終えたときの値
        public SlideEnd LastSlideEnd { get; private set; }

        public float LastSlideEndSpeed { get; private set; }

        public float LastSlideEndSeconds { get; private set; }

        public float LastSlideEndDistance { get; private set; }

        private static SlideMotion NewSlide(SlideTuning tuning)
        {
            return new SlideMotion(
                tuning.StartSpeed, tuning.Deceleration, tuning.EndSpeed, tuning.MinSeconds, tuning.MaxSeconds, tuning.SlopeAcceleration);
        }

        // SlideMovement.IsActive と同じく、滑っている間は続けられるかを、滑っていない間は始められるかを調べる。
        // 始めるのは立った姿勢から頼まれたときだけにする。滑り終えてしゃがんだままダッシュとしゃがみを押し続けても滑り直さないため。
        private CharacterSlideState UpdateSlide(
            CharacterMotorState state, CharacterMotorInput input, bool busy, Vector3 startVelocity, bool jump, out bool jumped)
        {
            jumped = false;
            var slide = state.Slide;
            if (slide.IsSliding)
            {
                if (!input.SlideAllowed || busy)
                {
                    ReportSlideEnd(SlideEnd.Cancelled, slide.Speed, slide.Elapsed, slide.Distance);
                    return CharacterSlideState.None;
                }

                if (jump && input.JumpAllowed && state.IsGrounded)
                {
                    ReportSlideEnd(SlideEnd.Jumped, slide.Speed, slide.Elapsed, slide.Distance);
                    jumped = true;
                    return CharacterSlideState.None;
                }

                return slide;
            }

            if (!input.Slide || !input.SlideAllowed || busy || !state.IsGrounded || state.Stance != PlayerStance.Standing
                || !SlideStep.TryMeasureEntry(new Vector2(startVelocity.X, startVelocity.Z), _motor.Spec.WalkSpeed, out var direction, out var speed))
            {
                return CharacterSlideState.None;
            }

            _slide.Restore(CharacterSlideState.None);
            _slide.TryStart(direction, speed);
            LastSlideStart = _slide.Capture(0f);
            return LastSlideStart;
        }

        // SlideMovement.Step と同じ動き。足場の傾きに沿って進め、接地中は地面へ吸着し、宙では重力で落とす。
        // 止められたかは動かした直後に比べる (オフラインは次のフレームの頭で比べるが、状態に前の狙いを持ち越さずに済むため)。
        private Vector3 SlideAlong(ref CharacterSlideState slide, CharacterMotorState state)
        {
            _slide.Restore(slide);
            var direction = new Vector3(slide.Direction.X, 0f, slide.Direction.Y);
            var grounded = TryProbeSlideGround(out var normal);
            var slopeSine = SlideStep.SlopeSine(direction, normal, grounded, out var along);
            var travelled = _slide.Step(TickSeconds, slopeSine);
            var displacement = along * travelled;
            var velocity = along * _slide.Speed;
            var airborne = slide.AirborneSeconds;
            var onGround = grounded || state.IsGrounded;
            if (onGround)
            {
                airborne = 0f;

                // 下り坂で体が足場から離れないよう、PlayerMotor の接地中と同じだけ下へ押し付ける。
                displacement.Y += PlayerMotor.GroundedVerticalSpeed * TickSeconds;
            }
            else
            {
                var before = airborne > 0f ? state.Velocity.Y : 0f;
                var after = before - (_motor.Spec.Gravity * TickSeconds);
                displacement.Y += (before + after) * 0.5f * TickSeconds;
                velocity.Y = after;
                airborne += TickSeconds;
                if (airborne >= SlideStep.AirborneGraceSeconds)
                {
                    _slide.Finish(SlideEnd.Airborne);
                }
            }

            var from = Body.Position;
            Body.Move(displacement);
            var expected = Vector3.Dot(displacement, direction);
            var moved = Vector3.Dot(Body.Position - from, direction);
            if (_slide.IsSliding && SlideStep.TryLimitForBlocked(_slide.Speed, moved, expected, out var limited))
            {
                _slide.LimitSpeed(limited);
                var fall = velocity.Y;
                velocity = along * _slide.Speed;
                if (!onGround)
                {
                    velocity.Y = fall;
                }
            }

            if (!_slide.IsSliding)
            {
                ReportSlideEnd(_slide.LastEnd, _slide.Speed, _slide.Elapsed, _slide.Distance);
            }

            slide = _slide.Capture(airborne);
            return velocity;
        }

        // SlideMovement.Jump と同じく、滑ってきた勢いを水平に残したまま PlayerMotor のジャンプと同じ初速で跳ぶ。続きの宙の動きは PlayerMotor が受け持つ。
        private Vector3 SlideJump(CharacterSlideState slide)
        {
            var horizontal = new Vector3(slide.Direction.X, 0f, slide.Direction.Y) * slide.Speed;
            var start = SlideStep.JumpSpeed(_motor.Spec.Gravity, _motor.Spec.JumpHeight);
            var end = start - (_motor.Spec.Gravity * TickSeconds);
            Body.Move((horizontal * TickSeconds) + (Vector3.UnitY * ((start + end) * 0.5f * TickSeconds)));
            return horizontal + (Vector3.UnitY * end);
        }

        // SlideMovement.TryProbeGround と同じく、カプセルの中心から足元より少し下まで真下へ足場を探す。
        private bool TryProbeSlideGround(out Vector3 normal)
        {
            normal = Vector3.UnitY;
            var position = Body.Position;
            var shape = Body.Current;
            var origin = new Vector3(position.X, position.Y + shape.CenterY, position.Z);
            var reach = (shape.Height * 0.5f) + SlideStep.GroundProbeMargin;
            if (!Body.World.Raycast(origin, -Vector3.UnitY, reach, Body.LayerMask, out var hit))
            {
                return false;
            }

            normal = hit.Normal;
            return true;
        }

        private void ReportSlideEnd(SlideEnd reason, float speed, float seconds, float distance)
        {
            LastSlideEnd = reason;
            LastSlideEndSpeed = speed;
            LastSlideEndSeconds = seconds;
            LastSlideEndDistance = distance;
        }

        private void ClearSlideReport()
        {
            LastSlideStart = CharacterSlideState.None;
            ReportSlideEnd(SlideEnd.None, 0f, 0f, 0f);
        }
    }
}
