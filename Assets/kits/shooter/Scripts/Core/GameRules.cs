using System;
using System.Collections.Generic;
using GameKit.Core;

namespace Shooter.Core
{
    public sealed class GameRules
    {
        public static readonly IReadOnlyList<RespawnAnchor> DefaultRespawnAnchors = MatchRules.DefaultRespawnAnchors;

        public static readonly GameRules Default = new GameRules(
            FriendlyFire.Off,
            RespawnMode.Individual,
            DefaultWaveIntervalSeconds,
            0f,
            0f,
            DeathBody.Vanish,
            DeathCamera.None,
            false,
            1f,
            Destruction.Off);

        public const float DefaultWaveIntervalSeconds = MatchRules.DefaultWaveIntervalSeconds;

        public const int MaxMinPlayers = MatchRules.MaxMinPlayers;

        // 持ち武器を送る電文 (OwnerSnapshot.MaxWeapons) に収まる数にする。
        public const int MaxWeaponsLimit = byte.MaxValue;

        public const int DefaultPlayerMaxHealth = MatchRules.DefaultPlayerMaxHealth;

        public const float DefaultIdleWarningSeconds = MatchRules.DefaultIdleWarningSeconds;

        public MatchRules Match { get; }

        public FriendlyFire FriendlyFire => Match.FriendlyFire;

        public RespawnMode RespawnMode => Match.RespawnMode;

        public float WaveIntervalSeconds => Match.WaveIntervalSeconds;

        public float RegenDelaySeconds => Match.RegenDelaySeconds;

        public float RegenPerSecond => Match.RegenPerSecond;

        public DeathBody DeathBody { get; }

        public DeathCamera DeathCamera { get; }

        public bool InfiniteReserve { get; }

        public float ReserveAmmoMultiplier { get; }

        public Destruction Destruction { get; }

        public ActionRules Actions { get; }

        public float SpawnProtectionSeconds => Match.SpawnProtectionSeconds;

        public FallDamage FallDamage { get; }

        public HudDensity HudDensity { get; }

        public MatchObjective Objective { get; }

        public int MinPlayers => Match.MinPlayers;

        public float IdleKickSeconds => Match.IdleKickSeconds;

        public float IdleWarningSeconds => Match.IdleWarningSeconds;

        public int MaxWeapons { get; }

        public ReloadMode ReloadMode { get; }

        public ViewMode ViewMode { get; }

        public AdsEyeView AdsEyeView { get; }

        public int PlayerMaxHealth => Match.PlayerMaxHealth;

        public ZoneDamageMultipliers ZoneDamage { get; }

        public Streaks Streaks { get; }

        public TextChat TextChat => Match.TextChat;

        public Spectating Spectating => Match.Spectating;

        public IReadOnlyList<RespawnAnchor> RespawnAnchors => Match.RespawnAnchors;

        public GameRules(
            FriendlyFire friendlyFire,
            RespawnMode respawnMode,
            float waveIntervalSeconds,
            float regenDelaySeconds,
            float regenPerSecond,
            DeathBody deathBody,
            DeathCamera deathCamera,
            bool infiniteReserve,
            float reserveAmmoMultiplier,
            Destruction destruction,
            ActionRules actions = null,
            float spawnProtectionSeconds = 0f,
            FallDamage fallDamage = FallDamage.Off,
            HudDensity hudDensity = HudDensity.Full,
            MatchObjective objective = MatchObjective.None,
            int minPlayers = 0,
            float idleKickSeconds = 0f,
            int maxWeapons = 0,
            ReloadMode reloadMode = ReloadMode.Auto,
            ViewMode viewMode = ViewMode.FirstPerson,
            int playerMaxHealth = DefaultPlayerMaxHealth,
            ZoneDamageMultipliers zoneDamage = null,
            Streaks streaks = Streaks.On,
            TextChat textChat = TextChat.On,
            Spectating spectating = Spectating.On,
            IReadOnlyList<RespawnAnchor> respawnAnchor = null,
            float idleWarningSeconds = DefaultIdleWarningSeconds,
            AdsEyeView adsEyeView = AdsEyeView.PerWeapon)
        {
            Match = new MatchRules(
                friendlyFire,
                respawnMode,
                waveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                spawnProtectionSeconds,
                minPlayers,
                idleKickSeconds,
                idleWarningSeconds,
                playerMaxHealth,
                textChat,
                spectating,
                respawnAnchor);

            if (!Enum.IsDefined(typeof(DeathBody), deathBody))
            {
                throw new ArgumentOutOfRangeException(nameof(deathBody), deathBody, "deathBody is not a defined DeathBody");
            }

            if (!Enum.IsDefined(typeof(DeathCamera), deathCamera))
            {
                throw new ArgumentOutOfRangeException(nameof(deathCamera), deathCamera, "deathCamera is not a defined DeathCamera");
            }

            if (!(reserveAmmoMultiplier >= 0f) || float.IsInfinity(reserveAmmoMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmoMultiplier), reserveAmmoMultiplier, "reserveAmmoMultiplier must be a non-negative finite number");
            }

