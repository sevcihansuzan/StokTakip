using System;
using System.Threading;
using System.Threading.Tasks;

namespace SideBar_Nav.Services
{
    public delegate Task<string> BarcodeScanRequest(int timeoutMs, CancellationToken cancellationToken);

    public static class BarcodeScannerBridge
    {
        private static readonly object Sync = new();
        private static BarcodeScanRequest? _scanFunc;
        private static Func<bool>? _isConnectedFunc;

        public static event EventHandler<bool>? ConnectionStateChanged;

        public static void Configure(BarcodeScanRequest scanFunc, Func<bool> isConnectedFunc)
        {
            if (scanFunc == null)
                throw new ArgumentNullException(nameof(scanFunc));
            if (isConnectedFunc == null)
                throw new ArgumentNullException(nameof(isConnectedFunc));

            lock (Sync)
            {
                _scanFunc = scanFunc;
                _isConnectedFunc = isConnectedFunc;
            }

            ConnectionStateChanged?.Invoke(null, IsConnected);
        }

        public static void Clear()
        {
            lock (Sync)
            {
                _scanFunc = null;
                _isConnectedFunc = null;
            }

            ConnectionStateChanged?.Invoke(null, false);
        }

        public static bool IsConnected
        {
            get
            {
                lock (Sync)
                {
                    return _isConnectedFunc?.Invoke() ?? false;
                }
            }
        }

        public static Task<string> TriggerScanAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            BarcodeScanRequest? scanFunc;
            lock (Sync)
            {
                scanFunc = _scanFunc;
            }

            if (scanFunc == null)
                throw new InvalidOperationException("Barkod okuyucu bağlı değil.");

            return scanFunc(timeoutMs, cancellationToken);
        }

        public static void NotifyConnectionChanged(bool connected)
        {
            ConnectionStateChanged?.Invoke(null, connected);
        }
    }
}

