using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeWeaponLightStatusSource : MonoBehaviour, IWeaponLightStatusSource
    {
        public WeaponLightStatus Status { get; set; } = new WeaponLightStatus(true, false);

        WeaponLightStatus IWeaponLightStatusSource.WeaponLightStatus => Status;
    }
}
