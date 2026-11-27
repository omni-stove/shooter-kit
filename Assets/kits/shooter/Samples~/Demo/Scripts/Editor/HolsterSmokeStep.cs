using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using Shooter.Runtime.Traversal;
using UnityEngine;

namespace Shooter.Editor
{
    // 射撃場には武器を出してはいけない場所を置かない (Playground だけに置く) ので、この手順の間だけ開けた場所に 2 つ置き、終わったら消す。
    // batchmode には O の入力が無いので、HolsterController.Holster・Draw と WeaponController.PullTrigger を直に呼ぶ。
    public sealed class HolsterSmokeStep : ITpsActionSmokeStep
    {
        public const string LogTag = "[HolsterSmokeStep]";

        public const string WarnZoneName = "SmokeZone_WarnOnly";
        public const string WarnZoneId = "smoke-warn-only";
        public const string AutoHolsterZoneName = "SmokeZone_AutoHolster";
        public const string AutoHolsterZoneId = "smoke-auto-holster";

        // 開けた場所 (TpsRangeSceneBuilder.OpenAreaHalfExtent) の中に、間を空けて 2 つ並べる。
        public static readonly Vector3 ZoneSize = new Vector3(4f, 3f, 4f);

        public static readonly Vector3 WarnZoneCenter = TpsRangeSceneBuilder.OpenAreaCenter + new Vector3(-3f, 0f, 0f);

        public static readonly Vector3 AutoHolsterZoneCenter = TpsRangeSceneBuilder.OpenAreaCenter + new Vector3(3f, 0f, 0f);

        // 置き直した足が床に埋まって押し戻されないよう、わずかに浮かせる。
        public const float PlaceLift = 0.05f;

        private readonly List<WeaponFreeZone> _zones = new List<WeaponFreeZone>();

        private Phase _phase;
        private HolsterController _holster;
        private string _holderName;
        private string _bodyId;
        private bool _sawHolstering;
        private bool _sawHolstered;
        private bool _sawDrawing;
        private bool _sawDrawn;
        private bool _sawWarnEntered;
        private bool _sawWarning;
        private bool _sawAutoHolster;
        private WeaponFreeZone _warnZone;
        private WeaponFreeZone _autoZone;

        private enum Phase
        {
            Start,
            Ready,
            WaitHolstered,
            WaitDrawn,
            FireDrawn,
            EnterWarnZone,
            EnterAutoHolsterZone,
            RestoreDrawn,
            Done,
        }

        public int Order => 600;

        public string Name => "holster";

        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _phase = Phase.Start;
            _holster = null;
            _holderName = null;
            _bodyId = null;
            _sawHolstering = false;
            _sawHolstered = false;
            _sawDrawing = false;
            _sawDrawn = false;
            _sawWarnEntered = false;
            _sawWarning = false;
            _sawAutoHolster = false;
            _warnZone = null;
            _autoZone = null;
            _zones.Clear();
        }

