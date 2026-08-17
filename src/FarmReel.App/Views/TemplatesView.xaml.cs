using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class TemplatesView : UserControl
    {
        private PostsViewModel _vm;
        public PostsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public TemplatesView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private TemplateRow Selected => Grid.SelectedItem as TemplateRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Ask("Template name", "");
            if (string.IsNullOrEmpty(name)) return;
            var category = InputBox.Ask("Category (post|active|login|reg)", "post");
            var caption = InputBox.Ask("Caption text", "");
            var comment = InputBox.Ask("Comment text", "");
            var hashtags = InputBox.Ask("Hashtags", "");
            var dailyLimit = int.TryParse(InputBox.Ask("Daily limit (0 = unlimited)", "0"), out var dl) ? dl : 0;
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                captionText = caption ?? "",
                commentText = comment ?? "",
                hashtagsText = hashtags ?? "",
                commentRandom = false,
                aiCaption = false,
                dailyLimit,
                audience = "public"
            });
            FarmReel.Core.Data.Db.Execute(
                "INSERT INTO Templates(Name,Category,Json,Notes,CreatedAt) VALUES($n,$c,$j,'',$t)",
                ("$n", name), ("$c", category ?? "post"), ("$j", json),
                ("$t", System.DateTime.Now.ToString("o")));
            _vm?.Refresh();
            FarmReel.Core.Utils.Log.Info("Templates", "Saved template: " + name);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => _vm?.DeleteTemplate(Selected);
    }
}
