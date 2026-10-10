using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimeMusic
{
    /// <summary>通用单行文本输入对话框。</summary>
    public class InputDialog : Window
    {
        private readonly TextBox _tb;
        public string ResultText => _tb.Text.Trim();

        public InputDialog(string title, string message, string def)
        {
            Title = title;
            Width = 380;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["PanelBrush"];
            Foreground = (Brush)Application.Current.Resources["TextBrush"];
            FontFamily = new FontFamily("Microsoft YaHei UI");
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.ToolWindow;

            var grid = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var msg = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
            };
            Grid.SetRow(msg, 0); 
            grid.Children.Add(msg);

            _tb = new TextBox
            {
                Text = def,
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 16),
            };
            Grid.SetRow(_tb, 1);
            grid.Children.Add(_tb);

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var ok = new Button
            {
                Content = "确定",
                FontSize = 13,
                Padding = new Thickness(20, 7, 20, 7),
                Background = (Brush)Application.Current.Resources["ThemeBrush"],
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
            };
            ok.Click += (s, e) => { DialogResult = true; Close(); };
            var cancel = new Button
            {
                Content = "取消",
                FontSize = 13,
                Padding = new Thickness(18, 7, 18, 7),
                Background = (Brush)Application.Current.Resources["PanelBrush2"],
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                BorderThickness = new Thickness(0),
            };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            Grid.SetRow(btns, 2);
            grid.Children.Add(btns);

            Content = grid;
            Loaded += (s, e) => { _tb.Focus(); _tb.SelectAll(); };
        }
    }

    /// <summary>时间范围(开始/结束)设置对话框,支持跨天。</summary>
    public class TimeRangeDialog : Window
    {
        private readonly ComboBox _sh, _sm, _eh, _em;
        public int StartMinutes { get; private set; }
        public int EndMinutes { get; private set; }

        public TimeRangeDialog(Models.TimePlaylist pl)
        {
            Title = "设置播放时间段";
            Width = 340;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["PanelBrush"];
            Foreground = (Brush)Application.Current.Resources["TextBrush"];
            FontFamily = new FontFamily("Microsoft YaHei UI");
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.ToolWindow;

            var panel = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            panel.Children.Add(new TextBlock
            {
                Text = "开始时间",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["DimTextBrush"],
                Margin = new Thickness(0, 0, 0, 4),
            });
            _sh = MakeTimeBox(pl.StartMinutes / 60);
            _sm = MakeTimeBox(pl.StartMinutes % 60);
            panel.Children.Add(MakeTimeRow(_sh, _sm));

            panel.Children.Add(new TextBlock
            {
                Text = "结束时间",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["DimTextBrush"],
                Margin = new Thickness(0, 14, 0, 4),
            });
            _eh = MakeTimeBox(pl.EndMinutes / 60);
            _em = MakeTimeBox(pl.EndMinutes % 60);
            panel.Children.Add(MakeTimeRow(_eh, _em));

            panel.Children.Add(new TextBlock
            {
                Text = "提示:支持跨天时间段,例如 22:00 - 02:00。到达结束时间后,当前曲播完再自动切换。",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["DimTextBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 12),
            });

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var ok = new Button
            {
                Content = "确定",
                FontSize = 13,
                Padding = new Thickness(20, 7, 20, 7),
                Background = (Brush)Application.Current.Resources["ThemeBrush"],
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
            };
            ok.Click += (s, e) =>
            {
                StartMinutes = int.Parse((string)_sh.SelectedItem) * 60 + int.Parse((string)_sm.SelectedItem);
                EndMinutes = int.Parse((string)_eh.SelectedItem) * 60 + int.Parse((string)_em.SelectedItem);
                DialogResult = true;
                Close();
            };
            var cancel = new Button
            {
                Content = "取消",
                FontSize = 13,
                Padding = new Thickness(18, 7, 18, 7),
                Background = (Brush)Application.Current.Resources["PanelBrush2"],
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                BorderThickness = new Thickness(0),
            };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            panel.Children.Add(btns);

            Content = panel;
        }

        private ComboBox MakeTimeBox(int val)
        {
            var cb = new ComboBox
            {
                Width = 60,
                FontSize = 14,
                Margin = new Thickness(0, 0, 6, 0),
                IsEditable = false,
            };
            for (int i = 0; i < 60; i++)
                cb.Items.Add(i.ToString("00"));
            cb.SelectedIndex = val;
            return cb;
        }

        private StackPanel MakeTimeRow(ComboBox h, ComboBox m)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(h);
            sp.Children.Add(new TextBlock { Text = ":", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(m);
            return sp;
        }
    }
}