        public void OnLog(string condition)
        {
            if (condition == null || _holderName == null)
            {
                return;
            }

            if (condition.StartsWith(BeganPrefix(_holderName, HolsterPhase.Holstering), StringComparison.Ordinal))
            {
                _sawHolstering = true;
            }
            else if (condition.StartsWith(BeganPrefix(_holderName, HolsterPhase.Drawing), StringComparison.Ordinal))
            {
                _sawDrawing = true;
            }
            else if (string.Equals(condition, HolsterLog.Settled(_holderName, HolsterPhase.Holstered), StringComparison.Ordinal))
            {
                _sawHolstered = true;
            }
            else if (string.Equals(condition, HolsterLog.Settled(_holderName, HolsterPhase.Drawn), StringComparison.Ordinal))
            {
                _sawDrawn = true;
            }
            else if (string.Equals(condition, WeaponFreeZoneLog.Entered(WarnZoneId, _bodyId, true), StringComparison.Ordinal))
            {
                _sawWarnEntered = true;
            }
            else if (condition.StartsWith(WarningPrefix(WarnZoneId, WeaponFreeZoneReaction.WarnOnly), StringComparison.Ordinal))
            {
                _sawWarning = true;
            }
            else if (string.Equals(condition, WeaponFreeZoneLog.AutoHolstered(AutoHolsterZoneId, _bodyId, true), StringComparison.Ordinal))
            {
                _sawAutoHolster = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Start:
                    _holster = world.Player.GetComponent<HolsterController>();
                    if (_holster == null || !_holster.IsInitialized)
                    {
                        problem = $"the player has no initialized {nameof(HolsterController)}; {nameof(HolsterActionInstaller)} is not registered with the player actions";
                        return false;
                    }

                    _holderName = world.Player.name;
                    _bodyId = world.PlayerDamageable != null ? world.PlayerDamageable.Id : null;
                    if (_holster.Phase != HolsterPhase.Drawn)
                    {
                        _holster.Draw();
                    }

                    _phase = Phase.Ready;
                    return false;

                case Phase.Ready:
                    {
                        if (_holster.Phase != HolsterPhase.Drawn || !LoadMagazine(world.Weapon, out problem))
                        {
                            return false;
                        }

                        if (!_holster.Holster())
                        {
                            problem = $"Holster() was refused while drawn (phase {_holster.Phase}, draw blocked {_holster.IsDrawBlocked})";
                            return false;
                        }

                        _phase = Phase.WaitHolstered;
                        return false;
                    }

                case Phase.WaitHolstered:
                    {
                        if (!_holster.IsHolstered || !_sawHolstered)
                        {
                            return false;
                        }

                        if (!_sawHolstering)
                        {
                            problem = $"the weapon was put away without a '{BeganPrefix(_holderName, HolsterPhase.Holstering)}' line";
                            return false;
                        }

                        if (!_holster.BlocksFire || world.Weapon.PullTrigger(true, true))
                        {
                            problem = "the weapon fired while it was put away";
                            return false;
                        }

                        Debug.Log($"{LogTag} holstered: the trigger did not fire");
                        if (!_holster.Draw())
                        {
                            problem = $"Draw() was refused while holstered (draw blocked {_holster.IsDrawBlocked})";
                            return false;
                        }

                        _phase = Phase.WaitDrawn;
                        return false;
                    }

                case Phase.WaitDrawn:
                    {
                        if (_holster.Phase != HolsterPhase.Drawn || !_sawDrawn)
                        {
                            return false;
                        }

                        if (!_sawDrawing)
                        {
                            problem = $"the weapon was drawn without a '{BeganPrefix(_holderName, HolsterPhase.Drawing)}' line";
                            return false;
                        }

                        _phase = Phase.FireDrawn;
                        return false;
                    }

                case Phase.FireDrawn:
                    {
                        // 落ち着いた直後は連射の間隔がまだ残っていることがあるので、撃てるまで毎フレーム引く。
                        if (!LoadMagazine(world.Weapon, out problem) || !world.Weapon.PullTrigger(true, true))
                        {
                            return false;
                        }

                        Debug.Log($"{LogTag} drawn again: the trigger fired");
                        _warnZone = AddZone(WarnZoneName, WarnZoneId, WeaponFreeZoneReaction.WarnOnly, WarnZoneCenter);
                        _autoZone = AddZone(AutoHolsterZoneName, AutoHolsterZoneId, WeaponFreeZoneReaction.AutoHolster, AutoHolsterZoneCenter);
                        Place(world, WarnZoneCenter);
                        _phase = Phase.EnterWarnZone;
                        return false;
                    }

                case Phase.EnterWarnZone:
                    {
                        if (!_sawWarning || !_warnZone.IsWarning)
                        {
                            return false;
                        }

                        if (!_sawWarnEntered)
                        {
                            problem = $"the warning began without a '{WeaponFreeZoneLog.Entered(WarnZoneId, _bodyId, true)}' line";
                            return false;
                        }

                        if (!_holster.Status.IsArmed || _holster.Phase != HolsterPhase.Drawn)
                        {
                            problem = $"the {WeaponFreeZoneReaction.WarnOnly} zone changed the weapon (phase {_holster.Phase})";
                            return false;
                        }

                        Debug.Log($"{LogTag} entered '{WarnZoneId}' armed: warned");
                        Place(world, AutoHolsterZoneCenter);
                        _phase = Phase.EnterAutoHolsterZone;
                        return false;
                    }

                case Phase.EnterAutoHolsterZone:
                    {
                        if (!_sawAutoHolster || !_holster.IsHolstered)
                        {
                            return false;
                        }

                        if (_warnZone.IsPlayerInside || _warnZone.IsWarning)
                        {
                            problem = $"'{WarnZoneId}' still held the player after moving to '{AutoHolsterZoneId}'";
                            return false;
                        }

                        Debug.Log($"{LogTag} entered '{AutoHolsterZoneId}' armed: holstered");
                        RemoveZones();
                        if (!_holster.Draw())
                        {
                            problem = $"Draw() was refused after the {WeaponFreeZoneReaction.AutoHolster} zone was removed (draw blocked {_holster.IsDrawBlocked})";
                            return false;
                        }

                        _phase = Phase.RestoreDrawn;
                        return false;
                    }

                case Phase.RestoreDrawn:
                    if (_holster.Phase != HolsterPhase.Drawn)
                    {
                        return false;
                    }

                    _phase = Phase.Done;
                    return true;

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            RemoveZones();
            if (_holster != null && _holster.Phase != HolsterPhase.Drawn)
            {
                _holster.Draw();
            }
        }

        // 秒数は武器ごとに違うので、行の頭 (誰が・どちらへ) だけで見分ける。
        public static string BeganPrefix(string holderName, HolsterPhase phase)
        {
            var line = HolsterLog.Began(holderName, phase, 0f);
            return line.Substring(0, line.IndexOf(" seconds=", StringComparison.Ordinal) + 1);
        }

        public static string WarningPrefix(string zoneId, WeaponFreeZoneReaction reaction)
        {
            var line = WeaponFreeZoneLog.WarningBegan(zoneId, reaction, 0f);
            return line.Substring(0, line.IndexOf(" secondsToHostile=", StringComparison.Ordinal) + 1);
        }

        // しまう・構えるの確かめの前に撃てる状態にしておく。空のまま引くと、しまっていなくても撃てないので確かめにならない。
        private static bool LoadMagazine(WeaponController weapon, out string problem)
        {
            problem = null;
            var current = weapon != null ? weapon.Weapon : null;
            if (current == null)
            {
                problem = "the player has no weapon to fire";
                return false;
            }

            if (current.IsReloading)
            {
                return false;
            }

            if (current.AmmoInMagazine == 0)
            {
                weapon.StartReload();
                return false;
            }

            return true;
        }

        private WeaponFreeZone AddZone(string name, string zoneId, WeaponFreeZoneReaction reaction, Vector3 center)
        {
            var zone = HolsterFeatureSceneBuilder.CreateZone(null, name, zoneId, new WeaponFreeZoneSpec(reaction), center, ZoneSize);
            _zones.Add(zone);
            return zone;
        }

        private void RemoveZones()
        {
            foreach (var zone in _zones)
            {
                if (zone != null)
                {
                    UnityEngine.Object.Destroy(zone.gameObject);
                }
            }

            _zones.Clear();
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Place(BootstrapWorld world, Vector3 feet)
        {
            var mover = world.Mover;
            var controller = world.Controller;
            var root = feet + (Vector3.up * PlaceLift) + (mover.transform.position - LadderMovement.FeetOf(mover));
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = root;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }
    }
}
