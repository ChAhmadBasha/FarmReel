using System;
using System.Windows;
using System.Windows.Controls;
using FarmReel.App.ViewModels;

namespace FarmReel.App.Views
{
    public partial class PostsView : UserControl
    {
        private PostsViewModel _vm;
        public PostsViewModel ViewModel
        {
            get => _vm;
            set { _vm = value; DataContext = _vm; }
        }

        public PostsView() { InitializeComponent(); }
        public void Refresh() => _vm?.Refresh();

        private PostJobRow Selected => Grid.SelectedItem as PostJobRow;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var pageId = long.TryParse(InputBox.Ask("Page ID", "0"), out var p) ? p : 0;
            var deviceId = long.TryParse(InputBox.Ask("Device ID", "0"), out var d) ? d : 0;
            var pageName = InputBox.Ask("Page name (label)", "");
            var type = InputBox.Ask("Post type (Photo|Video|Reel|Status|Story)", "Photo");
            var folder = InputBox.Ask("Content folder", "");
            var number = int.TryParse(InputBox.Ask("Number of posts per run", "1"), out var n) ? n : 1;
            var schedule = InputBox.Ask("Schedule (WhenRun|Clock|Weekly)", "WhenRun");
            var cron = InputBox.Ask("Cron (HH:mm or sun,mon 09:00)", "12:30");
            var limit = int.TryParse(InputBox.Ask("Daily limit (0 = unlimited)", "0"), out var l) ? l : 0;
            _vm.AddJob(pageId, deviceId, pageName, type, folder, number, schedule, cron, limit);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var job = Selected.Model;

            var caption = InputBox.Ask("Caption text", job.CaptionText);
            if (caption != null) _vm.UpdateJob(Selected, j => j.CaptionText = caption);

            var useCaptionFile = MessageBox.Show("Use caption from file (random line per post)?",
                "Post settings", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            var captionFile = useCaptionFile ? InputBox.Ask("Caption file path", job.CaptionFile) : null;
            _vm.UpdateJob(Selected, j =>
            {
                j.CaptionFromFile = useCaptionFile;
                if (captionFile != null) j.CaptionFile = captionFile;
            });

            var aiCap = MessageBox.Show("AI caption fallback when no caption found?",
                "Post settings", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            _vm.UpdateJob(Selected, j => j.AiCaption = aiCap);

            var comment = InputBox.Ask("Comment text (empty = no comment)", job.CommentText);
            if (comment != null) _vm.UpdateJob(Selected, j => { j.CommentEnabled = comment.Length > 0; j.CommentText = comment; });

            var randComment = MessageBox.Show("Random comment from a file?", "Post settings", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            var commentFile = randComment ? InputBox.Ask("Comment file path", job.CommentFile) : null;
            _vm.UpdateJob(Selected, j =>
            {
                j.CommentRandom = randComment;
                if (commentFile != null) j.CommentFile = commentFile;
            });

            var hashtags = InputBox.Ask("Hashtags (leave empty to keep)", job.HashtagsText);
            if (hashtags != null) _vm.UpdateJob(Selected, j => j.HashtagsText = hashtags);

            var folder = InputBox.Ask("Content folder", job.ContentFolder);
            if (folder != null) _vm.UpdateJob(Selected, j => j.ContentFolder = folder);

            var limit = int.TryParse(InputBox.Ask("Daily post limit (0 = unlimited)", job.DailyLimit.ToString()), out var l) ? l : 0;
            _vm.UpdateJob(Selected, j => j.DailyLimit = limit);

            var randomFolder = MessageBox.Show("Pick content folder randomly from RandomFolderRoot?", "Post settings", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            var randomRoot = randomFolder ? InputBox.Ask("Random folder root path", job.RandomFolderRoot) : null;
            _vm.UpdateJob(Selected, j =>
            {
                j.RandomFolder = randomFolder;
                if (randomRoot != null) j.RandomFolderRoot = randomRoot;
            });
        }

        private void Toggle_Click(object sender, RoutedEventArgs e) => _vm.ToggleJob(Selected);
        private void Reset_Click(object sender, RoutedEventArgs e) => _vm.ResetState(Selected);
        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteJob(Selected);
        private void Run_Click(object sender, RoutedEventArgs e) => _vm.RunNow(Selected);
        private void RunAll_Click(object sender, RoutedEventArgs e) => _vm.RunAllNow();

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose content folder" };
            if (dlg.ShowDialog() == true)
            {
                var files = _vm.PreviewFolder(dlg.FolderName, "", "");
                MessageBox.Show($"Found {files.Count} media files:\n" + string.Join("\n", files.Take(15)),
                    "Folder scan", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void CheckVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Video files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm" };
            if (dlg.ShowDialog() == true) await _vm.CheckVideoAsync(dlg.FileName);
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var name = InputBox.Ask("Template name", "");
            if (string.IsNullOrEmpty(name)) return;
            var notes = InputBox.Ask("Notes", "");
            _vm.SaveTemplate(name, "post", Selected, notes);
        }

        private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var name = InputBox.Ask("Template name to apply", "");
            if (string.IsNullOrEmpty(name)) return;
            foreach (var t in _vm.Templates)
            {
                if (t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    _vm.ApplyTemplate(Selected, t.Model);
                    return;
                }
            }
            MessageBox.Show("Template not found: " + name);
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            var name = TaskName.Text.Trim();
            var cron = TaskCron.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(cron)) return;
            var type = InputBox.Ask("Target (post|active|backup|shutdown)", "post");
            _vm.AddTask(name, cron, type);
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (_vm != null) _vm.Filter = FilterBox.Text;
        }
    }
}
