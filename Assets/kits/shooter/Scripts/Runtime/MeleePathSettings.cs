using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class MeleePathSettings
    {
        [SerializeField, Tooltip("刃の軌道を持たせる。切ると当たりの瞬間に扇の中のいちばん近い 1 人に当てる")]
        private bool _enabled;

        [SerializeField, Tooltip("刃の通り方。Horizontal は左右に払う、Vertical は上下に振り下ろす、Thrust は正面へ突く")]
        private MeleePathStyle _style = MeleePath.DefaultStyle;

        [SerializeField, UnityEngine.Range(-MeleePath.MaxYawDegrees, MeleePath.MaxYawDegrees), Tooltip("刃が通り始める角度 (度)。正面が 0、横は右が +、縦は上が + (縦は ±90 まで)。突きでは使わない")]
        private float _fromDegrees = MeleePath.DefaultFromDegrees;

        [SerializeField, UnityEngine.Range(-MeleePath.MaxYawDegrees, MeleePath.MaxYawDegrees), Tooltip("刃が通り終える角度 (度)。始めと違う値にする。突きでは使わない")]
        private float _toDegrees = MeleePath.DefaultToDegrees;

        [SerializeField, UnityEngine.Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("刃の通る向きと直角に測った幅 (度)。横振りなら上下、縦振りなら左右、突きなら両方")]
        private float _widthDegrees = MeleePath.DefaultWidthDegrees;

        [SerializeField, Min(0f), Tooltip("当たりの瞬間から刃が通り終えるまでの秒数。当たりの秒数に足して振りの秒数以下")]
        private float _activeSeconds = MeleePath.DefaultActiveSeconds;

        [SerializeField, Min(1), Tooltip("1 回の振りで当てられる人数")]
        private int _maxTargets = MeleePath.DefaultMaxTargets;

        public MeleePath ToPath()
        {
            return _enabled ? new MeleePath(_style, _fromDegrees, _toDegrees, _widthDegrees, _activeSeconds, _maxTargets) : null;
        }

        public void Set(MeleePath path)
        {
            _enabled = path != null;
            if (path == null)
            {
                return;
            }

            _style = path.Style;
            _fromDegrees = path.FromDegrees;
            _toDegrees = path.ToDegrees;
            _widthDegrees = path.WidthDegrees;
            _activeSeconds = path.ActiveSeconds;
            _maxTargets = path.MaxTargets;
        }
    }
}
