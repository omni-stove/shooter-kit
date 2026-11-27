# 専用サーバー

`dotnet/Shooter.Server` は Unity を使わない .NET のサーバーで、試合は Unity のクライアントと同じ `Shooter.Core` で動かす。
マップ・ルール・調整値は Unity で `server-data/` に書き出したものを読む。オンラインは専用サーバーにだけつなぐ (Host やプレイヤーのロビーは無い)。

コマンドは `mise tasks`、サーバーの引数は `ServerLaunch`、ルールの欄は `NetMatchDefinition` を見る。

## データを書き出す

シーンやアセットを変えたら、Editor を閉じて書き出し、コミットする。

```sh
make export-server-data     # マップ → server-data/maps/
make export-server-rules    # ルールと調整値 → server-data/rules/、server-data/tuning/
```

### 調整値 (`server-data/tuning/`)

| ファイル | 正 | 作り方 |
| --- | --- | --- |
| `ping`・`grenade`・`inventory`・`knockback`・`bots`・`assist`・`loot`・`melee`・`streaks`・`extraction`・`stash`・`holster`・`breach` (`.json`) | `Assets/kits/shooter/Resources/Tuning/` (入口 `GameContent` の欄があればそちら) | `make export-server-rules` で写す |
| `economy.json` | `server-data` | 手で書く。オンラインのルールの武器の一覧 (`catalog`) にある武器だけを、買える品と値段として置く。財布の額は試合の設定 (`match.economy`) から読む |
| `chat-words.json` | `server-data` | 手で書く。チャットはオンラインだけなので、伏せる語はサーバーの分を置く |
| `anticheat`・`deployables`・`battle-royale` (`.json`) | `server-data` | 手で書く。クライアントに同じ名前のファイルは無い |
| `pickup-drops.json` | `server-data` | 手で書く (無ければ Core の `PickupDropSpec` の既定)。倒れた人が落とす武器・予備弾・消えるまでの秒数。オフラインではプレイヤーの `DropOnDeath` に置く値 |
| `mutators.json` | `Assets/kits/shooter/Resources/Tuning/` | 使うときだけ写す。無ければオンラインの変則ルールは無し |
| `emotes.json` | 入口 `GameContent` の `EmotesTuning` (キットの `Resources/Tuning/` には無い) | `make export-server-rules` で写す。無ければ `{}` (エモート無し) を置き、サーバーはどの Id の要求も断る |

`server-data/tuning/` と `Resources/Tuning/` の両方にある同じ名前のファイルは、`economy.json`・`chat-words.json` を除き、中身が同じでないと `scripts/lint.sh` (CI の lint) が落ちる (`scripts/server-tuning.sh`)。片方にしか無いファイルも、`scripts/server-tuning.txt` に `server-only`・`resources-only` として書いていなければ落ちる。サーバー用に別の中身を持たせるファイル (`server-owned`) や片方にだけ置くファイルを増やすときは、その一覧とこの表に足す。

### ルール (`server-data/rules/`)

`make export-server-rules` が書き出すのは、試合の設定 (`Rules/NetMatch.asset`) の並びにあるルールアセットと、ルールのフォルダーにある `Objective` が `None` でないルールアセット。サーバーは目的をファイルの `objective` で選ぶので、`None` のアセットは書き出さない。

`server-data/rules/hand-written.txt` に名前のあるルールは書き出さず、手で保つ。オンラインだけの値 (キルカムの `deathCamera`、`match.gunGame.tierWeaponIds` など) をアセットが持たないモードがこれに当たり、変えている項目はその一覧に書いてある。ほかの項目は Demo の同じ名前のルールアセットに手で合わせる。書き出しの対象でも一覧でもない `*.json` が残っていると、`make export-server-rules` と `scripts/lint.sh` (`scripts/server-rules.sh`) が落ちる。

`server-data/playlists/*.json` はサーバーのマップとルールの組み合わせで、クライアントの `Tuning/playlists.json` とは別に手で書く。

## 動かす

```sh
make server-dotnet                                       # 手元で 0.0.0.0:7777
make eos-sdk ARGS="--fetch"                              # (任意) EOS を入れる。無ければ UDP だけのイメージになる
make server-image                                        # shooter-server:local
docker compose -f docker/server/compose.yaml up -d
```

- arm64 のイメージで EOS を使うときは、SDK の zip から置く ([dotnet/ThirdParty/EOSSDK](../dotnet/ThirdParty/EOSSDK/README.md))。
- EOS の認証情報はイメージに入れず `--env-file` で渡す (`eos.env` はコミットしない)。
- クライアントとサーバーは同じ版でビルドしたものを使う。
- 標準入力から管理のコマンド (キック・BAN) を読む。docker では `stdin_open: true` にして `docker attach` で打つ。
- 書き出すファイル (BAN の一覧・持ち物庫・通報・テレメトリ) は docker ではボリュームに置く。
- 止めるときの docker や Kubernetes の猶予は `-drain-timeout` より長くする (`compose.yaml` は 700 秒)。

