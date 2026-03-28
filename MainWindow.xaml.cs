using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Media.Control;
using Forms = System.Windows.Forms;

namespace MediaFlyout
{
    public partial class MainWindow : Window
    {
        private Forms.NotifyIcon? _trayIcon;
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private DispatcherTimer _ticker;
        private string _lastTitle = "";
        private string _currentAppId = "";

        [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private const int WM_APPCOMMAND = 0x319;
        private const int APPCOMMAND_VOLUME_UP = 0xA;
        private const int APPCOMMAND_VOLUME_DOWN = 0x9;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        internal struct WindowCompositionAttributeData { public WindowCompositionAttribute Attribute; public IntPtr Data; public int SizeOfData; }
        internal enum WindowCompositionAttribute { WCA_ACCENT_POLICY = 19 }
        internal enum AccentState { ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 } 
        internal struct AccentPolicy { public AccentState AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }

        private class DarkModeColorTable : Forms.ProfessionalColorTable
        {
            public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(43, 43, 43);
            public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(25, 25, 25);
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
            public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color MenuItemPressedGradientMiddle => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(65, 65, 65);
            public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(43, 43, 43);
            public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(43, 43, 43);
            public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(43, 43, 43);
        }

        private class LightModeColorTable : Forms.ProfessionalColorTable
        {
            public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(242, 242, 242);
            public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(200, 200, 200);
            public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
            public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color MenuItemPressedGradientMiddle => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(210, 210, 210);
            public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(242, 242, 242);
            public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(242, 242, 242);
            public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(242, 242, 242);
        }

        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => System.Windows.MessageBox.Show($"Internal Error: {e.ExceptionObject}");

            SetupTray();
            
            _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticker.Tick += (s, e) => _ = UpdateUISafe(); 
            _ticker.Start();

            _ = InitializeMediaAsync();
            this.Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);

            ApplyTheme(hwnd);
        }

        private void ApplyTheme(IntPtr hwnd)
        {
            bool isAppLight = false;
            bool isSystemLight = false;
            
            try {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    if (key.GetValue("AppsUseLightTheme") is int appVal && appVal == 1) isAppLight = true;
                    if (key.GetValue("SystemUsesLightTheme") is int sysVal && sysVal == 1) isSystemLight = true;
                }
            } catch { } 

