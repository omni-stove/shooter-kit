using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct LedgeContact
    {
        public LedgeContact(Vector3 point, Vector3 normal, bool continuesLeft, bool continuesRight)
        {
            IsFound = true;
            Point = point;
            Normal = normal;
            ContinuesLeft = continuesLeft;
            ContinuesRight = continuesRight;
        }

        public static LedgeContact None => default;

        public bool IsFound { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public bool ContinuesLeft { get; }

        public bool ContinuesRight { get; }
    }

    // ふちは「手の高さの前に壁の面があり、その上端が手の届く高さにある所」とし、面の位置と上端の高さを合わせた点を返す。
    // 左右の続きは、体の半径より少し先で同じ測り方をして決める。手が届いても体の端が外へ出る所まで横へ動かさないため。
    public sealed class CharacterLedgeSensor
    {
        public const float ReachBeyondRadius = CharacterWallSensor.ReachBeyondRadius;

        public const float MaxWallNormalY = CharacterWallSensor.MaxNormalY;

        // 手を掛けられる上端は、立てるくらい寝た面に限る。坂や丸い頭の上では手が滑るので掴ませない。
        public const float MinTopNormalY = 0.7f;

        public const float SideSlack = 0.05f;

        // 一度掴んだふちを測り直すときは高さが手の位置で決まっているので、手の届く範囲より狭い段差だけを同じふちとする。
        public const float FollowStep = 0.1f;

        public const float FollowDot = CharacterWallSensor.FollowDot;

        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly CharacterBody _body;

        public CharacterLedgeSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public float Reach => _body.Radius + ReachBeyondRadius;

        public float HandHeight => _body.Geometry.Standing.TopY;

        public float SideReach => _body.Radius + SideSlack;

        // 向いている向きでなく壁の法線から右を決めるのは、ぶら下がる間は向きを変えても左右の入力をふちに沿わせるため。
        public static Vector3 RightAlong(Vector3 wallNormal)
        {
            return new Vector3(-wallNormal.Z, 0f, wallNormal.X);
        }

        public LedgeContact Probe(Vector3 position, float yaw, float grabReach)
        {
            if (!(grabReach > 0f) || float.IsInfinity(grabReach))
            {
                throw new ArgumentOutOfRangeException(nameof(grabReach), grabReach, "grabReach must be finite and positive");
            }

            var forward = CharacterMotorSimulation.YawForward(yaw);
            var handY = position.Y + HandHeight;

            // 手の高さで当てると手より下にあるふちを見落とすので、届く範囲の下の端で当てる。
            var faceOrigin = new Vector3(position.X, handY - grabReach, position.Z);
            return TryMeasure(faceOrigin, forward, Reach, handY + grabReach, grabReach * 2f, out var point, out var normal)
                ? WithSides(point, normal)
                : LedgeContact.None;
        }

        public LedgeContact Follow(Vector3 ledgePoint, Vector3 wallNormal)
        {
            var flat = Flatten(wallNormal);
            if (flat == Vector3.Zero)
            {
                return LedgeContact.None;
            }

            if (!TryMeasureAt(ledgePoint, flat, out var point, out var normal) || Vector3.Dot(normal, flat) < FollowDot)
            {
                return LedgeContact.None;
            }

            return WithSides(point, normal);
        }

        // CharacterVaultSensor と同じく、軌跡の頂点と各区間の中点に立った体を置き、どこも重ならなければ通れるとする。
        public bool IsClimbClear(Vector3 hangPosition, VaultPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var points = path.Points;
            for (var i = 1; i < points.Count; i++)
            {
                var from = points[i - 1];
                var to = points[i];
                if (!_body.IsStandingClear(hangPosition + ((from + to) * 0.5f)) || !_body.IsStandingClear(hangPosition + to))
                {
                    return false;
                }
            }

            return true;
        }

        private LedgeContact WithSides(Vector3 point, Vector3 normal)
        {
            var right = RightAlong(normal);
            var continuesRight = TryMeasureAt(point + (right * SideReach), normal, out _, out _);
            var continuesLeft = TryMeasureAt(point - (right * SideReach), normal, out _, out _);
            return new LedgeContact(point, normal, continuesLeft, continuesRight);
        }

        // 面の位置ちょうどから当てると、少し斜めの面や丸めの誤差で始点が形の中に入って当たらないので、乗り越えの面の幅と同じだけ手前から当てる。
        private bool TryMeasureAt(Vector3 ledgePoint, Vector3 normal, out Vector3 point, out Vector3 measuredNormal)
        {
            var back = ledgePoint + (normal * VaultProbe.FaceWidthBack);
            var faceOrigin = new Vector3(back.X, ledgePoint.Y - FollowStep, back.Z);
            return TryMeasure(
                faceOrigin,
                -normal,
                VaultProbe.FaceWidthBack + VaultProbe.FaceWidthSlack,
                ledgePoint.Y + FollowStep,
                FollowStep * 2f,
                out point,
                out measuredNormal);
        }

        // 上端は形を問わず世界へ下ろした Ray で測る。壁の上に別の板が載っていれば、その板の上を手の掛かる所とするため。
        // 始点が形の中にあると当たらないので、上端が測る範囲より高い壁はふちにならない。
        private bool TryMeasure(
            Vector3 faceOrigin, Vector3 direction, float faceReach, float topStartY, float topDepth, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.Zero;
            normal = Vector3.Zero;
            if (!_body.World.Raycast(faceOrigin, direction, faceReach, _body.LayerMask, out var face) || Math.Abs(face.Normal.Y) > MaxWallNormalY)
            {
                return false;
            }

            var flat = Flatten(face.Normal);
            if (flat == Vector3.Zero)
            {
                return false;
            }

            var inset = face.Point - (flat * CharacterVaultSensor.SurfaceInset);
            var topStart = new Vector3(inset.X, topStartY, inset.Z);
            if (!_body.World.Raycast(topStart, _down, topDepth, _body.LayerMask, out var top) || top.Normal.Y < MinTopNormalY)
            {
                return false;
            }

            point = new Vector3(face.Point.X, top.Point.Y, face.Point.Z);
            normal = flat;
            return true;
        }

        private static Vector3 Flatten(Vector3 normal)
        {
            var length = MathF.Sqrt((normal.X * normal.X) + (normal.Z * normal.Z));
            return length > 0f ? new Vector3(normal.X / length, 0f, normal.Z / length) : Vector3.Zero;
        }
    }
}