            if (!Enum.IsDefined(typeof(Destruction), destruction))
            {
                throw new ArgumentOutOfRangeException(nameof(destruction), destruction, "destruction is not a defined Destruction");
            }

            if (!Enum.IsDefined(typeof(FallDamage), fallDamage))
            {
                throw new ArgumentOutOfRangeException(nameof(fallDamage), fallDamage, "fallDamage is not a defined FallDamage");
            }

            if (!Enum.IsDefined(typeof(HudDensity), hudDensity))
            {
                throw new ArgumentOutOfRangeException(nameof(hudDensity), hudDensity, "hudDensity is not a defined HudDensity");
            }

            if (!Enum.IsDefined(typeof(MatchObjective), objective))
            {
                throw new ArgumentOutOfRangeException(nameof(objective), objective, "objective is not a defined MatchObjective");
            }

            if (maxWeapons < 0 || maxWeapons > MaxWeaponsLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWeapons), maxWeapons, "maxWeapons must be between 0 and " + MaxWeaponsLimit);
            }

            if (!Enum.IsDefined(typeof(ReloadMode), reloadMode))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadMode), reloadMode, "reloadMode is not a defined ReloadMode");
            }

            if (!Enum.IsDefined(typeof(ViewMode), viewMode))
            {
                throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, "viewMode is not a defined ViewMode");
            }

            if (!Enum.IsDefined(typeof(AdsEyeView), adsEyeView))
            {
                throw new ArgumentOutOfRangeException(nameof(adsEyeView), adsEyeView, "adsEyeView is not a defined AdsEyeView");
            }

            if (!Enum.IsDefined(typeof(Streaks), streaks))
            {
                throw new ArgumentOutOfRangeException(nameof(streaks), streaks, "streaks is not a defined Streaks");
            }

            DeathBody = deathBody;
            DeathCamera = deathCamera;
            InfiniteReserve = infiniteReserve;
            ReserveAmmoMultiplier = reserveAmmoMultiplier;
            Destruction = destruction;
            Actions = actions ?? ActionRules.Default;
            FallDamage = fallDamage;
            HudDensity = hudDensity;
            Objective = objective;
            MaxWeapons = maxWeapons;
            ReloadMode = reloadMode;
            ViewMode = viewMode;
            AdsEyeView = adsEyeView;
            ZoneDamage = zoneDamage ?? ZoneDamageMultipliers.Default;
            Streaks = streaks;
        }

        public GameRules WithFriendlyFire(FriendlyFire value) => Copy(friendlyFire: value);

        public GameRules WithRespawnMode(RespawnMode value) => Copy(respawnMode: value);

        public GameRules WithWaveIntervalSeconds(float value) => Copy(waveIntervalSeconds: value);

        public GameRules WithRegenDelaySeconds(float value) => Copy(regenDelaySeconds: value);

        public GameRules WithRegenPerSecond(float value) => Copy(regenPerSecond: value);

        public GameRules WithDeathBody(DeathBody value) => Copy(deathBody: value);

        public GameRules WithDeathCamera(DeathCamera value) => Copy(deathCamera: value);

        public GameRules WithInfiniteReserve(bool value) => Copy(infiniteReserve: value);

        public GameRules WithReserveAmmoMultiplier(float value) => Copy(reserveAmmoMultiplier: value);

        public GameRules WithDestruction(Destruction value) => Copy(destruction: value);

        // コンストラクターと違って null を ActionRules.Default に置き換えないのは、差し替えたつもりの値が黙って既定に戻らないようにするため。
        public GameRules WithActions(ActionRules value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(actions: value);
        }

        public GameRules WithSpawnProtectionSeconds(float value) => Copy(spawnProtectionSeconds: value);

        public GameRules WithFallDamage(FallDamage value) => Copy(fallDamage: value);

        public GameRules WithHudDensity(HudDensity value) => Copy(hudDensity: value);

        public GameRules WithObjective(MatchObjective value) => Copy(objective: value);

        public GameRules WithMinPlayers(int value) => Copy(minPlayers: value);

        public GameRules WithIdleKickSeconds(float value) => Copy(idleKickSeconds: value);

        public GameRules WithIdleWarningSeconds(float value) => Copy(idleWarningSeconds: value);

        public GameRules WithMaxWeapons(int value) => Copy(maxWeapons: value);

        public GameRules WithReloadMode(ReloadMode value) => Copy(reloadMode: value);

        public GameRules WithViewMode(ViewMode value) => Copy(viewMode: value);

        public GameRules WithAdsEyeView(AdsEyeView value) => Copy(adsEyeView: value);

        public GameRules WithPlayerMaxHealth(int value) => Copy(playerMaxHealth: value);

        // WithActions と同じく、差し替えたつもりの値が黙って既定に戻らないよう null を受け付けない。
        public GameRules WithZoneDamage(ZoneDamageMultipliers value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(zoneDamage: value);
        }

        public GameRules WithStreaks(Streaks value) => Copy(streaks: value);

        public GameRules WithTextChat(TextChat value) => Copy(textChat: value);

        public GameRules WithSpectating(Spectating value) => Copy(spectating: value);

        // WithActions と同じく、差し替えたつもりの値が黙って既定に戻らないよう null を受け付けない。
        public GameRules WithRespawnAnchors(IReadOnlyList<RespawnAnchor> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(respawnAnchor: value);
        }

        // WithActions と同じく、差し替えたつもりの値が黙って既定に戻らないよう null を受け付けない。
        public GameRules WithMatch(MatchRules value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(match: value);
        }

        private GameRules Copy(
            FriendlyFire? friendlyFire = null,
            RespawnMode? respawnMode = null,
            float? waveIntervalSeconds = null,
            float? regenDelaySeconds = null,
            float? regenPerSecond = null,
            DeathBody? deathBody = null,
            DeathCamera? deathCamera = null,
            bool? infiniteReserve = null,
            float? reserveAmmoMultiplier = null,
            Destruction? destruction = null,
            ActionRules actions = null,
            float? spawnProtectionSeconds = null,
            FallDamage? fallDamage = null,
            HudDensity? hudDensity = null,
            MatchObjective? objective = null,
            int? minPlayers = null,
            float? idleKickSeconds = null,
            int? maxWeapons = null,
            ReloadMode? reloadMode = null,
            ViewMode? viewMode = null,
            int? playerMaxHealth = null,
            ZoneDamageMultipliers zoneDamage = null,
            Streaks? streaks = null,
            TextChat? textChat = null,
            Spectating? spectating = null,
            IReadOnlyList<RespawnAnchor> respawnAnchor = null,
            float? idleWarningSeconds = null,
            AdsEyeView? adsEyeView = null,
            MatchRules match = null)
        {
            var m = match ?? Match;
            return new GameRules(
                friendlyFire ?? m.FriendlyFire,
                respawnMode ?? m.RespawnMode,
                waveIntervalSeconds ?? m.WaveIntervalSeconds,
                regenDelaySeconds ?? m.RegenDelaySeconds,
                regenPerSecond ?? m.RegenPerSecond,
                deathBody ?? DeathBody,
                deathCamera ?? DeathCamera,
                infiniteReserve ?? InfiniteReserve,
                reserveAmmoMultiplier ?? ReserveAmmoMultiplier,
                destruction ?? Destruction,
                actions ?? Actions,
                spawnProtectionSeconds ?? m.SpawnProtectionSeconds,
                fallDamage ?? FallDamage,
                hudDensity ?? HudDensity,
                objective ?? Objective,
                minPlayers ?? m.MinPlayers,
                idleKickSeconds ?? m.IdleKickSeconds,
                maxWeapons ?? MaxWeapons,
                reloadMode ?? ReloadMode,
                viewMode ?? ViewMode,
                playerMaxHealth ?? m.PlayerMaxHealth,
                zoneDamage ?? ZoneDamage,
                streaks ?? Streaks,
                textChat ?? m.TextChat,
                spectating ?? m.Spectating,
                respawnAnchor ?? m.RespawnAnchors,
                idleWarningSeconds ?? m.IdleWarningSeconds,
                adsEyeView ?? AdsEyeView);
        }
    }
}
