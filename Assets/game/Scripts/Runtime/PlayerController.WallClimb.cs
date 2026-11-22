using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        partial void TryOverrideMoveForWallClimb(Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement);
    }
}
