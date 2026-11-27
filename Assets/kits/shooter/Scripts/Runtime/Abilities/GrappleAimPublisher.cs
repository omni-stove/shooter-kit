using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 手元のプレイヤーがグラップルを持っている間、今撃ったら掛かるかを GameSignals.GrappleAim に出す。
    // Bot にも同じ部品が付くが、出すのは GameSignals.LocalPlayer の AbilityHost のときだけ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityHost))]
    public sealed class GrappleAimPublisher : MonoBehaviour
    {
        private AbilityHost _host;
        private GrappleAimStatus? _published;

        public GrappleAimStatus? Published => _published;

        public AbilityHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = GetComponent<AbilityHost>();
                }

                return _host;
            }
        }

        // 能力の Tick (AbilityHost.Update) で縄を離した後の狙いを出すよう、LateUpdate で読む。
        private void LateUpdate()
        {
            Step();
        }

        private void OnDisable()
        {
            Withdraw();
        }

        // Edit Mode のテストや smoke からも進められるよう、出した値を返す。
        public GrappleAimStatus? Step()
        {
            var status = Read();
            if (!status.HasValue)
            {
                Withdraw();
                return null;
            }

            if (_published.HasValue && _published.Value == status.Value && GameSignals.GrappleAim.Value == _published)
            {
                return _published;
            }

            _published = status;
            GameSignals.GrappleAim.Publish(status);
            return status;
        }

        private GrappleAimStatus? Read()
        {
            var host = Host;
            if (host == null || !host.UsesAbilities || host.IsIncapacitated || !IsLocal(host))
            {
                return null;
            }

            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                if (!(host.Ability(i) is GrappleAbility grapple))
                {
                    continue;
                }

                var slot = host.Slot(i);
                var block = slot != null ? slot.Block : AbilityUseBlock.Empty;

                // 縄に掛かっている間は次の狙いを出さない。出すと、ぶら下がりながら次の先が掴めるように見える。
                if (block == AbilityUseBlock.Running)
                {
                    return null;
                }

                var miss = grapple.Preview(host.gameObject, host.Mover, out var aim);
                var hasSurface = aim.Surface != null;
                return new GrappleAimStatus(
                    i,
                    miss,
                    block,
                    hasSurface,
                    aim.Point.ToNumerics(),
                    aim.Normal.ToNumerics(),
                    aim.Distance,
                    aim.SnappedToEdge);
            }

            return null;
        }

        private void Withdraw()
        {
            if (!_published.HasValue)
            {
                return;
            }

            GameSignals.GrappleAim.ClearIfCurrent(_published);
            _published = null;
        }

        private static bool IsLocal(AbilityHost host)
        {
            var local = GameSignals.LocalPlayer.Value;
            return local != null && local.Abilities == host;
        }
    }
}
