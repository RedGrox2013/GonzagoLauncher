using System.Net.Http;
using System.IO;

namespace GonzagoLauncher
{
    class FileDownloader(HttpClient httpClient, int bufferSize = FileDownloader.DEFAULT_BUFFER_SIZE, IProgress<DownloadProgressInfo>? progress = null) : IDisposable
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly IProgress<DownloadProgressInfo>? _progress = progress;

        public const int DEFAULT_BUFFER_SIZE = 8192;
        public readonly int BufferSize = bufferSize;

        public FileDownloader(int bufferSize = DEFAULT_BUFFER_SIZE, IProgress<DownloadProgressInfo>? progress = null) : this(new(), bufferSize, progress) { }

        public async Task DownloadAsync(string uri, string downloadPath, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var progressInfo = new DownloadProgressInfo(0, response.Content.Headers.ContentLength ?? -1L);
            _progress?.Report(progressInfo);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var outStream = File.Create(downloadPath);

            var buffer = new byte[BufferSize];
            var lastTime = DateTime.Now;
            var isMoreRead = true;

            while (isMoreRead)
            {
                var readBytes = await contentStream.ReadAsync(buffer, 0, BufferSize, cancellationToken);
                if (readBytes == 0)
                {
                    isMoreRead = false;
                    break;
                }

                await outStream.WriteAsync(buffer, 0, readBytes, cancellationToken);
                progressInfo.BytesDownloaded += readBytes;

                var now = DateTime.Now;
                if ((now - lastTime).TotalSeconds >= 0.1)
                {
                    lastTime = now;
                    _progress?.Report(progressInfo);
                }
            }
        }

        public async Task<string> DownloadTempFileAsync(string uri, CancellationToken cancellationToken = default)
        {
            string path = Path.GetTempFileName();
            await DownloadAsync(uri, path, cancellationToken);

            return path;
        }

        #region IDisposable realization
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                    _httpClient.Dispose();
                }

                // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить метод завершения
                // TODO: установить значение NULL для больших полей
                _disposedValue = true;
            }
        }

        // // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
        // ~FileDownloader()
        // {
        //     // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    class DownloadProgressInfo(long bytesDownloaded, long totalBytes)
    {
        public long BytesDownloaded { get; set; } = bytesDownloaded;
        public readonly long TotalBytes = totalBytes;
        public double PercentDownloaded => Math.Round(BytesDownloaded * (TotalBytes / 100d), 1);
    }
}