            try 
            {
                var accent = new AccentPolicy { 
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, 
                    GradientColor = 0x01000000, 
                    AccentFlags = 2,
                    AnimationId = 0
                };
                
                int accentSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
                Marshal.StructureToPtr(accent, accentPtr, false);
                var data = new WindowCompositionAttributeData { Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY, SizeOfData = accentSize, Data = accentPtr };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { }

            if (isAppLight)
            {
                MainBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 245, 245, 245));
                MainBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 210, 210, 210));
                SeparatorLine.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 210, 210, 210));
                TxtTitle.Foreground = System.Windows.Media.Brushes.Black;
                TxtArtist.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 80, 80, 80));
                BtnClose.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 80, 80, 80));
                BtnPrev.Foreground = System.Windows.Media.Brushes.Black;
                BtnPlayPause.Foreground = System.Windows.Media.Brushes.Black;
                BtnNext.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                MainBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 25, 25, 25));
                MainBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 51, 51, 51));
                SeparatorLine.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 51, 51, 51));
                TxtTitle.Foreground = System.Windows.Media.Brushes.White;
                TxtArtist.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 170, 170, 170));
                BtnClose.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 150, 150, 150));
                BtnPrev.Foreground = System.Windows.Media.Brushes.White;
                BtnPlayPause.Foreground = System.Windows.Media.Brushes.White;
                BtnNext.Foreground = System.Windows.Media.Brushes.White;
            }

            if (_trayIcon?.ContextMenuStrip != null) 
            {
                _trayIcon.ContextMenuStrip.BackColor = isSystemLight ? System.Drawing.Color.FromArgb(242, 242, 242) : System.Drawing.Color.FromArgb(43, 43, 43);
                _trayIcon.ContextMenuStrip.ForeColor = isSystemLight ? System.Drawing.Color.Black : System.Drawing.Color.White;
                
                var colorTable = isSystemLight ? (Forms.ProfessionalColorTable)new LightModeColorTable() : new DarkModeColorTable();
                _trayIcon.ContextMenuStrip.Renderer = new Forms.ToolStripProfessionalRenderer(colorTable) { RoundedEdges = false };
            }
        }

        private void SetupTray()
        {
            _trayIcon = new Forms.NotifyIcon { Visible = true, Text = "Media Flyout (Scroll for Volume)" };
            
            // THE FIX: Extract the icon directly from the compiled .exe file
            try 
            {
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath)) {
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(processPath) ?? System.Drawing.SystemIcons.Application;
                } else {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch { _trayIcon.Icon = System.Drawing.SystemIcons.Application; }

            var menu = new Forms.ContextMenuStrip();
            menu.ShowImageMargin = false; 
            menu.Items.Add("Exit", null, (s, e) => { _trayIcon?.Dispose(); System.Windows.Application.Current.Shutdown(); });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.Click += (s, e) => { if (e is Forms.MouseEventArgs me && me.Button == Forms.MouseButtons.Left) ToggleFlyout(); };
        }

        private async Task InitializeMediaAsync() { try { _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); } catch { } }

        private GlobalSystemMediaTransportControlsSession? GetTargetSession()
        {
            if (_mediaManager == null) return null;
            var sessions = _mediaManager.GetSessions();
            var playing = sessions.FirstOrDefault(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            if (playing != null) { _currentAppId = playing.SourceAppUserModelId; return playing; }
            if (!string.IsNullOrEmpty(_currentAppId)) {
                var stuckSession = sessions.FirstOrDefault(s => s.SourceAppUserModelId == _currentAppId);
                if (stuckSession != null) return stuckSession;
            }
            return _mediaManager.GetCurrentSession() is var current && current != null ? (_currentAppId = current.SourceAppUserModelId, current).current : null;
        }

        private async Task UpdateUISafe()
        {
            try 
            {
                var session = GetTargetSession();
                if (session != null)
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    if (props == null) return;
                    var playback = session.GetPlaybackInfo();

                    Dispatcher.Invoke(() => {
                        TxtTitle.Text = props.Title;
                        TxtArtist.Text = props.Artist ?? session.SourceAppUserModelId;
                        BtnPlayPause.Content = (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ? "\uE769" : "\uE768";
                        BtnPrev.IsEnabled = playback.Controls.IsPreviousEnabled;
                        BtnNext.IsEnabled = playback.Controls.IsNextEnabled;
                    });

                    if (props.Title != _lastTitle)
                    {
                        _lastTitle = props.Title;
                        if (props.Thumbnail != null)
                        {
                            using var stream = await props.Thumbnail.OpenReadAsync();
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream.AsStream();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad; 
                            bitmap.EndInit();
                            bitmap.Freeze();
                            Dispatcher.Invoke(() => {
                                ImgAlbumArt.Source = bitmap;
                                ImgBlurBg.Source = bitmap;
                                TxtIcon.Visibility = Visibility.Collapsed;
                            });
                        }
                    }
                }
            } catch { }
        }

        private void Window_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (e.Delta > 0) SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)(APPCOMMAND_VOLUME_UP << 16));
            else SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)(APPCOMMAND_VOLUME_DOWN << 16));
        }

        private void ToggleFlyout() 
        { 
            if (this.IsVisible) this.Hide(); 
            else 
            { 
                ApplyTheme(new WindowInteropHelper(this).Handle);
                _ = UpdateUISafe(); 
                PositionWindow(); 
                this.Show(); 
                this.Activate(); 
                this.Topmost = true; 
            } 
        }
        
        private void PositionWindow() { var area = SystemParameters.WorkArea; this.Left = area.Right - this.Width - 10; this.Top = area.Bottom - this.Height - 10; }
        private void Window_Deactivated(object sender, EventArgs e) => this.Hide();
        private void Close_Click(object sender, RoutedEventArgs e) => this.Hide();
        
        private async void PlayPause_Click(object sender, RoutedEventArgs e) { BtnPlayPause.Content = BtnPlayPause.Content.ToString() == "\uE769" ? "\uE768" : "\uE769"; var s = GetTargetSession(); if (s != null) await s.TryTogglePlayPauseAsync(); await Task.Delay(150); _ = UpdateUISafe(); }
        private async void Prev_Click(object sender, RoutedEventArgs e) { var s = GetTargetSession(); if (s != null) await s.TrySkipPreviousAsync(); await Task.Delay(150); _ = UpdateUISafe(); }
        private async void Next_Click(object sender, RoutedEventArgs e) { var s = GetTargetSession(); if (s != null) await s.TrySkipNextAsync(); await Task.Delay(150); _ = UpdateUISafe(); }
    }
}