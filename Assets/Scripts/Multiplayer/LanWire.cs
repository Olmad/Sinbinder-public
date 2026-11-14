using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sinbinder.Multiplayer
{
    // Do not touch UnityEngine objects in this class. All JSON (de)serialization
    // is performed by the gameplay/main thread.
    internal sealed class LanWire : IDisposable
    {
        private const int MaxInboundBytes = 4096;
        private readonly TcpClient _socket;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private int _closed;

        public LanWire(TcpClient socket)
        {
            _socket = socket;
            _socket.NoDelay = true;
            _stream = socket.GetStream();
        }

        public async Task<string> ReadLineAsync(CancellationToken cancellation)
        {
            // Bounded incremental read avoids remote-controlled memory growth.
            using (var data = new MemoryStream())
            {
                var one = new byte[1];
                while (true)
                {
                    int count = await _stream.ReadAsync(one, 0, 1, cancellation);
                    if (count == 0) return null;
                    if (one[0] == (byte)'\n')
                        return Encoding.UTF8.GetString(data.ToArray()).TrimEnd('\r');
                    if (data.Length >= MaxInboundBytes)
                        throw new InvalidDataException("LAN packet is too large");
                    data.WriteByte(one[0]);
                }
            }
        }

        public async Task SendLineAsync(string line, CancellationToken cancellation)
        {
            if (Volatile.Read(ref _closed) != 0) return;
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            if (bytes.Length > MaxInboundBytes) throw new InvalidDataException("LAN packet is too large");
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                await _sendGate.WaitAsync(deadline.Token);
                try
                {
                    if (Volatile.Read(ref _closed) == 0)
                        await _stream.WriteAsync(bytes, 0, bytes.Length, deadline.Token);
                }
                finally { _sendGate.Release(); }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            _socket.Close();
            // Deliberately do not dispose the semaphore: a pending write may
            // still release it after the TCP stream has closed.
        }
    }
}
