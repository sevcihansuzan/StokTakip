using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SideBar_Nav.Pages
{
    /// <summary>
    /// Interaction logic for Uretim.xaml
    /// </summary>
    public partial class Uretim : Page
    {
        private const string UnityFolderName = "UnityApp";
        private const string UnityExecutableName = "akillifabrika.exe";

        private Process? unityProcess;
        private IntPtr unityWindowHandle = IntPtr.Zero;
        private bool isLaunching;

        private string UnityExecutablePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UnityFolderName, UnityExecutableName);

        public Uretim()
        {
            InitializeComponent();

            Loaded += Uretim_Loaded;
            Unloaded += Uretim_Unloaded;
            unityHost.SizeChanged += UnityHost_SizeChanged;
        }

        private void UnityHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeUnityWindow();
        }

        private void Uretim_Loaded(object sender, RoutedEventArgs e)
        {
            unityHost.SizeChanged -= UnityHost_SizeChanged;
            unityHost.SizeChanged += UnityHost_SizeChanged;

            if (unityPanel.IsHandleCreated)
            {
                _ = LaunchUnityAppAsync();
            }
            else
            {
                unityPanel.HandleCreated += UnityPanel_HandleCreated;
            }
        }

        private void UnityPanel_HandleCreated(object? sender, EventArgs e)
        {
            unityPanel.HandleCreated -= UnityPanel_HandleCreated;
            Dispatcher.BeginInvoke(new Action(() => _ = LaunchUnityAppAsync()));
        }

        private void Uretim_Unloaded(object sender, RoutedEventArgs e)
        {
            unityPanel.HandleCreated -= UnityPanel_HandleCreated;
            unityHost.SizeChanged -= UnityHost_SizeChanged;
            CleanupUnityProcess();
        }

        private async Task LaunchUnityAppAsync()
        {
            if (isLaunching)
            {
                return;
            }

            if (unityProcess != null && !unityProcess.HasExited)
            {
                ResizeUnityWindow();
                return;
            }

            if (!File.Exists(UnityExecutablePath))
            {
                MessageBox.Show(
                    $"Unity uygulaması bulunamadı.\nBeklenen konum: {UnityExecutablePath}",
                    "akıllıfabrika.exe bulunamadı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            isLaunching = true;

            try
            {
                unityProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = UnityExecutablePath,
                        WorkingDirectory = Path.GetDirectoryName(UnityExecutablePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false,
                    }
                };

                if (!unityProcess.Start())
                {
                    MessageBox.Show(
                        "Unity uygulaması başlatılamadı.",
                        "Başlatma Hatası",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    CleanupUnityProcess();
                    return;
                }

                bool windowReady = await WaitForUnityWindowAsync(unityProcess);

                if (!windowReady || unityProcess.HasExited)
                {
                    MessageBox.Show(
                        "Unity uygulamasının penceresi oluşturulamadı.",
                        "Başlatma Hatası",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    CleanupUnityProcess();
                    return;
                }

                unityWindowHandle = unityProcess.MainWindowHandle;

                if (unityWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show(
                        "Unity penceresi bulunamadı.",
                        "Başlatma Hatası",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    CleanupUnityProcess();
                    return;
                }

                EmbedUnityWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unity uygulaması başlatılırken bir hata oluştu.\n{ex.Message}",
                    "Başlatma Hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                CleanupUnityProcess();
            }
            finally
            {
                isLaunching = false;
            }
        }

        private static Task<bool> WaitForUnityWindowAsync(Process process, int timeoutMilliseconds = 15000)
        {
            return Task.Run(() =>
            {
                try
                {
                    process.WaitForInputIdle(timeoutMilliseconds);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                var stopwatch = Stopwatch.StartNew();

                while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    Thread.Sleep(50);
                }

                return !process.HasExited && process.MainWindowHandle != IntPtr.Zero;
            });
        }

        private void EmbedUnityWindow()
        {
            if (unityProcess == null || unityProcess.HasExited || unityWindowHandle == IntPtr.Zero)
            {
                return;
            }

            IntPtr panelHandle = unityPanel.Handle;

            SetParent(unityWindowHandle, panelHandle);

            int style = GetWindowLong(unityWindowHandle, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU);
            style |= WS_CHILD | WS_VISIBLE;
            SetWindowLong(unityWindowHandle, GWL_STYLE, style);

            ResizeUnityWindow();
        }

        private void ResizeUnityWindow()
        {
            if (unityWindowHandle == IntPtr.Zero)
            {
                return;
            }

            int width = Math.Max(0, (int)Math.Round(unityHost.ActualWidth));
            int height = Math.Max(0, (int)Math.Round(unityHost.ActualHeight));

            if (width == 0 || height == 0)
            {
                return;
            }

            SetWindowPos(unityWindowHandle, IntPtr.Zero, 0, 0, width, height, SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
        }

        private void CleanupUnityProcess()
        {
            try
            {
                if (unityProcess != null)
                {
                    if (!unityProcess.HasExited)
                    {
                        try
                        {
                            unityProcess.CloseMainWindow();
                            if (!unityProcess.WaitForExit(2000))
                            {
                                unityProcess.Kill(true);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Process might have already exited.
                        }
                    }

                    unityProcess.Dispose();
                    unityProcess = null;
                }
            }
            finally
            {
                unityWindowHandle = IntPtr.Zero;
            }
        }

        #region Win32

        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZE = 0x20000000;
        private const int WS_MAXIMIZE = 0x01000000;
        private const int WS_SYSMENU = 0x00080000;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        #endregion
    }
}
