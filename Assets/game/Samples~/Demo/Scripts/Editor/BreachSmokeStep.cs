using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using Shooter.Runtime.Traversal;
using UnityEngine;

namespace Shooter.Editor
{
    // 射撃場には突入の部屋を置かない (Playground だけに置く) ので、この手順の間だけ開けた場所にドア 2 枚と、その向こうに的を 1 体ずつ置き、終わったら消す。
    // batchmode には E の入力も照準も無いので、BreachDoor.Kick・BeginCharge を直に呼び、[Breach] と [Stagger] の行と、ドアが通れるかで確かめる。
    // 爆薬の品を数えて断るのは BreachController の入力の側なので、ここではドアの状態の進み (蹴る→怯む→仕掛ける→爆発→通れる) だけを通す。
    public sealed class BreachSmokeStep : ITpsActionSmokeStep
    {
        public const string LogTag = "[BreachSmokeStep]";

        public const string KickDoorName = "SmokeBreachDoor_Kick";
        public const string KickDoorId = "smoke-breach-kick";
        public const string KickTargetId = "smoke-breach-kick-target";
        public const string ChargeDoorName = "SmokeBreachDoor_Charge";
        public const string ChargeDoorId = "smoke-breach-charge";
        public const string ChargeTargetId = "smoke-breach-charge-target";

        public const int TargetMaxHealth = 1000;

        // ドアの前 (-Z) に立つ距離。Tuning/breach の reach より近い。
        public const float StandOff = 1f;

        // 仕掛けた後に下がる距離。爆薬の半径 (Tuning/breach の chargeRadius 2.5 m) の外へ出て、自分の爆発で倒れないようにする。
        public const float RetreatDistance = 4f;

        // 的はドアの奥の突入の箱 (roomDepth 5 m) の中で、爆薬の半径の外に立たせる。爆発の傷で先に怯むと、突入の怯みが重ならず数えられない。
        public const float TargetDepth = 3.5f;

        // 置き直した足が床に埋まって押し戻されないよう、わずかに浮かせる。
        public const float PlaceLift = 0.05f;

        // 開けた場所 (TpsRangeSceneBuilder.OpenAreaHalfExtent) の中に、間を空けて 2 枚並べる。ドアの前は -Z (開けた場所の手前) に向ける。
        public static readonly Vector3 KickDoorFeet = TpsRangeSceneBuilder.OpenAreaCenter + new Vector3(-3f, 0f, 0f);

        public static readonly Vector3 ChargeDoorFeet = TpsRangeSceneBuilder.OpenAreaCenter + new Vector3(3f, 0f, 0f);

        public static readonly Quaternion DoorRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private Phase _phase;
        private string _playerId;
        private BreachDoor _kickDoor;
        private BreachDoor _chargeDoor;
        private TargetDummy _kickTarget;
        private TargetDummy _chargeTarget;
        private string _kickedLine;
        private string _kickStaggeredLine;
        private string _kickImposedPrefix;
        private string _plantBeganLine;
        private string _armedLine;
        private string _detonatedLine;
        private string _chargeStaggeredLine;
        private string _chargeImposedPrefix;
        private bool _sawKicked;
        private bool _sawKickStaggered;
        private bool _sawKickImposed;
        private bool _sawPlantBegan;
        private bool _sawArmed;
        private bool _sawDetonated;
        private bool _sawChargeStaggered;
        private bool _sawChargeImposed;

        private enum Phase
        {
            Start,
            Kick,
            CheckKick,
            Plant,
            WaitDetonated,
            Done,
        }

        public int Order => 650;

        public string Name => "breach";

