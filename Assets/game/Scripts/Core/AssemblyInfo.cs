using System.Runtime.CompilerServices;

// サンプルの言語の一覧 (GameLanguageList) が入れ子の JSON を読むのに、Core の小さな JSON の読み手 (SaveJson) を公開の API にせずに使わせるため
[assembly: InternalsVisibleTo("Shooter.Runtime.UI")]

// キャンペーンのセーブ (Shooter.Campaign.Core の CampaignSave) が同じ JSON の読み書き (SaveJson) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Campaign.Core")]

// 隠密の足音の JSON (Shooter.Stealth.Core の StealthNoiseJson) がサーバーデータと同じ JSON の読み手 (ServerDataJson) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Stealth.Core")]

// 進行の JSON (Shooter.Progression.Core の ProgressionJson・AchievementJson・PlayerProfile) がサーバーデータとセーブと同じ JSON の読み書き (ServerDataJson・SpecJsonReader・SaveJson) と値の確かめ (SpecLists・SpecGuard) を、公開の API にせずに使うため
[assembly: InternalsVisibleTo("Shooter.Progression.Core")]

// 読み手と書き手 (JsonParser・JsonNode・JsonWriter・SaveJson) を公開の API にせずに、それだけを Core のテスト (Unity の EditMode と dotnet の両方で同じ名前) から直接確かめるため
[assembly: InternalsVisibleTo("Shooter.Core.Tests")]

// Core から別 asmdef へ切り出した機能の塊が、Core の JSON の読み書きと値の確かめ (ServerDataJson・SpecJsonReader・SpecLists・SaveJson・JsonNode・JsonWriter・TuningJson など) を、公開の API にせずに Core にいたときのまま使うため
[assembly: InternalsVisibleTo("Shooter.Core.Replay")]
[assembly: InternalsVisibleTo("Shooter.Core.Grapple")]
[assembly: InternalsVisibleTo("Shooter.Core.BattleRoyale")]
[assembly: InternalsVisibleTo("Shooter.Core.Extraction")]
[assembly: InternalsVisibleTo("Shooter.Core.Playlist")]
[assembly: InternalsVisibleTo("Shooter.Core.Party")]
[assembly: InternalsVisibleTo("Shooter.Core.Spectate")]
[assembly: InternalsVisibleTo("Shooter.Core.CoopHold")]
[assembly: InternalsVisibleTo("Shooter.Core.Services")]
[assembly: InternalsVisibleTo("Shooter.Core.Telemetry")]
[assembly: InternalsVisibleTo("Shooter.Core.Presentation")]
[assembly: InternalsVisibleTo("Shooter.Core.Vehicle")]
[assembly: InternalsVisibleTo("Shooter.Core.Chat")]
