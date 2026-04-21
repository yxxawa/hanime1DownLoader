using Hanime1Downloader.CSharp.Models;
using Hanime1Downloader.CSharp.Views;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

namespace Hanime1Downloader.CSharp.Services;

public sealed partial class HanimeApiClient(CloudflareWindow browserWindow, string siteHost = "hanime1.me")
{
    private readonly CloudflareWindow _browserWindow = browserWindow;
    private readonly string _siteBase = $"https://{siteHost}";

    public async Task<SearchPageResult> SearchAsync(string keyword, int page = 1, SearchFilterOptions? filters = null, CancellationToken cancellationToken = default)
    {
        var operationId = $"search-{Environment.TickCount64}";
        var normalizedPage = Math.Max(1, page);
        var queryString = BuildSearchQueryString(keyword, normalizedPage, filters);
        Debug.WriteLine($"[{operationId}] Search fetch: page={normalizedPage}, keyword={keyword}");
        var response = await _browserWindow.FetchHtmlAsync($"search?{queryString}", cancellationToken);
        EnsureNotBlocked(response);
        var result = await Task.Run(() => ParseSearchResult(response, normalizedPage), cancellationToken);
        Debug.WriteLine($"[{operationId}] Search parsed: page={result.CurrentPage}, total={result.TotalPages}, count={result.Results.Count}");
        return result;
    }

    private SearchPageResult ParseSearchResult(BrowserFetchResult response, int normalizedPage)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(response.Html);
        var results = new List<VideoSummary>();
        var seen = new HashSet<string>();

        var normalContainers = doc.DocumentNode.SelectNodes("//*[contains(@class, 'content-padding-new')]")?.ToList() ?? [];
        foreach (var container in normalContainers)
        {
            var cards = container.SelectNodes(".//div[starts-with(@class, 'horizontal-card') or contains(@class, 'horizontal-card')]")?.ToList() ?? [];
            foreach (var card in cards)
            {
                AppendNormalSearchItem(results, seen, card);
            }
        }

        var parsedCurrentPage = ParseCurrentPage(doc, normalizedPage);
        var parsedTotalPages = ParseTotalPages(doc, parsedCurrentPage);
        var hasNextPage = HasNextPage(doc, parsedCurrentPage);

        if (results.Count > 0)
        {
            return new SearchPageResult
            {
                CurrentPage = parsedCurrentPage,
                TotalPages = hasNextPage ? Math.Max(parsedTotalPages, parsedCurrentPage + 1) : parsedTotalPages,
                Results = results
            };
        }

        var simplifiedContainers = doc.DocumentNode.SelectNodes("//*[contains(@class, 'home-rows-videos-wrapper')]")?.ToList() ?? [];
        foreach (var container in simplifiedContainers)
        {
            var entries = container.ChildNodes.Where(node => node.NodeType == HtmlNodeType.Element).ToList();
            foreach (var entry in entries)
            {
                AppendSimplifiedSearchItem(results, seen, entry);
            }
        }

        if (results.Count == 0)
        {
            var fallbackLinks = doc.DocumentNode.SelectNodes("//a[@href]")?.ToList() ?? [];
            foreach (var link in fallbackLinks)
            {
                AppendSimplifiedSearchItem(results, seen, link);
            }
        }

