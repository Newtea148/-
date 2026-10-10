using System.Windows;
using TimeMusic.Services;

namespace TimeMusic
{
    public partial class App : Application
    {
        public static SettingsService SettingsService;
        public static BassPlayer Player;

        protected override void OnStartup(StartupEventArgs e) { base.OnStartup(e);

            NativeLoader.EnsureLoaded();
            SettingsService = new SettingsService();
            Player = new BassPlayer();
            Player.Init();

            var win = new MainWindow();
            MainWindow = win;

            // 开启 Windows 11 原生毛玻璃(Acrylic)背景
            GlassWindow.Enable(win, GlassWindow.DWM_SB_TRANSIENTWINDOW, onDark: true);
            win.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { Player?.Dispose(); } catch { }
            try { SettingsService?.Save(); } catch { }
            base.OnExit(e);
        }
    }
}