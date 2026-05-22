using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Core.Async
{
    public abstract class AsyncBehaviour : PaperBehaviour, IDisposable
    {
        private readonly CancellationTokenSource _lifetimeSource = new CancellationTokenSource();
        private readonly AsyncWatcher _mainAsyncWatcher = new AsyncWatcher();

        protected CancellationToken LifetimeToken => _lifetimeSource.Token;
        [Obsolete("This is a hacky one. Prefer InitMainToken result instead.")]
        protected CancellationToken MainToken => _mainAsyncWatcher.Token;

        public bool IsDisposed { get; private set; }

        protected virtual void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            ClearLifetimeTokenSource();
            _mainAsyncWatcher.Dispose();
            OnDispose();
        }

        protected void ClearMainTokenSource()
        {
            _mainAsyncWatcher.ClearTokenSource();
        }

        protected CancellationToken InitMainTokenSource(params CancellationToken[] tokens)
        {
            return _mainAsyncWatcher.InitTokenSource(tokens);
        }

        protected CancellationToken InitMainTokenSource(CancellationToken token)
        {
            return _mainAsyncWatcher.InitTokenSource(token);
        }

        protected CancellationToken InitMainTokenSource()
        {
            return _mainAsyncWatcher.InitTokenSource();
        }

        protected Coroutine RunCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        protected virtual void OnDispose()
        {
        }

        private void ClearLifetimeTokenSource()
        {
            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
        }
    }
}
