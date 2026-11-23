using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WeaponLightView : HudElementView
    {
        public const string UxmlName = "weapon-light";

        public const string OnClass = "weapon-light--on";

        public const string IconClass = "weapon-light__icon";

        public const string IconImageClass = "weapon-light__icon--image";

        public const string TextClass = "weapon-light__text";

        [SerializeField, Tooltip("ライトの印の画像。空なら Hud.uss の weapon-light__icon の丸 (点灯で塗り、消灯で縁だけ) を出す")]
        private Texture2D _iconImage;

        private WeaponLightStatus? _status;
        private VisualElement _icon;
        private Label _label;
        private string _text = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public WeaponLightStatus? Status => _status;

        public bool IsShown => WeaponLightText.IsShown(_status);

        public bool IsOn => IsShown && _status.Value.IsOn;

        public string Text => _text;

        public Texture2D IconImage
        {
            get => _iconImage;
            set
            {
                _iconImage = value;
                ApplyIconImage();
            }
        }

        public void Show(WeaponLightStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            var message = WeaponLightText.Message(_status);
            _text = message.IsEmpty ? string.Empty : GameText.Resolve(message);
            Apply();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _icon = new VisualElement { pickingMode = PickingMode.Ignore };
            _icon.AddToClassList(IconClass);
            element.Add(_icon);
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(TextClass);
            element.Add(_label);
            ApplyIconImage();
            Subscribe();
            Show(GameSignals.WeaponLight.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            _icon = null;
            _label = null;
            element.RemoveFromClassList(OnClass);
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

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(OnClass, IsOn);
            if (_label != null && _label.text != _text)
            {
                _label.text = _text;
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyIconImage()
        {
            if (_icon == null)
            {
                return;
            }

            _icon.style.backgroundImage = _iconImage != null ? new StyleBackground(_iconImage) : new StyleBackground(StyleKeyword.Null);
            _icon.EnableInClassList(IconImageClass, _iconImage != null);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.WeaponLight.Changed += HandleWeaponLightChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.WeaponLight.Changed -= HandleWeaponLightChanged;
            _subscribed = false;
        }

        private void HandleWeaponLightChanged(WeaponLightStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.WeaponLight.Changed -= HandleWeaponLightChanged;
                return;
            }

            Show(status);
        }
    }
}