        if (results.Count == 0)
        {
            var preview = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? string.Empty).Trim();
            preview = preview.Length > 120 ? preview[..120] : preview;
            throw new InvalidOperationException($"搜索页已打开但未解析到结果。status={response.Status}, url={response.Url}, title={response.Title}, preview={preview}");
        }

        return new SearchPageResult
        {
            CurrentPage = parsedCurrentPage,
            TotalPages = hasNextPage ? Math.Max(parsedTotalPages, parsedCurrentPage + 1) : parsedTotalPages,
            Results = results
        };
    }

    private static string BuildSearchQueryString(string keyword, int page, SearchFilterOptions? filters)
    {
        var parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters["query"] = keyword;
        parameters["page"] = page.ToString();

        if (filters is not null)
        {
            if (!string.IsNullOrWhiteSpace(filters.Genre))
            {
                parameters["genre"] = filters.Genre;
            }

            if (!string.IsNullOrWhiteSpace(filters.Sort))
            {
                parameters["sort"] = filters.Sort;
            }

            if (!string.IsNullOrWhiteSpace(filters.Date))
            {
                parameters["date"] = filters.Date;
            }

            if (!string.IsNullOrWhiteSpace(filters.Duration))
            {
                parameters["duration"] = filters.Duration;
            }

            if (filters.Broad)
            {
                parameters["broad"] = "on";
            }

            if (filters.Tags.Count > 0)
            {
                foreach (var tag in filters.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                {
                    parameters.Add("tags[]", tag);
                }
            }
        }

        return parameters.ToString() ?? string.Empty;
    }

    private static int ParseCurrentPage(HtmlDocument doc, int fallbackPage)
    {
        var currentNode = doc.DocumentNode.SelectSingleNode("//ul[contains(@class, 'pagination')]//*[contains(@class, 'active')]//*[self::a or self::span][contains(@class, 'page-link')]")
                         ?? doc.DocumentNode.SelectSingleNode("//ul[contains(@class, 'pagination')]//*[contains(@class, 'active') and self::a or self::span][contains(@class, 'page-link')]");
        if (currentNode is not null)
        {
            var currentText = HtmlEntity.DeEntitize(currentNode.InnerText ?? string.Empty).Trim();
            if (int.TryParse(currentText, out var currentPage))
            {
                return currentPage;
            }
        }

        return fallbackPage;
    }

    private static int ParseTotalPages(HtmlDocument doc, int currentPage)
    {
        var pageNumbers = ExtractPageNumbers(doc);
        var hasNextPage = HasNextPage(doc, currentPage);

        if (pageNumbers.Count > 0)
        {
            var calculatedTotalPages = pageNumbers.Max();
            if (hasNextPage && currentPage >= calculatedTotalPages)
            {
                calculatedTotalPages = currentPage + 1;
            }

            return Math.Max(1, calculatedTotalPages);
        }

        return hasNextPage ? currentPage + 1 : Math.Max(1, currentPage);
    }

    private static List<int> ExtractPageNumbers(HtmlDocument doc)
    {
        var pageNumbers = new HashSet<int>();
        var paginationNodes = doc.DocumentNode.SelectNodes("//ul[contains(@class, 'pagination')]//*[self::a or self::span][contains(@class, 'page-link')]")?.ToList() ?? [];
        foreach (var node in paginationNodes)
        {
            var href = node.GetAttributeValue("href", string.Empty);
            var pageMatch = PageNumberRegex().Match(href);
            if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out var pageFromHref))
            {
                pageNumbers.Add(pageFromHref);
            }

            var text = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();
            if (int.TryParse(text, out var pageFromText))
            {
                pageNumbers.Add(pageFromText);
            }
        }

        return pageNumbers.OrderBy(page => page).ToList();
    }

    private static bool HasNextPage(HtmlDocument doc, int currentPage)
    {
        var paginationLinks = doc.DocumentNode.SelectNodes("//ul[contains(@class, 'pagination')]//a[@href]")?.ToList() ?? [];
        foreach (var link in paginationLinks)
        {
            var text = HtmlEntity.DeEntitize(link.InnerText ?? string.Empty).Trim();
            var href = link.GetAttributeValue("href", string.Empty);
            var className = link.GetAttributeValue("class", string.Empty);
            var rel = link.GetAttributeValue("rel", string.Empty);
            var pageMatch = PageNumberRegex().Match(href);
            if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out var pageNumber) && pageNumber > currentPage)
            {
                return true;
            }

            if (NextPageTextRegex().IsMatch(text) ||
                className.Contains("next", StringComparison.OrdinalIgnoreCase) ||
                rel.Contains("next", StringComparison.OrdinalIgnoreCase) ||
                NextPageHrefRegex().IsMatch(href))
            {
                return true;
            }
        }

        return false;
    }

    private void AppendNormalSearchItem(List<VideoSummary> results, HashSet<string> seen, HtmlNode card)
        => AppendSearchItem(results, seen, card, [
            ".//div[contains(@class, 'title')]",
            ".//h4[contains(@class, 'video-title')]",
            ".//*[@title]"
        ], resolveHref: true);

    private void AppendSimplifiedSearchItem(List<VideoSummary> results, HashSet<string> seen, HtmlNode node)
        => AppendSearchItem(results, seen, node, [
            ".//div[contains(@class, 'home-rows-videos-title')]",
            ".//div[contains(@class, 'title')]",
            ".//h4[contains(@class, 'video-title')]",
            ".//*[@title]"
        ], resolveHref: false);

    private void AppendSearchItem(List<VideoSummary> results, HashSet<string> seen, HtmlNode node, string[] titleSelectors, bool resolveHref)
    {
        var href = resolveHref
            ? node.SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", string.Empty) ?? string.Empty
            : node.GetAttributeValue("href", string.Empty);

        if (string.IsNullOrWhiteSpace(href) && !resolveHref)
        {
            var linkNode = node.SelectSingleNode(".//a[@href]");
            if (linkNode is not null)
            {
                node = linkNode;
                href = node.GetAttributeValue("href", string.Empty);
            }
        }

        var match = VideoIdRegex().Match(href);
        if (!match.Success) return;

        var id = match.Groups[1].Value;
        if (!seen.Add(id)) return;

        HtmlNode? titleNode = null;
        foreach (var selector in titleSelectors)
        {
            titleNode = node.SelectSingleNode(selector);
            if (titleNode is not null) break;
        }

        var coverNode = node.SelectSingleNode(".//img[@src or @data-src or @data-original or @data-lazy-src]");
        var title = ToDisplayText(titleNode?.InnerText?.Trim(), titleNode?.GetAttributeValue("title", string.Empty) ?? $"视频 {id}");
        if (string.IsNullOrWhiteSpace(title))
        {
            seen.Remove(id);
            return;
        }

        results.Add(new VideoSummary
        {
            VideoId = id,
            Title = title,
            Url = $"{_siteBase}/watch?v={id}",
            CoverUrl = ExtractCoverUrl(coverNode)
        });
    }

    public async Task<VideoDetails?> GetDetailsAsync(string videoId, VideoDetailsLoadOptions loadOptions = VideoDetailsLoadOptions.All, CancellationToken cancellationToken = default)
    {
        var watchResponse = await _browserWindow.FetchHtmlAsync($"watch?v={videoId}", cancellationToken);
        EnsureNotBlocked(watchResponse);

        BrowserFetchResult? downloadResponse = null;
        var parsedDetails = await Task.Run(() => ParseWatchDetails(videoId, watchResponse, loadOptions), cancellationToken);
        if (loadOptions.HasFlag(VideoDetailsLoadOptions.Sources) && parsedDetails.Sources.Count == 0)
        {
            downloadResponse = await _browserWindow.FetchHtmlAsync($"download?v={videoId}", cancellationToken);
            EnsureNotBlocked(downloadResponse);
            parsedDetails = await Task.Run(() => MergeDownloadSources(parsedDetails, downloadResponse.Html), cancellationToken);
        }

        if (loadOptions.HasFlag(VideoDetailsLoadOptions.Sources))
        {
            parsedDetails.Sources = parsedDetails.Sources
                .DistinctBy(item => item.Url)
                .OrderByDescending(item => item.Quality)
                .ThenBy(item => item.Type.Contains("mp4", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }

        parsedDetails.LoadOptions = loadOptions;
        return parsedDetails;
    }

    private VideoDetails ParseWatchDetails(string videoId, BrowserFetchResult watchResponse, VideoDetailsLoadOptions loadOptions)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(watchResponse.Html);

        var titleNode = doc.DocumentNode.SelectSingleNode("//*[@id='shareBtn-title']")
                        ?? doc.DocumentNode.SelectSingleNode("//title");
        var title = ToDisplayText(titleNode?.InnerText?.Trim(), $"视频 {videoId}");

        var details = new VideoDetails
        {
            VideoId = videoId,
            Title = title,
            Url = $"{_siteBase}/watch?v={videoId}",
            LoadOptions = loadOptions
        };

        if (loadOptions.HasFlag(VideoDetailsLoadOptions.Cover))
        {
            var coverNode = doc.DocumentNode.SelectSingleNode("//*[@property='og:image']")
                            ?? doc.DocumentNode.SelectSingleNode("//meta[@name='og:image']")
                            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'plyr__poster') or contains(@class, 'cover') or contains(@class, 'poster')]");
            details.CoverUrl = ExtractCoverUrl(coverNode);
        }

        if (loadOptions.HasFlag(VideoDetailsLoadOptions.Meta) || loadOptions.HasFlag(VideoDetailsLoadOptions.Tags))
        {
            var infoPanel = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-description-panel')]");
            var infoText = HtmlEntity.DeEntitize(infoPanel?.InnerText?.Trim() ?? string.Empty);
            if (loadOptions.HasFlag(VideoDetailsLoadOptions.Meta))
            {
                details.UploadDate = ToDisplayText(ExtractFirstMatch(infoText, DateRegex()));
                details.Views = ToDisplayText(ExtractFirstMatch(infoText, ViewsRegex()));
                details.Duration = ToDisplayText(doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'card-mobile-duration')]")?.InnerText?.Trim());
                details.Likes = ToDisplayText(doc.DocumentNode.SelectSingleNode("//*[@id='video-like-btn']")?.InnerText?.Trim());
                details.Description = ToDisplayText(doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-caption-text')]")?.InnerText?.Trim());
            }

            if (loadOptions.HasFlag(VideoDetailsLoadOptions.Tags))
            {
                details.Tags = doc.DocumentNode.SelectNodes("//*[contains(@class, 'single-video-tag')]//a[@href]")?
                    .Select(node => ToDisplayText(node.InnerText.Trim()).TrimStart('#'))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct()
                    .ToList() ?? [];
            }
        }

        if (loadOptions.HasFlag(VideoDetailsLoadOptions.RelatedVideos))
        {
            details.RelatedVideos = ParseRelatedVideos(doc);
        }

        if (loadOptions.HasFlag(VideoDetailsLoadOptions.Sources))
        {
            AppendSourcesFromWatchPage(details.Sources, doc, watchResponse.Html);
        }
        return details;
    }

    private VideoDetails MergeDownloadSources(VideoDetails details, string downloadHtml)
    {
        AppendSourcesFromDownloadPage(details.Sources, downloadHtml);
        return details;
    }

    private List<VideoSummary> ParseRelatedVideos(HtmlDocument doc)
    {
        var results = new List<VideoSummary>();
        var seen = new HashSet<string>();
        var relatedNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'related-watch-wrap')]")?.ToList() ?? [];
        foreach (var item in relatedNodes)
        {
            var linkNode = item.SelectSingleNode(".//a[contains(@class, 'overlay') and @href]")
                           ?? item.SelectSingleNode(".//a[@href]");
            var href = linkNode?.GetAttributeValue("href", string.Empty) ?? string.Empty;
            var match = VideoIdRegex().Match(href);
            if (!match.Success)
            {
                continue;
            }

            var videoId = match.Groups[1].Value;
            if (!seen.Add(videoId))
            {
                continue;
            }

            var titleNode = item.SelectSingleNode(".//div[contains(@class, 'home-rows-videos-title')]")
                            ?? item.SelectSingleNode(".//div[contains(@class, 'card-mobile-title')]")
                            ?? item.SelectSingleNode(".//*[@title]");
            var title = ToDisplayText(titleNode?.InnerText?.Trim(), titleNode?.GetAttributeValue("title", string.Empty) ?? $"视频 {videoId}");
            var coverNode = item.SelectSingleNode(".//img[@src or @data-src or @data-original or @data-lazy-src]");
            results.Add(new VideoSummary
            {
                VideoId = videoId,
                Title = title,
                Url = $"{_siteBase}/watch?v={videoId}",
                CoverUrl = ExtractCoverUrl(coverNode)
            });
        }

        return results;
    }

    private void AppendSourcesFromWatchPage(List<VideoSource> sources, HtmlDocument doc, string html)
    {
        var sourceNodes = doc.DocumentNode.SelectNodes("//video[@id='player']//source")?.ToList()
                          ?? new List<HtmlNode>();
        foreach (var source in sourceNodes)
        {
            var src = source.GetAttributeValue("src", string.Empty);
            if (string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            AppendSource(
                sources,
                src,
                ParseQuality(source.GetAttributeValue("size", string.Empty)),
                source.GetAttributeValue("type", "video/mp4"));
        }

        foreach (Match match in SourceRegex().Matches(html))
        {
            AppendSource(sources, HttpUtility.HtmlDecode(match.Value));
        }

        foreach (Match match in JsSourceRegex().Matches(html))
        {
            AppendSource(sources, HttpUtility.HtmlDecode(match.Groups[1].Value));
        }

        foreach (Match match in ScriptUrlRegex().Matches(html))
        {
            AppendSource(sources, HttpUtility.HtmlDecode(match.Groups[1].Value));
        }
    }

    private void AppendSourcesFromDownloadPage(List<VideoSource> sources, string downloadHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(downloadHtml);

        var dataUrlNodes = doc.DocumentNode.SelectNodes("//a[@data-url]")?.ToList() ?? new List<HtmlNode>();
        foreach (var node in dataUrlNodes)
        {
            var dataUrl = node.GetAttributeValue("data-url", string.Empty);
            var quality = ParseQualityFromText(node.ParentNode?.InnerText ?? node.InnerText);
            AppendSource(sources, HttpUtility.HtmlDecode(dataUrl), quality);
        }

        var hrefNodes = doc.DocumentNode.SelectNodes("//a[@href]")?.ToList() ?? new List<HtmlNode>();
        foreach (var node in hrefNodes)
        {
            var href = node.GetAttributeValue("href", string.Empty);
            if (!LooksLikeMediaUrl(href))
            {
                continue;
            }

            var quality = ParseQualityFromText(node.InnerText);
            AppendSource(sources, HttpUtility.HtmlDecode(href), quality);
        }

        var sourceNodes = doc.DocumentNode.SelectNodes("//video//source[@src]")?.ToList() ?? new List<HtmlNode>();
        foreach (var node in sourceNodes)
        {
            var src = node.GetAttributeValue("src", string.Empty);
            var quality = ParseQuality(node.GetAttributeValue("size", string.Empty));
            AppendSource(sources, HttpUtility.HtmlDecode(src), quality, node.GetAttributeValue("type", string.Empty));
        }

        foreach (Match match in SourceRegex().Matches(downloadHtml))
        {
            AppendSource(sources, HttpUtility.HtmlDecode(match.Value));
        }

        foreach (Match match in DownloadUrlRegex().Matches(downloadHtml))
        {
            AppendSource(sources, HttpUtility.HtmlDecode(match.Groups[1].Value));
        }
    }

    private void AppendSource(List<VideoSource> sources, string rawUrl, int? quality = null, string? type = null)
    {
        var decoded = HttpUtility.HtmlDecode(rawUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return;
        }

        var hintedByType = !string.IsNullOrWhiteSpace(type) && type.Contains("video", StringComparison.OrdinalIgnoreCase);
        if (!hintedByType && !LooksLikeMediaUrl(decoded))
        {
            return;
        }

        var url = NormalizeUrl(decoded);
        if (sources.Any(item => item.Url == url))
        {
            return;
        }

        sources.Add(new VideoSource
        {
            Url = url,
            Type = string.IsNullOrWhiteSpace(type)
                ? (url.Contains("m3u8", StringComparison.OrdinalIgnoreCase) ? "application/x-mpegURL" : "video/mp4")
                : type,
            Quality = quality ?? ParseQualityFromText(url)
        });
    }

    private static void EnsureNotBlocked(BrowserFetchResult response)
    {
        if (response.Html.Contains("Performing security verification", StringComparison.OrdinalIgnoreCase) ||
            response.Html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
            response.Html.Contains("Enable JavaScript and cookies to continue", StringComparison.OrdinalIgnoreCase) ||
            response.Html.Contains("window._cf_chl_opt", StringComparison.OrdinalIgnoreCase) ||
            response.Html.Contains("cf-mitigated", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("请求被 Cloudflare 挑战页拦截，请重新验证。页面仍是 Cloudflare 验证页。");
        }

        if (response.Status == 403)
        {
            throw new InvalidOperationException("站点返回 403，当前浏览器会话未被接受。请在验证窗口中先确认主页已正常打开。");
        }
    }

    private static bool LooksLikeMediaUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        var lowered = rawUrl.Trim().ToLowerInvariant();
        if (lowered.Contains("cdnjs.cloudflare.com") || lowered.Contains("cdn.jsdelivr.net"))
        {
            return false;
        }

        var pathPart = lowered.Split('?')[0];
        return pathPart.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || pathPart.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeUrl(string src)
    {
        if (src.StartsWith("//", StringComparison.OrdinalIgnoreCase))
        {
            return $"https:{src}";
        }

        return src.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? src : $"{_siteBase}{src}";
    }

    private static string ExtractFirstMatch(string input, Regex regex)
    {
        var match = regex.Match(input ?? string.Empty);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ToDisplayText(string? value, string fallback = "")
    {
        var text = HtmlEntity.DeEntitize(value?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = fallback;
        }

        return SimplifiedChineseConverter.ToSimplified(text);
    }

    private string ExtractCoverUrl(HtmlNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var rawUrl = node.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)
            ? node.GetAttributeValue("content", string.Empty)
            : node.GetAttributeValue("src",
                node.GetAttributeValue("data-src",
                node.GetAttributeValue("data-original",
                node.GetAttributeValue("data-lazy-src", string.Empty))));
        return string.IsNullOrWhiteSpace(rawUrl) ? string.Empty : NormalizeUrl(HttpUtility.HtmlDecode(rawUrl));
    }

    private static int ParseQuality(string raw)
    {
        return int.TryParse(raw.Replace("p", string.Empty, StringComparison.OrdinalIgnoreCase), out var quality)
            ? quality
            : 0;
    }

    private static int ParseQualityFromText(string raw)
    {
        var match = QualityRegex().Match(raw ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, out var quality) ? quality : 0;
    }

    [GeneratedRegex(@"v=(\d+)")]
    private static partial Regex VideoIdRegex();

    [GeneratedRegex(@"(\d{4}-\d{2}-\d{2})")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"观看次数[：:]\s*([^\s]+)")]
    private static partial Regex ViewsRegex();

    [GeneratedRegex("https?://[^\"'\\s>]+\\.(?:mp4|m3u8)[^\"'\\s>]*", RegexOptions.IgnoreCase)]
    private static partial Regex SourceRegex();

    [GeneratedRegex("const\\s+source\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase)]
    private static partial Regex JsSourceRegex();

    [GeneratedRegex("(?:source|src)\\s*[:=]\\s*['\"](https?:\\/\\/[^'\"]+|\\/\\/[^'\"]+|[^'\"]+\\.(?:mp4|m3u8)[^'\"]*)['\"]", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptUrlRegex();

    [GeneratedRegex("data-url=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadUrlRegex();

    [GeneratedRegex(@"(\d{3,4})p", RegexOptions.IgnoreCase)]
    private static partial Regex QualityRegex();

    [GeneratedRegex(@"[?&]page=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PageNumberRegex();

    [GeneratedRegex(@"下一頁|下一页|>|»", RegexOptions.IgnoreCase)]
    private static partial Regex NextPageTextRegex();

    [GeneratedRegex(@"next|page=\d+", RegexOptions.IgnoreCase)]
    private static partial Regex NextPageHrefRegex();
}
