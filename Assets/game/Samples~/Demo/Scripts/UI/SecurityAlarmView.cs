using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SecurityAlarmView : HudElementView
    {
        public const string UxmlName = "security-alarm";

        public const string TextClass = "security-alarm__text";

        private SecurityAlarmStatus? _status;
        private Label _label;
        private string _text = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public SecurityAlarmStatus? Status => _status;

        public bool IsShown => SecurityAlarmText.IsShown(_status);

        public string Text => _text;

        public void Show(SecurityAlarmStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            _text = string.Empty;
            if (IsShown)
            {
                var message = SecurityAlarmText.Message(_status.Value);
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
            Show(GameSignals.StealthAlarm.Value);
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

        // 残り秒数は鳴っている間ずっと変わるので、要素を作り直さずに文だけ書き換える。
        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

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

            GameSignals.StealthAlarm.Changed += HandleAlarmChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.StealthAlarm.Changed -= HandleAlarmChanged;
            _subscribed = false;
        }

        private void HandleAlarmChanged(SecurityAlarmStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.StealthAlarm.Changed -= HandleAlarmChanged;
                return;
            }

            Show(status);
        }
    }
}