## モードとマップ

- `Payload` の道筋は `NetArena01` に、`BattleRoyale`・`Extraction` のエリアと脱出地点は `NetBattleRoyale` にだけある。
- `NetBattleRoyale` の箱の表はキットの `loot.json` に無いので、ゲームが表を足すまで箱は空で開く。
- 地面の拾い物 (`PickupSpawnPoint`) と弾薬箱 (`AmmoCrate`) は `make export-server-data` がマップごとの `<マップ>.pickups.json` に書き出す。書き出す前のマップでは拾い物が無いだけで動く。サーバーには防具が無いので、防具の拾い物は拾えない。
- 武器を出してはいけない場所 (`WeaponFreeZone`) は `make export-server-rules` がマップごとの `<マップ>.weapon-free-zones.json` に、軸にそろえた箱で書き出す。サーバーは「自動でしまう」と「構えられない」を判定し、「警告してしばらくで敵対」は警告だけにする (オンラインに中立の警備は無い)。Bot には効かない。
- Bot は人が 1 人もいないと置かれない。試すときはクライアントを 1 人つなぐ。
- ルールの `actions` は、`stealth` のほかはオフラインと同じくサーバーでも効く。`stealth` を `On` にしたルールで起動すると、サーバーは `[Rules] warning: actions stealth ...` を出してそれを無視する。
- スタミナの調整値は `server-data/tuning/stamina.json` を置いたときだけ読み、無ければ Core の `StaminaSpec` の既定で動く (Unity にも JSON は無い)。
- ドローンはサーバーが飛ばすので、使うときは `deployables.json` に `Drone` の種類を足す。暗視とサーマルは手元の画面だけで決まり、サーバーは扱わない。
- `Horde` で直す・置ける防衛物は、`make export-server-data` がマップの建物と一緒に書き出した `HordeFortification` だけ。書き出す前のマップでは、直す・置く要求をいつも断る。クライアントは波の合間に案内が出ている間だけ、Interact のキーで直す・置く要求を送る。
- プレイリストでは `match.*` と武器が最初の項目のルールのまま変わらないので、後の項目で使うモードの節も最初の項目に書く。
- `Infected` の調整値はルールの `match.infected` に `infected.json` と同じ項目名で書く。感染者の体力の上限と速さ (`infectedMaxHealth`・`infectedSpeedMultiplier`) は感染したときから効き、試合の頭で生存者の値に戻る。`infectedWeaponId` の武器はルールのロードアウトか武器の一覧 (カタログ) に無いと起動 (と次のルールへの切り替え) を断る。空なら感染しても持っている武器のまま。クライアントの体力の表示の上限は接続のときに受けた全員共通の値 (`match.maxHealth`) のままなので、感染者は上限を超えた体力が出る。
- `Juggernaut`・`Vip` の調整値はルールの `match.juggernaut`・`match.vip` に `juggernaut.json`・`vip.json` と同じ項目名で書く (その目的のルールか、既定値から変えたときだけ書き出す)。起動のログには `[Juggernaut] tuning ...`・`[Vip] tuning ...` の行で読んだ値が出る。
- `Juggernaut` はどのマップでも遊べ、ジャガーノートが替わると `[Juggernaut] change player=...`、決着すると `[Juggernaut] winner ...` の行が出る。ルールの `friendlyFire` にかかわらず全員が敵同士になる。
- `Vip` は VIP の向かう脱出地点の要るモードで、書き出したマップでは `NetBattleRoyale` にだけある。脱出地点の無いマップで起動すると `it has no extraction points for the vip to escape to` で止まる。VIP が決まると `[Vip] chosen ...`、ラウンドが終わると `[Vip] round_end ...`、決着すると `[Vip] ended winner=...` の行が出る。
- クライアントは HUD の目的の欄にジャガーノート・VIP を出し、頭上に目印を付ける。目印の見た目は `NetV2Client.JuggernautMarkerVisual`・`VipMarkerVisual` で差し替え、空ならオフラインと同じ仮の菱形を作る。
- `Execution` はラウンドの数と秒を `match.round*` に、とどめの秒数と距離を `match.execution` に書く。とどめは近接のボタンで刺すので、ルールの `melee` を `On` にする (`Off` ならダウン中の敵を撃って倒せる)。各チームの最後の 1 人もダウンし、全員がダウンしてもラウンドは終わらず、とどめを刺されるか出血で倒れきった側が落とす。とどめの進み具合は刺されている人のスナップショットの行 (`DownedSync`) に載り、刺している人の HUD に「とどめ NN%」として出る。

## CI のテンプレート

`templates/github-workflows/` にあり、このリポジトリでは走らせない。ゲームのリポジトリで `.github/workflows/` に写す。

- `server-image.yml`: リポジトリの変数 `SERVER_IMAGE` に push 先を入れる。
- `map-data.yml`: Secrets に `UNITY_LICENSE` を入れる。失敗したら `make export-server-data` してコミットする。
