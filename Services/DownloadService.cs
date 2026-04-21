using Hanime1Downloader.CSharp.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Hanime1Downloader.CSharp.Services;

public sealed class DownloadService(HttpClient httpClient, string siteHost = "hanime1.me")
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly Uri _referrer = new($"https://{siteHost}/");

    public async Task<DownloadProbeResult> ProbeAsync(string url, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, url, 0, 0);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new DownloadProbeResult
        {
            ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            ContentLength = response.Content.Headers.ContentLength,
            IsPartial = response.StatusCode == HttpStatusCode.PartialContent
        };
    }

    public async Task DownloadAsync(string url, string outputPath, IProgress<DownloadProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        var tmpPath = outputPath + ".tmp";
        var existingBytes = File.Exists(tmpPath) ? new FileInfo(tmpPath).Length : 0L;
        var requestedResume = existingBytes > 0;

        using var request = CreateRequest(HttpMethod.Get, url, requestedResume ? existingBytes : null, null);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var isResume = requestedResume && response.StatusCode == HttpStatusCode.PartialContent;
        if (!isResume)
        {
            response.EnsureSuccessStatusCode();
        }

        var contentLength = response.Content.Headers.ContentLength;
        var totalBytes = contentLength is long cl ? (isResume ? existingBytes + cl : cl) : (long?)null;

        var fileMode = isResume ? FileMode.Append : FileMode.Create;
        var bytesReceived = isResume ? existingBytes : 0L;
        var startedAt = DateTime.UtcNow;
        var lastReportedBytes = bytesReceived;
        var lastReportedAt = Environment.TickCount64;
        var keepPartialFile = requestedResume;
        var moved = false;
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(tmpPath, fileMode, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReceived += bytesRead;
                keepPartialFile = bytesReceived > 0;

                var now = Environment.TickCount64;
                var shouldReport = bytesReceived == totalBytes ||
                                   bytesReceived - lastReportedBytes >= 512 * 1024 ||
                                   now - lastReportedAt >= 150;
                if (shouldReport)
                {
                    var elapsedSeconds = Math.Max((DateTime.UtcNow - startedAt).TotalSeconds, 0.001d);
                    progress?.Report(new DownloadProgressInfo
                    {
                        BytesReceived = bytesReceived,
                        TotalBytes = totalBytes,
                        BytesPerSecond = bytesReceived / elapsedSeconds
                    });
                    lastReportedBytes = bytesReceived;
                    lastReportedAt = now;
                }
            }

            if (bytesReceived != lastReportedBytes)
            {
                var elapsedSeconds = Math.Max((DateTime.UtcNow - startedAt).TotalSeconds, 0.001d);
                progress?.Report(new DownloadProgressInfo
                {
                    BytesReceived = bytesReceived,
                    TotalBytes = totalBytes,
                    BytesPerSecond = bytesReceived / elapsedSeconds
                });
            }

        }
        finally
        {
            if (!moved && !keepPartialFile && File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        File.Move(tmpPath, outputPath);
        moved = true;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, long? rangeFrom, long? rangeTo)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Referrer = _referrer;
        if (rangeFrom.HasValue || rangeTo.HasValue)
        {
            request.Headers.Range = new RangeHeaderValue(rangeFrom, rangeTo);
        }
        return request;
    }
}
