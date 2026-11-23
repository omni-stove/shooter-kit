using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class SecurityCameraLog
    {
        public const string Tag = "[SecurityCamera]";

        // PhaseChanged の知らせ 1 回から 1 行を作る。行の中身を段階ごとに変えるのは、止めた秒数や見つけた場所のように、その段階でしか意味の無い値があるため。
        public static string Changed(string cameraId, SecurityCameraSweep camera, SecurityCameraPhase from)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            switch (camera.Phase)
            {
                case SecurityCameraPhase.Tracking:
                    return Tracking(cameraId, from, camera.YawDegrees, camera.Level);
                case SecurityCameraPhase.Spotted:
                    return Spotted(cameraId, from, camera.SpottedAt, camera.Level);
                case SecurityCameraPhase.Disabled:
                    return Disabled(cameraId, from, camera.DisabledSecondsLeft);
                case SecurityCameraPhase.Destroyed:
                    return Destroyed(cameraId, from);
                default:
                    return Sweeping(cameraId, from, camera.YawDegrees, camera.Level);
            }
        }

        public static string Sweeping(string cameraId, SecurityCameraPhase from, float yawDegrees, float level)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} yaw={1:0.00} level={2:0.00}",
                Prefix(cameraId, SecurityCameraPhase.Sweeping, from),
                yawDegrees,
                level);
        }

        public static string Tracking(string cameraId, SecurityCameraPhase from, float yawDegrees, float level)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} yaw={1:0.00} level={2:0.00}",
                Prefix(cameraId, SecurityCameraPhase.Tracking, from),
                yawDegrees,
                level);
        }

        public static string Spotted(string cameraId, SecurityCameraPhase from, Vector3 at, float level)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} at=({1:0.00}, {2:0.00}, {3:0.00}) level={4:0.00}",
                Prefix(cameraId, SecurityCameraPhase.Spotted, from),
                at.X,
                at.Y,
                at.Z,
                level);
        }

        public static string Disabled(string cameraId, SecurityCameraPhase from, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} seconds={1:0.00}", Prefix(cameraId, SecurityCameraPhase.Disabled, from), seconds);
        }

        public static string Destroyed(string cameraId, SecurityCameraPhase from)
        {
            return Prefix(cameraId, SecurityCameraPhase.Destroyed, from);
        }

        // 見張りと警報は入力が無くても動くので、止められないことだけを知らせる。
        public static string NoInteractAction(string cameraId, string inputActionPath)
        {
            return $"{Tag} camera={Name(cameraId)} input={Name(inputActionPath)} is missing from the Input Actions; the camera cannot be disabled by holding it";
        }

        private static string Prefix(string cameraId, SecurityCameraPhase to, SecurityCameraPhase from)
        {
            return $"{Tag} camera={Name(cameraId)} phase={to} from={from}";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
