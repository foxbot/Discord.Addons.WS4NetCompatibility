using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net.WebSockets;
using WebSocket4Net;
using System.Collections.Generic;
using System.Linq;

namespace Discord.Addons.WS4NetCompatibility
{
    public class WS4NetProvider : IWebSocketClient
    {
        public event Func<byte[], int, int, Task> BinaryMessage;
        public event Func<string, Task> TextMessage;
        public event Func<Exception, Task> Closed;

        private WebSocket _socket;
        private Dictionary<string, string> _headers;
        private CancellationTokenSource _cancelTokenSource;
        private CancellationToken _cancelToken, _parentToken;
        private readonly SemaphoreSlim _sendLock;
        private ManualResetEventSlim _waitUntilConnect;

        public WS4NetProvider()
        {
            _headers = new Dictionary<string, string>();
            _sendLock = new SemaphoreSlim(1, 1);
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = CancellationToken.None;
            _parentToken = CancellationToken.None;
            _waitUntilConnect = new ManualResetEventSlim();
        }

        public void SetHeader(string key, string value)
        {
            _headers[key] = value;
        }
        public void SetCancelToken(CancellationToken cancelToken)
        {
            _parentToken = cancelToken;
            _cancelToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancelTokenSource.Token).Token;
        }

        public async Task ConnectAsync(string host)
        {
            await DisconnectAsync().ConfigureAwait(false);

            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancelTokenSource.Token).Token;

            _socket = new WebSocket(host, customHeaderItems: _headers.ToList());
            _socket.EnableAutoSendPing = false;
            _socket.NoDelay = true;
            _socket.Proxy = null;

            _socket.MessageReceived += OnTextMessage;
            _socket.DataReceived += OnBinaryMessage;
            _socket.Opened += OnConnected;
            _socket.Closed += OnClosed;

            _socket.Open();
            _waitUntilConnect.Wait(_cancelToken);

            return;
        }

        public Task DisconnectAsync()
        {
            _cancelTokenSource.Cancel();
            if (_socket == null) return Task.CompletedTask;

            if (_socket.State == WebSocketState.Open)
            {
                try
                {
                    _socket.Close("1000");
                }
                catch { }
            }

            _socket.MessageReceived -= OnTextMessage;
            _socket.DataReceived -= OnBinaryMessage;
            _socket.Opened -= OnConnected;
            _socket.Closed -= OnClosed;

            _waitUntilConnect.Reset();

            return Task.CompletedTask;
        }

        public async Task SendAsync(byte[] data, int index, int count, bool isText)
        {
            await _sendLock.WaitAsync(_cancelToken).ConfigureAwait(false);
            if (isText)
            {
                try
                {
                    var json = Encoding.UTF8.GetString(data);
                    _socket.Send(json);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
            else
            {
                try
                {
                    var _data = new List<ArraySegment<byte>>();
                    _data.Add(new ArraySegment<byte>(data, index, count));
                    _socket.Send(_data);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
        }

        private void OnTextMessage(object sender, MessageReceivedEventArgs e) =>
            TextMessage(e.Message).GetAwaiter().GetResult();
        private void OnBinaryMessage(object sender, DataReceivedEventArgs e) =>
            BinaryMessage(e.Data, 0, e.Data.Count()).GetAwaiter().GetResult();
        private void OnConnected(object sender, object e)
        {
            _waitUntilConnect.Set();
        }
        private void OnClosed(object sender, object e)
        {
            Closed((e as SuperSocket.ClientEngine.ErrorEventArgs)?.Exception ?? new Net.WebSocketClosedException(0, "Unexpected WS close, no exception given")).GetAwaiter().GetResult();
        }
    }
}
