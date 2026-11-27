using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AkimboSettings
    {
        [SerializeField, Tooltip("二丁持ちの武器にする。左右の手に 1 丁ずつ持ち、攻撃のボタンで右手、覗き込みのボタンで左手を撃つ")]
        private bool _enabled;

        [SerializeField, Min(0f), Tooltip("二丁持ちの間に拡散へ掛ける倍率")]
        private float _spreadMultiplier = AkimboSpec.DefaultSpreadMultiplier;

        [SerializeField, Tooltip("二丁持ちの間は覗き込まない")]
        private bool _blocksAds = AkimboSpec.DefaultBlocksAds;

        [SerializeField, Min(0.01f), Tooltip("片手ぶんのリロードの秒数に掛ける倍率")]
        private float _reloadSecondsMultiplier = AkimboSpec.DefaultReloadSecondsMultiplier;

        [SerializeField, Min(0f), Tooltip("二丁持ちの間に移動の速さへ掛ける倍率。武器の倍率に重ねて掛ける")]
        private float _moveSpeedMultiplier = AkimboSpec.DefaultMoveSpeedMultiplier;

        [SerializeField, Min(0f), Tooltip("同じフレームに左右の引き金を引いたときに、左手を遅らせる秒数")]
        private float _offhandShotDelaySeconds = AkimboSpec.DefaultOffhandShotDelaySeconds;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float SpreadMultiplier { get => _spreadMultiplier; set => _spreadMultiplier = value; }

        public bool BlocksAds { get => _blocksAds; set => _blocksAds = value; }

        public float ReloadSecondsMultiplier { get => _reloadSecondsMultiplier; set => _reloadSecondsMultiplier = value; }

        public float MoveSpeedMultiplier { get => _moveSpeedMultiplier; set => _moveSpeedMultiplier = value; }

        public float OffhandShotDelaySeconds { get => _offhandShotDelaySeconds; set => _offhandShotDelaySeconds = value; }

        public AkimboSpec ToSpec()
        {
            return _enabled
                ? new AkimboSpec(_spreadMultiplier, _blocksAds, _reloadSecondsMultiplier, _moveSpeedMultiplier, _offhandShotDelaySeconds)
                : null;
        }

        public void Set(AkimboSpec spec)
        {
            _enabled = spec != null;
            if (spec == null)
            {
                return;
            }

            _spreadMultiplier = spec.SpreadMultiplier;
            _blocksAds = spec.BlocksAds;
            _reloadSecondsMultiplier = spec.ReloadSecondsMultiplier;
            _moveSpeedMultiplier = spec.MoveSpeedMultiplier;
            _offhandShotDelaySeconds = spec.OffhandShotDelaySeconds;
        }
    }
}
