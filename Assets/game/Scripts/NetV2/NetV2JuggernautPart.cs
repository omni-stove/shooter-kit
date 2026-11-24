using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 誰がジャガーノートで立っているかはサーバーが決めてスナップショットの場の物 (ObjectiveItemKind.Juggernaut) で送るので、
    // ここは ClientJuggernautStatus で読んだ立っているジャガーノートの体に、Guardian のリーダーと同じ目印 (GuardianLeaderMarker) を付けるだけにする。
    // 自分がジャガーノートのときは NetV2GuardianLeaderPart と同じく自分の頭上には出さず、HUD で知らせる。
    // HUD の ClientJuggernautStatus を借りずに自分で読むのは、HUD はマップを替えるたびに作り直され、部品は接続の間ずっと生きるため
    public sealed class NetV2JuggernautPart : INetV2ClientPart
    {
        public const string RootName = "NetV2JuggernautMarkers";

        private readonly Func<int> _localPlayerId;
        private readonly Func<int, NetV2RemotePlayer> _remote;
        private readonly GameObject _prefab;
        private readonly Action<string> _log;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private ClientJuggernautStatus _status;
        private int _statusFor;
        private int? _wanted;
        private bool _hasShown;
        private int _shownId;
        private NetV2RemotePlayer _shownRemote;
        private GuardianLeaderMarker _marker;
        private Team? _viewer;
        private Transform _root;
        private bool _disposed;

        // prefab は目印の見た目で、null ならオフラインのリーダーと同じ仮の菱形を作る。remote は相手の体を引く口で、null なら今の NetV2Client から引く
        public NetV2JuggernautPart(
            Func<int> localPlayerId,
            GameObject prefab = null,
            Func<int, NetV2RemotePlayer> remote = null,
            Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _prefab = prefab;
            _remote = remote ?? RemoteInCurrentClient;
            _log = log ?? (line => Debug.Log(line));
        }

        // NetV2ClientParts.Registered に並べる口。再生でも誰がジャガーノートかは見えるので、試合中と同じく作る
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            return new NetV2JuggernautPart(() => client.PlayerId, context.JuggernautMarkerVisual, log: context.Log);
        }

        // Juggernaut の試合でなければ null
        public ClientJuggernautStatus Status => _status != null && _status.Status != null ? _status : null;

        public GameObject MarkerVisual => _prefab;

        public int MarkerCount => _hasShown ? 1 : 0;

        public GuardianLeaderMarker MarkerOf(int playerId)
        {
            return _hasShown && _shownId == playerId && _marker != null ? _marker : null;
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            // 自分の Id は受け入れられてから決まり、部品はその前に作られるので、Id が変わったら読み手を作り直す
            var localId = _localPlayerId();
            if (_status == null || _statusFor != localId)
            {
                _status = new ClientJuggernautStatus(localId);
                _statusFor = localId;
            }

            _status.Apply(snapshot);
            var own = snapshot.Players.IndexOf(localId);
            _viewer = own >= 0 ? snapshot.Players[own].Team : (Team?)null;

            _wanted = null;
            var juggernaut = _status.JuggernautId;
            if (juggernaut.HasValue && juggernaut.Value != localId && _status.IsStandingJuggernaut(juggernaut.Value))
            {
                _wanted = juggernaut.Value;
            }

            Sync();
        }

        // 相手の体はスナップショットより後に現れたり、見た目の差し替えや出直しで作り直されたりするので、毎フレーム付け直す
        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            Sync();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wanted = null;
            Sync();
            _status = null;
            if (_root != null)
            {
                DestroySafely(_root.gameObject);
                _root = null;
            }
        }

        private void Sync()
        {
            // 体が消えたら目印は自分で消えるので、作り直した体へ付け直すよう手放す
            if (_hasShown && (_marker == null || _shownRemote == null || _marker.Anchor == null || _wanted != _shownId))
            {
                Remove();
            }

            if (!_wanted.HasValue)
            {
                return;
            }

            if (!_hasShown)
            {
                var remote = _remote(_wanted.Value);
                if (remote == null)
                {
                    return;
                }

                _shownId = _wanted.Value;
                _shownRemote = remote;
                _marker = CreateMarker(remote, new Side(_shownId));
                _hasShown = true;
                _log(ClientJuggernautLog.MarkerShown(_shownId));
            }

            if (_marker.ViewerTeam != _viewer)
            {
                _marker.ViewerTeam = _viewer;
            }

            // 隠れている相手の居場所を目印で明かさない
            var visible = !_shownRemote.IsConcealed;
            if (_marker.IsShown != visible)
            {
                _marker.SetShown(visible);
            }
        }

        private GuardianLeaderMarker CreateMarker(NetV2RemotePlayer remote, Side side)
        {
            if (_root == null)
            {
                _root = new GameObject(RootName).transform;
            }

            var marker = GuardianLeaderMarker.Create(remote.transform, side, () => BodyBounds(remote), _prefab);
            marker.ViewerTeam = _viewer;
            marker.transform.SetParent(_root, true);
            return marker;
        }

        private void Remove()
        {
            if (_marker != null)
            {
                _marker.Remove();
            }

            _marker = null;
            _shownRemote = null;
            _hasShown = false;
            _log(ClientJuggernautLog.MarkerRemoved(_shownId));
        }

        // 頭の高さは体の見た目で決める。ルートには撃つ・燃えるなどの演出も付き、その囲みで目印が浮かないよう体 (NetV2RemotePlayer.Body) の下だけを見る
        private Bounds? BodyBounds(NetV2RemotePlayer remote)
        {
            if (remote == null || remote.Body == null || !remote.Body.gameObject.activeInHierarchy)
            {
                return null;
            }

            remote.Body.GetComponentsInChildren(false, _renderers);
            Bounds? bounds = null;
            foreach (var bodyRenderer in _renderers)
            {
                if (!bodyRenderer.enabled)
                {
                    continue;
                }

                if (bounds.HasValue)
                {
                    var grown = bounds.Value;
                    grown.Encapsulate(bodyRenderer.bounds);
                    bounds = grown;
                }
                else
                {
                    bounds = bodyRenderer.bounds;
                }
            }

            _renderers.Clear();
            return bounds;
        }

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static NetV2RemotePlayer RemoteInCurrentClient(int playerId)
        {
            var client = NetV2Client.Current;
            if (client == null)
            {
                return null;
            }

            var body = client.RemoteObject(playerId);
            return body != null ? body.GetComponent<NetV2RemotePlayer>() : null;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