        // 仕掛け (plantSeconds 2.5) と信管 (fuseSeconds 2) に、スローモーションで遅れる分を見込む。
        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _phase = Phase.Start;
            _playerId = null;
            _kickDoor = null;
            _chargeDoor = null;
            _kickTarget = null;
            _chargeTarget = null;
            _kickedLine = null;
            _kickStaggeredLine = null;
            _kickImposedPrefix = null;
            _plantBeganLine = null;
            _armedLine = null;
            _detonatedLine = null;
            _chargeStaggeredLine = null;
            _chargeImposedPrefix = null;
            _sawKicked = false;
            _sawKickStaggered = false;
            _sawKickImposed = false;
            _sawPlantBegan = false;
            _sawArmed = false;
            _sawDetonated = false;
            _sawChargeStaggered = false;
            _sawChargeImposed = false;
            _spawned.Clear();
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            _sawKicked |= Is(condition, _kickedLine);
            _sawKickStaggered |= Is(condition, _kickStaggeredLine);
            _sawKickImposed |= StartsWith(condition, _kickImposedPrefix);
            _sawPlantBegan |= Is(condition, _plantBeganLine);
            _sawArmed |= Is(condition, _armedLine);
            _sawDetonated |= Is(condition, _detonatedLine);
            _sawChargeStaggered |= Is(condition, _chargeStaggeredLine);
            _sawChargeImposed |= StartsWith(condition, _chargeImposedPrefix);
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Start:
                    {
                        var controller = world.Player.GetComponent<BreachController>();
                        if (controller == null || !controller.IsConfigured)
                        {
                            problem = $"the player has no configured {nameof(BreachController)}; {nameof(BreachActionInstaller)} is not registered with the player actions";
                            return false;
                        }

                        _playerId = world.PlayerDamageable != null ? world.PlayerDamageable.Id : string.Empty;
                        _kickDoor = AddDoor(KickDoorName, KickDoorId, KickDoorFeet);
                        _kickTarget = AddTarget(KickTargetId, KickDoorFeet);
                        _kickedLine = BreachLog.Kicked(KickDoorId, _playerId);
                        _kickStaggeredLine = StaggeredLine(_kickDoor, BreachMethod.Kick);
                        _kickImposedPrefix = ImposedPrefix(KickTargetId, BreachMethod.Kick);
                        Place(world, InFrontOf(KickDoorFeet, StandOff));

                        // 置いたばかりの当たりを、次のフレームの突入の箱 (Physics.OverlapBox) に入れる。
                        _phase = Phase.Kick;
                        return false;
                    }

                case Phase.Kick:
                    Physics.SyncTransforms();
                    if (!_kickDoor.Kick(world.Player))
                    {
                        problem = $"Kick() was refused on a closed door (phase {_kickDoor.Phase})";
                        return false;
                    }

                    _phase = Phase.CheckKick;
                    return false;

                case Phase.CheckKick:
                    {
                        if (!_sawKicked || !_sawKickStaggered || !_sawKickImposed)
                        {
                            problem = $"the kick did not log '{_kickedLine}', '{_kickStaggeredLine}' and '{_kickImposedPrefix}...'";
                            return false;
                        }

                        if (!IsStaggered(_kickTarget))
                        {
                            problem = $"'{KickTargetId}' behind the kicked door is not staggered";
                            return false;
                        }

                        if (!_kickDoor.IsPassable)
                        {
                            problem = $"the kicked door is not passable (phase {_kickDoor.Phase})";
                            return false;
                        }

                        Debug.Log($"{LogTag} kicked '{KickDoorId}': the target behind it staggered and the door is passable");
                        _chargeDoor = AddDoor(ChargeDoorName, ChargeDoorId, ChargeDoorFeet);
                        _chargeTarget = AddTarget(ChargeTargetId, ChargeDoorFeet);
                        var spec = _chargeDoor.Spec;
                        _plantBeganLine = BreachLog.PlantBegan(ChargeDoorId, _playerId, spec.PlantSeconds, spec.FuseSeconds);
                        _armedLine = BreachLog.Armed(ChargeDoorId, _playerId, spec.FuseSeconds);
                        _detonatedLine = BreachLog.Detonated(ChargeDoorId, _playerId, spec.ChargeDamage, spec.ChargeRadius);
                        _chargeStaggeredLine = StaggeredLine(_chargeDoor, BreachMethod.Charge);
                        _chargeImposedPrefix = ImposedPrefix(ChargeTargetId, BreachMethod.Charge);
                        Place(world, InFrontOf(ChargeDoorFeet, StandOff));
                        _phase = Phase.Plant;
                        return false;
                    }

