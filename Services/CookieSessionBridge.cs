using System.Net;
using Hanime1Downloader.CSharp.Models;
using Microsoft.Web.WebView2.Core;

namespace Hanime1Downloader.CSharp.Services;

public sealed class CookieSessionBridge(string siteHost = "hanime1.me")
{
    private readonly Uri _baseUri = new($"https://{siteHost}/");
    private readonly string _defaultDomain = $".{siteHost}";

    public async Task<IReadOnlyList<BrowserCookieRecord>> ExportCookiesAsync(CoreWebView2CookieManager cookieManager)
    {
        var cookies = await cookieManager.GetCookiesAsync(string.Empty);
        return cookies
            .Select(cookie => new BrowserCookieRecord
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? _defaultDomain : cookie.Domain,
                Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                IsSecure = cookie.IsSecure,
                IsHttpOnly = cookie.IsHttpOnly
            })
            .ToList();
    }

    public string BuildCookieHeader(IEnumerable<BrowserCookieRecord> cookies)
    {
        return string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));
    }

    public CookieContainer CreateCookieContainer(IEnumerable<BrowserCookieRecord> cookies)
    {
        var container = new CookieContainer();
        foreach (var record in cookies)
        {
            if (string.IsNullOrWhiteSpace(record.Name) || string.IsNullOrWhiteSpace(record.Value))
            {
                continue;
            }

            try
            {
                var cookie = new Cookie(record.Name, record.Value, string.IsNullOrWhiteSpace(record.Path) ? "/" : record.Path, NormalizeDomain(record.Domain))
                {
                    Secure = record.IsSecure,
                    HttpOnly = record.IsHttpOnly
                };
                container.Add(cookie);
            }
            catch (CookieException ex) { System.Diagnostics.Debug.WriteLine($"[CookieSessionBridge] Skipped cookie '{record.Name}': {ex.Message}"); }
        }
        return container;
    }

    private string NormalizeDomain(string? domain)
    {
        return string.IsNullOrWhiteSpace(domain) ? _defaultDomain : domain.StartsWith('.') ? domain : $".{domain}";
    }
}
