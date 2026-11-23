using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // マップの武器を出してはいけない場所 (NetMapWeaponFreeZones) に人が入ったかを、オフラインの WeaponFreeZone と同じ Core の
    // WeaponFreeZoneState で人とゾーンの組ごとに見る。しまわせる・構えるのを断るのは ServerHolster が、ここの答えを見て行う。
    // オンラインの NPC はサーバーの Bot で中立の警備にあたるものが無いので、警告して敵対する反応は警告するだけ (WarnOnly) に読み替える
    internal sealed class ServerWeaponFreeZones
    {
        private readonly Dictionary<int, WeaponFreeZoneState[]> _occupants = new Dictionary<int, WeaponFreeZoneState[]>();
        private IReadOnlyList<MapWeaponFreeZone> _zones = Array.Empty<MapWeaponFreeZone>();
        private WeaponFreeZoneSpec[] _onlineSpecs = Array.Empty<WeaponFreeZoneSpec>();
        private TextWriter _log = TextWriter.Null;

        // holster が null ならオフラインの既定 (Resources/Tuning/holster.json と同じ値) で、どの状態を武装と数えるかを決める
        public ServerWeaponFreeZones(HolsterSpec holster = null)
        {
            Holster = holster ?? HolsterSpec.Default;
        }

        public HolsterSpec Holster { get; }

        public int Count => _zones.Count;

        public IReadOnlyList<MapWeaponFreeZone> Zones => _zones;

        public static WeaponFreeZoneSpec OnlineSpec(WeaponFreeZoneSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return spec.TurnsHostile ? new WeaponFreeZoneSpec(WeaponFreeZoneReaction.WarnOnly, spec.HostileAfterSeconds) : spec;
        }

        // マップを替えると前のマップのゾーンの中にいた記録は意味を失うので、誰の分も捨ててから読み直す
        public void Load(string map, NetMapWeaponFreeZones zones, TextWriter log)
        {
            ArgumentNullException.ThrowIfNull(zones);
            _log = log ?? TextWriter.Null;
            _occupants.Clear();
            _zones = zones.Zones;
            _onlineSpecs = new WeaponFreeZoneSpec[_zones.Count];
            for (var i = 0; i < _onlineSpecs.Length; i++)
            {
                _onlineSpecs[i] = OnlineSpec(_zones[i].Spec);
            }

            _log.WriteLine(ServerWeaponFreeZonesLog.Loaded(map, _zones.Count));
            for (var i = 0; i < _zones.Count; i++)
            {
                _log.WriteLine(ServerWeaponFreeZonesLog.Zone(_zones[i], _onlineSpecs[i].Reaction));
            }
        }

        // center が null なら、どのゾーンにもいない (死んでいる・まだ出ていない) と数える。
        // 戻り値は、構えたまま自動でしまうゾーンにこの tick で入ったそのゾーンの Id で、無ければ null
        public string Update(int playerId, Vector3? center, bool armed, float deltaSeconds)
        {
            if (_zones.Count == 0)
            {
                return null;
            }

            if (!_occupants.TryGetValue(playerId, out var states))
            {
                if (!center.HasValue || !IsInsideAny(center.Value))
                {
                    return null;
                }

                states = new WeaponFreeZoneState[_zones.Count];
                for (var i = 0; i < states.Length; i++)
                {
                    states[i] = new WeaponFreeZoneState(_onlineSpecs[i]);
                }

                _occupants.Add(playerId, states);
            }

            string holsterIn = null;
            var insideAny = false;
            for (var i = 0; i < states.Length; i++)
            {
                var zone = _zones[i];
                var state = states[i];
                var inside = center.HasValue && zone.Contains(center.Value);
                var wasInside = state.Inside;
                var wasWarning = state.Warning;
                state.Tick(deltaSeconds, inside, inside && armed);
                insideAny |= inside;

                if (inside && !wasInside)
                {
                    _log.WriteLine(ServerWeaponFreeZonesLog.Entered(zone.Id, playerId, armed));
                }
                else if (!inside && wasInside)
                {
                    _log.WriteLine(ServerWeaponFreeZonesLog.Exited(zone.Id, playerId));
                }

                if (state.Warning && !wasWarning)
                {
                    _log.WriteLine(ServerWeaponFreeZonesLog.WarningBegan(zone.Id, playerId));
                }
                else if (!state.Warning && wasWarning)
                {
                    var reason = inside ? WeaponFreeZoneLog.UnarmedReason : WeaponFreeZoneLog.ExitedReason;
                    _log.WriteLine(ServerWeaponFreeZonesLog.WarningCleared(zone.Id, playerId, reason));
                }

                if (state.ShouldHolster && holsterIn == null)
                {
                    holsterIn = zone.Id;
                }
            }

            // どこにもいない人の記録を持ち続けると、入ったことのある人の数だけ毎 tick 全部のゾーンを見ることになる
            if (!insideAny)
            {
                _occupants.Remove(playerId);
            }

            return holsterIn;
        }

        // 構えられないゾーンの中なら、そのゾーンの Id (重なっていれば並びの先のもの)。無ければ null
        public string DrawBlockedBy(int playerId)
        {
            if (!_occupants.TryGetValue(playerId, out var states))
            {
                return null;
            }

            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].BlocksDraw)
                {
                    return _zones[i].Id;
                }
            }

            return null;
        }

        // 重なったゾーンでは、警告しているものを先に、その中では並びの先のものを配る。警告の文言を、警告しない重なりのゾーンで消さないため
        public WeaponFreeZoneStatus StatusOf(int playerId)
        {
            if (!_occupants.TryGetValue(playerId, out var states))
            {
                return WeaponFreeZoneStatus.Outside;
            }

            WeaponFreeZoneState first = null;
            foreach (var state in states)
            {
                if (!state.Inside)
                {
                    continue;
                }

                if (state.Warning)
                {
                    return state.Status;
                }

                first ??= state;
            }

            return first?.Status ?? WeaponFreeZoneStatus.Outside;
        }

        public void Forget(int playerId)
        {
            _occupants.Remove(playerId);
        }

        public void Clear()
        {
            _occupants.Clear();
        }

        private bool IsInsideAny(Vector3 center)
        {
            foreach (var zone in _zones)
            {
                if (zone.Contains(center))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
