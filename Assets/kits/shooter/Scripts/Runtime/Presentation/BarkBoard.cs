using System;
using GameKit.Core;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // 話し手ごとに BarkSelector を持たせないのは、全体の最小間隔と半径内の同時発声の上限が、場面の話し手すべてで分け合う決まりだから。
    public static class BarkBoard
    {
        // 倒れた味方を探すための体の一覧を取り直す間隔 (秒)。話し手ごとに毎フレーム場面を探すと、敵の数だけ探す回数が増えるため。
        public const float BodyScanIntervalSeconds = 1f;

        private static BarkSelector _selector;
        private static BarkSpec _spec;
        private static Damageable[] _bodies = Array.Empty<Damageable>();
        private static float _bodiesScannedAt = float.NegativeInfinity;

        public static BarkSelector Selector
        {
            get
            {
                if (_selector == null)
                {
                    Use(KitTuning.Barks);
                }

                return _selector;
            }
        }

        // 判定 (キットの BarkSelector) は手榴弾の知らせの半径を持たないので、作った元の調整値をここで一緒に持つ。
        public static BarkSpec Spec
        {
            get
            {
                if (_selector == null)
                {
                    Use(KitTuning.Barks);
                }

                return _spec;
            }
        }

        // null を断らないのは、null で KitTuning.Barks から作り直す元の振る舞いへ戻せるようにするため。
        public static void Use(BarkSpec spec)
        {
            _spec = spec;
            _selector = spec == null ? null : new BarkSelector(spec.Pacing);
        }

        // Selector を通さないのは、終わり際の OnDestroy から呼ばれたときに、使っていない判定を調整値の読み込みから作り直さないため。
        public static void Forget(string speakerId)
        {
            _selector?.Forget(speakerId);
        }

        public static void Reset()
        {
            _selector = null;
            _spec = null;
            _bodies = Array.Empty<Damageable>();
            _bodiesScannedAt = float.NegativeInfinity;
        }

        // 時刻が戻ったら取り直すのは、Play をまたいで Time.time が 0 に戻ったときに前の場面の一覧を使い続けないため。
        public static Damageable[] Bodies(float time)
        {
            if (time < _bodiesScannedAt || time - _bodiesScannedAt >= BodyScanIntervalSeconds)
            {
                _bodies = UnityEngine.Object.FindObjectsByType<Damageable>(FindObjectsInactive.Exclude);
                _bodiesScannedAt = time;
            }

            return _bodies;
        }

        // 場面を替えたら、前の場面の話し手の間隔と体の一覧を持ち越さない。
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                Reset();
            }
        }

        // ドメインの再読み込みを切った Play でも、前の Play の判定と調整値を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }
}
