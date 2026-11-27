using System;
using System.IO;
using GameKit.Core;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    public sealed class ProgressionRecorder
    {
        public const string ResourcePath = KitTuning.ProgressionResourcePath;

        private readonly ProfileStore _store;
        private readonly IParticipants _participants;

        // 保存先にある内容。これと違うときだけ節目で書き、キルのたびにディスクへ書かずに済ませる。
        private PlayerProfile _saved;

        public ProgressionRecorder(ISaveStorage storage)
            : this(storage, LoadSpec())
        {
        }

        public ProgressionRecorder(ISaveStorage storage, ProgressionSpec spec)
            : this(storage, spec, new CurrentParticipants())
        {
        }

        public ProgressionRecorder(ISaveStorage storage, ProgressionSpec spec, IParticipants participants)
        {
            _store = new ProfileStore(storage);
            Ledger = new ProgressionLedger(spec);
            _participants = participants ?? throw new ArgumentNullException(nameof(participants));
            Profile = PlayerProfile.Empty;
        }

        // 実績の記録係が同じプロフィールを書き換えるので、変わるたびに知らせる。保存はこの記録係だけがする。
        public event Action<PlayerProfile> ProfileChanged;

        // 試合を数え終えてから保存するまでの間に呼ぶ。ここで書き換えた分は同じ保存に入る。
        public event Action<MatchResult> MatchRecording;

        public ProgressionLedger Ledger { get; }

        public IParticipants Participants => _participants;

        public PlayerProfile Profile { get; private set; }

        public int Level => Ledger.LevelOf(Profile);

        public bool IsInstalled { get; private set; }

        public bool IsUnlocked(string id)
        {
            return Ledger.IsUnlocked(id, Profile);
        }

        public static ProgressionSpec LoadSpec()
        {
            return KitTuning.Load(c => c.ProgressionTuning, ResourcePath, ProgressionJson.Parse, ProgressionSpec.Default);
        }

        public void Install()
        {
            if (IsInstalled)
            {
                return;
            }

            var loaded = _store.Load();
            Debug.Log(ProgressionLog.Configured(Ledger.Spec));
            Debug.Log(ProgressionLog.Loaded(loaded, Ledger.LevelOf(loaded)));
            CombatEvents.Killed += OnKilled;
            CombatEvents.MatchFinished += OnMatchFinished;
            SceneManager.sceneLoaded += OnSceneLoaded;
            IsInstalled = true;
            _saved = loaded;
            SetProfile(loaded);
        }

        // 保存先の中身が外から替わったとき (クラウドセーブが他の端末の新しいプロフィールを採ったとき) に呼ぶ。
        // 読み直さないと、次の保存で手元に持っている古いプロフィールが新しい時刻で書かれ、採った分を消してしまう。
        public void Reload()
        {
            if (!IsInstalled)
            {
                return;
            }

            var loaded = _store.Load();
            Debug.Log(ProgressionLog.Loaded(loaded, Ledger.LevelOf(loaded)));
            _saved = loaded;
            SetProfile(loaded);
        }

        public void Change(Func<PlayerProfile, PlayerProfile> change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            if (!IsInstalled)
            {
                // 読み込む前に書き換えると、Install で読んだプロフィールに上書きされて消えるので断る。
                throw new InvalidOperationException("the profile can only be changed while the progression recorder is installed");
            }

            SetProfile(change(Profile) ?? throw new InvalidOperationException("the change must return a profile"));
        }

        public void Uninstall()
        {
            if (!IsInstalled)
            {
                return;
            }

            // MatchFinished の来ない遊び方 (キャンペーン、対戦の途中抜け) で数えた分を、ゲームの終了で失わないよう外す前に書く。
            SaveIfChanged();
            CombatEvents.Killed -= OnKilled;
            CombatEvents.MatchFinished -= OnMatchFinished;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            IsInstalled = false;
        }

        // MatchFinished を出さずに次のミッションやメニューへ移る経路があるため、シーンの読み込みを節目にして書く。
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SaveIfChanged();
        }

        private void OnKilled(KillFeedEntry entry)
        {
            if (entry.IsSuicide || entry.Friendly)
            {
                return;
            }

            if (IsLocal(entry.KillerId))
            {
                var kill = Ledger.RecordKill(Profile, entry.Headshot);
                Debug.Log(ProgressionLog.Killed(entry.Headshot, kill));
                LogLevelUp(kill);
                SetProfile(kill.Profile);
                return;
            }

            if (!AssistedLocally(entry))
            {
                return;
            }

            var assist = Ledger.RecordAssist(Profile);
            Debug.Log(ProgressionLog.Assisted(assist));
            LogLevelUp(assist);
            SetProfile(assist.Profile);
        }

        // 自分の Id が並びに 2 度あってもアシストの XP を 2 度与えないよう、数えずに有無だけを見る。
        private bool AssistedLocally(KillFeedEntry entry)
        {
            foreach (var id in entry.AssisterIds)
            {
                if (IsLocal(id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLocal(int id)
        {
            return _participants.TryGet(id, out var participant) && participant.IsLocal;
        }

        private void OnMatchFinished(MatchResult result)
        {
            var award = Ledger.RecordMatch(Profile, result);
            Debug.Log(ProgressionLog.MatchRecorded(result, award));
            LogLevelUp(award);
            SetProfile(award.Profile);
            Notify(MatchRecording, result);
            Save();
        }

        private void SetProfile(PlayerProfile profile)
        {
            if (profile.Equals(Profile))
            {
                return;
            }

            Profile = profile;
            Notify(ProfileChanged, profile);
        }

        // 聞き手の例外で数えた結果の保存を止めないよう、CombatEvents と同じくログに出して続ける。
        private static void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // 保存先と同じなら書かないので、ゲーム側の節目 (ミッションの終わりなど) から何度呼んでもよい。
        public bool SaveIfChanged()
        {
            if (!IsInstalled || Profile.Equals(_saved))
            {
                return false;
            }

            return Save();
        }

        // 試合の外で書き換えたもの (着けた見た目など) は次の節目まで保存されないので、書き換えた側がすぐ呼ぶ。
        public bool Save()
        {
            if (!IsInstalled)
            {
                // 読み込む前に書くと、保存してあるプロフィールを始めのプロフィールで上書きして消すので断る。
                throw new InvalidOperationException("the profile can only be saved while the progression recorder is installed");
            }

            try
            {
                _store.Save(Profile);
                _saved = Profile;
                Debug.Log(ProgressionLog.Saved(Profile));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 手元の Profile は持ち続けるので、次の節目でまとめて書ける。
                Debug.LogError(ProgressionLog.SaveFailed(e.Message));
                return false;
            }
        }

        private static void LogLevelUp(ProgressionAward award)
        {
            if (award.LeveledUp)
            {
                Debug.Log(ProgressionLog.LeveledUp(award));
            }
        }

        // 名簿を作るときに決めて持たないのは、記録係が Play の間ずっと残り、対戦に入るたびに Id の意味が替わるため。
        private sealed class CurrentParticipants : IParticipants
        {
            private readonly OfflineParticipants _offline = new OfflineParticipants();

            public bool TryGet(int id, out Participant participant)
            {
                // 名簿が出ている間にオフラインの表も引くと、サーバーの Id が手元の GameObject の連番と重なったときに他人のキルを自分のものと数える。
                var published = GameSignals.Participants.Value;
                return published != null ? published.TryGet(id, out participant) : _offline.TryGet(id, out participant);
            }
        }
    }
}
