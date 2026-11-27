using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 秒数と半径は警報ごとに持たず StealthActionInstaller.Tuning (隠密の JSON の alarm) から読む。建物に警報を何個置いても同じ値で鳴るように。
    [DisallowMultipleComponent]
    public sealed class SecurityAlarm : MonoBehaviour
    {
        public const string LogTag = SecurityAlarmLog.Tag;

        public const float DefaultVolume = 1f;

        [SerializeField, Tooltip("警報の音。空なら SecurityCameraCatalog の音、それも無ければ鳴らさない")]
        private AudioClip _alarmClip;

        [SerializeField, Range(0f, 1f)]
        private float _volume = DefaultVolume;

        private readonly List<EnemyStealth> _candidates = new List<EnemyStealth>();
        private readonly List<System.Numerics.Vector3> _candidatePositions = new List<System.Numerics.Vector3>();
        private readonly List<int> _selected = new List<int>();

        private SecurityAlarmState _state;
        private AudioSource _source;
        private bool _listening;
        private SecurityAlarmStatus? _published;

        public SecurityAlarmState State => _state;

        public bool IsListening => _listening;

        public bool Ringing => _state != null && _state.Ringing;

        public AudioSource Source => _source;

        public AudioClip AlarmClip
        {
            get => _alarmClip;
            set => _alarmClip = value;
        }

        public float Volume
        {
            get => _volume;
            set
            {
                if (!(value >= 0f) || value > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "volume must be between 0 and 1");
                }

                _volume = value;
            }
        }

        // Edit Mode では OnEnable が走らず EnemyStealth.Active が空のままなので、テストから伝える先を差し替える。
        internal IReadOnlyList<EnemyStealth> ListenersOverride { get; set; }

        public AudioClip ResolveClip()
        {
            if (_alarmClip != null)
            {
                return _alarmClip;
            }

            var catalog = SecurityCameraCatalog.Load();
            return catalog != null ? catalog.AlarmClip : null;
        }

        public void Initialize(SecurityAlarmSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"SecurityAlarm '{name}' is already initialized");
            }

            _state = new SecurityAlarmState(spec);
        }

        // 最初に鳴るときまで読まないのは、シーンの OnEnable の時点ではまだ GameContent が選ばれていないことがあるため。
        public void EnsureInitialized()
        {
            if (_state == null)
            {
                _state = new SecurityAlarmState(StealthActionInstaller.Tuning.Alarm);
            }
        }

        public void Listen()
        {
            if (_listening)
            {
                return;
            }

            SecurityAlarmBus.Raised += OnRaised;
            _listening = true;
        }

        public void StopListening()
        {
            if (_listening)
            {
                SecurityAlarmBus.Raised -= OnRaised;
                _listening = false;
            }

            Silence();
        }

        public int Raise(Vector3 spottedAt, string cameraId)
        {
            return Raise(spottedAt, cameraId, ListenersOverride ?? EnemyStealth.Active);
        }

        // 鳴り始めにだけ敵へ伝える。鳴っている間はカメラが見続けて何度も Raise するので、そのたびに伝えると敵ごとのログが毎フレーム出る。
        public int Raise(Vector3 spottedAt, string cameraId, IReadOnlyList<EnemyStealth> listeners)
        {
            if (listeners == null)
            {
                throw new ArgumentNullException(nameof(listeners));
            }

            EnsureInitialized();
            var change = _state.Raise(spottedAt.ToNumerics(), cameraId);
            Debug.Log(SecurityAlarmLog.Raised(change, _state.CameraId, _state.SpottedAt, _state.Spec), this);
            Publish();
            if (change != SecurityAlarmRaise.Started)
            {
                return 0;
            }

            Play();
            return Alert(spottedAt, listeners);
        }

        public void Tick(float deltaSeconds)
        {
            if (!Ringing)
            {
                return;
            }

            // 止んだ Tick では SecurityAlarmState が場所とカメラを消すので、先に控えておく。
            var at = _state.SpottedAt;
            var cameraId = _state.CameraId;
            if (_state.Tick(deltaSeconds))
            {
                Debug.Log(SecurityAlarmLog.Stopped(cameraId, at), this);
                StopSound();
                ClearSignal();
                return;
            }

            Publish();
        }

        public void Silence()
        {
            if (Ringing)
            {
                Debug.Log(SecurityAlarmLog.Stopped(_state.CameraId, _state.SpottedAt), this);
            }

            _state?.Reset();
            StopSound();
            ClearSignal();
        }

        // 伝えた敵が調べに行く中で一覧が変わることがあるので写しから選ぶ。1 人が投げても残りの敵には伝える。
        private int Alert(Vector3 spottedAt, IReadOnlyList<EnemyStealth> listeners)
        {
            _candidates.Clear();
            _candidatePositions.Clear();
            for (var i = 0; i < listeners.Count; i++)
            {
                var listener = listeners[i];
                if (listener == null || listener.IsDead)
                {
                    continue;
                }

                _candidates.Add(listener);
                _candidatePositions.Add(listener.GroundPosition.ToNumerics());
            }

            _state.SelectRecipients(_candidatePositions, _selected);
            var recipients = new EnemyStealth[_selected.Count];
            for (var i = 0; i < recipients.Length; i++)
            {
                recipients[i] = _candidates[_selected[i]];
            }

            _candidates.Clear();
            var cameraId = _state.CameraId;
            var investigating = 0;
            foreach (var recipient in recipients)
            {
                try
                {
                    if (recipient != null && recipient.ReceiveSecurityAlarm(spottedAt, cameraId) == HearingOutcome.Investigating)
                    {
                        investigating++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e, recipient);
                }
            }

            return investigating;
        }

        private void Publish()
        {
            _published = _state.Status;
            GameSignals.StealthAlarm.Publish(_published);
        }

        private void ClearSignal()
        {
            if (!_published.HasValue)
            {
                return;
            }

            GameSignals.StealthAlarm.ClearIfCurrent(_published);
            _published = null;
        }

        // 2D で鳴らすのは、警報は建物じゅうに聞こえる音で、置いた場所から離れると聞こえなくなっては困るため。
        // PlayOneShot でなく clip を差して鳴らすのは、止んだときに Stop で音も切れるように。
        private void Play()
        {
            var clip = ResolveClip();
            if (clip == null)
            {
                return;
            }

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }

            _source.clip = clip;
            _source.volume = _volume * AppliedControlSettings.Current.EffectsVolume;
            _source.Play();
        }

        private void StopSound()
        {
            if (_source != null && _source.isPlaying)
            {
                _source.Stop();
            }
        }

        private void OnRaised(Vector3 spottedAt, string cameraId)
        {
            Raise(spottedAt, cameraId);
        }

        private void OnEnable()
        {
            Listen();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private void OnDestroy()
        {
            StopListening();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
