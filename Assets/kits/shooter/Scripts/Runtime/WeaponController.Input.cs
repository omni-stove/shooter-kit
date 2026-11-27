using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        public const string AttackActionPath = InputActionPaths.Attack;

        public const string ReloadActionPath = InputActionPaths.Reload;

        public const string NextActionPath = "Player/Next";

        public const string PreviousActionPath = "Player/Previous";

        // 二丁持ちの左手の引き金。覗き込みと同じボタンにするのは、二丁持ちの間は覗き込まない (AkimboSpec.BlocksAds) ため。
        public const string OffhandActionPath = InputActionPaths.Aim;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _attackAction;
        [NonSerialized]
        private InputAction _reloadAction;
        [NonSerialized]
        private InputAction _nextAction;
        [NonSerialized]
        private InputAction _previousAction;
        [NonSerialized]
        private InputAction _offhandAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;
        private bool _enabledOffhandAction;

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        public InputAction NextAction => _nextAction;

        public InputAction PreviousAction => _previousAction;

        public InputAction OffhandAction => _offhandAction;

        public void SetOffhandAction(InputAction action)
        {
            ReplaceAction(ref _offhandAction, ref _enabledOffhandAction, action);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                _enabledOffhandAction |= EnableIfNeeded(_offhandAction);
            }
        }

        private void ReadTriggers()
        {
            var rightPressed = _attackAction.IsPressed();
            var rightWasPressed = _attackAction.WasPressedThisFrame();

            // 二丁持ちでない武器の間に読まないのは、同じボタンを覗き込み (AimDownSights) と近接武器の投げが使うため。
            if (_offhandAction == null || !IsAkimbo)
            {
                DropOffhand();
                PullTrigger(rightPressed, rightWasPressed);
                return;
            }

            PullTriggers(rightPressed, rightWasPressed, _offhandAction.IsPressed(), _offhandAction.WasPressedThisFrame());
        }

        private void EnableActions()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            _enabledAttackAction |= EnableIfNeeded(_attackAction);
            _enabledReloadAction |= EnableIfNeeded(_reloadAction);
            _enabledNextAction |= EnableIfNeeded(_nextAction);
            _enabledPreviousAction |= EnableIfNeeded(_previousAction);
            _enabledSwitchAmmoTypeAction |= EnableIfNeeded(_switchAmmoTypeAction);
            _enabledOffhandAction |= EnableIfNeeded(_offhandAction);
        }

        private void DisableActions()
        {
            DisableIfEnabledHere(_attackAction, ref _enabledAttackAction);
            DisableIfEnabledHere(_reloadAction, ref _enabledReloadAction);
            DisableIfEnabledHere(_nextAction, ref _enabledNextAction);
            DisableIfEnabledHere(_previousAction, ref _enabledPreviousAction);
            DisableIfEnabledHere(_switchAmmoTypeAction, ref _enabledSwitchAmmoTypeAction);
            DisableIfEnabledHere(_offhandAction, ref _enabledOffhandAction);
        }

        private InputAction FindOptionalAction(InputActionAsset actions, string path, string effect)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning($"[WeaponController] Input Actions '{actions.name}' に '{path}' がないため、{effect}は入力からはできません。", this);
            }

            return action;
        }

        private static void ReplaceAction(ref InputAction current, ref bool enabledHere, InputAction next)
        {
            if (current != next)
            {
                DisableIfEnabledHere(current, ref enabledHere);
            }

            current = next;
        }

        private static void DisableIfEnabledHere(InputAction action, ref bool enabledHere)
        {
            if (enabledHere && action != null)
            {
                action.Disable();
            }

            enabledHere = false;
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action == null || action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }
    }
}
