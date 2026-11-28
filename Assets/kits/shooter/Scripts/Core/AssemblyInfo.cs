using System.Runtime.CompilerServices;

// Core の internal の読み手 (BotArchetypeJson・*SpecJson) と値の確かめ (TuningChecks) を公開の API にせずに、それだけを Core のテスト (Unity の EditMode と dotnet の両方で同じ名前) から直接確かめるため
[assembly: InternalsVisibleTo("Shooter.Core.Tests")]

// Core から別 asmdef へ切り出した機能の塊が、Core の internal の型とメンバー (TuningChecks など) を、公開の API にせずに Core にいたときのまま使うため
[assembly: InternalsVisibleTo("Shooter.Core.Grapple")]
[assembly: InternalsVisibleTo("Shooter.Core.BattleRoyale")]
[assembly: InternalsVisibleTo("Shooter.Core.Extraction")]
[assembly: InternalsVisibleTo("Shooter.Core.CoopHold")]
[assembly: InternalsVisibleTo("Shooter.Core.Presentation")]
[assembly: InternalsVisibleTo("Shooter.Core.Vehicle")]
