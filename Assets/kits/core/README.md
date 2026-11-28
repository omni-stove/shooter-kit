# Game Kit (`com.omnistove.game-kit`)

ジャンルによらず、どのゲームでも使う土台だけを持つ UPM パッケージ。
ジャンルごとのゲームの進行は `com.omnistove.game-kit.<ジャンル>` (`Assets/kits/<ジャンル>`。今はシューターの `shooter`) が持ち、このパッケージに依存する。

## 入っているもの

| アセンブリ | 名前空間 | 中身 |
| --- | --- | --- |
| `GameKit.Core` | `GameKit.Core` | JSON の読み書き (`JsonParser`・`JsonNode`・`JsonWriter`・`SaveJson`)、調整値のファイルの読み取りと値の確かめ (`ServerDataJson`・`SpecJsonReader`・`SpecGuard`・`SpecLists`・`TuningJsonException`)、時間の速さの調停 (`TimeScaleArbitration`)、入力機器と操作の値 (`InputDeviceKind`・`ControlValue`)、キーの割り当て直しの値 (`RebindSlot`・`RebindResult`・`BindingConflict`)、セーブの保存先の口 (`ISaveStorage`)、アカウントとクラウドセーブの値と判定 (`AccountStatus`・`AccountLog`・`CloudSaveMerge`・`CloudSaveEnvelope`・`CloudSaveStamps` など)、コスメのカタログ (`CosmeticCatalog`・`CosmeticJson`)、リーダーボードの値 (`LeaderboardSpec`・`LeaderboardJson`・`PlayerStat`)、リプレイのファイルの読み書き (`ReplayFile`・`ReplayReader`・`ReplayWriter`・`ReplaySpec`)、試合の順番の回し方 (`Playlist`・`PlaylistCursor`・`PlaylistJson`)、文字のチャットの受け付けと言葉の絞り込み (`ChatGate`・`ChatLine`・`ChatWordFilter`)、チーム・参加者・出現地点 (`Team`・`Side`・`ParticipantTable`・`TeamBalancer`・`SpawnSelector`)、モードの進行と勝ち負け (`IGameMode`・`ModeSession`・`RoundSession`・`IWinCondition`・`ScoreLimitWin`・`MatchResult`)、スコアボード (`ScoreboardRows`・`ScoreboardEntry`)、チームの色 (`TeamPalette`・`ColorPalette`)、オンラインの値 (`MapVote`・`JoinCode`・`LobbyAttribute`・`RejoinKey`・`BlockList`・`ServerDrainState`・`PredictionBuffer`)、腕前の評価 (`SkillRating`・`SkillSave`)、ゲームパッドの配置と押し続けの切り替え (`GamepadLayout`・`HoldToggleState`)、ドロップと保管庫 (`LootRoll`・`StashSpec`)、ピン・ミニマップ・ネームプレート (`PingAim`・`PingScreen`・`MinimapProjection`・`NameplateRule`)、体力・一時停止・フォトモード (`Health`・`PauseState`・`PhotoModeSpec`)、スタミナ (`StaminaMeter`・`StaminaSpec`・`StaminaJson`。一回ごとに払う動きの種類はゲームがキーの文字列で決める)、エモート (`EmoteSpec`・`EmoteJson`・`EmoteLog`・`EmoteRefusal`)、掛け声の間隔と混み具合の判定 (`BarkPacing`・`BarkSelector`・`BarkVerdict`。掛け声の種類はゲームがキーの文字列で決める)、パーティーの集まりと状態 (`PartyLobby`・`PartyStatus`)、経験値・レベル・解放・実績とプロフィールの保存 (`ProgressionLedger`・`ProgressionSpec`・`AchievementTracker`・`PlayerProfile`)、観戦で見る味方の選び方 (`SpectateTargets`・`SpectateStatus`) |
| `GameKit.World` | `GameKit.World` | 地形の当たり (`IWorldCollision`・`BvhWorldCollision`・`WorldGeometry`)、マップのファイル (`ShooterMapFile`)、カプセルの移動 (`CapsuleMover`) |
| `GameKit.Navigation` | `GameKit.Navigation` | 経路の口 (`INavigation`・`NavPath`) と、NavMesh のファイルとその上の経路探索 (`NavMeshFile`・`MeshNavigation`) |
| `GameKit.Net` | `GameKit.Net` | 試合の通信の口と UDP の実装 (`IMatchTransport`・`UdpMatchTransport`・`UdpTransportOptions`・`TransportEvent`)、メッセージの口と読み書き (`IMessage`・`MessageReader`・`MessageWriter`) |
| `GameKit.Runtime` | `GameKit.Runtime` | Unity の Input System の上で、保存したキーの割り当ての読み書きと起動時の適用 (`InputBindingOverridesStore`・`AppliedInputBindings`)、押しっぱなしの判定 (`HeldInput`)、入力機器の見分け (`InputDeviceKinds`)、手元のファイルへのセーブ (`FileSaveStorage`)、クラウドセーブとの同期 (`CloudSaveSync`・`ICloudSaveBackend`) |

`GameKit.Runtime` のほかは Unity に依存しない (`noEngineReferences`)。専用サーバーは `dotnet/GameKit.Core`・`dotnet/GameKit.Net` で同じソースをビルドする。`GameKit.Net` の UDP は LiteNetLib を使うので、`Assets/Plugins/LiteNetLib` を入れる。

## 決まり

- ゲームのアセンブリ (`Shooter.*` など) を参照しない。`InternalsVisibleTo` でゲームのアセンブリに中身を見せない。ゲームが使うものは公開の API にする。
- 決まりは `Tests/EditMode/Core/GameKitBoundaryTests.cs`・`Tests/EditMode/Core/KitLayoutTests.cs`・`Tests/EditMode/Runtime/GameKitRuntimeBoundaryTests.cs` が確かめる。

## 導入

`Packages/manifest.json` の `dependencies` に、ゲームのキットより先に足す。`#` の後ろはタグか SHA にする。

```json
"com.omnistove.game-kit": "https://github.com/omni-stove/game-kit.git?path=/Assets/kits/core#v0.1.0"
```
