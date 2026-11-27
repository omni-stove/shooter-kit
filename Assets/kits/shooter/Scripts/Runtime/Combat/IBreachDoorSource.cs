using UnityEngine;

namespace Shooter.Runtime
{
    // マップの書き出し (Editor) が突入できるドアの部品 (Interaction) を参照せずに .doors.json と板の当たりを書けるよう、ドアに板と寸法だけを見せてもらう。
    // 書き出しは有効で動いている部品だけを拾い、Panel の下の当たりをそのドアの組にする。
    public interface IBreachDoorSource
    {
        Transform Panel { get; }

        Vector3 PanelCenter { get; }

        Vector3 PanelFacing { get; }

        Vector3 PanelSize { get; }
    }
}
