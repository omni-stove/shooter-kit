using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // 射撃場の持ち武器には始めからライトが付いていないので、この手順の間だけ付けられる武器にライトを付け、終わったら付けていた物と持っていた武器に戻す。
    // batchmode には L の入力が無いので、WeaponLightController.Toggle と WeaponController.Select を直に呼ぶ。
    public sealed class WeaponLightSmokeStep : ITpsActionSmokeStep
    {
        public const string LogTag = "[WeaponLightSmokeStep]";

        private Phase _phase;
        private WeaponLightController _light;
        private WeaponController _weapon;
        private HolsterController _holster;
        private string _holderName;
        private string _lightItemId;
        private string _litWeaponId;
        private string _darkWeaponId;
        private string _originalWeaponId;
        private IReadOnlyList<string> _savedAttachments;
        private bool _attached;
        private bool _sawOn;
        private bool _sawSwitchedOff;
        private bool _sawNoLight;

        private enum Phase
        {
            Start,
            WaitLitWeapon,
            CheckLit,
            WaitDarkWeapon,
            Done,
        }

        public int Order => 610;

        public string Name => "weaponLight";

        public float TimeoutSeconds => 20f;

        public static string OnLine(string holderName, string weaponId)
        {
            return WeaponLightLog.Toggled(holderName, weaponId, true);
        }

        public static string SwitchedOffLine(string holderName, string weaponId)
        {
            return WeaponLightLog.TurnedOff(holderName, weaponId, WeaponLightOffCause.Switched);
        }

        public static string NoLightLine(string holderName, string weaponId)
        {
            return WeaponLightLog.NotLit(holderName, weaponId, WeaponLightController.NoLightReason);
        }

        public void Reset()
        {
            _phase = Phase.Start;
            _light = null;
            _weapon = null;
            _holster = null;
            _holderName = null;
            _lightItemId = null;
            _litWeaponId = null;
            _darkWeaponId = null;
            _originalWeaponId = null;
            _savedAttachments = null;
            _attached = false;
            _sawOn = false;
            _sawSwitchedOff = false;
            _sawNoLight = false;
        }

        public void OnLog(string condition)
        {
            if (condition == null || _holderName == null)
            {
                return;
            }

            if (_litWeaponId != null && string.Equals(condition, OnLine(_holderName, _litWeaponId), StringComparison.Ordinal))
            {
                _sawOn = true;
            }
            else if (_litWeaponId != null && string.Equals(condition, SwitchedOffLine(_holderName, _litWeaponId), StringComparison.Ordinal))
            {
                _sawSwitchedOff = true;
            }
            else if (_darkWeaponId != null && string.Equals(condition, NoLightLine(_holderName, _darkWeaponId), StringComparison.Ordinal))
            {
                _sawNoLight = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Start:
                    return Begin(world, out problem);

                case Phase.WaitLitWeapon:
                    {
                        if (!IsSettledOn(_litWeaponId))
                        {
                            return false;
                        }

                        if (_light.CurrentLight == null)
                        {
                            problem = $"'{_litWeaponId}' has no light after attaching '{_lightItemId}'";
                            return false;
                        }

                        if (!_light.Toggle())
                        {
                            problem = $"Toggle() did not light '{_litWeaponId}' (status hasLight={_light.Status.HasLight} on={_light.Status.IsOn})";
                            return false;
                        }

                        _phase = Phase.CheckLit;
                        return false;
                    }

                case Phase.CheckLit:
                    {
                        if (!_sawOn)
                        {
                            return false;
                        }

                        var signal = GameSignals.WeaponLight.Value;
                        if (!_light.IsOn || _light.Beam == null || !_light.Beam.IsOn || !signal.HasValue || !signal.Value.IsOn)
                        {
                            problem = $"after '{OnLine(_holderName, _litWeaponId)}' the light was not shining (on={_light.IsOn} beam={_light.Beam != null && _light.Beam.IsOn} signal={(signal.HasValue && signal.Value.IsOn)})";
                            return false;
                        }

                        Debug.Log($"{LogTag} lit: weapon={_litWeaponId} beam=on");
                        if (!_weapon.Select(_darkWeaponId))
                        {
                            problem = $"Select('{_darkWeaponId}') was refused";
                            return false;
                        }

                        _phase = Phase.WaitDarkWeapon;
                        return false;
                    }

                case Phase.WaitDarkWeapon:
                    {
                        if (!IsSettledOn(_darkWeaponId))
                        {
                            return false;
                        }

                        if (_light.IsOn || (_light.Beam != null && _light.Beam.IsOn))
                        {
                            problem = $"the light stayed on after switching from '{_litWeaponId}' to '{_darkWeaponId}'";
                            return false;
                        }

                        if (!_sawSwitchedOff)
                        {
                            problem = $"the light went off without a '{SwitchedOffLine(_holderName, _litWeaponId)}' line";
                            return false;
                        }

                        Debug.Log($"{LogTag} switched: weapon={_darkWeaponId} light=off cause={WeaponLightOffCause.Switched}");
                        if (_light.Toggle() || _light.IsOn)
                        {
                            problem = $"'{_darkWeaponId}' has no light but Toggle() lit it";
                            return false;
                        }

                        if (!_sawNoLight)
                        {
                            problem = $"pressing the light on '{_darkWeaponId}' gave no '{NoLightLine(_holderName, _darkWeaponId)}' line";
                            return false;
                        }

                        Debug.Log($"{LogTag} no light: weapon={_darkWeaponId} not-lit reason={WeaponLightController.NoLightReason}");
                        Restore();
                        _phase = Phase.Done;
                        return true;
                    }

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            Restore();
        }

        private bool Begin(BootstrapWorld world, out string problem)
        {
            problem = null;
            _light = world.Player.GetComponent<WeaponLightController>();
            if (_light == null || !_light.IsInitialized)
            {
                problem = $"the player has no initialized {nameof(WeaponLightController)}; {nameof(WeaponLightActionInstaller)} is not registered with the player actions";
                return false;
            }

            _weapon = world.Weapon;
            if (_weapon == null || !_weapon.IsInitialized || _weapon.IsUnarmed)
            {
                problem = "the player has no weapon to light";
                return false;
            }

            _holderName = world.Player.name;
            _holster = world.Player.GetComponent<HolsterController>();
            if (_holster != null && _holster.Phase != HolsterPhase.Drawn)
            {
                _holster.Draw();
            }

            var catalog = _weapon.AttachmentCatalog;
            _lightItemId = WeaponLightFeatureSceneBuilder.LightItemId(catalog);
            if (_lightItemId == null)
            {
                problem = "the inventory items have no attachment with a light; check Resources/Tuning/inventory.json";
                return false;
            }

            var carried = _weapon.DescribeWeapons(out _originalWeaponId);
            foreach (var state in carried)
            {
                var id = state.WeaponId;
                var attached = _weapon.AttachmentsOf(id);
                if (_litWeaponId == null && Contains(WeaponAttachments.Keep(catalog, id, With(attached, _lightItemId)), _lightItemId))
                {
                    _litWeaponId = id;
                }
                else if (_darkWeaponId == null && WeaponAttachments.LightOf(catalog, id, attached) == null)
                {
                    _darkWeaponId = id;
                }
            }

            if (_litWeaponId == null || _darkWeaponId == null)
            {
                problem = $"the player needs one weapon that takes '{_lightItemId}' and another without a light (lit={_litWeaponId ?? "-"} dark={_darkWeaponId ?? "-"})";
                return false;
            }

            _savedAttachments = new List<string>(_weapon.AttachmentsOf(_litWeaponId));
            if (!_weapon.SetAttachments(_litWeaponId, With(_savedAttachments, _lightItemId)))
            {
                problem = $"attaching '{_lightItemId}' to '{_litWeaponId}' was refused";
                return false;
            }

            _attached = true;
            if (!string.Equals(_weapon.Weapon.Spec.Id, _litWeaponId, StringComparison.Ordinal) && !_weapon.Select(_litWeaponId))
            {
                problem = $"Select('{_litWeaponId}') was refused";
                return false;
            }

            Debug.Log($"{LogTag} attached '{_lightItemId}' to '{_litWeaponId}'; '{_darkWeaponId}' has no light");
            _phase = Phase.WaitLitWeapon;
            return false;
        }

        // 持ち替えとしまう動作の途中は武装と数えないので、落ち着いてから押す。
        private bool IsSettledOn(string weaponId)
        {
            var current = _weapon.Weapon;
            return current != null
                && string.Equals(current.Spec.Id, weaponId, StringComparison.Ordinal)
                && !_weapon.IsSwitching
                && (_holster == null || _holster.Phase == HolsterPhase.Drawn);
        }

        private void Restore()
        {
            if (_weapon == null)
            {
                return;
            }

            if (_light != null && _light.IsOn)
            {
                _light.Toggle();
            }

            if (_attached && _litWeaponId != null)
            {
                _weapon.SetAttachments(_litWeaponId, _savedAttachments ?? WeaponAttachments.None);
                _attached = false;
            }

            if (_originalWeaponId != null && _weapon.Weapon != null && !string.Equals(_weapon.Weapon.Spec.Id, _originalWeaponId, StringComparison.Ordinal))
            {
                _weapon.Select(_originalWeaponId);
            }
        }

        private static IReadOnlyList<string> With(IReadOnlyList<string> attachments, string itemId)
        {
            var list = new List<string>(attachments);
            if (!list.Contains(itemId))
            {
                list.Add(itemId);
            }

            return list;
        }

        private static bool Contains(IReadOnlyList<string> attachments, string itemId)
        {
            foreach (var attached in attachments)
            {
                if (string.Equals(attached, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
