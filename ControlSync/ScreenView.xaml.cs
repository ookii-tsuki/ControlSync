using System.Windows;
using System.Windows.Input;

namespace ControlSync
{
    /// <summary>
    /// Interaction logic for ScreenView.xaml
    /// </summary>
    public partial class ScreenView : Window
    {
        public static ScreenView instance;
        public ScreenView()
        {
            InitializeComponent();
            instance = this;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;  // cancels the window close    
            this.Hide();
            ClientPg.instance.showScreen.Visibility = Visibility.Visible;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (this.WindowStyle == WindowStyle.None)
                {
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                }
                else
                {
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Maximized;
                }
            }
        }
    }
}
