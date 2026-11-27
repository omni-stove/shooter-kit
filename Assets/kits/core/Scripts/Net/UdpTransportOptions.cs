using System;

namespace GameKit.Net
{
    public sealed class UdpTransportOptions
    {
        public const string DefaultConnectionKey = "game-kit-match";
        public const string DefaultProtocolMismatchReason = "game-kit:rejected:protocol-mismatch";
        public const int DefaultDisconnectTimeoutMilliseconds = 5000;
        public const int DefaultPingIntervalMilliseconds = 1000;

        // 1 秒ごとに 10 回、約 10 秒で諦める。
        public const int DefaultConnectAttemptIntervalMilliseconds = 1000;

        public const int DefaultMaxConnectAttempts = 10;

        // 切断の知らせは UDP 1 つなので落ちると相手にはタイムアウトに見える。止めるときに相手の受け取りをこの時間まで待ち、
        // 落ちた知らせを送り直させる。
        public const int DefaultShutdownLingerMilliseconds = 300;

        private string _connectionKey = DefaultConnectionKey;
        private string _protocolMismatchReason = DefaultProtocolMismatchReason;
        private int _disconnectTimeoutMilliseconds = DefaultDisconnectTimeoutMilliseconds;
        private int _pingIntervalMilliseconds = DefaultPingIntervalMilliseconds;
        private int _connectAttemptIntervalMilliseconds = DefaultConnectAttemptIntervalMilliseconds;
        private int _maxConnectAttempts = DefaultMaxConnectAttempts;
        private int _shutdownLingerMilliseconds = DefaultShutdownLingerMilliseconds;

        // サーバーとクライアントで同じでなければならない。違う相手は ProtocolMismatchReason で断る。
        public string ConnectionKey
        {
            get => _connectionKey;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("connection key must not be empty", nameof(value));
                }

                _connectionKey = value;
            }
        }

        // 断られた側はこの理由を画面の文に分けるので、ゲームが自分の切断理由の形に合わせて渡す。
        public string ProtocolMismatchReason
        {
            get => _protocolMismatchReason;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("protocol mismatch reason must not be empty", nameof(value));
                }

                _protocolMismatchReason = value;
            }
        }

        public int DisconnectTimeoutMilliseconds
        {
            get => _disconnectTimeoutMilliseconds;
            set => _disconnectTimeoutMilliseconds = Positive(value);
        }

        public int PingIntervalMilliseconds
        {
            get => _pingIntervalMilliseconds;
            set => _pingIntervalMilliseconds = Positive(value);
        }

        public int ConnectAttemptIntervalMilliseconds
        {
            get => _connectAttemptIntervalMilliseconds;
            set => _connectAttemptIntervalMilliseconds = Positive(value);
        }

        public int MaxConnectAttempts
        {
            get => _maxConnectAttempts;
            set => _maxConnectAttempts = Positive(value);
        }

        public int ShutdownLingerMilliseconds
        {
            get => _shutdownLingerMilliseconds;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "linger must not be negative");
                }

                _shutdownLingerMilliseconds = value;
            }
        }

        private static int Positive(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "value must be positive");
            }

            return value;
        }
    }
}
