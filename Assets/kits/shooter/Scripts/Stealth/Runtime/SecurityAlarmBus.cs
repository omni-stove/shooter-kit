using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // カメラが警報を直接持たないのは、1 つの警報を複数のカメラが鳴らし、カメラの無い仕掛け (警報のボタンなど) からも鳴らせるようにするため。
    public static class SecurityAlarmBus
    {
        public static event Action<Vector3, string> Raised;

        // 一つの購読者が投げても残りの購読者には届けたいので、event をそのまま呼ばずに一つずつ呼ぶ。
        public static void Raise(Vector3 spottedAt, string cameraId)
        {
            var raised = Raised;
            if (raised == null)
            {
                return;
            }

            var id = string.IsNullOrWhiteSpace(cameraId) ? string.Empty : cameraId.Trim();
            foreach (var handler in raised.GetInvocationList())
            {
                try
                {
                    ((Action<Vector3, string>)handler)(spottedAt, id);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, handler.Target as UnityEngine.Object);
                }
            }
        }

        // ドメインの読み直しを切った Play では静的な event が前回の購読を持ち越すので、Play の始めに外す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlay()
        {
            Raised = null;
        }
    }
}
