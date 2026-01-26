using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace GonzagoLauncher
{
    class MainWindowViewModel : ObservableRecipient
    {
        public ICommand PlayButtonCommand { get; private set; }

        public ReadOnlyCollection<GonzagoMode> Modes { get; } = [
            GonzagoMode.None,
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

        private const string GONZAGO_PATH = "Gonzago";
        private const string APP_NAME = "GonzagoGL.exe";
        public static readonly string PatchPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, "GonzagoGL_flyswim_patch.exe"));
        public static readonly string AppPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, APP_NAME));

        public MainWindowViewModel()
        {
            PlayButtonCommand = new AsyncRelayCommand(PlayButtonClickAsync);
        }

        private async Task PlayButtonClickAsync()
        {
            PlayButtonIsEnabled = false;

            try
            {
                if (!Directory.Exists(GONZAGO_PATH))
                    await DownloadGonzagoAsync();

                var startInfo = new ProcessStartInfo()
                {
                    FileName = AppPath,
                    WorkingDirectory = Path.GetFullPath(GONZAGO_PATH),
                    UseShellExecute = true,
                    Verb = "runas"
                };
                if (!string.IsNullOrEmpty(CommandLineArguments))
                    startInfo.Arguments = CommandLineArguments;

                switch (Modes[SelectedModeIndex])
                {
                    case GonzagoMode.FlySwim:
                        if (!File.Exists(PatchPath))
                            await PatchFlySwimMode();
                        EditFlySwimIni();

                        startInfo.FileName = PatchPath;
                        break;
                    default:
                        await EditDefaultIniAsync();
                        break;
                }

                Process.Start(startInfo);
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
            Status = "Loading...";
            string? zipPath = null;
            using HttpClient client = new() { Timeout = TimeSpan.FromHours(2) };
            using FileDownloader downloader = new(client, progress: new Progress<DownloadProgressInfo>(p =>
            {
                IsIndeterminate = false;
                ProgressValue = (int)p.PercentDownloaded;
                Status = $"Downloading GonzagoGL: {p.BytesDownloaded} / {p.TotalBytes} bytes ({p.PercentDownloaded})";
            }));

            try
            {
                zipPath = await downloader.DownloadTempFileAsync("http://www.spore.com/static/war/images/community/prototypes/gonzago.zip");
                using var stream = File.OpenRead(zipPath);
                using ZipArchive zip = new(stream);
                ProgressValue = 0;
                MaxProgressValue = zip.Entries.Count;

                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    string destinationPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, entry.FullName));
                    Status = $"Unpacking \"{destinationPath}\"";

                    string dir = Path.GetDirectoryName(destinationPath) ?? GONZAGO_PATH;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    await entry.ExtractToFileAsync(destinationPath, overwrite: true);

                    ProgressValue++;
                }

                await PatchFlySwimMode();
            }
            catch { throw; }
            finally
            {
                IsIndeterminate = false;
                ProgressValue = 0;
                MaxProgressValue = 100;
                Status = null;

                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }

        private async Task PatchFlySwimMode()
        {
            IsIndeterminate = true;
            string? oldStatus = Status;
            Status = "Patching fly/swim mode...";

            try
            {
                File.Copy(AppPath, PatchPath, true);
                using var stream = File.OpenWrite(PatchPath);
                stream.Seek(0x155e8, SeekOrigin.Begin);
                stream.WriteByte(0xB8);
            }
            catch { throw; }
            finally
            {
                IsIndeterminate = false;
                Status = oldStatus;
            }
        }

        private static void EditFlySwimIni()
        {
            string iniPath = Path.Combine(GONZAGO_PATH, "Data", "Gonzago.ini");
            var iniLines = File.ReadAllLines(iniPath);
            File.WriteAllLines(iniPath, [.. iniLines.Where(l => !l.Equals("load_game = predators"))]);
        }

        private static async Task EditDefaultIniAsync()
        {
            string iniPath = Path.Combine(GONZAGO_PATH, "Data", "Gonzago.ini");
            var iniLines = File.ReadAllLines(iniPath);
            if (iniLines.Contains("load_game = predators"))
                return;

            using var sw = File.AppendText(iniPath);
            await sw.WriteLineAsync("load_game = predators");
        }
    }

    enum GonzagoMode
    {
        None,
        FlySwim
    }
}
