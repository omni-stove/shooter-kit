using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct FrontWallContact
    {
        public FrontWallContact(Vector3 normal, float distance, float topHeight, Vector3 ledge)
        {
            Normal = normal;
            Distance = normal == Vector3.Zero ? 0f : distance;
            TopHeight = normal == Vector3.Zero ? 0f : topHeight;
            Ledge = normal == Vector3.Zero || float.IsPositiveInfinity(topHeight) ? Vector3.Zero : ledge;
        }

        public static FrontWallContact None => default;

        public Vector3 Normal { get; }

        public float Distance { get; }

        // 足元から壁の上端までの高さ。調べた高さの中に上端が無ければ正の無限大にする。
        public float TopHeight { get; }

        public Vector3 Ledge { get; }

        public bool IsFound => Normal != Vector3.Zero;

        public bool HasTop => IsFound && !float.IsPositiveInfinity(TopHeight);

        public static FrontWallContact WithoutTop(Vector3 normal, float distance)
        {
            return new FrontWallContact(normal, distance, float.PositiveInfinity, Vector3.Zero);
        }
    }

    public sealed class CharacterFrontWallSensor
    {
        public const float ReachBeyondRadius = CharacterWallSensor.ReachBeyondRadius;

        // 斜めにかすめた壁まで取ると、壁沿いに走って跳んだだけで駆け上がるので、正面から 45 度くらいまでの壁に限る。
        public const float MinFacingDot = 0.7f;

        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly CharacterBody _body;

        public CharacterFrontWallSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public float Reach => _body.Radius + ReachBeyondRadius;

        // 上端は駆け上がって届く高さ (よじ登れる高さ + 駆け上がる高さ) まで調べる。それより高い上端は届かないので測らない。
        public FrontWallContact Probe(Vector3 position, float yaw, MotorSpec spec, WallClimbTuning tuning)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var forward = CharacterMotorSimulation.YawForward(yaw);

            // 足元では低い段を、頭では張り出しを壁と取るので、CharacterWallSensor と同じく体の中心の高さで調べる
            var origin = new Vector3(position.X, position.Y + _body.Current.CenterY, position.Z);
            if (!_body.World.Raycast(origin, forward, Reach, _body.LayerMask, out var hit)
                || Math.Abs(hit.Normal.Y) > CharacterWallSensor.MaxNormalY)
            {
                return FrontWallContact.None;
            }

            var normal = Flatten(hit.Normal);
            if (normal == Vector3.Zero || Vector3.Dot(forward, -normal) < MinFacingDot)
            {
                return FrontWallContact.None;
            }

            var feetY = _body.FeetY(position);
            var startHeight = spec.MantleMaxHeight + tuning.MaxHeight + CharacterVaultSensor.TopProbeMargin;
            var inset = hit.Point - (normal * CharacterVaultSensor.SurfaceInset);
            var start = new Vector3(inset.X, feetY + startHeight, inset.Z);

            // 当たった形だけに上から下ろすのは CharacterVaultSensor と同じく、上に張り出した別の物を上端と取らないため。
            // 始点が形の中 (上端がもっと高い) なら当たらないので、上端は無いとする
            if (!_body.World.RaycastCollider(hit.ColliderId, start, _down, startHeight, out var top))
            {
                return FrontWallContact.WithoutTop(normal, hit.Distance);
            }

            var topHeight = Math.Max(0f, top.Point.Y - feetY);
            return new FrontWallContact(normal, hit.Distance, topHeight, new Vector3(hit.Point.X, top.Point.Y, hit.Point.Z));
        }

        private static Vector3 Flatten(Vector3 normal)
        {
            var length = MathF.Sqrt((normal.X * normal.X) + (normal.Z * normal.Z));
            return length > 0f ? new Vector3(normal.X / length, 0f, normal.Z / length) : Vector3.Zero;
        }
    }
}
