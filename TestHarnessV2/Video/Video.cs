using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace TestHarnessV2
{
    internal class Video
    {
        // Video serving methods
        // File cache for small, frequently accessed videos (max 50MB per file)
        private readonly Dictionary<string, byte[]> _fileCache = new Dictionary<string, byte[]>();
        private readonly object _cacheLock = new object();
        private const long MAX_CACHE_FILE_SIZE = 50 * 1024 * 1024; // 50MB


        // Video serving fields - support both URL path names (ObliqueView) and disk folder names (Oblique View)
        private readonly string[] _localVideoFolders = new[]
        {
            Path.Combine(Application.StartupPath, "Video", "TopView"),
            Path.Combine(Application.StartupPath, "Video", "SideView"),
            Path.Combine(Application.StartupPath, "Video", "Oblique View"),
            Path.Combine(Application.StartupPath, "Video", "ObliqueView"),
            @"D:\Users\saleesha\Desktop\Videos\TopView",
            @"D:\Users\saleesha\Desktop\Videos\SideView",
            @"D:\Users\saleesha\Desktop\Videos\Oblique View",
            @"D:\Users\saleesha\Desktop\Videos\ObliqueView"
        };
        /// <summary>Precomputed full paths for allowed roots - avoids GetFullPath in hot path.</summary>
        private readonly string[] _allowedRoots;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        public Video(Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _allowedRoots = new string[_localVideoFolders.Length];
            for (int i = 0; i < _localVideoFolders.Length; i++)
                _allowedRoots[i] = Path.GetFullPath(_localVideoFolders[i]);
        }

        /// <summary>
        /// Sets up HTTP request interception for video/media requests and preloads videos.
        /// Call once after the main page has loaded (e.g. from NavigationCompleted).
        /// </summary>
        internal void AttachRequestInterception(CoreWebView2 coreWebView2)
        {
            try
            {
                coreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Media);
                coreWebView2.AddWebResourceRequestedFilter("*videos*", CoreWebView2WebResourceContext.All);
                coreWebView2.AddWebResourceRequestedFilter("*Videos*", CoreWebView2WebResourceContext.All);
                coreWebView2.AddWebResourceRequestedFilter("*@fs*", CoreWebView2WebResourceContext.All);
                coreWebView2.AddWebResourceRequestedFilter("*localhost*", CoreWebView2WebResourceContext.All);
                coreWebView2.WebResourceRequested += OnWebResourceRequested;
                // No preload: avoids I/O storm and memory spike; videos load on demand and are cached then
            }
            catch (Exception ex)
            {
                Logger.LogError("[INIT] Error setting up video interception: " + ex.Message, ex);
            }
        }

        internal void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                var requestUri = e.Request.Uri;

                // NEVER intercept the main page/document request - let it load normally
                if (e.ResourceContext == CoreWebView2WebResourceContext.Document)
                {
                    return; // Always let document requests proceed
                }

                // Never intercept audio - let game load .mp3, .wav, etc. from its URL
                if (IsAudioRequest(requestUri))
                    return;

                // Only intercept actual video requests (.mp4, /videos/, /video/, etc.)
                bool isVideoRequest = IsVideoOrMediaRequest(requestUri);
                if (!isVideoRequest)
                    return;
                if (!isVideoRequest)
                {
                    return; // Let the request proceed normally
                }


                string? localFilePath = ExtractFilePathFromUrl(requestUri);
                if (string.IsNullOrEmpty(localFilePath))
                {
                    Logger.Log($"[VIDEO] No path extracted for: {requestUri}");
                    return;
                }

                string? resolvedFilePath = ResolveAndValidatePath(localFilePath);
                if (resolvedFilePath == null)
                {
                    Logger.Log($"[VIDEO] No local file for: {requestUri}");
                    return;
                }

                // Check if file exists and serve it
                if (File.Exists(resolvedFilePath))
                {
                    var fileInfo = new FileInfo(resolvedFilePath);
                    long fileLength = fileInfo.Length;
                    string mimeType = GetMimeType(Path.GetExtension(resolvedFilePath));

                        // Header not present or error reading it
                    // Handle Range requests for video seeking
                    string? rangeHeader = null;
                    bool isRangeRequest = false;
                    try
                    {
                        rangeHeader = e.Request.Headers.GetHeader("Range");
                    }
                    catch
                    {
                        rangeHeader = null;
                    }
                    isRangeRequest = !string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=");

                    Stream fileStream;
                    long startByte = 0;
                    long endByte = fileLength - 1;
                    int statusCode = 200;
                    string statusText = "OK";
                    string responseHeaders = $"Content-Type: {mimeType}\r\nAccept-Ranges: bytes\r\n";

                    // Check cache first for small files
                    byte[]? cachedData = null;
                    bool useCache = fileLength <= MAX_CACHE_FILE_SIZE;

                    if (useCache)
                    {
                        lock (_cacheLock)
                        {
                            _fileCache.TryGetValue(resolvedFilePath, out cachedData);
                        }
                    }

                    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                    {
                        // Parse range request (e.g., "bytes=0-1023" or "bytes=1024-")
                        string range = rangeHeader.Substring(6);
                        string[] ranges = range.Split('-');

                        if (ranges.Length == 2)
                        {
                            if (!string.IsNullOrEmpty(ranges[0]))
                                long.TryParse(ranges[0], out startByte);
                            if (!string.IsNullOrEmpty(ranges[1]))
                                long.TryParse(ranges[1], out endByte);
                        }
                        else if (ranges.Length == 1 && !string.IsNullOrEmpty(ranges[0]))
                        {
                            long.TryParse(ranges[0], out startByte);
                            endByte = fileLength - 1;
                        }

                        // Ensure valid range
                        if (startByte < 0) startByte = 0;
                        if (endByte >= fileLength) endByte = fileLength - 1;
                        if (startByte > endByte)
                        {
                            startByte = 0;
                            endByte = fileLength - 1;
                        }

                        long contentLength = endByte - startByte + 1;
                        responseHeaders += $"Content-Range: bytes {startByte}-{endByte}/{fileLength}\r\n";
                        responseHeaders += $"Content-Length: {contentLength}\r\n";
                        statusCode = 206; // Partial Content
                        statusText = "Partial Content";

                        if (cachedData != null)
                        {
                            // Create memory stream from cached data for range
                            var memoryStream = new MemoryStream(cachedData);
                            memoryStream.Position = startByte;
                            fileStream = new RangeStream(memoryStream, startByte, endByte);
                        }
                        else
                        {
                            fileStream = new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            fileStream.Position = startByte;
                            fileStream = new RangeStream(fileStream, startByte, endByte);
                        }
                    }
                    else
                    {
                        // Full file request
                        responseHeaders += $"Content-Length: {fileLength}\r\n";

                        if (cachedData != null)
                        {
                            fileStream = new MemoryStream(cachedData);
                        }
                        else
                        {
                            fileStream = File.OpenRead(resolvedFilePath);

                            if (useCache)
                            {
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        byte[] fileData = File.ReadAllBytes(resolvedFilePath);
                                        lock (_cacheLock)
                                        {
                                            if (!_fileCache.ContainsKey(resolvedFilePath))
                                                _fileCache[resolvedFilePath] = fileData;
                                        }
                                    }
                                    catch { /* ignore cache errors */ }
                                });
                            }
                        }
                    }

                    // Avoid long cache so when the game requests a new video URL we serve the new file
                    responseHeaders += "Cache-Control: max-age=0, no-cache\r\n";
                    responseHeaders += $"ETag: \"{fileInfo.LastWriteTime.Ticks}\"\r\n";
                    responseHeaders += $"Last-Modified: {fileInfo.LastWriteTime.ToUniversalTime():R}\r\n";

                    e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        fileStream,
                        statusCode,
                        statusText,
                        responseHeaders
                    );
                    if (!isRangeRequest)
                        Logger.Log($"[VIDEO] Serving: {requestUri} -> {resolvedFilePath}");
                }
                else
                {
                    Logger.Log($"[VIDEO] No local file for: {requestUri}");
                }
                // else: file not found, let request proceed normally
            }
            catch
            {
                // On error, let the request proceed normally
            }
        }

        /// <summary>True if URL is clearly audio - we never intercept these.</summary>
        private static bool IsAudioRequest(string uri)
        {
            if (uri.IndexOf("/audio/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return true;
            if (uri.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return true;
            if (uri.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) return true;
            if (uri.IndexOf(".mp3?", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf(".wav?", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Fast check: MP4 + known video URL patterns only; no ToLower allocation.</summary>
        private bool IsVideoOrMediaRequest(string uri)
        {
            if (IsAudioRequest(uri)) return false;
            if (uri.IndexOf(".mp4", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf("/videos/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf("/videos", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf("/@fs/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (uri.IndexOf("localhost:5005", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (uri.IndexOf("/videos", StringComparison.OrdinalIgnoreCase) >= 0 || uri.IndexOf("/video", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            return false;
        }

        /// <summary>Single fast path: /Videos/ or /@fs/; one decode, minimal allocations.</summary>
        private string? ExtractFilePathFromUrl(string uri)
        {
            try
            {
                int videosIdx = uri.IndexOf("/Videos/", StringComparison.OrdinalIgnoreCase);
                if (videosIdx >= 0)
                {
                    string path = uri.Substring(videosIdx + 8);
                    int queryIdx = path.IndexOf('?');
                    if (queryIdx >= 0) path = path.Substring(0, queryIdx);
                    path = Uri.UnescapeDataString(path).Replace('/', '\\');
                    return path.Length > 0 ? path : null;
                }
                int fsIdx = uri.IndexOf("/@fs/", StringComparison.OrdinalIgnoreCase);
                if (fsIdx >= 0)
                {
                    string path = uri.Substring(fsIdx + 5);
                    int queryIdx = path.IndexOf('?');
                    if (queryIdx >= 0) path = path.Substring(0, queryIdx);
                    path = Uri.UnescapeDataString(path).Replace('/', '\\');
                    return path.Length > 0 ? path : null;
                }
                var uriObj = new Uri(uri);
                string absolutePath = uriObj.AbsolutePath;
                if (absolutePath.StartsWith("/video/", StringComparison.OrdinalIgnoreCase))
                    absolutePath = absolutePath.Substring(7);
                else if (absolutePath.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                    absolutePath = absolutePath.Substring(1);
                string path2 = Uri.UnescapeDataString(absolutePath).Replace('/', '\\');
                return path2.Length > 0 ? path2 : null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ERROR] Error extracting path: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>Uses precomputed roots; combine root + relative path, then validate with StartsWith.</summary>
        private string? ResolveAndValidatePath(string filePath)
        {
            try
            {
                bool isAbsolute = filePath.Length >= 3 && filePath[1] == ':' && (filePath[2] == '\\' || filePath[2] == '/');
                if (isAbsolute)
                {
                    string fullPath = Path.GetFullPath(filePath);
                    foreach (string root in _allowedRoots)
                    {
                        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            return fullPath;
                        if (fullPath.StartsWith("C:\\", StringComparison.OrdinalIgnoreCase))
                        {
                            string dPath = Path.GetFullPath("D:" + fullPath.Substring(2));
                            if (dPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                return dPath;
                        }
                    }
                    return null;
                }
                return SearchForFileInAllowedFolders(filePath);
            }
            catch
            {
                return null;
            }
        }



        /// <summary>Path.Combine + File.Exists first; GetFiles only as last resort to avoid full tree scan.</summary>
        private string? SearchForFileInAllowedFolders(string relativePath)
        {
            string normalized = relativePath.Replace('/', '\\');
            foreach (string root in _allowedRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    string folderName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string pathToSearch = normalized;
                    if (normalized.StartsWith(folderName + "\\", StringComparison.OrdinalIgnoreCase))
                        pathToSearch = normalized.Substring(folderName.Length + 1);
                    else if (folderName.Equals("Oblique View", StringComparison.OrdinalIgnoreCase) && normalized.StartsWith("ObliqueView\\", StringComparison.OrdinalIgnoreCase))
                        pathToSearch = normalized.Substring(12);
                    else if (folderName.Equals("ObliqueView", StringComparison.OrdinalIgnoreCase) && normalized.StartsWith("Oblique View\\", StringComparison.OrdinalIgnoreCase))
                        pathToSearch = normalized.Substring(13);
                    string fullPath = Path.Combine(root, pathToSearch);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch { /* ignore */ }
            }
            string fileName = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(fileName)) return null;
            foreach (string root in _localVideoFolders)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0];
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static string GetMimeType(string extension) => "video/mp4";

        /// <summary>Returns path to first .mp4 found in allowed folders, or null if none.</summary>
        private string? GetFirstAvailableVideo()
        {
            foreach (string root in _localVideoFolders)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    string[] files = Directory.GetFiles(root, "*.mp4", SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0];
                }
                catch { /* ignore */ }
            }
            return null;
        }

        internal async void PreloadVideos()
        {
            await Task.Run(() =>
            {
                try
                {
                    int preloadCount = 0;
                    int skippedCount = 0;

                    foreach (var folder in _localVideoFolders)
                    {
                        if (!Directory.Exists(folder))
                            continue;

                        try
                        {
                            var videoFiles = Directory.GetFiles(folder, "*.mp4", SearchOption.AllDirectories)
                                .OrderBy(f => f)
                                .Take(50)
                                .ToList();

                            foreach (var videoFile in videoFiles)
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(videoFile);
                                    if (fileInfo.Length > MAX_CACHE_FILE_SIZE)
                                    {
                                        skippedCount++;
                                        continue;
                                    }
                                    lock (_cacheLock)
                                    {
                                        if (_fileCache.ContainsKey(videoFile))
                                        {
                                            skippedCount++;
                                            continue;
                                        }
                                    }
                                    byte[] fileData = File.ReadAllBytes(videoFile);
                                    lock (_cacheLock)
                                    {
                                        if (!_fileCache.ContainsKey(videoFile))
                                        {
                                            _fileCache[videoFile] = fileData;
                                            preloadCount++;
                                        }
                                    }
                                }
                                catch { /* skip failed file */ }
                            }
                        }
                        catch { /* skip failed folder */ }
                    }
                }
                catch { /* ignore preload errors */ }
            });
        }
        // Helper class for handling HTTP Range requests
        private class RangeStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _start;
            private readonly long _end;
            private long _position;

            public RangeStream(Stream baseStream, long start, long end)
            {
                _baseStream = baseStream;
                _start = start;
                _end = end;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _end - _start + 1;
            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = Length - _position;
                if (remaining <= 0) return 0;

                if (count > remaining) count = (int)remaining;

                _baseStream.Position = _start + _position;
                int bytesRead = _baseStream.Read(buffer, offset, count);
                _position += bytesRead;

                return bytesRead;
            }

            public override void Flush() => _baseStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();



            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _baseStream?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
