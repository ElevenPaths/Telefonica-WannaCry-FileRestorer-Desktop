using FirstFloor.ModernUI.Windows.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WannaCryFileFinder;

namespace WannaCryFileRestorer
{
    public partial class MainWindow : ModernWindow
    {
        public ObservableCollection<string> Files { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            this.Files = new ObservableCollection<string>();
            this.fileList.ItemsSource = this.Files;
        }

        private void scanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Files.Clear();
                this.loadingText.Text = "SCANNING FILES...";
                this.loadingBar.Visibility = Visibility.Visible;
                this.scanButton.IsEnabled = false;
                this.recoverButton.IsEnabled = false;
                Task.Factory.StartNew(() =>
                {
                    Action<string> addMethod = this.Files.Add;
                    foreach (string filePath in TempFileFinder.Find())
                    {
                        Dispatcher.BeginInvoke(addMethod, filePath);
                    }

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        this.loadingBar.Visibility = Visibility.Hidden;
                        this.scanButton.IsEnabled = true;
                        if (this.Files.Count == 0)
                        {
                            MessageBox.Show("No files found", "No files", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            this.recoverButton.IsEnabled = true;
                        }
                    }));
                });
            }
            catch
            {
            }
        }

        private void recoverButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Files.Count > 0)
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        this.loadingText.Text = "RESTORING FILES...";
                        this.loadingBar.Visibility = Visibility.Visible;
                        this.scanButton.IsEnabled = false;
                        this.recoverButton.IsEnabled = false;
                        bool overwriteFiles = this.overwriteFiles.IsChecked == true;
                        Task.Factory.StartNew(() =>
                        {
                            TempFileFinder.CopyRecognizedFilesTo(dialog.SelectedPath, overwriteFiles, this.Files.ToArray());
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                this.loadingBar.Visibility = Visibility.Hidden;
                                this.scanButton.IsEnabled = true;
                            }));
                            MessageBox.Show("Files restored succesfully", "Files restored", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                }
            }
        }
    }
}
