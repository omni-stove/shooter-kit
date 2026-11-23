using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayerWeapons
    {
        // 押していた時間は届いた時刻の差で測るので、前の射撃だけ遅れて届くと短く見える。その揺れで正しく溜めた弾を削らないよう、この秒数だけ長く見る。
        public const double ChargeArrivalToleranceSeconds = 0.2d;

        private readonly int _playerId;
        private readonly TextWriter _log;
        private readonly Func<double> _now;
        private Loadout<ServerWeapon> _loadout;
        private double _reloadStartedAt;
        private float _chargeFraction;
        private double _chargeFrom;
        private int _burstRoundsLeft;
        private double _burstUntil;
        private Func<WeaponSpec, IAmmoPool> _poolFor;

        private IReadOnlyList<WeaponSpec> _baseSpecs = Array.Empty<WeaponSpec>();
        private readonly Dictionary<string, IReadOnlyList<string>> _attachments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        // 二丁持ちの武器の左右の手。並びの武器ごとに持ち、しまっている間も弾数を覚えておく。
        // 持ち替えるたびに作り直さないのは、作り直すと弾倉が満タンに戻り、持ち替えるだけでリロードを省けてしまうため。
        // 二丁持ちの武器の ServerWeapon は Spec と並びの位置のためだけに持ち、弾を数えない (予備弾の受け皿にもつながない)
        private readonly Dictionary<ServerWeapon, ServerAkimbo> _akimbo = new Dictionary<ServerWeapon, ServerAkimbo>();

        public ServerPlayerWeapons(int playerId, TextWriter log, Func<double> now)
        {
            _playerId = playerId;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public bool IsEquipped => _loadout != null;

        public WeaponSpec CurrentSpec => _loadout?.Current.Spec;

        public bool IsSwitching => _loadout != null && _loadout.IsSwitching;

        // 今の武器を構える秒数。持ち替えで構えるのと同じ秒数 (Loadout.DrawSecondsOf) にそろえる。武器が無ければ 0
        public float CurrentDrawSeconds => _loadout == null ? 0f : _loadout.DrawSecondsOf(_loadout.CurrentIndex);

        // 今の武器をしまう秒数。WeaponSpec.HolsterSeconds が 0 の武器は、構えるのと同じ秒数をかける (オフラインの HolsterController と同じ)
        public float CurrentHolsterSeconds
        {
            get
            {
                if (_loadout == null)
                {
                    return 0f;
                }

                var holster = _loadout.HolsterSecondsOf(_loadout.CurrentIndex);
                return holster > 0f ? holster : CurrentDrawSeconds;
            }
        }

        // 盾の武器は弾を込めない (オフラインの WeaponController.StartReload と同じ)
        private bool HoldsShield => _loadout != null && _loadout.Current.Spec.IsShield;

        public IReadOnlyList<ServerWeapon> Items => _loadout?.Items ?? Array.Empty<ServerWeapon>();

        public IReadOnlyList<WeaponSpec> Specs { get; private set; } = Array.Empty<WeaponSpec>();

        public RoundSpec CurrentRound => _loadout == null ? null : CurrentAmmoType.Round;

        public ImpactSpec CurrentImpact => _loadout == null ? null : CurrentAmmoType.Impact;

        public byte AmmoTypeIndex => _loadout == null ? (byte)0 : (byte)(CurrentAmmoType.Index + 1);

        private AmmoTypeSelector CurrentAmmoType => CurrentAkimbo?.AmmoTypes ?? _loadout.Current.AmmoTypes;

        // 今の武器が二丁持ちなら左右の手。1 丁で持つ武器や武器が無ければ null
        private ServerAkimbo CurrentAkimbo => _loadout == null ? null : AkimboOf(_loadout.Current);

        public bool HoldsAkimbo => CurrentAkimbo != null;

        public void Equip(IReadOnlyList<WeaponSpec> specs, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(specs);
            ArgumentNullException.ThrowIfNull(rules);
            if (specs.Count == 0 || specs.Count > OwnerSnapshot.MaxWeapons)
            {
                throw new ArgumentException("specs must hold 1-" + OwnerSnapshot.MaxWeapons + " weapons", nameof(specs));
            }

            // 作る途中で断ると二丁持ちの手だけが _akimbo に残るので、作り始める前に確かめる
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    throw new ArgumentException("specs must not contain null", nameof(specs));
                }
            }

            // 前の並びの武器は受け皿 (持ち物) の Changed に購読が残るので、捨てる前に外す
            foreach (var old in Items)
            {
                Release(old);
            }

            var weapons = new ServerWeapon[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                weapons[i] = CreateWeapon(specs[i], rules);
            }

            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            _baseSpecs = new List<WeaponSpec>(specs).AsReadOnly();
            _attachments.Clear();
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds));
        }

        // 箱から拾った武器のように、出直しを待たずに外から持たせる口。今の武器を構えたまま並びの末尾に足す。
        // 同じ武器を持っている・並びが一杯なら足さない (持ち替えの並びは同じ Id を 2 つ持てないため)
        public bool Give(WeaponSpec spec, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(rules);
            var items = Items;
            foreach (var held in items)
            {
                if (string.Equals(held.Spec.Id, spec.Id, StringComparison.Ordinal))
                {
                    _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, "held"));
                    return false;
                }
            }

            if (items.Count >= OwnerSnapshot.MaxWeapons)
            {
                _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, "full"));
                return false;
            }

            if (_loadout == null)
            {
                Equip(new[] { spec }, switchSeconds, rules);
                _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, null));
                return true;
            }

            var weapon = CreateWeapon(spec, rules);
            var weapons = new List<ServerWeapon>(items) { weapon };
            _baseSpecs = new List<WeaponSpec>(_baseSpecs) { spec }.AsReadOnly();
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds, _loadout.CurrentIndex));
            _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, null));
            return true;
        }

        // 拾った武器で weaponId の武器を置き換え、置き換えた武器を返す。並びの位置と構えている番号はそのままにする
        // (今の武器を置き換えたら、拾った武器を構える)。weaponId を持っていない・spec の武器を既に持っているなら何もせず null
        public ServerHeldWeapon Replace(string weaponId, WeaponSpec spec, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(weaponId);
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(rules);
            var index = IndexOf(weaponId);
            if (index < 0 || (!string.Equals(weaponId, spec.Id, StringComparison.Ordinal) && IndexOf(spec.Id) >= 0))
            {
                _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, index < 0 ? "not-held:" + weaponId : "held"));
                return null;
            }

            var items = _loadout.Items;
            var replaced = HeldAt(index);
            Release(items[index]);
            var weapon = CreateWeapon(spec, rules);
            var weapons = new List<ServerWeapon>(items);
            weapons[index] = weapon;
            var bases = new List<WeaponSpec>(_baseSpecs);
            bases[index] = spec;
            _baseSpecs = bases.AsReadOnly();
            _attachments.Remove(weaponId);
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds, _loadout.CurrentIndex));
            _log.WriteLine(ServerWeaponsLog.Replaced(_playerId, spec.Id, weaponId));
            return replaced;
        }

        // 構えている武器を手放さずに写す。武器が無ければ null
        public ServerHeldWeapon CurrentHeld()
        {
            return _loadout == null ? null : HeldAt(_loadout.CurrentIndex);
        }

        // 持っている武器のうち weaponId のもの。無ければ null。
        // 二丁持ちの武器の弾数はこの ServerWeapon ではなく左右の手にあるので、弾を足す・弾数を決めるのは AddReserveAmmo・SetAmmo を使う
        public ServerWeapon Find(string weaponId)
        {
            var index = IndexOf(weaponId);
            return index < 0 ? null : _loadout.Items[index];
        }

        // 拾った予備弾を weaponId の武器に足し、入った数を返す。持っていない・入らなければ 0
        public int AddReserveAmmo(string weaponId, int amount)
        {
            var weapon = Find(weaponId);
            if (weapon == null)
            {
                return 0;
            }

            var akimbo = AkimboOf(weapon);
            return akimbo != null ? akimbo.AddReserveAmmo(amount) : weapon.AddReserveAmmo(amount);
        }

        // 落とした武器を拾い直したときの弾数で持たせる。持っていなければ false
        public bool SetAmmo(string weaponId, int ammoInMagazine, int reserveAmmo)
        {
            var weapon = Find(weaponId);
            if (weapon == null)
            {
                return false;
            }

            var akimbo = AkimboOf(weapon);
            if (akimbo != null)
            {
                akimbo.SetAmmo(ammoInMagazine, reserveAmmo);
            }
            else
            {
                weapon.SetAmmo(ammoInMagazine, reserveAmmo);
            }

            return true;
        }

        // 拾い物の判定に渡す、持っている武器ごとの予備弾
        public List<PickupWeaponState> PickupStates()
        {
            var items = Items;
            var states = new List<PickupWeaponState>(items.Count);
            foreach (var weapon in items)
            {
                var akimbo = AkimboOf(weapon);
                states.Add(akimbo != null
                    ? new PickupWeaponState(weapon.Spec.Id, akimbo.ReserveAmmo, akimbo.MaxReserveAmmo, akimbo.HasInfiniteReserve)
                    : new PickupWeaponState(weapon.Spec.Id, weapon.ReserveAmmo, weapon.MaxReserveAmmo, weapon.HasInfiniteReserve));
            }

            return states;
        }

        // 弾薬箱の補給。持っている武器の予備弾をそれぞれ上限まで足し、入った数の合計を返す
        public int TopUpReserveAmmo()
        {
            var added = 0;
            foreach (var weapon in Items)
            {
                var akimbo = AkimboOf(weapon);
                if (akimbo != null)
                {
                    var room = akimbo.HasInfiniteReserve ? 0 : Math.Max(0, akimbo.MaxReserveAmmo - akimbo.ReserveAmmo);
                    added += room > 0 ? akimbo.AddReserveAmmo(room) : 0;
                }
                else
                {
                    var room = weapon.HasInfiniteReserve ? 0 : Math.Max(0, weapon.MaxReserveAmmo - weapon.ReserveAmmo);
                    added += room > 0 ? weapon.AddReserveAmmo(room) : 0;
                }
            }

            return added;
        }

        private int IndexOf(string weaponId)
        {
            if (_loadout == null || weaponId == null)
            {
                return -1;
            }

            var items = _loadout.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private ServerHeldWeapon HeldAt(int index)
        {
            var weapon = _loadout.Items[index];
            var akimbo = AkimboOf(weapon);
            if (akimbo != null)
            {
                return new ServerHeldWeapon(_baseSpecs[index], AttachmentsOf(weapon.Spec.Id), akimbo.HeldAmmoInMagazine, akimbo.HeldReserveAmmo);
            }

            return new ServerHeldWeapon(
                _baseSpecs[index], AttachmentsOf(weapon.Spec.Id), weapon.AmmoInMagazine, weapon.HasAmmoPool ? 0 : weapon.ReserveAmmo);
        }

        // 武器をすべて手放す。次の Equip か Give まで撃てない (TryFire は NoWeapon を返す)
        public void Clear()
        {
            if (_loadout == null)
            {
                return;
            }

            var count = _loadout.Count;
            foreach (var weapon in _loadout.Items)
            {
                Release(weapon);
            }

            Unsubscribe();
            _loadout = null;
            Specs = Array.Empty<WeaponSpec>();
            _baseSpecs = Array.Empty<WeaponSpec>();
            _attachments.Clear();
            _chargeFraction = 0f;
            _burstRoundsLeft = 0;
            _log.WriteLine(ServerWeaponsLog.Clear(_playerId, count));
        }

        // 次の武器は持ち替えの待ちを課さずに構える。並びを作り直す Give・Refill と同じく、TrySelect で待たせると構える番号が先に動いてしまうため。
        // 付けた物を外した元の値を返すのは、Take と同じく、拾い直した先で付けた物を別に扱えるようにするため
        public bool Drop(string weaponId, out WeaponSpec spec)
        {
            spec = null;
            if (_loadout == null || weaponId == null)
            {
                return false;
            }

            var items = _loadout.Items;
            var index = -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            spec = _baseSpecs[index];
            if (items.Count == 1)
            {
                Clear();
                _log.WriteLine(ServerWeaponsLog.Drop(_playerId, weaponId, 0));
                return true;
            }

            var current = _loadout.CurrentIndex;
            var weapons = new List<ServerWeapon>(items);
            var baseSpecs = new List<WeaponSpec>(_baseSpecs);
            Release(weapons[index]);
            weapons.RemoveAt(index);
            baseSpecs.RemoveAt(index);
            _baseSpecs = baseSpecs.AsReadOnly();
            _attachments.Remove(weaponId);
            var selected = current > index ? current - 1 : Math.Min(current, weapons.Count - 1);
            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            SetLoadout(new Loadout<ServerWeapon>(weapons, _loadout.SwitchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds, selected));
            _log.WriteLine(ServerWeaponsLog.Drop(_playerId, weaponId, weapons.Count));
            return true;
        }

        // 持っている武器を並びの順に取り出して手放す。付けた物を外した元の値と、付けていた物の Id を返すのは、
        // 取り出した先 (倒れた人の箱など) が付けた物を別の拾い物として扱えるようにするため
        public IReadOnlyList<(WeaponSpec Spec, IReadOnlyList<string> Attachments)> Take()
        {
            var taken = new List<(WeaponSpec Spec, IReadOnlyList<string> Attachments)>(_baseSpecs.Count);
            foreach (var spec in _baseSpecs)
            {
                taken.Add((spec, AttachmentsOf(spec.Id)));
            }

            Clear();
            return taken;
        }

        public IReadOnlyList<string> AttachmentsOf(string weaponId)
        {
            return weaponId != null && _attachments.TryGetValue(weaponId, out var attached) ? attached : WeaponAttachments.None;
        }

        public bool SetAttachments(string weaponId, IReadOnlyList<string> attachments, ItemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            var items = Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (!string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    continue;
                }

                var kept = WeaponAttachments.Keep(catalog, weaponId, attachments);
                var applied = WeaponAttachments.Apply(_baseSpecs[i], kept, catalog);
                var akimbo = AkimboOf(items[i]);

                // 二丁持ちの手を先に変えるのは、弾を数えているのが手の方で、手が断ったら ServerWeapon も変えずに残すため
                if ((akimbo != null && !akimbo.ChangeSpec(applied)) || !items[i].ChangeSpec(applied))
                {
                    _log.WriteLine("[Net] attachments player=" + Text(_playerId) + " weapon=" + weaponId + " attached=" + WeaponAttachments.Describe(kept)
                        + " rejected=NoRoomForAmmo");
                    return false;
                }

                if (kept.Count == 0)
                {
                    _attachments.Remove(weaponId);
                }
                else
                {
                    _attachments[weaponId] = kept;
                }

                var specs = new WeaponSpec[items.Count];
                for (var s = 0; s < specs.Length; s++)
                {
                    specs[s] = items[s].Spec;
                }

                Specs = specs;
                _log.WriteLine("[Net] attachments player=" + Text(_playerId) + " weapon=" + weaponId + " attached=" + WeaponAttachments.Describe(kept)
                    + " magazine=" + Text(akimbo?.HeldAmmoInMagazine ?? items[i].AmmoInMagazine) + "/" + Text(items[i].Spec.MagazineSize));
                return true;
            }

            return false;
        }

        // 武器ごとの予備弾の受け皿 (持ち物の弾薬) を決める。今の武器と、この後に持たせる (Equip) 武器に使う。null で外す
        public void UseAmmoPools(Func<WeaponSpec, IAmmoPool> poolFor)
        {
            _poolFor = poolFor;
            foreach (var weapon in Items)
            {
                var pool = poolFor?.Invoke(weapon.Spec);
                var akimbo = AkimboOf(weapon);
                if (akimbo != null)
                {
                    akimbo.UseAmmoPool(pool);
                }
                else
                {
                    weapon.UseAmmoPool(pool);
                }
            }
        }

        // 予備弾を受け皿から出し入れしている武器と、その最初の予備弾
        public IReadOnlyList<(string WeaponId, int StartingReserveAmmo)> PooledAmmo()
        {
            var list = new List<(string WeaponId, int StartingReserveAmmo)>();
            foreach (var weapon in Items)
            {
                var akimbo = AkimboOf(weapon);
                if (akimbo != null ? akimbo.AmmoPool != null : weapon.HasAmmoPool)
                {
                    list.Add((weapon.Spec.Id, akimbo?.StartingReserveAmmo ?? weapon.StartingReserveAmmo));
                }
            }

            return list;
        }

        public void Refill()
        {
            if (_loadout == null)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.Refill();
                AkimboOf(weapon)?.Refill();
            }

            // TrySelect(0) では持ち替えの待ちが始まるので、同じ武器で並びを作り直して待たずに 0 番を構える
            if (_loadout.CurrentIndex != 0 || _loadout.IsSwitching)
            {
                SetLoadout(new Loadout<ServerWeapon>(_loadout.Items, _loadout.SwitchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds));
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_loadout == null)
            {
                return;
            }

            _loadout.Tick(deltaSeconds);
            _loadout.Current.Tick(deltaSeconds);
            CurrentAkimbo?.Tick(deltaSeconds);
        }

        public ShotRejection TryFire()
        {
            return TryFire(AkimboHand.Right);
        }

        // 二丁持ちの武器は hand の手の弾倉で撃つ。1 丁で持つ武器は右の手 (撃った手を運ばない前の版の要求と同じ) だけを受け付ける
        public ShotRejection TryFire(AkimboHand hand)
        {
            if (_loadout == null)
            {
                return ShotRejection.NoWeapon;
            }

            if (_loadout.IsSwitching)
            {
                return ShotRejection.Switching;
            }

            if (HoldsShield)
            {
                return ShotRejection.ShieldWeapon;
            }

            var akimbo = CurrentAkimbo;
            if (akimbo != null)
            {
                return akimbo.TryFire(hand);
            }

            return hand == AkimboHand.Right ? _loadout.Current.TryFire() : ShotRejection.NotAkimbo;
        }

        // ボットが次に撃つ手。二丁持ちでなければ右の手
        public AkimboHand HandForBot()
        {
            return CurrentAkimbo?.HandForBot() ?? AkimboHand.Right;
        }

        // 二丁持ちの武器は、満タンでない手を両方込める
        public bool TryReload()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield)
            {
                return false;
            }

            var akimbo = CurrentAkimbo;
            return akimbo != null ? akimbo.TryReload() : _loadout.Current.TryStartReload();
        }

        // ボットのリロード。二丁持ちの武器は撃ち切った手だけを込め、もう片方の手で撃ち続ける
        public bool TryBotReload()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield)
            {
                return false;
            }

            var akimbo = CurrentAkimbo;
            return akimbo != null ? akimbo.TryReloadEmpty() : _loadout.Current.TryStartReload();
        }

        public bool CancelReload()
        {
            if (_loadout == null)
            {
                return false;
            }

            var akimbo = CurrentAkimbo;
            if (akimbo != null)
            {
                return akimbo.CancelReload();
            }

            if (!_loadout.Current.IsReloading)
            {
                return false;
            }

            _loadout.Current.CancelReload();
            return true;
        }

        public bool TryActiveReload()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield)
            {
                return false;
            }

            var akimbo = CurrentAkimbo;
            ActiveReloadResult result;
            if (!(akimbo != null ? akimbo.TryActiveReload(out result) : _loadout.Current.TryActiveReload(out result)))
            {
                return false;
            }

            _log.WriteLine("[Net] active-reload player=" + Text(_playerId) + " " + (result == ActiveReloadResult.Success ? "success" : "fail"));
            return true;
        }

        public ShotRejection AcceptCharge(float requested, out float fraction)
        {
            var charge = CurrentSpec?.Trigger.Charge;
            if (charge == null)
            {
                _chargeFraction = 0f;
                fraction = 0f;
                return ShotRejection.None;
            }

            // バーストの続きの弾は離したときの 1 回の溜めで出るので、測り直さずに最初の弾の溜めを使う。
            // 測り直すと、続きの弾の間隔しか押していなかったことになり、最小に届かない弾として弾いてしまう。
            var now = _now();
            if (_burstRoundsLeft > 0 && now <= _burstUntil)
            {
                _burstRoundsLeft--;
                fraction = Math.Min(_chargeFraction, float.IsFinite(requested) ? Math.Max(0f, requested) : 0f);
                return ShotRejection.None;
            }

            // 弾かれた射撃でも、撃った側は離して溜め直しているので、ここから測り直す。
            _burstRoundsLeft = 0;
            var held = now - _chargeFrom + ChargeArrivalToleranceSeconds;
            _chargeFrom = now;
            fraction = ChargeTrigger.Reachable(charge, float.IsFinite(requested) ? requested : 0f, held);
            if (!ChargeTrigger.Fires(charge, fraction))
            {
                fraction = 0f;
                return ShotRejection.Undercharged;
            }

            _chargeFraction = fraction;
            var burst = CurrentSpec.Trigger.Burst;
            if (burst != null)
            {
                _burstRoundsLeft = burst.RoundsPerBurst - 1;
                _burstUntil = now + (_burstRoundsLeft * (double)burst.RoundIntervalSeconds) + ChargeArrivalToleranceSeconds;
            }

            return ShotRejection.None;
        }

        // Bot は人のように押して溜めないので、溜め切るまで撃たない (オフラインの敵の TriggerDriver.PullAsBot と同じ)。
        // 溜め切る前に撃たせると、最小に届かない弾として弾かれ続け、連射の枠と警告だけを使い続けるため。
        public bool IsChargedForBot()
        {
            var charge = CurrentSpec?.Trigger.Charge;
            return charge == null || ChargeTrigger.Fraction(charge, _now() - _chargeFrom) >= 1f;
        }

        public bool TrySwitchAmmoType()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield)
            {
                return false;
            }

            var akimbo = CurrentAkimbo;
            if (!(akimbo != null ? akimbo.TrySwitchAmmoType() : _loadout.Current.TrySwitchAmmoType()))
            {
                return false;
            }

            _log.WriteLine("[Net] ammo-type player=" + Text(_playerId) + " weapon=" + _loadout.Current.Spec.Id + " index=" + Text(AmmoTypeIndex));
            return true;
        }

        public bool TrySwitch(string weaponId)
        {
            if (_loadout == null)
            {
                return false;
            }

            var items = _loadout.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    return _loadout.TrySelect(i);
                }
            }

            _log.WriteLine("[Net] ignored switch client=" + Text(_playerId) + " weaponIdLength=" + Text(weaponId?.Length ?? 0) + " not in the loadout");
            return false;
        }

        // Unity の NetPlayerWeapon.BuildState と同じものを載せる。
        public WeaponSync Sync()
        {
            if (_loadout == null)
            {
                return default;
            }

            var weapon = _loadout.Current;
            var spec = weapon.Spec;
            var showsProgress = weapon.HasActiveReload && weapon.IsReloading;
            var windowOpen = showsProgress && !weapon.HasJudgedActiveReload;
            var sync = new WeaponSync
            {
                AmmoInMagazine = weapon.AmmoInMagazine,
                ReserveAmmo = weapon.ReserveAmmo,
                IsReloading = weapon.IsReloading,
                EquippedIndex = (byte)_loadout.CurrentIndex,
                WeaponCount = (byte)_loadout.Count,
                IsSwitching = _loadout.IsSwitching,
                ReloadStartedAt = showsProgress ? _reloadStartedAt : 0d,
                ReloadSeconds = showsProgress ? weapon.ReloadDurationSeconds : 0f,
                ActiveReloadWindowStart = windowOpen ? spec.ActiveReloadWindowStart : 0f,
                ActiveReloadWindowEnd = windowOpen ? spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength : 0f,
                Heat = weapon.HeatFraction ?? 0f,
                IsOverheated = weapon.IsOverheated,
                ChargeFraction = spec.Trigger.Charge != null ? _chargeFraction : 0f,
                AmmoTypeIndex = AmmoTypeIndex,
            };

            // 二丁持ちの武器の弾数とリロードは ServerWeapon ではなく左右の手から載せる
            CurrentAkimbo?.Fill(ref sync);
            return sync;
        }

        public void FillIds(ContentIdList ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            ids.Clear();
            foreach (var weapon in Items)
            {
                ids.Add(weapon.Spec.Id);
            }
        }

        private void SetLoadout(Loadout<ServerWeapon> loadout)
        {
            Unsubscribe();
            _loadout = loadout;
            var specs = new WeaponSpec[loadout.Items.Count];
            for (var i = 0; i < specs.Length; i++)
            {
                specs[i] = loadout.Items[i].Spec;
            }

            Specs = specs;
            _loadout.Switched += HandleSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.ReloadStarted += HandleReloadStarted;
                weapon.Reloaded += HandleReloaded;
            }
        }

        private void Unsubscribe()
        {
            if (_loadout == null)
            {
                return;
            }

            _loadout.Switched -= HandleSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.ReloadStarted -= HandleReloadStarted;
                weapon.Reloaded -= HandleReloaded;
            }
        }

        private static float? DrawSeconds(ServerWeapon weapon) => weapon.Spec.DrawSeconds;

        private static float HolsterSeconds(ServerWeapon weapon) => weapon.Spec.HolsterSeconds;

        private void HolsterWeapon(ServerWeapon weapon)
        {
            weapon.CancelReload();
            AkimboOf(weapon)?.CancelReload();
        }

        private ServerAkimbo AkimboOf(ServerWeapon weapon)
        {
            return weapon != null && _akimbo.TryGetValue(weapon, out var akimbo) ? akimbo : null;
        }

        // 二丁持ちの武器は左右の手を作り、予備弾の受け皿は手につなぐ (ServerWeapon は弾を数えないので受け皿につながない)
        private ServerWeapon CreateWeapon(WeaponSpec spec, GameRules rules)
        {
            var weapon = new ServerWeapon(spec, rules);
            var pool = _poolFor?.Invoke(spec);
            if (spec.IsAkimbo)
            {
                _akimbo.Add(weapon, new ServerAkimbo(_playerId, spec, rules, _log, _now, pool));
            }
            else if (_poolFor != null)
            {
                weapon.UseAmmoPool(pool);
            }

            return weapon;
        }

        // 手放す武器を予備弾の受け皿 (持ち物) から外す。受け皿の Changed に購読が残らないようにするため
        private void Release(ServerWeapon weapon)
        {
            weapon.UseAmmoPool(null);
            if (_akimbo.Remove(weapon, out var akimbo))
            {
                akimbo.Release();
            }
        }

        private void HandleSwitched(int index)
        {
            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            _log.WriteLine("[Net] switch client=" + Text(_playerId) + " index=" + Text(index) + " weapon=" + _loadout.Items[index].Spec.Id);
        }

        // しまった武器のリロードは HolsterWeapon でやめ、Tick も今の武器だけなので、リロードが起きるのは今の武器だけ
        private void HandleReloadStarted(bool automatic)
        {
            _reloadStartedAt = _now();
            _burstRoundsLeft = 0;
            var weapon = _loadout.Current;
            _log.WriteLine("[Net] reload start client=" + Text(_playerId) + " auto=" + automatic + " ammo=" + Text(weapon.AmmoInMagazine)
                + " reserve=" + Text(weapon.ReserveAmmo));
        }

        private void HandleReloaded()
        {
            var weapon = _loadout.Current;
            _log.WriteLine("[Net] reloaded client=" + Text(_playerId) + " ammo=" + Text(weapon.AmmoInMagazine) + " reserve=" + Text(weapon.ReserveAmmo));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
