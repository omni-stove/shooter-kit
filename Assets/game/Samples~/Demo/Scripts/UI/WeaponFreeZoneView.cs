using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WeaponFreeZoneView : HudElementView
    {
        public const string UxmlName = "weapon-free-warning";

        public const string TextClass = "weapon-free-warning__text";

        public const string HostileClass = "weapon-free-warning--hostile";

        public const string ForcedClass = "weapon-free-warning--forced";

        private WeaponFreeZoneStatus? _zone;
        private HolsterStatus? _holster;
        private Label _label;
        private string _text = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public WeaponFreeZoneStatus? Zone => _zone;

        public HolsterStatus? Holster => _holster;

        public bool IsShown => WeaponFreeZoneText.IsShown(_zone, _holster);

        public string Text => _text;

        public void Show(WeaponFreeZoneStatus? zone, HolsterStatus? holster)
        {
            _zone = zone;
            _holster = holster;
            Refresh();
        }

        public void Refresh()
        {
            _text = string.Empty;
            if (IsShown)
            {
                var message = WeaponFreeZoneText.Message(_zone.Value);
                _text = message.IsEmpty ? string.Empty : GameText.Resolve(message);
            }

            Apply();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(TextClass);
            element.Add(_label);
            Subscribe();
            Show(GameSignals.WeaponFreeZone.Value, GameSignals.Holster.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            _label = null;
            element.RemoveFromClassList(HostileClass);
            element.RemoveFromClassList(ForcedClass);
            element.style.display = DisplayStyle.None;
        }

        // HUD を組んだ後に足されたときは、HUD が結び直さないので自分で結ぶ。
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.childCount > 0)
            {
                Bind(root);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // 残り秒数は毎フレーム変わるので、要素を作り直さずに文とクラスだけ書き換える。
        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var reaction = IsShown ? _zone.Value.Reaction : WeaponFreeZoneReaction.WarnOnly;
            element.EnableInClassList(HostileClass, IsShown && reaction == WeaponFreeZoneReaction.WarnThenHostile);
            element.EnableInClassList(ForcedClass, IsShown && (reaction == WeaponFreeZoneReaction.AutoHolster || reaction == WeaponFreeZoneReaction.BlockDraw));

            if (_label != null && _label.text != _text)
            {
                _label.text = _text;
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.WeaponFreeZone.Changed += HandleZoneChanged;
            GameSignals.Holster.Changed += HandleHolsterChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.WeaponFreeZone.Changed -= HandleZoneChanged;
            GameSignals.Holster.Changed -= HandleHolsterChanged;
            _subscribed = false;
        }

        private void HandleZoneChanged(WeaponFreeZoneStatus? zone)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.WeaponFreeZone.Changed -= HandleZoneChanged;
                GameSignals.Holster.Changed -= HandleHolsterChanged;
                return;
            }

            Show(zone, _holster);
        }

        private void HandleHolsterChanged(HolsterStatus? holster)
        {
            if (this == null)
            {
                GameSignals.WeaponFreeZone.Changed -= HandleZoneChanged;
                GameSignals.Holster.Changed -= HandleHolsterChanged;
                return;
            }

            Show(_zone, holster);
        }
    }
}
