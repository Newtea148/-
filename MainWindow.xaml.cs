using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TimeMusic.Controls;
using TimeMusic.Models;
using TimeMusic.Services;

namespace TimeMusic
{
    public partial class MainWindow : Window
    {
        private static readonly string[] AudioExts = { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".m4b", ".ogg", ".ape", ".wma", ".opus", ".alac" };
        private static readonly string[] ModeNames = { "顺序", "随机", "单曲循环", "列表循环" };

        private readonly List<TimePlaylist> _playlists;
        private readonly PlaylistScheduler _scheduler2;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _scheduleTimer;
        private readonly DispatcherTimer _clockTimer;

        private TimePlaylist _currentPlaylist;
        private List<SongInfo> _currentSongs = new List<SongInfo>();
        private int _songIndex = -1; // 顺序模式索引
        private bool _shuffle; // 随机模式
        private int[] _order; // 随机洗牌顺序
        private int _orderPos; // 随机游标
        private List<LrcLine> _lyrics = new List<LrcLine>();
        private int _curLrcIndex = -1;
        private bool _pendingSwitch;
        private DesktopLyricsWindow _desktopLyrics;

        private bool _seekDragging;
        private bool _settingsInit;

        public MainWindow()
        {
            InitializeComponent();
            _playlists = App.SettingsService.Playlists;
            _scheduler2 = new PlaylistScheduler(_playlists);
            RefreshPlaylistList();
            ApplyTheme(App.SettingsService.Settings.GetThemeColor());
            ApplyEqFromSettings();

            // 音量
            _settingsInit = true;
            VolumeSlider.Value = App.SettingsService.Settings.Volume;
            App.Player.SetVolume((float)App.SettingsService.Settings.Volume);
            _settingsInit = false;

            // 播放模式
            ModeBtn.Content = ModeNames[App.SettingsService.Settings.PlayMode];
            _shuffle = App.SettingsService.Settings.PlayMode == 1;
            if (_shuffle) ResetShuffle();

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _progressTimer.Tick += ProgressTick;
            _progressTimer.Start();

            _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _scheduleTimer.Tick += ScheduleTick;
            _scheduleTimer.Start();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _clockTimer.Tick += ClockTick;
            _clockTimer.Start();
            ClockTick(null, null);

            App.Player.SongEnded += OnSongEnded;

            if (App.SettingsService.Settings.DesktopLyrics)
            {
                _desktopLyrics = new DesktopLyricsWindow();
                _desktopLyrics.Show();
            }

            // 开机根据当前时间自动启动对应歌单
            var now = DateTime.Now;
            var current = _scheduler2.GetCurrent(now);
            if (current != null)
            {
                _currentPlaylist = current;
                HighlightPlaylist(current);
                StartPlaylist(current);
            }

            // 队列显隐
            if (!App.SettingsService.Settings.ShowQueue)
                ToggleQueue(false);
            if (!App.SettingsService.Settings.ShowLyrics)
                SetLyricsVisible(false);

            Closed += (s, e) =>
            {
                try { _progressTimer.Stop(); } catch { }
                try { _scheduleTimer.Stop(); } catch { }
                try { _clockTimer.Stop(); } catch { }
                _desktopLyrics?.Close();
                App.SettingsService.Settings.Volume = VolumeSlider.Value;
                App.SettingsService.Save();
            };
        }

        // ================= 窗口控制 =================

        private void Win_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ClockTick(object sender, EventArgs e)
        {
            if (ClockText != null)
                ClockText.Text = DateTime.Now.ToString("HH:mm");
        }

        // ================= 歌单管理 =================

        private void RefreshPlaylistList()
        {
            PlaylistList.ItemsSource = null;
            PlaylistList.ItemsSource = _playlists;
            foreach (var p in _playlists)
                p.IsActive = _scheduler2?.GetCurrent(DateTime.Now) == p;
        }

        private void HighlightPlaylist(TimePlaylist target)
        {
            PlaylistList.SelectedItem = target;
            target.IsSelected = true;
            foreach (var p in _playlists)
                p.IsActive = (p == target);
        }

        private void AddPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptInput("新建时间歌单", "请输入歌单名称(例如:早安):", "新歌单");
            if (name == null) return;
            var pl = new TimePlaylist { Name = name };
            var folder = ChooseFolder();
            if (folder == null) return;
            pl.FolderPath = folder;
            _playlists.Add(pl);
            RefreshPlaylistList();
            App.SettingsService.Save();
        }

