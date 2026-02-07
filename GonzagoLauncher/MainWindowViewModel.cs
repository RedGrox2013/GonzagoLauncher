using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace GonzagoLauncher
{
    class MainWindowViewModel : ObservableRecipient
    {
        private readonly LauncherService _launcher = new();

        public ICommand PlayButtonCommand { get; private set; }

        public ReadOnlyCollection<GonzagoMode> Modes { get; } = [
            GonzagoMode.None,
            GonzagoMode.TribeMode,
            GonzagoMode.CivMode,
            GonzagoMode.FlySwim,
            ];
        private int _selectedModeIndex;
        public int SelectedModeIndex
        {
            get => _selectedModeIndex;
            set => SetProperty(ref _selectedModeIndex, value);
        }

        private string? _cmdLineArgs;
        public string? CommandLineArguments
        {
            get => _cmdLineArgs;
            set => SetProperty(ref _cmdLineArgs, value);
        }

        private bool _isIndeterminate = false;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private int _progressValue = 0;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }
        private int _maxProgressValue = 100;
        public int MaxProgressValue
        {
            get => _maxProgressValue;
            set => SetProperty(ref _maxProgressValue, value);
        }

        private bool _playButtonIsEnabled = true;
        public bool PlayButtonIsEnabled
        {
            get => _playButtonIsEnabled;
            set => SetProperty(ref _playButtonIsEnabled, value);
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public MainWindowViewModel()
        {
            PlayButtonCommand = new AsyncRelayCommand(PlayButtonClickAsync);
        }

        private async Task PlayButtonClickAsync()
        {
            PlayButtonIsEnabled = false;

            try
            {
                if (!Directory.Exists(LauncherService.GONZAGO_PATH))
                    await DownloadGonzagoAsync();

                await _launcher.LaunchAsync(Modes[SelectedModeIndex], CommandLineArguments);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PlayButtonIsEnabled = true;
            }
        }

        private async Task DownloadGonzagoAsync()
        {
            IsIndeterminate = true;
            Status = "Downloading GonzagoGL...";

            try
            {
                await _launcher.DownloadGonzagoAsync(new Progress<DownloadProgressInfo>(p =>
                {
                    IsIndeterminate = false;
                    ProgressValue = (int)p.PercentDownloaded;
                    Status = $"Downloading GonzagoGL: {p.BytesDownloaded} / {p.TotalBytes} bytes ({p.PercentDownloaded})";
                }), new Progress<LauncherService.UnpackProgressInfo>(p =>
                {
                    ProgressValue = p.CurrentProgress;
                    Status = $"Unpacking \"{p.CurrentEntryName}\"";
                    MaxProgressValue = p.Total;
                }));

                PatchFlySwimMode();
            }
            catch { throw; }
            finally
            {
                IsIndeterminate = false;
                ProgressValue = 0;
                MaxProgressValue = 100;
                Status = null;
            }
        }

        private void PatchFlySwimMode()
        {
            IsIndeterminate = true;
            string? oldStatus = Status;
            Status = "Patching fly/swim mode...";

            try
            {
                _launcher.PatchFlySwimMode();
            }
            catch { throw; }
            finally
            {
                IsIndeterminate = false;
                Status = oldStatus;
            }
        }
    }
}
