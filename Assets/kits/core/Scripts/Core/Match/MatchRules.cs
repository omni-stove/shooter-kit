using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameKit.Core
{
    // ジャンルの規則 (Shooter.Core.GameRules など) に項目を並べずに分けて持つのは、体力の回復やリスポーンのようにジャンルに依らない処理が、ジャンルの規則の型に縛られないようにするため。
    public sealed class MatchRules
    {
        // Default より先に初期化しないと、Default のコンストラクターが null を受けて既定に置き換えるときに null を写す。
        public static readonly IReadOnlyList<RespawnAnchor> DefaultRespawnAnchors =
            new ReadOnlyCollection<RespawnAnchor>(new[] { RespawnAnchor.Base });

        public static readonly MatchRules Default = new MatchRules();

        public const float DefaultWaveIntervalSeconds = 10f;

        // サーバーが Bot を際限なく足さないよう、MinPlayers に上限を置く。
        public const int MaxMinPlayers = 64;

        // GameKit.Core はキットの Runtime (Shooter.Runtime の Damageable.DefaultMaxHealth など) を参照できないので同じ値を持ち、揃っていることは各キットの EditMode のテストで確かめる。
        public const int DefaultPlayerMaxHealth = 100;

        public const float DefaultIdleWarningSeconds = 15f;

        public FriendlyFire FriendlyFire { get; }

        public RespawnMode RespawnMode { get; }

        public float WaveIntervalSeconds { get; }

        public float RegenDelaySeconds { get; }

        public float RegenPerSecond { get; }

        public float SpawnProtectionSeconds { get; }

        public int MinPlayers { get; }

        public float IdleKickSeconds { get; }

        public float IdleWarningSeconds { get; }

        public int PlayerMaxHealth { get; }

        public TextChat TextChat { get; }

        public Spectating Spectating { get; }

        // 1 つでなく試す順の並びで持つのは、分隊がいない・ビーコンが無いなどで使えない候補から次の候補へ下がれるようにするため。
        public IReadOnlyList<RespawnAnchor> RespawnAnchors { get; }

        // 引数名はルールの JSON の項目名と同じにして、例外の ParamName をそのまま項目名として返せるようにする (Shooter.Core.GameRulesJson が返す)。
        public MatchRules(
            FriendlyFire friendlyFire = FriendlyFire.Off,
            RespawnMode respawnMode = RespawnMode.Individual,
            float waveIntervalSeconds = DefaultWaveIntervalSeconds,
            float regenDelaySeconds = 0f,
            float regenPerSecond = 0f,
            float spawnProtectionSeconds = 0f,
            int minPlayers = 0,
            float idleKickSeconds = 0f,
            float idleWarningSeconds = DefaultIdleWarningSeconds,
            int playerMaxHealth = DefaultPlayerMaxHealth,
            TextChat textChat = TextChat.On,
            Spectating spectating = Spectating.On,
            IReadOnlyList<RespawnAnchor> respawnAnchor = null)
        {
            if (!Enum.IsDefined(typeof(FriendlyFire), friendlyFire))
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyFire), friendlyFire, "friendlyFire is not a defined FriendlyFire");
            }

            if (!Enum.IsDefined(typeof(RespawnMode), respawnMode))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnMode), respawnMode, "respawnMode is not a defined RespawnMode");
            }

            // Wave 以外でも検査するのは、後からモードだけ差し替えた値でも間隔が必ず使える状態にしておくため。
            if (!(waveIntervalSeconds > 0f) || float.IsInfinity(waveIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waveIntervalSeconds), waveIntervalSeconds, "waveIntervalSeconds must be positive and finite");
            }

            if (!(regenDelaySeconds >= 0f) || float.IsInfinity(regenDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), regenDelaySeconds, "regenDelaySeconds must be a non-negative finite number");
            }

            if (!(regenPerSecond >= 0f) || float.IsInfinity(regenPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), regenPerSecond, "regenPerSecond must be a non-negative finite number");
            }

            if (!(spawnProtectionSeconds >= 0f) || float.IsInfinity(spawnProtectionSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(spawnProtectionSeconds), spawnProtectionSeconds, "spawnProtectionSeconds must be a non-negative finite number");
            }

            if (minPlayers < 0 || minPlayers > MaxMinPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(minPlayers), minPlayers, "minPlayers must be between 0 and " + MaxMinPlayers);
            }

            if (!(idleKickSeconds >= 0f) || float.IsInfinity(idleKickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(idleKickSeconds), idleKickSeconds, "idleKickSeconds must be a non-negative finite number");
            }

            // 放置で切らないルール (idleKickSeconds が 0) でも検査するのは、後から切る秒だけ差し替えた値でも警告の秒が必ず使える状態にしておくため。
            if (!(idleWarningSeconds > 0f) || float.IsInfinity(idleWarningSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(idleWarningSeconds), idleWarningSeconds, "idleWarningSeconds must be positive and finite");
            }

            if (playerMaxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerMaxHealth), playerMaxHealth, "playerMaxHealth must be at least 1");
            }

            if (!Enum.IsDefined(typeof(TextChat), textChat))
            {
                throw new ArgumentOutOfRangeException(nameof(textChat), textChat, "textChat is not a defined TextChat");
            }

            if (!Enum.IsDefined(typeof(Spectating), spectating))
            {
                throw new ArgumentOutOfRangeException(nameof(spectating), spectating, "spectating is not a defined Spectating");
            }

            FriendlyFire = friendlyFire;
            RespawnMode = respawnMode;
            WaveIntervalSeconds = waveIntervalSeconds;
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
            SpawnProtectionSeconds = spawnProtectionSeconds;
            MinPlayers = minPlayers;
            IdleKickSeconds = idleKickSeconds;
            IdleWarningSeconds = idleWarningSeconds;
            PlayerMaxHealth = playerMaxHealth;
            TextChat = textChat;
            Spectating = spectating;
            RespawnAnchors = respawnAnchor == null ? DefaultRespawnAnchors : CopyAnchors(respawnAnchor);
        }

        public MatchRules WithFriendlyFire(FriendlyFire value) => Copy(friendlyFire: value);

        public MatchRules WithRespawnMode(RespawnMode value) => Copy(respawnMode: value);

        public MatchRules WithWaveIntervalSeconds(float value) => Copy(waveIntervalSeconds: value);

        public MatchRules WithRegenDelaySeconds(float value) => Copy(regenDelaySeconds: value);

        public MatchRules WithRegenPerSecond(float value) => Copy(regenPerSecond: value);

        public MatchRules WithSpawnProtectionSeconds(float value) => Copy(spawnProtectionSeconds: value);

        public MatchRules WithMinPlayers(int value) => Copy(minPlayers: value);

        public MatchRules WithIdleKickSeconds(float value) => Copy(idleKickSeconds: value);

        public MatchRules WithIdleWarningSeconds(float value) => Copy(idleWarningSeconds: value);

        public MatchRules WithPlayerMaxHealth(int value) => Copy(playerMaxHealth: value);

        public MatchRules WithTextChat(TextChat value) => Copy(textChat: value);

        public MatchRules WithSpectating(Spectating value) => Copy(spectating: value);

        // コンストラクターと違って null を既定に置き換えないのは、差し替えたつもりの値が黙って既定に戻らないようにするため。
        public MatchRules WithRespawnAnchors(IReadOnlyList<RespawnAnchor> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(respawnAnchor: value);
        }

        // 呼んだ側が後から配列を書き換えてもルールが変わらないよう、検査した写しを持つ。
        // 引数名をコンストラクターと同じ respawnAnchor にして、例外の ParamName が項目名になるようにする。
        private static ReadOnlyCollection<RespawnAnchor> CopyAnchors(IReadOnlyList<RespawnAnchor> respawnAnchor)
        {
            if (respawnAnchor.Count == 0)
            {
                throw new ArgumentException("respawnAnchor must list at least one anchor", nameof(respawnAnchor));
            }

            var copy = new RespawnAnchor[respawnAnchor.Count];
            for (var i = 0; i < respawnAnchor.Count; i++)
            {
                var anchor = respawnAnchor[i];
                if (!Enum.IsDefined(typeof(RespawnAnchor), anchor))
                {
                    throw new ArgumentOutOfRangeException(nameof(respawnAnchor), anchor, "respawnAnchor has an anchor that is not a defined RespawnAnchor");
                }

                if (Array.IndexOf(copy, anchor, 0, i) >= 0)
                {
                    throw new ArgumentException($"respawnAnchor lists {anchor} more than once", nameof(respawnAnchor));
                }

                copy[i] = anchor;
            }

            return new ReadOnlyCollection<RespawnAnchor>(copy);
        }

        private MatchRules Copy(
            FriendlyFire? friendlyFire = null,
            RespawnMode? respawnMode = null,
            float? waveIntervalSeconds = null,
            float? regenDelaySeconds = null,
            float? regenPerSecond = null,
            float? spawnProtectionSeconds = null,
            int? minPlayers = null,
            float? idleKickSeconds = null,
            float? idleWarningSeconds = null,
            int? playerMaxHealth = null,
            TextChat? textChat = null,
            Spectating? spectating = null,
            IReadOnlyList<RespawnAnchor> respawnAnchor = null)
        {
            return new MatchRules(
                friendlyFire ?? FriendlyFire,
                respawnMode ?? RespawnMode,
                waveIntervalSeconds ?? WaveIntervalSeconds,
                regenDelaySeconds ?? RegenDelaySeconds,
                regenPerSecond ?? RegenPerSecond,
                spawnProtectionSeconds ?? SpawnProtectionSeconds,
                minPlayers ?? MinPlayers,
                idleKickSeconds ?? IdleKickSeconds,
                idleWarningSeconds ?? IdleWarningSeconds,
                playerMaxHealth ?? PlayerMaxHealth,
                textChat ?? TextChat,
                spectating ?? Spectating,
                respawnAnchor ?? RespawnAnchors);
        }
    }
}
