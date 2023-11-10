using ControlSync.Properties;
using ModernWpf.Controls;
using ScpDriverInterface;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlSync
{
    /// <summary>
    /// Interaction logic for Mapping.xaml
    /// </summary>
    public partial class Mapping : System.Windows.Controls.Page
    {
        public static Mapping instance;
        public static List<ControllerMap> buttons = new List<ControllerMap>();

        public List<ControllerMap> Buttons { get => buttons;}
        public List<string> Profiles { get => ProfileManager.Profiles; } 

        private X360Buttons? clickedButton;
        private AnalogInput clickedAnalog;
        public Mapping()
        {
            InitializeComponent();
            mappingPg.Background = null;
            instance = this;
            EventManager.RegisterClassHandler(typeof(Window),
            Keyboard.KeyDownEvent, new KeyEventHandler(SetKey), true);

            buttonsList.DataContext = this;
            profiles.DataContext = this;

            profiles.SelectedIndex = Settings.Default.lastSelectedProfile;
        }

        public void AssignButton(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            var name = btn.Name.Split('_');

            if (name[1] == "Btn")
            {
                comment.Text = $"{name[0]} button selected";
                clickedButton = (X360Buttons)Enum.Parse(typeof(X360Buttons), name[0]);
            }
            else
            {
                comment.Text = $"{name[0]} selected";
                clickedAnalog = new AnalogInput
                {
                    Type = (Analog)Enum.Parse(typeof(Analog), name[0]),
                    Value = int.Parse(btn.Content.ToString())
                };
            }
        }

        public void SetKey(object sender, KeyEventArgs e)
        {
            if (clickedButton != null || clickedAnalog != null) {
                var button = Buttons.Find(x => x.PcControl == e.Key || x.XBtnControl != null ? x.XBtnControl == clickedButton : x.XAnalogControl == clickedAnalog);
                if (button != null)
                    buttons.Remove(button);
                else
                    button = new ControllerMap();
                button.PcControl = e.Key;
                button.XBtnControl = clickedButton;
                button.XAnalogControl = clickedAnalog;
                buttons.Add(button);
                if (profiles.SelectedIndex >= 0)
                    ProfileManager.SaveProfile(buttons, profiles.SelectedValue.ToString());

                UpdateList();
                clickedButton = null;
                clickedAnalog = null;
                comment.Text = "Click on any button on the controller and assign a corresponding key from the keyboard";
            }
        }

        private void buttonsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                foreach (var item in buttonsList.SelectedItems)
                    buttons.Remove((ControllerMap)item);
                UpdateList();
            }
        }
        private void UpdateList()
        {
            buttonsList.ItemsSource = null;
            buttonsList.ItemsSource = Buttons;
        }
        private void UpdateProfiles()
        {
            profiles.ItemsSource = null;
            profiles.ItemsSource = Profiles;
        }

        private async void addProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            bool error = false;
            Retry:
            ContentDialog dialog = new ContentDialog();
            dialog.Title = "Add profile";
            dialog.PrimaryButtonText = "Save";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = new AddProfile();
            ((AddProfile)dialog.Content).error.Visibility = error ? Visibility.Visible : Visibility.Collapsed;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string name = ((AddProfile)dialog.Content).name.Text;
                if(string.IsNullOrEmpty(name))
                {
                    error = true;
                    goto Retry;
                }
                SaveProfile(name);
            }
        }
        private void SaveProfile(string name)
        {
            if (profiles.SelectedIndex >= 0)
            {
                buttons = new List<ControllerMap>();
                UpdateList();
            }
            ProfileManager.SaveProfile(buttons, name);
            UpdateProfiles();
            int i = profiles.Items.IndexOf(name);
            profiles.SelectedIndex = i;
        }

        private void profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profiles.SelectedIndex >= 0)
            {
                ComboBox cb = (ComboBox)sender;
                buttons = ProfileManager.LoadProfile(cb.SelectedValue.ToString());
                UpdateList();
                if (Settings.Default.lastSelectedProfile < profiles.Items.Count)
                {
                    Settings.Default.lastSelectedProfile = profiles.SelectedIndex;
                    Settings.Default.Save();
                }                
            }
        }
    }
}
