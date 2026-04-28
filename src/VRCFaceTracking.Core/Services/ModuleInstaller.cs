using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Services;

public class ModuleInstaller
{
    private readonly ILogger<ModuleInstaller> _logger;
    private static readonly HttpClient Http = new();

    // Prevent concurrent installs of the same package
    private static readonly ConcurrentDictionary<string, bool> _installing = new(StringComparer.OrdinalIgnoreCase);

    public ModuleInstaller(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ModuleInstaller>();
    }

    public async Task<bool> InstallAsync(
        InstallableTrackingModule module,
        IProgress<float>? progress = null,
        CancellationToken ct = default)
    {
        var meta = module.Metadata;

        if (string.IsNullOrEmpty(meta.DownloadUrl))
        {
            module.InstallState = InstallState.Error;
            module.ErrorMessage = "No download URL";
            return false;
        }

        // Guard against concurrent/double installs of the same package
        if (!_installing.TryAdd(meta.PackageId, true))
        {
            _logger.LogWarning($"Install of {meta.DisplayName} already in progress — ignoring duplicate request");
            return false;
        }

        string installDir = Path.Combine(UnifiedLibManager.ModulesDir, meta.PackageId);

        // Determine if download is a zip archive or a raw DLL
        bool isZip = IsZipUrl(meta.DownloadUrl);
        string ext = isZip ? ".zip" : ".dll";
        string tempFile = Path.Combine(Path.GetTempPath(), $"vrcft_{meta.PackageId}_{Guid.NewGuid():N}{ext}");

        try
        {
            module.InstallState = InstallState.Installing;
            module.InstallProgress = 0f;

            // ── Download ─────────────────────────────────────────────────────────
            _logger.LogInformation($"Downloading {meta.DisplayName} from {meta.DownloadUrl}");
            using var response = await Http.GetAsync(meta.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            // Write to temp file in its own scope so the FileStream is fully
            // disposed (and its exclusive lock released) before we try to read
            // the file for MD5 verification or extraction.
            {
                await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(
                    tempFile, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, useAsync: true);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;
                    if (totalBytes > 0)
                    {
                        module.InstallProgress = 0.8f * downloaded / totalBytes.Value;
                        progress?.Report(module.InstallProgress);
                    }
                }
            } // ← fileStream disposed here; lock released before MD5/extract

            module.InstallProgress = 0.8f;

            // ── Verify MD5 (if registry provides a hash) ─────────────────────────
            if (!string.IsNullOrEmpty(meta.Md5Hash))
            {
                _logger.LogInformation($"Verifying MD5 for {meta.DisplayName}");
                var computedHash = await ComputeMd5Async(tempFile, ct);
                if (!string.Equals(computedHash, meta.Md5Hash, StringComparison.OrdinalIgnoreCase))
                {
                    module.InstallState = InstallState.Error;
                    module.ErrorMessage = $"MD5 mismatch: expected {meta.Md5Hash}, got {computedHash}";
                    _logger.LogError(module.ErrorMessage);
                    return false;
                }
            }

            module.InstallProgress = 0.9f;

            // ── Prepare install directory ─────────────────────────────────────────
            if (Directory.Exists(installDir))
                Directory.Delete(installDir, recursive: true);
            Directory.CreateDirectory(installDir);

            // ── Extract or copy ───────────────────────────────────────────────────
            if (isZip)
            {
                _logger.LogInformation($"Extracting {meta.DisplayName} to {installDir}");
                await Task.Run(() => ZipFile.ExtractToDirectory(tempFile, installDir), ct);

                var dlls = Directory.GetFiles(installDir, "*.dll", SearchOption.AllDirectories);
                if (dlls.Length == 0)
                {
                    module.InstallState = InstallState.Error;
                    module.ErrorMessage = "No DLL found in downloaded archive";
                    return false;
                }
                module.InstallPath = PickPrimaryDll(dlls, meta);
            }
            else
            {
                // Direct DLL download — use DllFileName from registry or infer from URL
                string dllName = meta.DllFileName;
                if (string.IsNullOrEmpty(dllName))
                    dllName = Path.GetFileName(new Uri(meta.DownloadUrl).AbsolutePath);
                if (string.IsNullOrEmpty(dllName) || !dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    dllName = meta.DisplayName.Replace(" ", "") + ".dll";

                string destPath = Path.Combine(installDir, dllName);
                _logger.LogInformation($"Copying {meta.DisplayName} DLL to {destPath}");
                File.Copy(tempFile, destPath, overwrite: true);
                module.InstallPath = destPath;
            }

            module.InstalledVersion = meta.Version;
            module.InstallState = InstallState.Installed;
            module.InstallProgress = 1f;
            progress?.Report(1f);

            _logger.LogInformation($"Installed {meta.DisplayName} v{meta.Version}");
            return true;
        }
        catch (OperationCanceledException)
        {
            module.InstallState = InstallState.NotInstalled;
            return false;
        }
        catch (Exception ex)
        {
            module.InstallState = InstallState.Error;
            module.ErrorMessage = ex.Message;
            _logger.LogError($"Failed to install {meta.DisplayName}: {ex.Message}");
            return false;
        }
        finally
        {
            _installing.TryRemove(meta.PackageId, out _);
            if (File.Exists(tempFile))
                try { File.Delete(tempFile); } catch { }
        }
    }

    public bool Uninstall(InstallableTrackingModule module)
    {
        var installDir = Path.Combine(UnifiedLibManager.ModulesDir, module.Metadata.PackageId);
        try
        {
            if (Directory.Exists(installDir))
                Directory.Delete(installDir, recursive: true);

            module.InstallState = InstallState.NotInstalled;
            module.InstallPath = null;
            module.InstalledVersion = null;
            _logger.LogInformation($"Uninstalled {module.Metadata.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to uninstall {module.Metadata.DisplayName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pick the most likely "primary" DLL out of an extracted archive.
    /// Priority:
    ///   1. Exact match on <c>meta.DllFileName</c> if the registry specified one.
    ///   2. DLL whose filename has the smallest Levenshtein distance to the packageId
    ///      or display name (so a multi-DLL archive that includes dependency assemblies
    ///      still picks the right one).
    ///   3. First DLL found, as a last resort.
    /// </summary>
    private static string PickPrimaryDll(string[] dlls, TrackingModuleMetadata meta)
    {
        if (dlls.Length == 1) return dlls[0];

        if (!string.IsNullOrEmpty(meta.DllFileName))
        {
            var exact = dlls.FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), meta.DllFileName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }

        var keys = new List<string>();
        if (!string.IsNullOrEmpty(meta.PackageId))   keys.Add(meta.PackageId.ToLowerInvariant());
        if (!string.IsNullOrEmpty(meta.DisplayName)) keys.Add(meta.DisplayName.Replace(" ", "").ToLowerInvariant());
        if (keys.Count == 0) return dlls[0];

        return dlls
            .Select(d => (Path: d,
                          Score: keys.Min(k => LevenshteinDistance(
                              Path.GetFileNameWithoutExtension(d).ToLowerInvariant(), k))))
            .OrderBy(x => x.Score)
            .First().Path;
    }

    /// <summary>
    /// Standard iterative-DP Levenshtein. Tiny by design — only used to pick
    /// among a handful of DLLs in a freshly-extracted archive.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }

    /// <summary>
    /// Returns true if the URL points to a zip archive (vs a raw DLL or other file).
    /// Also returns true if we can't determine the type — safer to attempt extraction.
    /// </summary>
    private static bool IsZipUrl(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".dll") return false;
            if (ext == ".zip") return true;
        }
        catch { }
        // Unknown extension — assume zip (most modules are packaged as zip)
        return true;
    }

    private static async Task<string> ComputeMd5Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        using var md5 = MD5.Create();
        var hash = await Task.Run(() => md5.ComputeHash(stream), ct);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
