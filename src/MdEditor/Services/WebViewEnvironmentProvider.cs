using System.Collections.Concurrent;
using Microsoft.Web.WebView2.Core;

namespace VibeCode.MdEditor.Services;

public static class WebViewEnvironmentProvider
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<CoreWebView2Environment>>> Environments =
        new(StringComparer.OrdinalIgnoreCase);

    public static Task<CoreWebView2Environment> GetAsync()
    {
        return GetAsync(null);
    }

    public static Task<CoreWebView2Environment> GetAsync(string? userDataFolder)
    {
        var folder = string.IsNullOrWhiteSpace(userDataFolder)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MdEditor",
                "WebView2")
            : Path.GetFullPath(userDataFolder);

        Directory.CreateDirectory(folder);

        return Environments.GetOrAdd(
            folder,
            path => new Lazy<Task<CoreWebView2Environment>>(
                () => CreateAsync(path),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static async Task<CoreWebView2Environment> CreateAsync(string folder)
    {
        return await CoreWebView2Environment.CreateWithOptionsAsync(
            null,
            folder,
            new CoreWebView2EnvironmentOptions());
    }
}
