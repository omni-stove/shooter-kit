using System;
using System.Collections.Generic;
using System.IO;
using GameKit.Core;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Editor
{
    // 粒の材質とテクスチャも自分のフォルダに作り、ほかの手順の生成物には頼らない。
    public sealed class WeaponLightCatalogAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Shooter/Build Weapon Light Catalog";
        public const string LogTag = "[WeaponLightCatalogAssetStep]";

        public const string PrefabPrefix = "WeaponLight_";
        public const string BeamName = "Beam";
        public const string DustName = "Dust";

        public const string DotTextureName = "WeaponLightDot";
        public const string BeamMaterialName = "WeaponLightBeam";
        public const string DustMaterialName = "WeaponLightDust";

        // 光の筋は光の届く距離の全部には伸ばさない。遠くまで伸ばすと、狭い部屋で壁の向こうまで筋が抜けて見えるため。
        public const float MaxBeamLength = 6f;

        public const float BeamLifetime = 0.8f;
        public const float BeamRate = 14f;
        public const float BeamAlpha = 0.05f;
        public const float BeamStartSize = 0.04f;

        public const float DustLifetimeMin = 1.2f;
        public const float DustLifetimeMax = 2.4f;
        public const float DustRate = 30f;
        public const float DustSizeMin = 0.008f;
        public const float DustSizeMax = 0.018f;
        public const float DustAlpha = 0.35f;
        public const float DustDriftSpeed = 0.06f;

        public const float InnerSpotAngleRatio = 0.6f;

        public static readonly Color LightColor = new Color(1f, 0.96f, 0.88f);

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + WeaponLightCatalog.ResourcePath + ".asset";

        public static string AssetFolder => ShooterKitRoot.OutputPath("Presentation/WeaponLight");

        public string Name => "WeaponLightCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AssetPath, AssetFolder);
        }

        // -executeMethod Shooter.Editor.WeaponLightCatalogAssetStep.BuildFromMenu でも呼べるよう、引数の無い public static にしておく。
        [MenuItem(MenuPath)]
        public static void BuildFromMenu()
        {
            BuildAt(AssetPath, AssetFolder);
        }

        // 同じ visualId を持つライトが複数あれば先の品の値で作る。見た目は 1 つの visualId に 1 つしか置けないため。
        public static IReadOnlyList<WeaponLightVisual> Visuals(ItemCatalog items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var visuals = new List<WeaponLightVisual>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items.Items)
            {
                var light = item != null && item.IsAttachment() ? item.Attachment().Light : null;
                if (light == null || !light.HasVisual || !seen.Add(light.VisualId))
                {
                    continue;
                }

                visuals.Add(new WeaponLightVisual(light.VisualId, light));
            }

            return visuals;
        }

        public static string PrefabName(string visualId)
        {
            if (string.IsNullOrWhiteSpace(visualId))
            {
                throw new ArgumentException("visual id must not be empty", nameof(visualId));
            }

            var chars = visualId.Trim().ToCharArray();
            var invalid = Path.GetInvalidFileNameChars();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\' || chars[i] == '.')
                {
                    chars[i] = '_';
                }
            }

            return PrefabPrefix + new string(chars);
        }

        public static float BeamLength(WeaponLightSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Mathf.Min(spec.Range, MaxBeamLength);
        }

        // 円錐の端の幅。光の筋の粒をこの大きさまで育て、Spot の円錐からはみ出さないようにする。
        public static float BeamEndWidth(WeaponLightSpec spec)
        {
            return 2f * BeamLength(spec) * Mathf.Tan(spec.SpotAngle * 0.5f * Mathf.Deg2Rad);
        }

        public static WeaponLightCatalog BuildAt(string path, string assetFolder)
        {
            return BuildAt(path, assetFolder, KitTuning.Items);
        }

        public static WeaponLightCatalog BuildAt(string path, string assetFolder, ItemCatalog items)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            assetFolder = PresentationAssetBuilder.ValidateFolderPath(assetFolder, nameof(assetFolder));
            var visuals = Visuals(items);

            SceneBuildHelpers.EnsureFolder(assetFolder);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var dot = PresentationAssetBuilder.BuildSoftDot($"{assetFolder}/{DotTextureName}.asset");
            var beam = PresentationAssetBuilder.BuildParticleMaterial($"{assetFolder}/{BeamMaterialName}.mat", dot, PresentationAssetBuilder.BlendAdditive);
            var dust = PresentationAssetBuilder.BuildParticleMaterial($"{assetFolder}/{DustMaterialName}.mat", dot, PresentationAssetBuilder.BlendAdditive);

            var entries = new List<WeaponLightEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var visual in visuals)
            {
                var prefabName = PrefabName(visual.VisualId);
                if (!names.Add(prefabName))
                {
                    throw new InvalidOperationException(
                        $"weapon light visual ids give the same prefab name '{prefabName}'; rename one of the attachment.light.visualId values");
                }

                var prefab = PresentationAssetBuilder.SavePrefab(assetFolder, prefabName, root => BuildVisual(root, visual.Spec, beam, dust));
                entries.Add(new WeaponLightEntry(visual.VisualId, prefab));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponLightCatalog>(path, c => c.SetLights(entries));
            Debug.Log($"{LogTag} saved {entries.Count} light visual(s) to '{assetFolder}' and '{path}' lights={catalog.Lights.Count}");
            return catalog;
        }

        internal static void BuildVisual(GameObject root, WeaponLightSpec spec, Material beamMaterial, Material dustMaterial)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // 影を落とさないのは、レンズのすぐ後ろにある自分の腕や銃の影が光の円の中に大きく出るため。
            var light = root.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = LightColor;
            light.range = spec.Range;
            light.spotAngle = spec.SpotAngle;
            light.innerSpotAngle = spec.SpotAngle * InnerSpotAngleRatio;
            light.intensity = spec.Intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;

            var length = BeamLength(spec);
            var endWidth = Mathf.Max(BeamEndWidth(spec), BeamStartSize);

            var beam = new GameObject(BeamName);
            beam.transform.SetParent(root.transform, false);
            PresentationAssetBuilder.AddParticles(beam, new PresentationAssetBuilder.ParticleSettings
            {
                Duration = 1f,
                Lifetime = new Vector2(BeamLifetime, BeamLifetime),
                Speed = new Vector2(length / BeamLifetime, length / BeamLifetime),
                Size = new Vector2(endWidth, endWidth),
                Color = new Color(LightColor.r, LightColor.g, LightColor.b, BeamAlpha),
                Burst = 0,
                MaxParticles = Mathf.CeilToInt(BeamRate * BeamLifetime) + 2,
                RandomRotation = true,
                SizeStart = BeamStartSize / endWidth,
                SizeEnd = 1f,
                Fade = PresentationAssetBuilder.Fade(Color.white, Color.white, 1f, 0f),
                Material = beamMaterial,
            });
            Loop(beam, BeamRate, 0f, 0f);

            var dust = new GameObject(DustName);
            dust.transform.SetParent(root.transform, false);
            PresentationAssetBuilder.AddParticles(dust, new PresentationAssetBuilder.ParticleSettings
            {
                Duration = 1f,
                Lifetime = new Vector2(DustLifetimeMin, DustLifetimeMax),
                Speed = new Vector2(0f, DustDriftSpeed),
                Size = new Vector2(DustSizeMin, DustSizeMax),
                Color = new Color(LightColor.r, LightColor.g, LightColor.b, DustAlpha),
                Burst = 0,
                MaxParticles = Mathf.CeilToInt(DustRate * DustLifetimeMax) + 2,
                WorldSpace = true,
                Shape = ParticleSystemShapeType.ConeVolume,
                ShapeAngle = spec.SpotAngle * 0.5f,
                ShapeRadius = 0.01f,
                Fade = DustFade(),
                Material = dustMaterial,
            });
            Loop(dust, DustRate, length, DustDriftSpeed);
        }

        // AddParticles は一度きりの粒の系を組むので、点けている間ずっと出す系に組み替える。
        private static void Loop(GameObject target, float rate, float coneLength, float noise)
        {
            var system = target.GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.prewarm = true;

            var emission = system.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            if (coneLength > 0f)
            {
                var shape = system.shape;
                shape.length = coneLength;
            }

            // ほこりは空気の流れでゆっくり漂って見えるよう、弱い揺らぎを掛ける。
            var noiseModule = system.noise;
            noiseModule.enabled = noise > 0f;
            if (noise > 0f)
            {
                noiseModule.strength = noise;
                noiseModule.frequency = 0.5f;
                noiseModule.scrollSpeed = 0.2f;
                noiseModule.quality = ParticleSystemNoiseQuality.Low;
            }

            var renderer = target.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // 出てすぐと消える前を透かし、粒が急に現れたり消えたりして見えないようにする。
        private static Gradient DustFade()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }

    public readonly struct WeaponLightVisual
    {
        public WeaponLightVisual(string visualId, WeaponLightSpec spec)
        {
            VisualId = visualId;
            Spec = spec;
        }

        public string VisualId { get; }

        public WeaponLightSpec Spec { get; }
    }
}
