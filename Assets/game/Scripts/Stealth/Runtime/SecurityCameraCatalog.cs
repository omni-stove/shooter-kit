using UnityEngine;

namespace Shooter.Runtime
{
    // 枠は任意 (PresentationCatalog の規約)。無いか空なら、カメラは形をコードで作り、警報は音を鳴らさない。
    [CreateAssetMenu(fileName = "SecurityCameraCatalog", menuName = "Shooter/Security Camera Catalog")]
    public sealed class SecurityCameraCatalog : ScriptableObject
    {
        public const string CatalogName = "SecurityCamera";

        // PresentationCatalog.CatalogResourcePath(CatalogName) と同じ値。メソッドを呼ばず const にして、ログの文字列にも使えるようにする。
        public const string ResourcePath = PresentationCatalog.ResourceFolder + "/" + CatalogName + PresentationCatalog.CatalogSuffix;

        [SerializeField, Tooltip("監視カメラの見た目のプレハブ")]
        private GameObject _cameraPrefab;

        [SerializeField, Tooltip("警報の音")]
        private AudioClip _alarmClip;

        public GameObject CameraPrefab
        {
            get => _cameraPrefab;
            set => _cameraPrefab = value;
        }

        public AudioClip AlarmClip
        {
            get => _alarmClip;
            set => _alarmClip = value;
        }

        public static SecurityCameraCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.SecurityCamera as SecurityCameraCatalog);
            return fromContent != null ? fromContent : Resources.Load<SecurityCameraCatalog>(ResourcePath);
        }
    }
}
