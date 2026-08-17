using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Acai.src
{
    class Updater
    {
        private const string CurrentVersion = "1.0.0"; // Your language's current version
        private const string GitHubApiUrl = "https://api.github.com";

        /// <summary>
        /// Execute an upgrade by downloading the OS-specific installer from the GitHub releases page.
        /// `channel` may be "release" (stable) or "pre-release"/"beta" to pick a prerelease tag.
        /// </summary>
        public static async Task ExecuteUpgradeAsync(string channel = "release")
        {
            // 1. Save the operating system name in a local variable
            string osName = GetTargetOsName();
            string architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            Console.WriteLine($"[Acai] Running on OS: {osName} ({architecture})");

            // 2. Setup the local target folder path: ./Acai/update
            string baseDirectory = AppContext.BaseDirectory;
            string updateFolder = Path.Combine(baseDirectory, "Acai", "update");
            if (!Directory.Exists(updateFolder)) Directory.CreateDirectory(updateFolder);

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AcaiLanguage-Updater");

            // Determine repository and channel preferences
            const string owner = "acai-lang";
            const string repo = "acai-language";
            bool pickPrerelease = channel?.ToLowerInvariant() is "pre-release" or "prerelease" or "beta";

            Console.WriteLine($"[Acai] Querying GitHub releases for {owner}/{repo} (channel={channel})...");

            List<GitHubRelease>? releases;
            try
            {
                var releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";
                releases = await client.GetFromJsonAsync<List<GitHubRelease>>(releasesUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Acai Error] Failed to query releases: {ex.Message}");
                return;
            }

            if (releases == null || releases.Count == 0)
            {
                Console.WriteLine("[Acai] No releases found for repository.");
                return;
            }

            // select the latest release matching the requested prerelease flag
            var selected = releases.FirstOrDefault(r => r.Prerelease == pickPrerelease) ?? releases.First();
            var selectedTag = selected.TagName;
            Console.WriteLine($"[Acai] Selected tag: {selectedTag} (prerelease={selected.Prerelease})");

            // pick file name by platform
            string fileName = osName switch
            {
                "windows" => "Acai-windows-x64.exe",
                "macos" => "Acai-mac-arm64.pkg",
                "linux" => "Acai-linux-x64.tar.gz",
                _ => throw new InvalidOperationException("Unsupported OS for automatic download")
            };

            var downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{selectedTag}/{fileName}";
            var downloadedAssetPath = Path.Combine(updateFolder, fileName);

            Console.WriteLine($"[Acai] Prepared download URL: {downloadUrl}");

            // check existence before downloading
            try
            {
                using var head = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!head.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Acai Error] Release asset not found: {downloadUrl} (status {(int)head.StatusCode})");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Acai Error] Failed to access {downloadUrl}: {ex.Message}");
                return;
            }

            Console.WriteLine($"[Acai] Downloading {fileName} to {downloadedAssetPath}...");
            await DownloadToPathAsync(client, downloadUrl, downloadedAssetPath);

            // optional checksums
            var checksumsUrl = $"https://github.com/{owner}/{repo}/releases/download/{selectedTag}/checksums.txt";
            var checksumPath = Path.Combine(updateFolder, "checksums.txt");
            try
            {
                using var resp = await client.GetAsync(checksumsUrl, HttpCompletionOption.ResponseHeadersRead);
                if (resp.IsSuccessStatusCode)
                {
                    await DownloadToPathAsync(client, checksumsUrl, checksumPath);
                    Console.WriteLine("[Acai] Verifying checksums...");
                    if (!VerifySha256(downloadedAssetPath, checksumPath, fileName))
                    {
                        Console.WriteLine("[Acai Critical Error] Checksum verification failed!");
                        CleanUpFiles(updateFolder);
                        return;
                    }
                }
            }
            catch { /* ignore if checksums are unavailable */ }

            // simple deploy: for archive installer we leave it to the user; for windows exe or mac pkg we attempt to place into update folder
            Console.WriteLine("[Acai] Download complete. Inspect the downloaded file in the update folder to proceed with installation.");
        }

        private static string GetTargetOsName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
            return "unknown";
        }

        private static async Task DownloadToPathAsync(HttpClient client, string url, string destinationPath)
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using Stream remoteStream = await response.Content.ReadAsStreamAsync();
            using FileStream localFileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await remoteStream.CopyToAsync(localFileStream);
        }

        private static bool VerifySha256(string filePath, string checksumFilePath, string targetFilename)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream fileStream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(fileStream);
            string calculatedHashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            string[] lines = File.ReadAllLines(checksumFilePath);
            foreach (string line in lines)
            {
                string[] segments = line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2) continue;

                if (string.Equals(segments[1].Trim(), targetFilename, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(calculatedHashStr, segments[0].Trim().ToLowerInvariant(), StringComparison.Ordinal);
                }
            }
            return false;
        }

        private static void DeployNewBinary(string stagingBinaryPath, string actualInstallPath, string osName)
        {
            if (osName == "windows")
            {
                string batchScriptPath = Path.Combine(Path.GetTempPath(), "acai_updater_flush.bat");
                string scriptContent = $@"
@echo off
:loop
tasklist | findstr /I ""{Path.GetFileName(actualInstallPath)}"" >nul
if %errorlevel% equ 0 (
    timeout /t 1 /nobreak >nul
    goto loop
)
move /y ""{stagingBinaryPath}"" ""{actualInstallPath}""
rmdir /s /q ""{Path.GetDirectoryName(stagingBinaryPath)}""
del ""{batchScriptPath}""
";
                File.WriteAllText(batchScriptPath, scriptContent);
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{batchScriptPath}\"", CreateNoWindow = true, UseShellExecute = false });
                Environment.Exit(0);
            }
            else // macOS & Linux
            {
                try
                {
                    if (File.Exists(actualInstallPath)) File.Delete(actualInstallPath);
                    File.Move(stagingBinaryPath, actualInstallPath);

                    // This built-in guard tells the compiler this block is 100% safe from Windows
                    if (!OperatingSystem.IsWindows())
                    {
                        // Apply execution flags back to the newly swapped binary in Unix configurations
                        File.SetUnixFileMode(actualInstallPath,
                            UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                            UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    }

                    // Clear temporary update structure cleanly
                    Directory.Delete(Path.GetDirectoryName(stagingBinaryPath)!, true);
                    Console.WriteLine("[Acai] Installation finished successfully!");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("[Acai Error] Privileges Denied. Please re-run update operation via 'sudo acai upgrade'.");
                }
            }

        }
        private static void CleanUpFiles(string folderPath) { try { if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true); } catch { } }
    }
    class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public System.Collections.Generic.List<GitHubAsset> Assets { get; set; } = new();
    }
    class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}