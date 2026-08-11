using System;
using System.Threading;

namespace VideoGenerator.Services
{
    public class TaskCancellationService
    {
        private CancellationTokenSource _cts;
        private readonly object _lock = new object();

        public CancellationToken Token
        {
            get
            {
                lock (_lock)
                {
                    return _cts?.Token ?? CancellationToken.None;
                }
            }
        }

        public bool IsCancellationRequested
        {
            get
            {
                lock (_lock)
                {
                    return _cts?.IsCancellationRequested ?? false;
                }
            }
        }

        public CancellationToken CreateNewToken()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                return _cts.Token;
            }
        }

        public void Cancel()
        {
            lock (_lock)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