                case Phase.Plant:
                    Physics.SyncTransforms();
                    if (!_chargeDoor.BeginCharge(world.Player))
                    {
                        problem = $"BeginCharge() was refused on a closed door (phase {_chargeDoor.Phase})";
                        return false;
                    }

                    if (!_sawPlantBegan)
                    {
                        problem = $"planting began without a '{_plantBeganLine}' line";
                        return false;
                    }

                    // 爆薬を貼る面と爆発の向こう側は仕掛けたときの立ち位置で決まるので、仕掛けた後に下がっても変わらない。
                    Place(world, InFrontOf(ChargeDoorFeet, RetreatDistance));
                    _phase = Phase.WaitDetonated;
                    return false;

                case Phase.WaitDetonated:
                    {
                        if (!_sawDetonated || _chargeDoor.Phase != BreachDoorPhase.Destroyed)
                        {
                            return false;
                        }

                        if (!_sawArmed)
                        {
                            problem = $"the charge blew without a '{_armedLine}' line";
                            return false;
                        }

                        if (!_sawChargeStaggered || !_sawChargeImposed)
                        {
                            problem = $"the blast did not log '{_chargeStaggeredLine}' and '{_chargeImposedPrefix}...'";
                            return false;
                        }

                        if (!IsStaggered(_chargeTarget))
                        {
                            problem = $"'{ChargeTargetId}' behind the blown door is not staggered";
                            return false;
                        }

                        if (!_chargeDoor.IsPassable)
                        {
                            problem = "the blown door is not passable";
                            return false;
                        }

                        Debug.Log($"{LogTag} blew '{ChargeDoorId}': the target behind it staggered and the door is passable");
                        _phase = Phase.Done;
                        return true;
                    }

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    UnityEngine.Object.Destroy(spawned);
                }
            }

            _spawned.Clear();
        }

        public static Vector3 InFrontOf(Vector3 doorFeet, float distance)
        {
            return doorFeet + (DoorRotation * Vector3.forward * distance);
        }

        // ドアの前に立つ人から見た奥 (突入の箱) の中。
        public static Vector3 TargetFeet(Vector3 doorFeet)
        {
            return doorFeet - (DoorRotation * Vector3.forward * TargetDepth);
        }

        // 秒数は怯みの判定が丸めることがあるので、行の頭 (誰が・どの怯みで) だけで見分ける。
        public static string ImposedPrefix(string targetId, BreachMethod method)
        {
            var line = StaggerReaction.FormatImposeLog(targetId, BreachSpec.StaggerKindFor(method), 0f, BreachDoor.StaggerSource);
            return line.Substring(0, line.IndexOf(" seconds=", StringComparison.Ordinal) + 1);
        }

        private static string StaggeredLine(BreachDoor door, BreachMethod method)
        {
            var spec = door.Spec;
            return BreachLog.Staggered(door.Id, method, BreachSpec.StaggerKindFor(method), spec.StaggerSecondsFor(method), 1);
        }

        private BreachDoor AddDoor(string name, string doorId, Vector3 feet)
        {
            var door = BreachFeatureSceneBuilder.CreateDoor(null, name, doorId, feet, DoorRotation, BreachFeatureSceneBuilder.PanelSize);
            _spawned.Add(door.gameObject);
            return door;
        }

        private TargetDummy AddTarget(string targetId, Vector3 doorFeet)
        {
            // 板の方 (ドアの前に立つ人) を向かせる。
            var target = BreachFeatureSceneBuilder.CreateTarget(null, targetId, TargetFeet(doorFeet), DoorRotation, TargetMaxHealth);
            _spawned.Add(target.gameObject);
            return target;
        }

        private static bool IsStaggered(TargetDummy target)
        {
            var reaction = target != null ? target.GetComponent<StaggerReaction>() : null;
            return reaction != null && reaction.IsStaggered;
        }

        private static bool Is(string condition, string expected)
        {
            return expected != null && string.Equals(condition, expected, StringComparison.Ordinal);
        }

        private static bool StartsWith(string condition, string prefix)
        {
            return prefix != null && condition.StartsWith(prefix, StringComparison.Ordinal);
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
