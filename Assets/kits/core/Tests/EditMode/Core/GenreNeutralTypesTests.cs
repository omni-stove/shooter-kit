using System;
using System.Reflection;
using NUnit.Framework;

namespace GameKit.Core.Tests
{
    public sealed class GenreNeutralTypesTests
    {
        private static readonly Type[] _movedFromShooter =
        {
            typeof(Team),
            typeof(TeamRelation),
            typeof(TeamAssigner),
            typeof(TeamBalancer),
            typeof(TeamBalanceMember),
            typeof(TeamMove),
            typeof(Participant),
            typeof(ParticipantTable),
            typeof(IParticipants),
            typeof(Side),
            typeof(SideScore),
            typeof(SpawnPoint),
            typeof(SpawnSelector),
            typeof(IGameMode),
            typeof(GameModeState),
            typeof(Match),
            typeof(MatchOutcome),
            typeof(MatchResult),
            typeof(ModeSession),
            typeof(ModeOutcome),
            typeof(ModeStatus),
            typeof(RoundSession),
            typeof(RoundPhase),
            typeof(RoundEndReason),
            typeof(IWinCondition),
            typeof(IScoreRule),
            typeof(ITeamLayout),
            typeof(IRespawnPolicy),
            typeof(ScoreLimitWin),
            typeof(RespawnAnchor),
            typeof(RespawnAnchorOrder),
            typeof(RespawnCountdown),
            typeof(TeamPalette),
            typeof(TeamPaletteJson),
            typeof(ColorPalette),
            typeof(PaletteColor),
            typeof(ScoreboardEntry),
            typeof(ScoreboardRows),
            typeof(ScoreboardSection),
            typeof(ScoreboardName),
            typeof(ScoreboardNameKind),
            typeof(ScoreboardConnection),
            typeof(MapVote),
            typeof(MapVoteCandidate),
            typeof(MapVoteCast),
            typeof(MapVoteDecision),
            typeof(MapVoteDecisionReason),
            typeof(MapVoteStatus),
            typeof(MapVoteLog),
            typeof(RejoinKey),
            typeof(RetrySchedule),
            typeof(ReportReason),
            typeof(BlockList),
            typeof(TextChat),
            typeof(VoiceSettings),
            typeof(VoiceStatus),
            typeof(ServerDrainState),
            typeof(ServerDrainStop),
            typeof(ServerLogFormat),
            typeof(JoinCode),
            typeof(JoinCodeOutcome),
            typeof(ConnectionExtras),
            typeof(LobbyAttribute),
            typeof(LobbyCommand),
            typeof(TickClock),
            typeof(PredictionBuffer<,>),
            typeof(ReconcileOutcome),
            typeof(ReconcileResult),
            typeof(DisconnectKind),
            typeof(SkillRating),
            typeof(SkillSpec),
            typeof(SkillSave),
            typeof(SkillJson),
            typeof(SkillLog),
            typeof(HoldMode),
            typeof(HoldToggleState),
            typeof(GamepadLayout),
            typeof(GamepadLayoutBinding),
            typeof(GamepadLayoutJson),
            typeof(GamepadLayoutLog),
            typeof(GamepadLayoutSpec),
            typeof(GamepadSwap),
            typeof(GamepadSwapPair),
            typeof(DirectionalKeys),
            typeof(LootSpec),
            typeof(LootJson),
            typeof(LootLog),
            typeof(LootRoll),
            typeof(StashSpec),
            typeof(StashJson),
            typeof(StashLog),
            typeof(StashSave),
            typeof(ItemCount),
            typeof(PingAim),
            typeof(PingCandidate),
            typeof(PingHit),
            typeof(PingKind),
            typeof(PingSpec),
            typeof(PingScreen),
            typeof(PingScreenEdge),
            typeof(PingScreenPlacement),
            typeof(PingRefusal),
            typeof(PingRemoval),
            typeof(PingTrackResult),
            typeof(Ping),
            typeof(MinimapContact),
            typeof(MinimapContactKind),
            typeof(MinimapJson),
            typeof(MinimapLog),
            typeof(MinimapProjection),
            typeof(MinimapSpec),
            typeof(MinimapStatus),
            typeof(NameplateContact),
            typeof(NameplateJson),
            typeof(NameplateLog),
            typeof(NameplateRule),
            typeof(NameplateSpec),
            typeof(HudFormat),
            typeof(PauseState),
            typeof(PauseTimeMode),
            typeof(PhotoModeSpec),
            typeof(ClientPlatform),
            typeof(ClientBuildOptions),
            typeof(Health),
            typeof(InteractHint),
            typeof(Stamina),
            typeof(StaminaJson),
            typeof(StaminaLog),
            typeof(StaminaMeter),
            typeof(StaminaSpec),
            typeof(StaminaStatus),
        };

        [Test]
        public void GenreNeutralTypesLiveInTheGameKitCore()
        {
            Assert.That(_movedFromShooter, Has.Length.EqualTo(131));
            foreach (var type in _movedFromShooter)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("GameKit.Core"), type.Name);
                Assert.That(type.IsPublic, Is.True, type.Name);
            }
        }

        [TestCase(typeof(PingAim), "IsFinite")]
        [TestCase(typeof(MinimapContact), "RequireFinite")]
        [TestCase(typeof(MinimapContact), "NormalizeHeading")]
        [TestCase(typeof(MinimapContact), "DescribePose")]
        public void MarkerHelperIsPublicForGenreKits(Type type, string method)
        {
            var found = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);

            Assert.That(found, Is.Not.Null);
        }
    }
}
