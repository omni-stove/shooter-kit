using System;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 数分ぶんのスナップショットを残すと大きくなるので、送った物は捨てる
    internal sealed class DiscardingTransport : IMatchTransport
    {
        public bool IsActive => true;

        public void Listen(string address, ushort port)
        {
        }

        public void Connect(string address, ushort port)
        {
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            return channel == TransportChannel.Unreliable ? RecordingTransport.UnreliableLimit : ushort.MaxValue;
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            milliseconds = 0;
            return false;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            transportEvent = default;
            return false;
        }

        public void Shutdown(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
