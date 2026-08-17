using System.Windows;

namespace FarmReel.App.Views
{
    /// <summary>Simple input prompt used by the views.</summary>
    public partial class InputBox : Window
    {
        public InputBox(string title, string initial)
        {
            Title = title;
            Width = 420; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
            var box = new System.Windows.Controls.TextBox { Text = initial ?? "", Margin = new Thickness(0, 0, 0, 10) };
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += (s, e) => { Result = box.Text; DialogResult = true; };
            panel.Children.Add(box);
            panel.Children.Add(ok);
            Content = panel;
            box.Focus();
        }

        public string Result { get; private set; } = "";

        public static string Ask(string title, string initial = "")
        {
            var box = new InputBox(title, initial) { Owner = Application.Current?.MainWindow };
            return box.ShowDialog() == true ? box.Result : null;
        }
    }
}
