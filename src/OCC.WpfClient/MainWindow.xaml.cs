using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Features.AuthHub.ViewModels;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Features.Shell.ViewModels.ShellViewModel _viewModel;
        private readonly LocalSettingsService _settingsService;

        public MainWindow(Features.Shell.ViewModels.ShellViewModel viewModel, LocalSettingsService settingsService)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _settingsService = settingsService;
            DataContext = _viewModel;

            StateChanged += MainWindow_StateChanged;
            SourceInitialized += MainWindow_SourceInitialized;

            // Register for messages
            WeakReferenceMessenger.Default.Register<ResizeWindowMessage>(this, (r, m) => ((MainWindow)r).Receive(m));
            WeakReferenceMessenger.Default.Register<PreferenceChangedMessage>(this, (r, m) => ((MainWindow)r).Receive(m));

            // Set initial view
            _viewModel.Navigation.NavigateTo<AuthViewModel>();
        }

        private void MainWindow_StateChanged(object? sender, System.EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = ""; // Restore icon
            }
            else
            {
                BtnMaximize.Content = ""; // Maximize icon
            }
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        public void Receive(ResizeWindowMessage message)
        {
            var info = message.Value;
            if (info.Width > 0) Width = info.Width;
            if (info.Height > 0) Height = info.Height;
            WindowState = info.State;
        }

        public void Receive(PreferenceChangedMessage message)
        {
            if (message.PreferenceName == nameof(LocalSettings.MaximizeOverTaskbar))
            {
                if (WindowState == WindowState.Maximized)
                {
                    // Force re-evaluation of bounds
                    WindowState = WindowState.Normal;
                    WindowState = WindowState.Maximized;
                }
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO && !_settingsService.Settings.MaximizeOverTaskbar)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;
                    
                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const int MONITOR_DEFAULTTONEAREST = 2;
    }
}