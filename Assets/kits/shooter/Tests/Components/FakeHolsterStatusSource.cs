using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeHolsterStatusSource : MonoBehaviour, IHolsterStatusSource
    {
        public HolsterStatus Status { get; set; } = new HolsterStatus(HolsterPhase.Drawn, 0f, true);

        HolsterStatus IHolsterStatusSource.HolsterStatus => Status;
    }
}
