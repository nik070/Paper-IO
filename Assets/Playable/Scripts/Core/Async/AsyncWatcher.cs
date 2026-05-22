using System;
using System.Threading;

namespace Core.Async
{
    public class AsyncWatcher : IDisposable
    {
        private CancellationTokenSource _tokenSource;

        public bool IsDisposed { get; private set; }

        public CancellationToken Token { get; private set; } = new CancellationToken(true);

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            ClearTokenSource();
        }

        public void ClearTokenSource()
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
        }

        public CancellationToken InitTokenSource(params CancellationToken[] tokens)
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokens);
            Token = _tokenSource.Token;
            return _tokenSource.Token;
        }

        public CancellationToken InitTokenSource(CancellationToken token)
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            Token = _tokenSource.Token;
            return _tokenSource.Token;
        }

        public CancellationToken InitTokenSource()
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = new CancellationTokenSource();
            Token = _tokenSource.Token;
            return _tokenSource.Token;
        }
    }
}
