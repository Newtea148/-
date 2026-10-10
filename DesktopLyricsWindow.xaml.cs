using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TimeMusic
{
    public partial class DesktopLyricsWindow : Window
    {

        public DesktopLyricsWindow()
        {
            InitializeComponent();
            PositionBottomRight();
        }

        private void PositionBottomRight()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 24;
            Top = wa.Bottom - Height - 40;
        }

        public void SetLyrics(string text)
        {
            if (LyricText != null)
                LyricText.Text = text;
        }

        public void SetColor(Color c)
        {
            if (LyricText != null)
                LyricText.Foreground = new SolidColorBrush(Color.FromArgb(230, c.R, c.G, c.B));
        }

        private void Win_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Bigger_Click(object sender, RoutedEventArgs e)
        {
            LyricText.FontSize += 2;
            PositionBottomRight();
        }

        private void Smaller_Click(object sender, RoutedEventArgs e)
        {
            if (LyricText.FontSize > 12)
            {
                LyricText.FontSize -= 2;
                PositionBottomRight();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}