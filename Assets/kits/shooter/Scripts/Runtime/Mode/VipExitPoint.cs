using System;
using System.Collections.Generic;
using GameKit.Core;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 高さを見ないのは、オンラインの脱出地点 (NetMapExtractionPoint) と同じ判定にするため。
    // いるかどうかは VipRunner が毎回位置で確かめる。トリガーの出入りで数えると、倒れて消えた人の出口が届かず残ることがある (ExtractionZone と同じ)。
    [DisallowMultipleComponent]
    public sealed class VipExitPoint : MonoBehaviour
    {
        public const float DefaultRadius = 3f;
        public const float MinRadius = 0.1f;
        public const string MarkerName = "VipExitMarker";

        private const int GizmoSegments = 32;

        [SerializeField, Min(MinRadius), Tooltip("この水平の距離 (m) 以内に立てば脱出地点にいるとみなす")]
        private float _radius = DefaultRadius;

        [SerializeField, Tooltip("Play 中に範囲を示す印を足元に出すか")]
        private bool _showMarker = true;

        [SerializeField, Tooltip("範囲を示す印の見た目。原点を中心にした直径 1m の形にする (水平に範囲の直径まで拡大し、高さはそのまま)。当たり判定は外し、材質は目標の色で塗る。空なら仮の薄い円盤を作る")]
        private GameObject _markerPrefab;

        private readonly List<Material> _markerMaterials = new List<Material>();
        private GameObject _marker;
        private bool _markerFromPrefab;
        private bool _followsSettings;

        public float Radius
        {
            get => _radius;
            set
            {
                if (!IsValidRadius(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"radius must be at least {MinRadius} and finite");
                }

                _radius = value;
                if (_marker != null)
                {
                    RefreshMarker();
                }
            }
        }

        public bool ShowsMarker
        {
            get => _showMarker;
            set => _showMarker = value;
        }

        public GameObject MarkerPrefab
        {
            get => _markerPrefab;
            set => _markerPrefab = value;
        }

        public GameObject Marker => _marker;

        public bool IsMarkerFromPrefab => _marker != null && _markerFromPrefab;

        public Vector3 Position => transform.position;

        public static bool IsValidRadius(float radius) => radius >= MinRadius && !float.IsInfinity(radius);

        public static Color MarkerColorOf(TeamPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            return TeamColors.ToColor(palette.Objective);
        }

        public static VipExitPoint[] FindAll()
        {
            return FindObjectsByType<VipExitPoint>(FindObjectsInactive.Exclude);
        }

        public bool Contains(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            var radius = IsValidRadius(_radius) ? _radius : DefaultRadius;
            return offset.sqrMagnitude <= radius * radius;
        }

        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        // Start でも呼ぶが、Edit Mode で置いたときやテストで印を確かめるには置いた側が呼ぶので公開する。
        public void RefreshMarker()
        {
            if (!_showMarker)
            {
                return;
            }

            if (_marker == null)
            {
                _markerFromPrefab = _markerPrefab != null;
                _marker = ControlPointZone.CreateMarkerObject(_markerPrefab, transform, MarkerName, _markerMaterials);
                if (!_followsSettings)
                {
                    AppliedControlSettings.Changed += HandleSettingsChanged;
                    _followsSettings = true;
                }

                ApplyMarkerColor();
            }

            ControlPointZone.FitMarker(_marker.transform, transform.lossyScale, IsValidRadius(_radius) ? _radius : DefaultRadius, _markerFromPrefab);
        }

        private void ApplyMarkerColor()
        {
            var color = MarkerColorOf(TeamColors.Current);
            foreach (var material in _markerMaterials)
            {
                if (material != null)
                {
                    material.color = color;
                }
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            ApplyMarkerColor();
        }

        private void Start()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            RefreshMarker();
        }

        private void OnDestroy()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            ControlPointZone.DestroyMaterials(_markerMaterials);
        }

        private void OnDrawGizmos()
        {
            var radius = IsValidRadius(_radius) ? _radius : DefaultRadius;
            Gizmos.color = MarkerColorOf(TeamPalette.Standard);
            var center = transform.position;
            var previous = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= GizmoSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / GizmoSegments;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
