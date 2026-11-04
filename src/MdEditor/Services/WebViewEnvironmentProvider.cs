using Microsoft.Web.WebView2.Core;

namespace VibeCode.MdEditor.Services;

public static class WebViewEnvironmentProvider
{
    private static readonly Lazy<Task<CoreWebView2Environment>> EnvironmentTask = new(
        CreateAsync,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<CoreWebView2Environment> GetAsync()
    {
        return EnvironmentTask.Value;
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MdEditor",
            "WebView2");

        Directory.CreateDirectory(folder);

        return await CoreWebView2Environment.CreateWithOptionsAsync(
            null,
            folder,
            new CoreWebView2EnvironmentOptions());
    }
}
