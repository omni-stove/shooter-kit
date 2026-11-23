using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // AddComponent が型を見つけられるよう、ファイル名と同じ名前の最上位の型にする。
    public sealed class FakeBreachDoorSource : MonoBehaviour, IBreachDoorSource
    {
        public Transform PanelTransform;

        public Vector3 Size = new Vector3(1.2f, 2.2f, 0.1f);

        public Transform Panel => PanelTransform;

        public Vector3 PanelCenter => PanelTransform == null ? transform.position : PanelTransform.position;

        public Vector3 PanelFacing => PanelTransform == null ? transform.forward : PanelTransform.forward;

        public Vector3 PanelSize => Size;
    }
}
