using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Editor
{
    public enum ProjectileLookKind
    {
        None,
        Arrow,
        Grenade,
        Rocket,
        Slug,
    }

    public readonly struct ProjectileTrailShape
    {
        public ProjectileTrailShape(float seconds, float startWidth, float endWidth, Color startColor, Color endColor, float tailZ)
        {
            Seconds = seconds;
            StartWidth = startWidth;
            EndWidth = endWidth;
            StartColor = startColor;
            EndColor = endColor;
            TailZ = tailZ;
        }

        public float Seconds { get; }

        public float StartWidth { get; }

        public float EndWidth { get; }

        public Color StartColor { get; }

        public Color EndColor { get; }

        public float TailZ { get; }
    }

    // 目で追えるほど遅い弾だけに、目立つ体と飛んだ道筋の尾を付ける。
    // 速い弾まで尾を引くと連射で画面が線だらけになり、曳光弾の代わりにもならないので、kit の細い棒のままにする。
    public static class ProjectileLookAssetBuilder
    {
        public const string MenuPath = "Shooter/Build Projectile Looks";
        public const string LogTag = "[ProjectileLookAssetBuilder]";

        public const string FolderName = "Projectiles";
        public const string PrefabPrefix = "Projectile_";
        public const string TrailMaterialName = "ProjectileTrail";
        public const string BodyName = "Body";
        public const string TrailName = "Trail";

        public const float MaxTrailSpeed = 150f;

        public const float MinGrenadeGravity = 0.5f;

        public const float TrailMinVertexDistance = 0.1f;

        public static string Folder => PresentationAssetBuilder.PresentationFolder + "/" + FolderName;

        public static ProjectileLookKind KindFor(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var round = spec.Round;
            if (round.Delivery != RoundDelivery.Projectile || round.Projectile == null || round.Projectile.Speed > MaxTrailSpeed)
            {
                return ProjectileLookKind.None;
            }

            if (spec.Impact.Explosion != null)
            {
                return round.Projectile.GravityMultiplier >= MinGrenadeGravity ? ProjectileLookKind.Grenade : ProjectileLookKind.Rocket;
            }

            return round.Projectile.OnWorldHit == WorldHitResponse.Stick ? ProjectileLookKind.Arrow : ProjectileLookKind.Slug;
        }

        public static string PrefabName(ProjectileLookKind kind)
        {
            if (kind == ProjectileLookKind.None)
            {
                throw new ArgumentException("a weapon without a projectile look has no prefab", nameof(kind));
            }

            return PrefabPrefix + kind;
        }

        public static ProjectileTrailShape TrailFor(ProjectileLookKind kind)
        {
            switch (kind)
            {
                case ProjectileLookKind.Arrow:
                    return new ProjectileTrailShape(0.35f, 0.025f, 0f, new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f), -0.62f);
                case ProjectileLookKind.Grenade:
                    return new ProjectileTrailShape(0.5f, 0.05f, 0.02f, new Color(0.8f, 0.8f, 0.78f, 0.55f), new Color(0.8f, 0.8f, 0.78f, 0f), -0.06f);
                case ProjectileLookKind.Rocket:
                    return new ProjectileTrailShape(0.9f, 0.1f, 0.35f, new Color(1f, 0.7f, 0.25f, 0.9f), new Color(0.6f, 0.6f, 0.6f, 0f), -0.55f);
                case ProjectileLookKind.Slug:
                    return new ProjectileTrailShape(0.25f, 0.03f, 0f, new Color(1f, 0.95f, 0.7f, 0.7f), new Color(1f, 0.95f, 0.7f, 0f), -0.05f);
                default:
                    throw new ArgumentException("a weapon without a projectile look has no trail", nameof(kind));
            }
        }

        // 先端を原点に置き、後ろ (-Z) へ伸ばす。kit の棒 (ProjectileBody.BuildRod) と同じく、弾の位置が先端になるため。
        public static IReadOnlyList<WeaponPart> PartsFor(ProjectileLookKind kind)
        {
            switch (kind)
            {
                case ProjectileLookKind.Arrow:
                    return new[]
                    {
                        new WeaponPart("Head", WeaponPartShape.Box, new Vector3(0f, 0f, -0.03f), new Vector3(0.03f, 0.006f, 0.06f), WeaponPaint.Body),
                        new WeaponPart("Shaft", WeaponPartShape.Cylinder, new Vector3(0f, 0f, -0.35f), new Vector3(0.012f, 0.012f, 0.6f), WeaponPaint.Furniture),
                        new WeaponPart("FletchingH", WeaponPartShape.Box, new Vector3(0f, 0f, -0.58f), new Vector3(0.05f, 0.002f, 0.09f), WeaponPaint.Accent),
                        new WeaponPart("FletchingV", WeaponPartShape.Box, new Vector3(0f, 0f, -0.58f), new Vector3(0.002f, 0.05f, 0.09f), WeaponPaint.Accent),
                    };
                case ProjectileLookKind.Grenade:
                    return new[]
                    {
                        new WeaponPart("Shell", WeaponPartShape.Sphere, new Vector3(0f, 0f, -0.03f), new Vector3(0.045f, 0.045f, 0.06f), WeaponPaint.Body),
                        new WeaponPart("Band", WeaponPartShape.Cylinder, new Vector3(0f, 0f, -0.03f), new Vector3(0.048f, 0.048f, 0.01f), WeaponPaint.Accent),
                    };
                case ProjectileLookKind.Rocket:
                    return new[]
                    {
                        new WeaponPart("Nose", WeaponPartShape.Sphere, new Vector3(0f, 0f, -0.06f), new Vector3(0.07f, 0.07f, 0.12f), WeaponPaint.Accent),
                        new WeaponPart("Tube", WeaponPartShape.Cylinder, new Vector3(0f, 0f, -0.3f), new Vector3(0.07f, 0.07f, 0.45f), WeaponPaint.Body),
                        new WeaponPart("FinH", WeaponPartShape.Box, new Vector3(0f, 0f, -0.5f), new Vector3(0.16f, 0.005f, 0.08f), WeaponPaint.Furniture),
                        new WeaponPart("FinV", WeaponPartShape.Box, new Vector3(0f, 0f, -0.5f), new Vector3(0.005f, 0.16f, 0.08f), WeaponPaint.Furniture),
                    };
                case ProjectileLookKind.Slug:
                    return new[]
                    {
                        new WeaponPart("Slug", WeaponPartShape.Sphere, new Vector3(0f, 0f, -0.025f), new Vector3(0.03f, 0.03f, 0.05f), WeaponPaint.Body),
                    };
                default:
                    throw new ArgumentException("a weapon without a projectile look has no parts", nameof(kind));
            }
        }

        public static Color ColorFor(ProjectileLookKind kind, WeaponPaint paint)
        {
            switch (kind)
            {
                case ProjectileLookKind.Arrow:
                    return paint == WeaponPaint.Body ? new Color(0.7f, 0.72f, 0.75f)
                        : paint == WeaponPaint.Accent ? new Color(0.95f, 0.3f, 0.2f)
                        : new Color(0.45f, 0.32f, 0.2f);
                case ProjectileLookKind.Grenade:
                    return paint == WeaponPaint.Accent ? new Color(0.85f, 0.75f, 0.2f) : new Color(0.3f, 0.36f, 0.22f);
                case ProjectileLookKind.Rocket:
                    return paint == WeaponPaint.Accent ? new Color(0.35f, 0.38f, 0.3f)
                        : paint == WeaponPaint.Furniture ? new Color(0.2f, 0.2f, 0.2f)
                        : new Color(0.42f, 0.46f, 0.36f);
                default:
                    return new Color(0.75f, 0.6f, 0.3f);
            }
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(Folder, new[]
            {
                WeaponAssetBuilder.LoadRangeWeapons(),
                WeaponAssetBuilder.LoadReferenceWeapons(),
                WeaponAssetBuilder.LoadRealisticWeapons(),
            });
        }

        public static int BuildAt(string folder, IEnumerable<WeaponCatalogAsset> catalogs)
        {
            if (catalogs == null)
            {
                throw new ArgumentNullException(nameof(catalogs));
            }

            folder = PresentationAssetBuilder.ValidateFolderPath(folder, nameof(folder));
            SceneBuildHelpers.EnsureFolder(folder);

            var trailMaterial = PresentationAssetBuilder.BuildParticleMaterial(
                $"{folder}/{TrailMaterialName}.mat", null, PresentationAssetBuilder.BlendAlpha);
            var paint = ViewmodelAssetStep.PaintCache(folder);
            var prefabs = new Dictionary<ProjectileLookKind, GameObject>();
            var wired = 0;
            var cleared = 0;
            foreach (var catalog in catalogs)
            {
                if (catalog == null)
                {
                    throw new ArgumentException("a weapon catalog in the list is missing", nameof(catalogs));
                }

                foreach (var definition in catalog.Weapons)
                {
                    if (definition == null)
                    {
                        continue;
                    }

                    var kind = KindFor(definition.ToSpec());
                    var look = definition.Projectile;
                    GameObject visual = null;
                    if (kind != ProjectileLookKind.None)
                    {
                        if (!prefabs.TryGetValue(kind, out visual))
                        {
                            visual = PresentationAssetBuilder.SavePrefab(folder, PrefabName(kind), root => BuildLook(root, kind, paint, trailMaterial));
                            prefabs.Add(kind, visual);
                        }

                        wired++;
                    }
                    else if (look.Visual == null || !IsBuiltHere(look.Visual, folder))
                    {
                        // ゲームが自分で差した見た目は残し、ここで差したもので速くなった弾の分だけ外す。
                        continue;
                    }
                    else
                    {
                        cleared++;
                    }

                    if (look.Visual == visual)
                    {
                        continue;
                    }

                    look.Visual = visual;
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} saved {1} projectile look(s) to '{2}', gave them to {3} weapon(s) and took them from {4} weapon(s)",
                LogTag,
                prefabs.Count,
                folder,
                wired,
                cleared));
            return wired;
        }

        public static void BuildLook(GameObject root, ProjectileLookKind kind, Func<WeaponPaint, Color, Material> paint, Material trailMaterial)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (paint == null)
            {
                throw new ArgumentNullException(nameof(paint));
            }

            var body = new GameObject(BodyName).transform;
            body.SetParent(root.transform, false);
            WeaponLook.BuildParts(body, PartsFor(kind), part => paint(part.Paint, ColorFor(kind, part.Paint)), castShadows: false);

            var shape = TrailFor(kind);
            var tail = new GameObject(TrailName);
            tail.transform.SetParent(root.transform, false);
            tail.transform.localPosition = new Vector3(0f, 0f, shape.TailZ);
            var trail = tail.AddComponent<TrailRenderer>();
            trail.time = shape.Seconds;
            trail.startWidth = shape.StartWidth;
            trail.endWidth = shape.EndWidth;
            trail.colorGradient = Fade(shape.StartColor, shape.EndColor);
            trail.minVertexDistance = TrailMinVertexDistance;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.autodestruct = false;
            trail.emitting = true;
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Gradient Fade(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        private static bool IsBuiltHere(GameObject visual, string folder)
        {
            var path = AssetDatabase.GetAssetPath(visual);
            return !string.IsNullOrEmpty(path) && path.StartsWith(folder + "/", StringComparison.Ordinal);
        }
    }
}
