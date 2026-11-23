using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 手元の保存先を包む ISaveStorage にするのは、ProfileStore と CampaignRunner が書く経路を変えずに、書くたびに同期へ回すため。
    // 読み書きはいつも手元で済ませ、クラウドとはその後で合わせるので、オフラインやサインインしていないときも手元だけで動く。
    public sealed class CloudSaveSync : ISaveStorage
    {
        public const string StampsSlot = "cloud-save-stamps";

        // 同期するスロットの名前 (CloudSaveBootstrap.RegisterSlots で登録されるもの) と重ならない名前にする。
        public const string ReplacedPrefix = "cloud-replaced-";

        // 今のアカウントでないアカウントの手元のセーブと時刻の記録を置くスロットの頭。ReplacedPrefix と同じく、同期するスロットの名前と重ならない名前にする。
        public const string AccountPrefix = "cloud-account-";

        public const string FileExtension = ".json";

        public const double DefaultRetrySeconds = 15d;

        public const double DefaultMaxRetrySeconds = 600d;

        public const double DefaultTimeoutSeconds = 60d;

        private readonly ISaveStorage _local;
        private readonly ICloudSaveBackend _backend;
        private readonly Func<string, bool> _syncs;
        private readonly string[] _startupSlots;
        private readonly Func<long> _clock;
        private CloudSaveStamps _stamps;
        private readonly List<string> _clearedBySwitch = new List<string>();
        private readonly List<string> _queue = new List<string>();
        private readonly HashSet<string> _tooLong = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadable = new HashSet<string>(StringComparer.Ordinal);
        private Operation _inFlight;
        private string _account;
        private bool _listPending;
        private int _failures;
        private double _now;
        private double _retryAt;
        private double _accountRetryAt;

        public CloudSaveSync(
            ISaveStorage local, ICloudSaveBackend backend, Func<string, bool> syncs, IEnumerable<string> startupSlots, Func<long> clock)
        {
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _backend = backend;
            _syncs = syncs ?? throw new ArgumentNullException(nameof(syncs));
            _startupSlots = startupSlots != null ? new List<string>(startupSlots).ToArray() : throw new ArgumentNullException(nameof(startupSlots));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _stamps = LoadStamps(local);
        }

        // 手元のスロットをクラウドの方で書き換えたときに呼ぶ。読み込み済みの中身を持つ側 (プロフィールの記録係) が読み直す。
        public event Action<string> SlotPulled;

        public ISaveStorage Local => _local;

        // 中身を読めるかを確かめる。読めない中身 (新しい版のゲームが書いたものなど) を手元に書くと、読み直しで空のセーブに差し替わり、それがクラウドへ戻ってしまう。
        public Func<string, string, bool> CanRead { get; set; }

        public ICloudSaveBackend Backend => _backend;

        public bool IsSignedIn => _account != null;

        public bool IsBusy => _inFlight != null;

        public int PendingCount => _queue.Count + (_listPending ? 1 : 0);

        public double RetrySeconds { get; set; } = DefaultRetrySeconds;

        public double MaxRetrySeconds { get; set; } = DefaultMaxRetrySeconds;

        public double TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        public static string FileNameOf(string slot)
        {
            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException("slot must not be empty", nameof(slot));
            }

            return slot + FileExtension;
        }

        public static string SlotOf(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)
                || fileName.Length <= FileExtension.Length
                || !fileName.EndsWith(FileExtension, StringComparison.Ordinal))
            {
                return null;
            }

            return fileName.Substring(0, fileName.Length - FileExtension.Length);
        }

        // アカウントの Id そのものを手元のファイルやその名前に残さないよう、記録の持ち主と取り置きのスロット名にはこの値を使う。
        public static string AccountKey(string account)
        {
            if (string.IsNullOrEmpty(account))
            {
                throw new ArgumentException("account must not be empty", nameof(account));
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(account));
                var builder = new StringBuilder(32);
                for (var i = 0; i < 16; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public static string AccountStampsSlot(string accountKey)
        {
            if (string.IsNullOrEmpty(accountKey))
            {
                throw new ArgumentException("accountKey must not be empty", nameof(accountKey));
            }

            return AccountPrefix + accountKey;
        }

        public static string AccountSaveSlot(string accountKey, string slot)
        {
            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException("slot must not be empty", nameof(slot));
            }

            return AccountStampsSlot(accountKey) + "-" + slot;
        }

        public bool TryGetStamp(string slot, out CloudSaveStamp stamp)
        {
            return _stamps.TryGet(slot, out stamp);
        }

        public bool TryRead(string slot, out string text)
        {
            return _local.TryRead(slot, out text);
        }

        public void Write(string slot, string text)
        {
            _local.Write(slot, text);
            if (!_syncs(slot))
            {
                return;
            }

            RecordStamp(slot, new CloudSaveStamp(NextSavedAt(slot), false));
            Enqueue(slot);
        }

        public void Delete(string slot)
        {
            _local.Delete(slot);
            if (!_syncs(slot))
            {
                return;
            }

            // 消したことも時刻付きで残してクラウドへ送る。送らないと、他の端末のセーブが次の同期で戻ってくる。
            if (_stamps.TryGet(slot, out var recorded) && recorded.Deleted)
            {
                return;
            }

            RecordStamp(slot, new CloudSaveStamp(NextSavedAt(slot), true));
            Enqueue(slot);
        }

        public void Tick(double now)
        {
            _now = now;
            if (_backend == null)
            {
                return;
            }

            _backend.Tick();
            var account = _backend.SignedInAccount;
            if (!string.Equals(account, _account, StringComparison.Ordinal) && (account == null || _now >= _accountRetryAt))
            {
                ChangeAccount(account);
            }

            if (_account == null)
            {
                return;
            }

            if (_inFlight != null)
            {
                if (_now - _inFlight.StartedAt >= TimeoutSeconds)
                {
                    Fail(_inFlight, "timed out after " + TimeoutSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s");
                }

                return;
            }

            if (_now < _retryAt)
            {
                return;
            }

            if (_listPending)
            {
                StartList();
            }
            else if (_queue.Count > 0)
            {
                var slot = _queue[0];
                _queue.RemoveAt(0);
                StartSlot(slot);
            }
        }

        private void ChangeAccount(string account)
        {
            // 前のアカウントで始めた読み書きの結果は、今のアカウントの手元に当てない。
            _inFlight = null;
            _failures = 0;
            _retryAt = 0d;
            _account = null;
            _listPending = false;
            _unreadable.Clear();
            if (account == null)
            {
                Debug.Log(CloudSaveLog.SignedOut());
                return;
            }

            var key = AccountKey(account);
            var changed = new List<string>();
            var previousStamps = _stamps;
            var previousOwner = _stamps.Account;
            if (_stamps.Account == null)
            {
                // 持ち主の決まっていない手元 (持ち主を書く前の版の記録・サインインせずに遊んだ分) は、最初にサインインしたアカウントのものにする。
                // 取り置くと、これまで同期していた利用者の進行が手元から消えて見える。
                try
                {
                    _stamps.Account = key;
                    SaveStamps(true);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // 持ち主が書けていないのに進むと、次の起動で別のアカウントのものとして扱われうるので、待ってやり直す。
                    _stamps.Account = previousOwner;
                    _accountRetryAt = _now + RetrySeconds;
                    Debug.LogError(CloudSaveLog.AccountChangeFailed(e.Message, RetrySeconds));
                    return;
                }
            }
            else if (!string.Equals(_stamps.Account, key, StringComparison.Ordinal))
            {
                try
                {
                    var setAside = SetAsideLocal();
                    changed.AddRange(setAside);
                    var restored = RestoreLocal(key);
                    foreach (var slot in restored)
                    {
                        if (!changed.Contains(slot))
                        {
                            changed.Add(slot);
                        }
                    }

                    foreach (var slot in _clearedBySwitch)
                    {
                        if (!changed.Contains(slot))
                        {
                            changed.Add(slot);
                        }
                    }

                    _clearedBySwitch.Clear();
                    Debug.Log(CloudSaveLog.AccountChanged(setAside.Count, restored.Count));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // 取り置けないまま進めると、前のアカウントの手元が今のアカウントのクラウドへ上がるので、サインインしていない扱いで待つ。
                    // 読み込み済みの側へも知らせない。空で読み直させると、その空の進行が前のアカウントの新しいセーブとして残る。
                    _stamps = previousStamps;
                    _accountRetryAt = _now + RetrySeconds;
                    Debug.LogError(CloudSaveLog.AccountChangeFailed(e.Message, RetrySeconds));
                    return;
                }
            }
            else
            {
                // 別のアカウントへの入れ替えが手元を消した後で止まると、記録は今のアカウントのまま手元だけが取り置きへ移っている。
                // 戻さずに同期すると、まだ送っていない進行が「手元に無い」と見られて、クラウドの古い中身に置き換わる。
                foreach (var slot in RestoreMissing(key))
                {
                    changed.Add(slot);
                    _clearedBySwitch.Remove(slot);
                }
            }

            _account = account;
            _listPending = true;
            foreach (var slot in _startupSlots)
            {
                Enqueue(slot);
            }

            foreach (var slot in _stamps.Slots)
            {
                Enqueue(slot);
            }

            Debug.Log(CloudSaveLog.SignedIn(_queue.Count));

            // 読み込み済みの中身は前のアカウントのものなので、次の保存でそれが今のアカウントの時刻で書かれないよう読み直させる。
            foreach (var slot in changed)
            {
                NotifyPulled(slot);
            }
        }

        // 手元から消すのは、取り置きとその時刻の記録を書き切ってから。途中で落ちても、残るのは前のアカウントの記録と欠けた手元で、
        // 欠けたスロットは CloudSaveMerge.Local が「手元に無い」と見るので、Upload も Removed も出ない。
        private List<string> SetAsideLocal()
        {
            var leaving = _stamps;
            var slots = new List<string>(leaving.Slots);
            foreach (var slot in _startupSlots)
            {
                if (_syncs(slot) && !slots.Contains(slot))
                {
                    slots.Add(slot);
                }
            }

            var earlier = LoadAccountStamps(leaving.Account);
            var kept = new CloudSaveStamps { Account = leaving.Account };
            var present = new List<string>();
            foreach (var slot in slots)
            {
                var recorded = leaving.TryGet(slot, out var stamp);
                if (_local.TryRead(slot, out var text) && text != null)
                {
                    _local.Write(AccountSaveSlot(leaving.Account, slot), text);
                    kept.Set(slot, recorded && !stamp.Deleted ? stamp : new CloudSaveStamp(CloudSaveMerge.UnstampedSavedAt, false));
                    present.Add(slot);
                }
                else if (recorded && stamp.Deleted)
                {
                    kept.Set(slot, stamp);
                }
                else if (earlier.TryGet(slot, out var before) && !before.Deleted)
                {
                    // 前の取り置きが手元を消す途中で止まったときの分。手元にはもう無いので、前に書いた取り置きを生かす。
                    kept.Set(slot, before);
                }
            }

            _local.Write(AccountStampsSlot(leaving.Account), kept.ToJson());
            foreach (var slot in present)
            {
                _local.Delete(slot);
                // 入れ替えが失敗してやり直しになっても、ここで消した分は読み込み済みの側へ知らせる必要がある。
                if (!_clearedBySwitch.Contains(slot))
                {
                    _clearedBySwitch.Add(slot);
                }
            }

            return present;
        }

        // 時刻の記録を先に今のアカウントのものへ替えてから手元を戻す。逆にすると、途中で落ちたときに今のアカウントの中身が前のアカウントの時刻で残る。
        private List<string> RestoreLocal(string key)
        {
            var arriving = LoadAccountStamps(key);
            arriving.Account = key;
            _stamps = arriving;
            SaveStamps(true);

            var restored = new List<string>();
            foreach (var slot in arriving.Slots)
            {
                if (!arriving.TryGet(slot, out var stamp) || stamp.Deleted)
                {
                    continue;
                }

                try
                {
                    if (_local.TryRead(AccountSaveSlot(key, slot), out var text) && text != null)
                    {
                        _local.Write(slot, text);
                        restored.Add(slot);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // 戻せなかったスロットは手元に無いものとして扱われ、クラウドにあればそちらを採る。取り置きは残る。
                    Debug.LogError(CloudSaveLog.AccountSlotNotRestored(slot, e.Message));
                }
            }

            return restored;
        }

        // 取り置きの時刻が記録と同じものだけを戻す。違うものは、取り置いた後に手元が書き換わった古い控えなので戻さない。
        private List<string> RestoreMissing(string key)
        {
            var restored = new List<string>();
            var setAside = LoadAccountStamps(key);
            if (setAside.Count == 0)
            {
                return restored;
            }

            foreach (var slot in setAside.Slots)
            {
                if (!setAside.TryGet(slot, out var kept) || kept.Deleted)
                {
                    continue;
                }

                // 記録の無いスロットは、SetAsideLocal が UnstampedSavedAt で取り置いたもの。
                var same = _stamps.TryGet(slot, out var stamp)
                    ? !stamp.Deleted && kept.SavedAt == stamp.SavedAt
                    : kept.SavedAt == CloudSaveMerge.UnstampedSavedAt;
                if (!same)
                {
                    continue;
                }

                try
                {
                    if (_local.TryRead(slot, out var current) && current != null)
                    {
                        continue;
                    }

                    if (_local.TryRead(AccountSaveSlot(key, slot), out var text) && text != null)
                    {
                        _local.Write(slot, text);
                        restored.Add(slot);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // 戻せなかったスロットは手元に無いものとして扱われ、クラウドにあればそちらを採る。取り置きは残るので、次のサインインでまた戻す。
                    Debug.LogError(CloudSaveLog.AccountSlotNotRestored(slot, e.Message));
                }
            }

            return restored;
        }

        private CloudSaveStamps LoadAccountStamps(string key)
        {
            if (_local.TryRead(AccountStampsSlot(key), out var text) && text != null)
            {
                try
                {
                    var stamps = CloudSaveStamps.FromJson(text);
                    if (string.Equals(stamps.Account, key, StringComparison.Ordinal))
                    {
                        return stamps;
                    }

                    Debug.LogWarning(CloudSaveLog.StampsRejected("the set-aside save times belong to another account"));
                }
                catch (FormatException e)
                {
                    Debug.LogWarning(CloudSaveLog.StampsRejected(e.Message));
                }
            }

            return new CloudSaveStamps();
        }

        private void StartList()
        {
            var operation = Begin(null);
            try
            {
                _backend.List((fileNames, error) => OnListed(operation, fileNames, error));
            }
            catch (Exception e)
            {
                // 保存先の SDK との境界なので、どの例外も失敗として後でやり直す。
                Fail(operation, e.GetType().Name + ": " + e.Message);
            }
        }

        private void OnListed(Operation operation, IReadOnlyList<string> fileNames, string error)
        {
            if (!IsCurrent(operation))
            {
                return;
            }

            if (error != null || fileNames == null)
            {
                Fail(operation, error ?? "the backend returned no file list");
                return;
            }

            _listPending = false;
            foreach (var fileName in fileNames)
            {
                var slot = SlotOf(fileName);
                if (slot != null && _syncs(slot))
                {
                    Enqueue(slot);
                }
            }

            Complete(operation);
        }

        private void StartSlot(string slot)
        {
            var operation = Begin(slot);
            try
            {
                _backend.Read(FileNameOf(slot), (text, error) => OnRead(operation, text, error));
            }
            catch (Exception e)
            {
                Fail(operation, e.GetType().Name + ": " + e.Message);
            }
        }

        private void OnRead(Operation operation, string text, string error)
        {
            if (!IsCurrent(operation))
            {
                return;
            }

            var slot = operation.Slot;
            if (error != null)
            {
                Fail(operation, error);
                return;
            }

            CloudSaveEnvelope cloud = null;
            if (text != null)
            {
                try
                {
                    cloud = CloudSaveEnvelope.FromJson(text);
                    if (!string.Equals(cloud.Slot, slot, StringComparison.Ordinal))
                    {
                        throw new FormatException("the file holds slot '" + cloud.Slot + "'");
                    }
                }
                catch (FormatException e)
                {
                    // 新しい版のゲームが書いたものかもしれないので、上書きも取り込みもせずに手元だけで続ける。
                    // 読めなかったスロットは覚えておき、後の保存でも古い版の中身でクラウドを上書きしない。
                    _unreadable.Add(slot);
                    Debug.LogWarning(CloudSaveLog.CloudRejected(slot, e.Message));
                    Complete(operation);
                    return;
                }
            }

            // 読んでいる間に手元で書いた分を消さないよう、決めるのは読み終えた今の手元で行う。
            var exists = _local.TryRead(slot, out var localText) && localText != null;
            var local = CloudSaveMerge.Local(_stamps.TryGet(slot, out var recorded) ? recorded : (CloudSaveStamp?)null, exists);
            var action = CloudSaveMerge.Decide(local, cloud);
            if (action != CloudSaveAction.None && cloud != null && !cloud.Deleted && CanRead != null && !CanRead(slot, cloud.Text))
            {
                _unreadable.Add(slot);
                Debug.LogWarning(CloudSaveLog.CloudRejected(slot, "the saved content cannot be read by this version"));
                Complete(operation);
                return;
            }

            _unreadable.Remove(slot);

            switch (action)
            {
                case CloudSaveAction.Download:
                    Download(operation, cloud, exists ? localText : null);
                    break;
                case CloudSaveAction.Upload:
                    Upload(operation, local.Value, localText);
                    break;
                default:
                    Debug.Log(CloudSaveLog.UpToDate(slot));
                    Complete(operation);
                    break;
            }
        }

        private void Download(Operation operation, CloudSaveEnvelope cloud, string localText)
        {
            var slot = operation.Slot;
            try
            {
                if (localText != null && (cloud.Deleted || !string.Equals(localText, cloud.Text, StringComparison.Ordinal)))
                {
                    // 時計のずれなどで古い方を採ってしまっても戻せるよう、置き換える手元の分を残す。
                    var backup = ReplacedPrefix + slot;
                    _local.Write(backup, localText);
                    Debug.Log(CloudSaveLog.KeptReplaced(slot, backup));
                }

                if (cloud.Deleted)
                {
                    _local.Delete(slot);
                }
                else
                {
                    _local.Write(slot, cloud.Text);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Fail(operation, e.Message);
                return;
            }

            RecordStamp(slot, cloud.Stamp);
            Debug.Log(CloudSaveLog.Downloaded(slot, cloud.Stamp));
            Complete(operation);
            NotifyPulled(slot);
        }

        private void Upload(Operation operation, CloudSaveStamp local, string localText)
        {
            var slot = operation.Slot;
            if (local.SavedAt == CloudSaveMerge.UnstampedSavedAt)
            {
                local = new CloudSaveStamp(NextSavedAt(slot), false);
                RecordStamp(slot, local);
            }

            var envelope = local.Deleted
                ? CloudSaveEnvelope.Removed(slot, local.SavedAt)
                : CloudSaveEnvelope.Saved(slot, local.SavedAt, localText);
            try
            {
                _backend.Write(FileNameOf(slot), envelope.ToJson(), error => OnWritten(operation, envelope.Stamp, error));
            }
            catch (Exception e)
            {
                Fail(operation, e.GetType().Name + ": " + e.Message);
            }
        }

        private void OnWritten(Operation operation, CloudSaveStamp stamp, string error)
        {
            if (!IsCurrent(operation))
            {
                return;
            }

            if (error != null)
            {
                Fail(operation, error);
                return;
            }

            Debug.Log(CloudSaveLog.Uploaded(operation.Slot, stamp));
            Complete(operation);
        }

        private Operation Begin(string slot)
        {
            var operation = new Operation(slot, _now);
            _inFlight = operation;
            return operation;
        }

        private bool IsCurrent(Operation operation)
        {
            return ReferenceEquals(_inFlight, operation);
        }

        private void Complete(Operation operation)
        {
            if (!IsCurrent(operation))
            {
                return;
            }

            _inFlight = null;
            _failures = 0;
        }

        private void Fail(Operation operation, string error)
        {
            if (!IsCurrent(operation))
            {
                return;
            }

            _inFlight = null;
            _failures++;
            var delay = RetrySeconds;
            for (var i = 1; i < _failures && delay < MaxRetrySeconds; i++)
            {
                delay *= 2d;
            }

            delay = Math.Min(delay, MaxRetrySeconds);
            _retryAt = _now + delay;
            if (operation.Slot == null)
            {
                Debug.LogWarning(CloudSaveLog.ListFailed(error, delay));
                return;
            }

            Enqueue(operation.Slot);
            Debug.LogWarning(CloudSaveLog.Failed(operation.Slot, error, delay));
        }

        private void Enqueue(string slot)
        {
            if (_backend == null || !_syncs(slot) || _queue.Contains(slot))
            {
                return;
            }

            if (FileNameOf(slot).Length > _backend.MaxFileNameLength)
            {
                if (_tooLong.Add(slot))
                {
                    Debug.LogWarning(CloudSaveLog.NameTooLong(slot, _backend.MaxFileNameLength));
                }

                return;
            }

            _queue.Add(slot);
        }

        // 時計が戻っても、手元で書いた分が前の時刻 (クラウドから採った時刻を含む) より新しいと分かるようにする。
        private long NextSavedAt(string slot)
        {
            var now = Math.Max(0L, _clock());
            if (_stamps.TryGet(slot, out var previous) && previous.SavedAt >= now)
            {
                return previous.SavedAt + 1;
            }

            return Math.Max(now, CloudSaveMerge.UnstampedSavedAt + 1);
        }

        private void RecordStamp(string slot, CloudSaveStamp stamp)
        {
            _stamps.Set(slot, stamp);
            SaveStamps();
        }

        private void SaveStamps(bool throwOnFailure = false)
        {
            try
            {
                _local.Write(StampsSlot, _stamps.ToJson());
            }
            catch (Exception) when (throwOnFailure)
            {
                // 記録が書けないまま手元を入れ替えると「別アカウントの記録と中身」の組が残るので、呼び出し側で止めさせる。
                throw;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // セーブ本体は書けているので止めない。時刻を失うと次の起動ではクラウドの方を採り、手元の分は ReplacedPrefix に残る。
                Debug.LogError(CloudSaveLog.StampsNotSaved(e.Message));
            }
        }

        private void NotifyPulled(string slot)
        {
            var handlers = SlotPulled;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string>)handler)(slot);
                }
                catch (Exception e)
                {
                    // 聞き手の例外で同期を止めないよう、CombatEvents と同じくログに出して続ける。
                    Debug.LogException(e);
                }
            }
        }

        private static CloudSaveStamps LoadStamps(ISaveStorage local)
        {
            if (!local.TryRead(StampsSlot, out var text) || text == null)
            {
                return new CloudSaveStamps();
            }

            try
            {
                return CloudSaveStamps.FromJson(text);
            }
            catch (FormatException e)
            {
                Debug.LogWarning(CloudSaveLog.StampsRejected(e.Message));
                return new CloudSaveStamps();
            }
        }

        private sealed class Operation
        {
            public Operation(string slot, double startedAt)
            {
                Slot = slot;
                StartedAt = startedAt;
            }

            // null は一覧の取得。
            public string Slot { get; }

            public double StartedAt { get; }
        }
    }
}