        private void RootDir_Click(object sender, RoutedEventArgs e)
        {
            var pl = (TimePlaylist)((FrameworkElement)sender).Tag;
            if (pl == null) return;
            var folder = ChooseFolder(pl.FolderPath);
            if (folder == null) return;
            pl.FolderPath = folder;
            RefreshPlaylistList();
            App.SettingsService.Save();
            if (pl == _currentPlaylist)
                StartPlaylist(pl);
        }

        private void TimeBtn_Click(object sender, RoutedEventArgs e)
        {
            var pl = (TimePlaylist)((FrameworkElement)sender).Tag;
            if (pl == null) return;
            var (s, en) = PromptTimeRange(pl);
            if (s < 0) return;
            pl.StartMinutes = s;
            pl.EndMinutes = en;
            RefreshPlaylistList();
            App.SettingsService.Save();
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var pl = (TimePlaylist)((FrameworkElement)sender).Tag;
            if (pl == null) return;
            if (MessageBox.Show(this, $"删除歌单「{pl.Name}」?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _playlists.Remove(pl);
            if (_currentPlaylist == pl)
            {
                _currentPlaylist = null;
                _currentSongs.Clear();
                SongTitleText.Text = "时间音乐";
            }
            RefreshPlaylistList();
            App.SettingsService.Save();
        }

        private void Name_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tb = (TextBlock)sender;
            var pl = (TimePlaylist)tb.DataContext;
            var name = PromptInput("重命名歌单", "请输入新的歌单名称:", pl.Name);
            if (name == null) return;
            pl.Name = name;
            RefreshPlaylistList();
            App.SettingsService.Save();
        }

        private void PlaylistList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PlaylistList.SelectedItem is TimePlaylist pl && pl != _currentPlaylist)
            {
                _currentPlaylist = pl;
                StartPlaylist(pl);
            }
        }

        // ================= 歌曲扫描与播放 =================

        /// <summary>
        /// 递归扫描歌单根目录(含子文件夹),跳过无权限访问的子目录。
        /// 适配"歌单绑定一个本地音乐文件夹"而用户常把音乐放在子文件夹的场景。
        /// </summary>
        private List<SongInfo> ScanSongs(string folder)
        {
            var list = new List<SongInfo>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return list;
            EnumerateFilesRecursive(folder, f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (AudioExts.Contains(ext))
                {
                    list.Add(new SongInfo
                    {
                        FilePath = f,
                        Title = Path.GetFileNameWithoutExtension(f),
                        LrcPath = LyricsParser.FindLrcFor(f),
                    });
                }
            });
            return list.OrderBy(s => s.FileName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>递归枚举文件,遇到无权限目录时跳过而不是抛异常。</summary>
        private static void EnumerateFilesRecursive(string dir, Action<string> onFile)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { return; }

            foreach (var f in files)
                onFile(f);

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { return; }

            foreach (var sub in subDirs)
                EnumerateFilesRecursive(sub, onFile);
        }

        private void StartPlaylist(TimePlaylist pl)
        {
            _currentSongs = ScanSongs(pl.FolderPath);
            RefreshQueue();
            if (_currentSongs.Count == 0)
            {
                SongTitleText.Text = "该歌单暂无音乐";
                _lyrics.Clear();
                SetLyricsVisible(false);
                StatusText.Text = $"歌单「{pl.Name}」无可用音乐";
                CoverArt.Source = null;
                return;
            }
            ResetShuffle();
            PlaySong(_shuffle ? (_order.Length > 0 ? _order[0] : 0) : 0);
            StatusText.Text = $"正在播放歌单: {pl.Name}  {pl.TimeRangeText}";
        }

        private void RefreshQueue()
        {
            SongList.ItemsSource = null;
            SongList.ItemsSource = _currentSongs;
            QueueCount.Text = _currentSongs.Count + " 首";
            SongList.SelectedIndex = _songIndex;
        }

        private void ResetShuffle()
        {
            _order = Enumerable.Range(0, _currentSongs.Count).ToArray();
            Shuffle(_order);
            _orderPos = 0;
        }

        private static readonly Random _rng = new Random();

        private static void Shuffle(int[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int t = arr[i]; arr[i] = arr[j]; arr[j] = t;
            }
        }

        private void PlaySong(int index)
        {
            if (_currentSongs.Count == 0) return;
            if (index < 0 || index >= _currentSongs.Count) index = 0;
            _songIndex = index;
            var song = _currentSongs[index];
            var info = App.Player.Open(song.FilePath);
            if (info == null) return;
            song.DurationSeconds = info.DurationSeconds;
            if (song.LrcPath == null) song.LrcPath = info.LrcPath;
            SongTitleText.Text = song.Title;
            ApplyEqFromSettings();
            App.Player.SetVolume((float)App.SettingsService.Settings.Volume);
            LoadLyrics(song.LrcPath);
            LoadCover(song.FilePath);
            App.Player.Play(true);
            PlayBtn.Content = "⏸";
            UpdateDesktopLyrics();
            SongList.SelectedIndex = index;
            if (SongList.SelectedItem != null)
                SongList.ScrollIntoView(SongList.SelectedItem);
        }

        private void LoadCover(string filePath)
        {
            try
            {
                CoverArt.Source = AlbumArt.Load(filePath);
            }
            catch
            {
                CoverArt.Source = null;
            }
        }

        private void Next()
        {
            if (_currentSongs.Count == 0) return;
            int idx;
            int mode = App.SettingsService.Settings.PlayMode;
            if (mode == 2) // 单曲循环
            {
                idx = _songIndex;
            }
            else if (mode == 3) // 列表循环
            {
                idx = (_songIndex + 1) % _currentSongs.Count;
            }
            else if (_shuffle || mode == 1)
            {
                _orderPos++;
                if (_orderPos >= _order.Length) ResetShuffle();
                idx = _order[_orderPos % _order.Length];
            }
            else
            {
                idx = (_songIndex + 1) % _currentSongs.Count;
            }
            PlaySong(idx);
        }

        private void Prev()
        {
            if (_currentSongs.Count == 0) return;
            int idx;
            int mode = App.SettingsService.Settings.PlayMode;
            if (mode == 2)
            {
                idx = _songIndex;
            }
            else if (_shuffle || mode == 1)
            {
                _orderPos--;
                if (_orderPos < 0) { ResetShuffle(); _orderPos = _order.Length - 1; }
                idx = _order[(_orderPos + _order.Length) % _order.Length];
            }
            else
            {
                idx = (_songIndex - 1 + _currentSongs.Count) % _currentSongs.Count;
            }
            PlaySong(idx);
        }

        private void Prev_Click(object sender, RoutedEventArgs e) => Prev();
        private void Next_Click(object sender, RoutedEventArgs e) => Next();

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_songIndex < 0)
            {
                if (_currentSongs.Count > 0) PlaySong(0);
                return;
            }
            if (App.Player.IsPlaying)
            {
                App.Player.Pause();
                PlayBtn.Content = "▶";
            }
            else
            {
                App.Player.Play(false);
                PlayBtn.Content = "⏸";
            }
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            var s = App.SettingsService.Settings;
            s.PlayMode = (s.PlayMode + 1) % 4;
            _shuffle = s.PlayMode == 1;
            ModeBtn.Content = ModeNames[s.PlayMode];
            if (_shuffle) ResetShuffle();
            App.SettingsService.Save();
        }

        // ================= 歌曲队列 =================

        private void SongList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] is SongInfo song)
            {
                int idx = _currentSongs.IndexOf(song);
                if (idx >= 0 && idx != _songIndex)
                    PlaySong(idx);
            }
        }

        private void QueueToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleQueue(SongList.Visibility != Visibility.Visible);
        }

        private void ToggleQueue(bool show)
        {
            if (show)
            {
                SongList.Visibility = Visibility.Visible;
                QueueToggle.Content = "收起队列 ▾";
            }
            else
            {
                SongList.Visibility = Visibility.Collapsed;
                QueueToggle.Content = "展开队列 ▸";
            }
        }

        // ================= 音量 =================

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settingsInit) return;
            App.Player.SetVolume((float)VolumeSlider.Value);
            App.SettingsService.Settings.Volume = VolumeSlider.Value;
        }

        // ================= 定时调度 =================

        private void ScheduleTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            if (_currentPlaylist != null && _scheduler2.IsPastEnd(_currentPlaylist, now))
            {
                if (App.Player.IsPlaying && _songIndex >= 0)
                    _pendingSwitch = true;
                else
                    DoSwitchNow(now);
            }
            else if (_currentPlaylist == null)
            {
                var cur = _scheduler2.GetCurrent(now);
                if (cur != null)
                {
                    _currentPlaylist = cur;
                    HighlightPlaylist(cur);
                    StartPlaylist(cur);
                }
            }
        }

        private void DoSwitchNow(DateTime now)
        {
            var next = _scheduler2.GetCurrent(now);
            _pendingSwitch = false;
            if (next != null && next != _currentPlaylist)
            {
                _currentPlaylist = next;
                HighlightPlaylist(next);
                StartPlaylist(next);
            }
        }

        private void OnSongEnded()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_pendingSwitch)
                {
                    DoSwitchNow(DateTime.Now);
                }
                else
                {
                    Next();
                }
            }));
        }

        // ================= 进度与歌词 =================

        private void ProgressTick(object sender, EventArgs e)
        {
            var pos = App.Player.GetPositionSeconds();
            var len = App.Player.GetLengthSeconds();
            ProgressBar.Position = pos;
            ProgressBar.Length = len;
            CurTimeText.Text = SongInfo.FormatTime(pos);
            TotalTimeText.Text = SongInfo.FormatTime(len);

            // 横向进度条(拖动时不同步)
            if (!_seekDragging)
            {
                SeekCurText.Text = SongInfo.FormatTime(pos);
                SeekTotalText.Text = SongInfo.FormatTime(len);
                if (len > 0)
                    SeekSlider.Value = (pos / len) * 1000;
            }

            if (_lyrics.Count > 0)
                UpdateLyricIndex(pos);

            if (_songIndex >= 0 && _currentSongs.Count > 0)
            {
                string status = $"正在播放歌单: {(_currentPlaylist?.Name ?? "—")}  {(_currentPlaylist?.TimeRangeText ?? "")}";
                // 附加"下一个时段"提示,便于用户预览之后会切换到哪个歌单。
                var next = _scheduler2.GetNext(DateTime.Now, _currentPlaylist);
                if (next != null && next != _currentPlaylist)
                    status += $"   |  下一时段 {next.TimeRangeText}: {next.Name}";
                StatusText.Text = status;
            }
        }

        // 横向进度条拖动
        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_seekDragging)
            {
                double len = App.Player.GetLengthSeconds();
                if (len > 0)
                    SeekCurText.Text = SongInfo.FormatTime((SeekSlider.Value / 1000.0) * len);
            }
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _seekDragging = true;
        }

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _seekDragging = false;
            double len = App.Player.GetLengthSeconds();
            if (len > 0)
                App.Player.SeekTo((SeekSlider.Value / 1000.0) * len);
        }

        private void LoadLyrics(string lrcPath)
        {
            _lyrics = LyricsParser.ParseFile(lrcPath);
            _curLrcIndex = -1;
            if (_lyrics.Count > 0)
            {
                LyricList.ItemsSource = _lyrics.Select(l => l.Text).ToList();
                SetLyricsVisible(true);
            }
            else
            {
                LyricList.ItemsSource = null;
                SetLyricsVisible(false);
            }
        }

        private void SetLyricsVisible(bool hasLyrics)
        {
            LyricList.Visibility = hasLyrics ? Visibility.Visible : Visibility.Collapsed;
            BigTitleText.Visibility = hasLyrics ? Visibility.Collapsed : Visibility.Visible;
            if (hasLyrics)
                BigTitleText.Text = "";
            else if (_songIndex >= 0 && _currentSongs.Count > 0)
                BigTitleText.Text = _currentSongs[_songIndex].Title;
        }

        private void UpdateLyricIndex(double pos)
        {
            int idx = -1;
            for (int i = 0; i < _lyrics.Count; i++)
            {
                if (_lyrics[i].Time <= pos) idx = i;
                else break;
            }
            if (idx != _curLrcIndex)
            {
                _curLrcIndex = idx;
                LyricList.SelectedIndex = idx;
                if (idx >= 0)
                {
                    LyricList.ScrollIntoView(LyricList.Items[idx]);
                    var li = LyricList.ItemContainerGenerator.ContainerFromIndex(idx) as System.Windows.Controls.ListBoxItem;
                    if (li != null)
                    {
                        var tb = li.Content as TextBlock;
                        if (tb != null)
                        {
                            tb.Foreground = Application.Current.Resources["ThemeBrush"] as SolidColorBrush;
                            tb.FontWeight = FontWeights.Bold;
                            tb.FontSize = 16;
                        }
                    }
                    for (int i = 0; i < LyricList.Items.Count; i++)
                    {
                        if (i == idx) continue;
                        var l2 = LyricList.ItemContainerGenerator.ContainerFromIndex(i) as System.Windows.Controls.ListBoxItem;
                        var tb2 = l2?.Content as TextBlock;
                        if (tb2 != null)
                        {
                            tb2.Foreground = (SolidColorBrush)FindResource("DimTextBrush");
                            tb2.FontWeight = FontWeights.Normal;
                            tb2.FontSize = 14;
                        }
                    }
                }
                UpdateDesktopLyrics();
            }
        }

        private void UpdateDesktopLyrics()
        {
            if (_desktopLyrics == null) return;
            string txt = "";
            if (_curLrcIndex >= 0 && _curLrcIndex < _lyrics.Count)
                txt = _lyrics[_curLrcIndex].Text;
            else if (_songIndex >= 0 && _currentSongs.Count > 0)
                txt = _currentSongs[_songIndex].Title;
            _desktopLyrics.SetLyrics(txt);
        }

        // ================= 进度条点击跳转 =================

        private void ProgressBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pb = (VerticalProgressBar)sender;
            var p = e.GetPosition(pb);
            double len = App.Player.GetLengthSeconds();
            if (len <= 0) return;
            double frac = 1.0 - (p.Y / pb.ActualHeight);
            frac = Math.Max(0, Math.Min(1, frac));
            App.Player.SeekTo(frac * len);
        }

        // ================= 设置与主题 =================

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(this, App.SettingsService.Settings);
            win.Owner = this;
            win.ShowDialog();
        }

        public void ApplyTheme(Color color)
        {
            try
            {
                var brush = new SolidColorBrush(color);
                Application.Current.Resources["ThemeBrush"] = brush;
                ProgressBar.BarBrush = brush;
                PlayBtn.Background = brush;
            }
            catch { }
        }

        public void ApplyEqFromSettings()
        {
            App.Player.ApplyEq(App.SettingsService.Settings.EqGains);
        }

        public void OnSettingsApplied()
        {
            bool wantDesktop = App.SettingsService.Settings.DesktopLyrics;
            if (wantDesktop && _desktopLyrics == null)
            {
                _desktopLyrics = new DesktopLyricsWindow();
                _desktopLyrics.Show();
            }
            else if (!wantDesktop && _desktopLyrics != null)
            {
                _desktopLyrics.Hide();
            }
            if (App.SettingsService.Settings.DesktopLyrics && _desktopLyrics != null)
                _desktopLyrics.Show();

            // 新设置:队列、歌词、模式
            if (App.SettingsService.Settings.ShowQueue && SongList.Visibility != Visibility.Visible)
                ToggleQueue(true);
            else if (!App.SettingsService.Settings.ShowQueue && SongList.Visibility == Visibility.Visible)
                ToggleQueue(false);

            if (App.SettingsService.Settings.ShowLyrics)
            {
                if (_lyrics.Count > 0) SetLyricsVisible(true);
            }
            else
            {
                LyricList.Visibility = Visibility.Collapsed;
                BigTitleText.Visibility = Visibility.Visible;
            }

            ModeBtn.Content = ModeNames[App.SettingsService.Settings.PlayMode];
            _shuffle = App.SettingsService.Settings.PlayMode == 1;
        }

        // ================= 输入对话框 =================

        private string ChooseFolder(string initial = null)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择音乐文件夹";
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                    dlg.SelectedPath = initial;
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
            }
        }

        private string PromptInput(string title, string message, string def)
        {
            var dlg = new InputDialog(title, message, def) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.ResultText : null;
        }

        private (int, int) PromptTimeRange(TimePlaylist pl)
        {
            var dlg = new TimeRangeDialog(pl) { Owner = this };
            if (dlg.ShowDialog() == true)
                return (dlg.StartMinutes, dlg.EndMinutes);
            return (-1, -1);
        }
    }
}