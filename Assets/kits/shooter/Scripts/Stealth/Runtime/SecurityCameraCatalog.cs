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

        [SerializeField, Tooltip("監視カメラの首の見た目のプレハブ。首振りで丸ごと回る。原点が首の箱の中心で +Z が正面。当たり判定は外して、撃たれるのはキットの箱で受ける")]
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
