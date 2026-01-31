using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace GonzagoLauncher
{
    internal class LauncherService
    {
        public record class UnpackProgressInfo(int CurrentProgress, string? CurrentEntryName, int Total);

        public const string GONZAGO_PATH = "Gonzago";
        public const string APP_NAME = "GonzagoGL.exe";
        public static readonly string PatchPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, "GonzagoGL_flyswim_patch.exe"));
        public static readonly string AppPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, APP_NAME));

        public async Task DownloadGonzagoAsync(IProgress<DownloadProgressInfo> downloadProgress, IProgress<UnpackProgressInfo> unpackProgress)
        {
            string? zipPath = null;
            using HttpClient client = new() { Timeout = TimeSpan.FromHours(2) };
            using FileDownloader downloader = new(client, progress: downloadProgress);

            try
            {
                zipPath = await downloader.DownloadTempFileAsync("http://www.spore.com/static/war/images/community/prototypes/gonzago.zip");
                using var stream = File.OpenRead(zipPath);
                using ZipArchive zip = new(stream);

                for (int i = 0; i < zip.Entries.Count; i++)
                {
                    unpackProgress.Report(new(i, zip.Entries[i].FullName, zip.Entries.Count));

                    if (string.IsNullOrEmpty(zip.Entries[i].Name))
                        continue;

                    string destinationPath = Path.GetFullPath(Path.Combine(GONZAGO_PATH, zip.Entries[i].FullName));

                    string dir = Path.GetDirectoryName(destinationPath) ?? GONZAGO_PATH;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    await zip.Entries[i].ExtractToFileAsync(destinationPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Download failed", ex);
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }

        public void PatchFlySwimMode()
        {
            File.Copy(AppPath, PatchPath, true);
            using var stream = File.OpenWrite(PatchPath);
            stream.Seek(0x155e8, SeekOrigin.Begin);
            stream.WriteByte(0xB8);
        }

        public async Task LaunchAsync(GonzagoMode launchMode, string? arguments = null)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = AppPath,
                WorkingDirectory = Path.GetFullPath(GONZAGO_PATH),
                UseShellExecute = true,
                Verb = "runas"
            };
            if (!string.IsNullOrEmpty(arguments))
                startInfo.Arguments = arguments;

            switch (launchMode)
            {
                case GonzagoMode.FlySwim:
                    if (!File.Exists(PatchPath))
                        PatchFlySwimMode();
                    EditFlySwimIni();

                    startInfo.FileName = PatchPath;
                    break;
                default:
                    await EditDefaultIniAsync();
                    break;
            }

            Process.Start(startInfo);
        }

        public static void EditFlySwimIni()
        {
            string iniPath = Path.Combine(GONZAGO_PATH, "Data", "Gonzago.ini");
            var iniLines = File.ReadAllLines(iniPath);
            File.WriteAllLines(iniPath, [.. iniLines.Where(l => !l.Equals("load_game = predators"))]);
        }

        public static async Task EditDefaultIniAsync()
        {
            string iniPath = Path.Combine(GONZAGO_PATH, "Data", "Gonzago.ini");
            var iniLines = File.ReadAllLines(iniPath);
            if (iniLines.Contains("load_game = predators"))
                return;

            using var sw = File.AppendText(iniPath);
            await sw.WriteLineAsync("load_game = predators");
        }
    }
}
