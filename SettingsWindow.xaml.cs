using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimeMusic.Models;
using TimeMusic.Services;

namespace TimeMusic
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _main;
        private readonly AppSettings _settings;
        private readonly List<EqRow> _eqRows = new List<EqRow>();
        private readonly string[] _presetColors = {
            "#6A7BFF", "#5B8DEF", "#29B6F6", "#26A69A", "#66BB6A",
            "#F4B400", "#FF7043", "#F44336", "#E040FB", "#8D6E63"
        };
        private Button _selectedColorBtn;

        public SettingsWindow(MainWindow main, AppSettings settings)
        {
            InitializeComponent();
            _main = main;
            _settings = settings;
            BuildColorButtons();
            BuildEq();
            AutoStartCheck.IsChecked = settings.AutoStart;
            DesktopCheck.IsChecked = settings.DesktopLyrics;
            LyricsCheck.IsChecked = settings.ShowLyrics;
            QueueCheck.IsChecked = settings.ShowQueue;
            ModeCombo.SelectedIndex = settings.PlayMode;
            VolumeSlider.Value = settings.Volume;
            VolumeLabel.Text = ((int)(settings.Volume * 100)) + "%";
            VolumeSlider.ValueChanged += (s, e) => VolumeLabel.Text = ((int)(e.NewValue * 100)) + "%";
            Services.GlassWindow.Enable(this, Services.GlassWindow.DWM_SB_TRANSIENTWINDOW, onDark: true);
        }

        private void BuildColorButtons()
        {
            foreach (var hex in _presetColors)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var btn = new Button
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(color),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Tag = hex,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                btn.Click += Color_Click;
                ColorWrap.Children.Add(btn);
                if (hex.Equals(_settings.ThemeColor, System.StringComparison.OrdinalIgnoreCase))
                    _selectedColorBtn = btn;
            }
            UpdateColorSelection();
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            _selectedColorBtn = btn;
            _settings.ThemeColor = (string)btn.Tag;
            UpdateColorSelection();
            _main.ApplyTheme(_settings.GetThemeColor());
        }

        private void UpdateColorSelection()
        {
            foreach (var child in ColorWrap.Children)
            {
                var b = child as Button;
                if (b == null) continue;
                b.BorderBrush = (b == _selectedColorBtn) ? Brushes.White : Brushes.Transparent;
            }
        }

        private void BuildEq()
        {
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                var row = new EqRow(BassPlayer.EqFrequencies[idx], _settings.EqGains[idx]);
                row.Changed += g =>
                {
                    _settings.EqGains[idx] = g;
                    App.Player?.SetEqGain(idx, g);
                };
                _eqRows.Add(row);
            }
            EqItems.ItemsSource = _eqRows;
        }

        private void ChooseDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择音乐文件夹";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _settings.DefaultMusicDir = dlg.SelectedPath;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoStart = AutoStartCheck.IsChecked == true;
            _settings.DesktopLyrics = DesktopCheck.IsChecked == true;
            _settings.ShowLyrics = LyricsCheck.IsChecked == true;
            _settings.ShowQueue = QueueCheck.IsChecked == true;
            _settings.PlayMode = ModeCombo.SelectedIndex < 0 ? 0 : ModeCombo.SelectedIndex;
            _settings.Volume = VolumeSlider.Value;
            App.SettingsService.SetAutoStart(_settings.AutoStart);
            _main.OnSettingsApplied();
            App.SettingsService.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class EqRow : INotifyPropertyChanged
        {
            private double _gain;
            public string Freq { get; }
            public event System.Action<double> Changed;
            public event PropertyChangedEventHandler PropertyChanged;

            public EqRow(int freq, double gain)
            {
                Freq = freq >= 1000 ? (freq / 1000.0).ToString("0.#") + "K" : freq.ToString();
                _gain = gain;
            }

            public double Gain
            {
                get { return _gain; }
                set
                {
                    if (_gain != value)
                    {
                        _gain = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gain)));
                        Changed?.Invoke(_gain);
                    }
                }
            }
        }
    }
}