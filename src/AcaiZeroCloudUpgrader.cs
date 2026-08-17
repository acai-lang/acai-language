using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Acai.src
{
    /// <summary>
    /// AcaiZeroCloudUpgrader: downloads a plain-text upgrade-matrix from raw.githubusercontent.com
    /// and performs staged binary assembly using patch steps declared as keys like
    /// patch-{from}-to-{to}=https://.../patch.bin
    ///
    /// This implementation respects the constraints described in the task (no GitHub API usage,
    /// streaming downloads, staging files, Windows ghost-swap fallback and cross-platform guards).
    /// </summary>
    public static class AcaiZeroCloudUpgrader
    {
        // Local version is read from metadata/VERSION at runtime. If not found, a fallback of "0.0.0" is used.
        private const string MatrixUrl = "https://raw.githubusercontent.com/acai-lang/acai-language/refs/heads/main/upgrade-matrix.txt";

        public static async Task RunAsync(string channel = "stable", bool freshInstall = false)
        {
            try
            {
                var updateFolder = GetUserUpdateFolder();
                Directory.CreateDirectory(updateFolder);

                // try to remove leftover old file from previous runs as requested
                TryDeleteOldExecutableAtStart();

                Console.WriteLine($"[AcaiUpgrader] Downloading upgrade matrix from {MatrixUrl}...");
                var matrix = await DownloadMatrixAsync(MatrixUrl);
                if (matrix == null)
                {
                    Console.WriteLine("[AcaiUpgrader] Failed to download or parse upgrade matrix.");
                    return;
                }

                if (!matrix.TryGetValue("latest", out var latestRaw) || string.IsNullOrWhiteSpace(latestRaw))
                {
                    Console.WriteLine("[AcaiUpgrader] 'latest=' token not found in matrix.");
                    return;
                }

                var latest = latestRaw.Trim();
                Console.WriteLine($"[AcaiUpgrader] Matrix latest: {latest}");

                var current = ReadLocalVersion();

                // support channel-specific latest token: latest-beta etc.
                string channelKey = channel?.ToLower() == "beta" ? "latest-beta" : "latest";
                if (matrix.TryGetValue(channelKey, out var channelLatest) && !string.IsNullOrWhiteSpace(channelLatest))
                {
                    latest = channelLatest.Trim();
                }

                Console.WriteLine($"[AcaiUpgrader] Using latest for channel '{channel}': {latest}");

                if (string.Equals(current, latest, StringComparison.OrdinalIgnoreCase) && !freshInstall)
                {
                    Console.WriteLine($"[AcaiUpgrader] Already up-to-date (local={current}).");
                    return;
                }

                // If requested, do a fresh install for the channel: download the OS-specific release asset and run installer/extractor
                if (freshInstall)
                {
                    Console.WriteLine("[AcaiUpgrader] Performing fresh install for channel.");
                    await PerformFreshInstallAsync(latest, updateFolder);
                    return;
                }

                var inputBase = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine current process path");
                string assembledStage = string.Empty;

                // loop until current == latest, applying chained patches
                while (!string.Equals(current, latest, StringComparison.OrdinalIgnoreCase))
                {
                    var keyPrefix = $"patch-{current}-to-";
                    string? foundKey = null;
                    foreach (var kv in matrix)
                    {
                        if (kv.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = kv.Key;
                            break;
                        }
                    }

                    if (foundKey == null)
                    {
                        Console.WriteLine($"[AcaiUpgrader] No patch entry found for current version {current}.");
                        return;
                    }

                    var toVersion = foundKey.Substring(keyPrefix.Length);
                    var patchUrl = matrix[foundKey].Trim();
                    Console.WriteLine($"[AcaiUpgrader] Found patch {foundKey} -> {patchUrl}");

                    var patchLocal = Path.Combine(updateFolder, Path.GetFileName(new Uri(patchUrl).LocalPath));
                    await DownloadToFileAsync(patchUrl, patchLocal);

                    // staging path for this step
                    var stagePath = Path.Combine(updateFolder, $"acai_v{toVersion}.stage");

                    Console.WriteLine($"[AcaiUpgrader] Applying patch to create {stagePath} (inputBase={inputBase})...");

                    // Apply patch: attempt BSDIFF if recognized, otherwise treat as full binary replacement
                    await ApplyPatchWithFallbackAsync(inputBase, patchLocal, stagePath);

                    // advance input base for next iteration
                    inputBase = stagePath;
                    assembledStage = stagePath;
                    current = toVersion;

                    Console.WriteLine($"[AcaiUpgrader] Advanced to version {current}.");
                }

                if (string.IsNullOrEmpty(assembledStage) || !File.Exists(assembledStage))
                {
                    Console.WriteLine("[AcaiUpgrader] No assembled stage found after patching.");
                    return;
                }

                // final swap
                var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine current executable path");
                Console.WriteLine($"[AcaiUpgrader] Swapping assembled binary into place: {assembledStage} -> {currentExe}");

                SwapFinalBinary(assembledStage, currentExe);

                Console.WriteLine("[AcaiUpgrader] Upgrade complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AcaiUpgrader Error] {ex.Message}");
            }
        }

        private static void TryDeleteOldExecutableAtStart()
        {
            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe)) return;
                var oldPath = Path.Combine(Path.GetDirectoryName(currentExe)!, Path.GetFileNameWithoutExtension(currentExe) + ".exe.old");
                if (File.Exists(oldPath))
                {
                    try { File.Delete(oldPath); Console.WriteLine($"[AcaiUpgrader] Removed old file {oldPath}"); } catch { }
                }
            }
            catch { }
        }

        private static async Task<Dictionary<string, string>?> DownloadMatrixAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AcaiZeroCloudUpgrader");
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                using var s = await resp.Content.ReadAsStreamAsync();
                using var sr = new StreamReader(s, Encoding.UTF8);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    var raw = await sr.ReadLineAsync();
                    if (raw == null) break;
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#")) continue; // comment
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();
                    dict[k] = v;
                }
                return dict;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AcaiUpgrader] Failed to download matrix: {ex.Message}");
                return null;
            }
        }

        private static string? FindVersionFile()
        {
            string dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "metadata", "VERSION");
                if (File.Exists(candidate)) return candidate;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return null;
        }

        private static string ReadLocalVersion()
        {
            try
            {
                var vf = FindVersionFile();
                if (vf != null)
                {
                    var txt = File.ReadAllText(vf).Trim();
                    if (!string.IsNullOrEmpty(txt)) return txt;
                }
            }
            catch { }
            Console.WriteLine("[AcaiUpgrader] WARNING: metadata/VERSION not found. Using fallback version 0.0.0");
            return "0.0.0";
        }

        private static async Task DownloadToFileAsync(string url, string destinationPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AcaiZeroCloudUpgrader");
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            using var src = await resp.Content.ReadAsStreamAsync();
            using var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dst);
            Console.WriteLine($"[AcaiUpgrader] Downloaded {url} -> {destinationPath}");
        }

        private static async Task ApplyPatchWithFallbackAsync(string inputPath, string patchPath, string outputPath)
        {
            // Quick heuristic: if patch file starts with ASCII "BSDIFF40" we attempt to use a bsdiff library via reflection.
            // Otherwise, treat the downloaded patch as a full binary replacement.
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(patchPath))
            {
                var read = await fs.ReadAsync(header.AsMemory(0, header.Length));
                if (read < header.Length)
                {
                    // short read - treat as fallback full binary
                    header = header[..read];
                }
            }

            var headerStr = Encoding.ASCII.GetString(header);
            if (headerStr.StartsWith("BSDIFF40"))
            {
                Console.WriteLine("[AcaiUpgrader] Detected bsdiff patch format. Attempting to apply via available library.");
                var applied = TryApplyBsdiffViaReflection(inputPath, patchPath, outputPath);
                if (!applied)
                {
                    throw new NotSupportedException("bsdiff patch detected but no suitable bsdiff implementation was found. Please include a bsdiff library or supply full binary patch files.");
                }
                return;
            }

            // Fallback: treat patch as full binary
            Console.WriteLine("[AcaiUpgrader] Treating patch as full binary replacement (fallback).");
            File.Copy(patchPath, outputPath, true);
        }

        private static bool TryApplyBsdiffViaReflection(string oldFile, string patchFile, string outputFile)
        {
            try
            {
                // Search loaded assemblies for a candidate bsdiff implementation
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name ?? string.Empty;
                    if (!name.Contains("BsDiff", StringComparison.OrdinalIgnoreCase) && !name.Contains("Bsdiff", StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (var type in asm.GetTypes())
                    {
                        // common method signatures: static void Patch(string oldFile, string newFile, string patchFile)
                        var m = type.GetMethod("Patch", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (m == null) continue;
                        var pars = m.GetParameters();
                        if (pars.Length == 3)
                        {
                            // try invoking Patch(oldPath, newPath, patchPath)
                            try
                            {
                                m.Invoke(null, new object[] { oldFile, outputFile, patchFile });
                                Console.WriteLine($"[AcaiUpgrader] Applied bsdiff using {type.FullName} from {asm.GetName().Name}");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AcaiUpgrader] Reflection bsdiff invocation failed: {ex.Message}");
                                // continue searching
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AcaiUpgrader] Error while probing for bsdiff: {ex.Message}");
            }
            return false;
        }

        private static void SwapFinalBinary(string assembledPath, string currentExe)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var dir = Path.GetDirectoryName(currentExe) ?? throw new InvalidOperationException("Cannot get exe directory");
                var exeName = Path.GetFileName(currentExe);
                var oldPath = Path.Combine(dir, exeName + ".old");

                try
                {
                    // Attempt to move the running executable to .old (may fail on Windows if file locked)
                    if (File.Exists(oldPath))
                    {
                        try { File.Delete(oldPath); } catch { }
                    }

                    File.Move(currentExe, oldPath);
                    File.Move(assembledPath, currentExe);

                    Console.WriteLine("[AcaiUpgrader] Swap completed using File.Move.");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AcaiUpgrader] Direct File.Move swap failed (expected on Windows): {ex.Message}");
                    // fallback: create a batch script that waits for process to exit then performs swap
                    var batch = Path.Combine(Path.GetTempPath(), "acai_swap.bat");
                    var script = $"@echo off\r\n" +
                                 $":loop\r\n" +
                                 $"tasklist | findstr /I \"{exeName}\" >nul\r\n" +
                                 $"if %errorlevel% equ 0 (\r\n" +
                                 $"  timeout /t 1 /nobreak >nul\r\n" +
                                 $"  goto loop\r\n" +
                                 $")\r\n" +
                                 $"move /y \"{assembledPath}\" \"{currentExe}\"\r\n" +
                                 $"move /y \"{currentExe}\" \"{oldPath}\" 2>nul\r\n" +
                                 $"del \"{batch}\"\r\n";

                    File.WriteAllText(batch, script);
                    Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{batch}\"", CreateNoWindow = true, UseShellExecute = false });
                    Console.WriteLine("[AcaiUpgrader] Swap batched via helper batch script; exiting to allow swap.");
                    Environment.Exit(0);
                }
            }
            else
            {
                // Unix platforms: move and set file mode
                try
                {
                    if (File.Exists(currentExe)) File.Delete(currentExe);
                    File.Move(assembledPath, currentExe);
                    if (!OperatingSystem.IsWindows())
                    {
                        try
                        {
                            File.SetUnixFileMode(currentExe,
                                UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                        }
                        catch { }
                    }
                    Console.WriteLine("[AcaiUpgrader] Swap completed on Unix-like platform.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AcaiUpgrader] Unix swap failed: {ex.Message}");
                }
            }
        }

        private static string GetUserUpdateFolder()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "Acai", "update");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return Path.Combine(home, "Library", "Application Support", "Acai", "update");
            }

            var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(linuxHome, ".local", "share", "Acai", "update");
        }

        private static async Task PerformFreshInstallAsync(string tagName, string updateFolder)
        {
            // build OS-specific filename
            string osName = GetTargetOsName();
            string fileName = osName switch
            {
                "windows" => "Acai-windows-x64.exe",
                "macos" => "Acai-mac-arm64.pkg",
                "linux" => "Acai-linux-x64.tar.gz",
                _ => throw new InvalidOperationException("Unsupported OS for automatic download")
            };

            var owner = "acai-lang";
            var repo = "acai-language";
            var downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{tagName}/{fileName}";
            var localPath = Path.Combine(updateFolder, fileName);

            Console.WriteLine($"[AcaiUpgrader] Fresh install: downloading {downloadUrl} -> {localPath}");
            await DownloadToFileAsync(downloadUrl, localPath);

            Console.WriteLine("[AcaiUpgrader] Running platform installer for fresh install...");
            InstallDownloadedAsset(localPath, osName);

            try { File.Delete(localPath); } catch { }
        }

        private static void InstallDownloadedAsset(string assetPath, string osName)
        {
            if (osName == "windows")
            {
                var start = new ProcessStartInfo
                {
                    FileName = assetPath,
                    UseShellExecute = true,
                };
                var p = Process.Start(start);
                p?.WaitForExit();
                if (p != null && p.ExitCode != 0)
                    throw new InvalidOperationException($"Installer exited with code {p.ExitCode}");
                return;
            }

            if (osName == "macos")
            {
                if (assetPath.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = "installer",
                        Arguments = $"-pkg \"{assetPath}\" -target /",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    var p = Process.Start(start);
                    p?.WaitForExit();
                    if (p != null && p.ExitCode != 0)
                        throw new InvalidOperationException($"macOS installer exited with code {p.ExitCode}");
                    return;
                }
                throw new InvalidOperationException("Unsupported macOS asset type for automatic install");
            }

            if (osName == "linux")
            {
                if (assetPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "acai_fresh_install_extracted");
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    Directory.CreateDirectory(tmp);
                    var start = new ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"-xzf \"{assetPath}\" -C \"{tmp}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    var p = Process.Start(start);
                    p?.WaitForExit();
                    if (p != null && p.ExitCode != 0)
                        throw new InvalidOperationException($"Failed to extract tarball (code {p.ExitCode})");

                    // find candidate binary and deploy
                    string currentExe = Path.GetFileName(Environment.ProcessPath ?? "acai");
                    var files = Directory.GetFiles(tmp, currentExe, SearchOption.AllDirectories);
                    if (files.Length == 0) files = Directory.GetFiles(tmp, "*", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        var incoming = files[0];
                        var current = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate current executable path");
                        SwapFinalBinary(incoming, current);
                        try { Directory.Delete(tmp, true); } catch { }
                        return;
                    }
                    throw new InvalidOperationException("No suitable binary found inside the tarball to replace the current executable.");
                }

                throw new InvalidOperationException("Unsupported Linux asset type for automatic install");
            }

            throw new InvalidOperationException("Unsupported OS for automatic installation");
        }

        private static string GetTargetOsName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
            return "unknown";
        }

        // Compatibility helper: deploy a new binary to actualInstallPath.
        // Mirrors the behavior expected by older code paths.
        public static void DeployNewBinary(string stagingBinaryPath, string actualInstallPath, string osName)
        {
            // Try to perform an atomic swap using SwapFinalBinary semantics
            try
            {
                SwapFinalBinary(stagingBinaryPath, actualInstallPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AcaiUpgrader] DeployNewBinary fallback: {ex.Message}");
                // as a last resort, attempt a Move
                if (File.Exists(actualInstallPath)) File.Delete(actualInstallPath);
                File.Move(stagingBinaryPath, actualInstallPath);
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(actualInstallPath,
                            UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite |
                            UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    }
                    catch { }
                }
            }
        }
    }
}
