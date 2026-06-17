using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

namespace Acai.src;

class Updater
{
    // CRITICAL: Change these to your actual GitHub Organization and Repository!
    private const string GitHubOwner = "/acai-lang"; 
    private const string GitHubRepo = "acai-language";

    public static void Execute(string channel)
    {
        using var client = new HttpClient();
        
        // Fix 1: Add required API headers explicitly
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AcaiLangCompilerUpdater");
	client.DefaultRequestHeaders.Accept.Clear();
	// Replace the previous media type line with this broad wildcard
	client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

                var currentVersion = typeof(Program).Assembly.GetName().Version;
        string currentVersionStr = currentVersion != null 
            ? $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}" 
            : "0.1.0";

        Console.WriteLine($"🔄 Local version: v{currentVersionStr}");

        // Make sure there are NO 'return;' statements right here!

        try // This will no longer be underlined
        {
            string apiUrl = $"https://github.com{GitHubOwner}/{GitHubRepo}/releases";
            
            // Get the response headers first
            var response = client.GetAsync(apiUrl).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Update Failure: GitHub server returned a status error code ({response.StatusCode}).");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("👉 Check if your Organization or Repository name has a typo, or if the repository is private.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("👉 You might have hit the GitHub API hourly rate limit. Try again in an hour.");
                }
                Console.ResetColor();
                return;
            }

            // Now that we know it is a successful 200 OK JSON response, it is safe to read it
            var releases = response.Content.ReadFromJsonAsync<List<GitHubRelease>>().GetAwaiter().GetResult();

            if (releases == null || releases.Count == 0)
            {
                Console.WriteLine("❌ Error: No public releases found on GitHub for this project.");
                return;
            }

            GitHubRelease? targetRelease = null;

            foreach (var release in releases)
            {
                if (channel == "stable" && !release.Prerelease)
                {
                    targetRelease = release;
                    break;
                }
                else if (channel == "beta")
                {
                    targetRelease = release;
                    break;
                }
            }

            if (targetRelease == null)
            {
                Console.WriteLine($"❌ Error: No builds available for the '{channel}' channel.");
                return;
            }

            string latestTagClean = targetRelease.TagName.TrimStart('v', 'V');
            
            // Strip out sub-tags like "-beta.1" before parsing into the native System.Version object
            string versionForParsing = latestTagClean.Split('-')[0];
            var localVersionObj = new Version(currentVersionStr);
            var remoteVersionObj = new Version(versionForParsing);

            if (remoteVersionObj <= localVersionObj)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✨ Up to date! No newer '{channel}' updates available.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🚀 A new {channel} update is available: v{latestTagClean}!");
            Console.WriteLine("📥 Downloading engine binary...");
            Console.ResetColor();

            string? downloadUrl = null;
            foreach (var asset in targetRelease.Assets)
            {
                if (asset.Name.Contains("acai", StringComparison.OrdinalIgnoreCase) || asset.Name.EndsWith(".zip"))
                {
                    downloadUrl = asset.BrowserDownloadUrl;
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl) && targetRelease.Assets.Count > 0)
            {
                downloadUrl = targetRelease.Assets[0].BrowserDownloadUrl;
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Console.WriteLine("❌ Error: No downloadable assets found in this release package.");
                return;
            }

            byte[] fileBytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
            string targetFilename = Path.GetFileName(downloadUrl);
            File.WriteAllBytes(targetFilename, fileBytes);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🎉 Success! Saved update file: {targetFilename}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Update Failure: {ex.Message}");
            Console.ResetColor();
        }
    }
}

public record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("assets")] List<GitHubAsset> Assets
);

public record GitHubAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl
);
