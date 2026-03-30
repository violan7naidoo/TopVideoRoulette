using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace TestHarnessV2
{
    /// <summary>
    /// Intercepts video requests to localhost:5005/Videos/... and serves files from local disk.
    /// Logs each request, path resolution, and serve result to the app log file.
    /// </summary>
    internal class Video
    {
        private readonly string[] _localVideoFolders;
        private readonly string[] _allowedRoots;
        private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

        // Track recent relative paths to detect loops (same video over and over).
        private const int RecentHistorySize = 20;
        private readonly Queue<string> _recentRelativePaths = new Queue<string>(RecentHistorySize);
        private DateTime _lastLoopWarningUtc = DateTime.MinValue;

        public Video(Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));

            // Build search paths for local videos from config.
            // - Relative paths are combined with Application.StartupPath.
            // - Absolute paths (e.g. D:\...) are used as-is.
            var roots = new List<string>();
            foreach (var root in AppConfig.Current.VideoRoots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                try
                {
                    string full =
                        Path.IsPathRooted(root)
                            ? root
                            : Path.Combine(Application.StartupPath, root);
                    roots.Add(full);
                }
                catch
                {
                    // Ignore bad entries
                }
            }

            if (roots.Count == 0)
            {
                // Fallback to legacy defaults if config is empty
                roots.Add(Path.Combine(Application.StartupPath, "Videos", "TopView"));
                roots.Add(Path.Combine(Application.StartupPath, "Videos", "SideView"));
                roots.Add(Path.Combine(Application.StartupPath, "Videos", "Oblique View"));
                roots.Add(Path.Combine(Application.StartupPath, "Videos", "ObliqueView"));
            }

            _localVideoFolders = roots.ToArray();

            _allowedRoots = new string[_localVideoFolders.Length];
            for (int i = 0; i < _localVideoFolders.Length; i++)
                _allowedRoots[i] = Path.GetFullPath(_localVideoFolders[i]);
        }

        /// <summary>
        /// Attach request interception for localhost:5005/Videos/ so we serve from local disk.
        /// </summary>
        internal void AttachRequestInterception(CoreWebView2 coreWebView2)
        {
            try
            {
                coreWebView2.AddWebResourceRequestedFilter("*localhost:5005*", CoreWebView2WebResourceContext.All);
                coreWebView2.AddWebResourceRequestedFilter("*Videos*", CoreWebView2WebResourceContext.Media);
                coreWebView2.AddWebResourceRequestedFilter("*Videos*", CoreWebView2WebResourceContext.All);
                coreWebView2.WebResourceRequested += OnWebResourceRequested;
                Logger.Log("[VIDEO] Interception attached: localhost:5005/Videos/ requests will be served from local Video folder.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[VIDEO] Failed to attach interception: " + ex.Message, ex);
            }
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                string uri = e.Request.Uri;

                // Only intercept localhost:5005/Videos/ video requests
                if (uri.IndexOf("localhost:5005", StringComparison.OrdinalIgnoreCase) < 0 ||
                    uri.IndexOf("/Videos/", StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                Logger.Log($"[VIDEO] Request received: {uri}");

                string relativePath = ExtractPathFromUrl(uri);
                if (string.IsNullOrEmpty(relativePath))
                {
                    Logger.Log($"[VIDEO] Could not extract path from URL: {uri}");
                    return;
                }

                Logger.Log($"[VIDEO] Extracted path: {relativePath}");

                // Update recent-paths history and detect potential loops.
                UpdateRecentPathsAndDetectLoop(relativePath);

                string resolvedPath = ResolveToLocalFile(relativePath);
                if (resolvedPath == null)
                {
                    Logger.Log($"[VIDEO] File not found for path. Searched in: {string.Join("; ", _localVideoFolders)}");
                    return;
                }

                if (!File.Exists(resolvedPath))
                {
                    Logger.Log($"[VIDEO] Resolved path does not exist: {resolvedPath}");
                    return;
                }

                ServeFile(e, resolvedPath, uri);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VIDEO] Error handling request: {e.Request.Uri}", ex);
            }
        }

        private void UpdateRecentPathsAndDetectLoop(string relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativePath)) return;

                // Maintain fixed-size queue of last N relative paths.
                _recentRelativePaths.Enqueue(relativePath);
                while (_recentRelativePaths.Count > RecentHistorySize)
                    _recentRelativePaths.Dequeue();

                // Only start checking when we have a full window.
                if (_recentRelativePaths.Count < RecentHistorySize) return;

                string latest = relativePath;
                int sameCount = 0;
                foreach (var p in _recentRelativePaths)
                {
                    if (string.Equals(p, latest, StringComparison.OrdinalIgnoreCase))
                        sameCount++;
                }

                // If 15 or more of the last 20 are the same, and we haven't warned recently, log a warning.
                const int LoopThreshold = 15;
                if (sameCount >= LoopThreshold)
                {
                    var nowUtc = DateTime.UtcNow;
                    // Avoid spamming: only log once every 5 minutes for the same pattern.
                    if ((nowUtc - _lastLoopWarningUtc).TotalMinutes >= 5)
                    {
                        _lastLoopWarningUtc = nowUtc;
                        Logger.Log($"[WARN][VIDEO-LOOP] Possible video loop detected: path='{latest}' occurred {sameCount}/{RecentHistorySize} recent requests.");
                    }
                }
            }
            catch
            {
                // Ignore loop-detection failures; they should never break serving.
            }
        }

        private static string ExtractPathFromUrl(string uri)
        {
            try
            {
                int idx = uri.IndexOf("/Videos/", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;
                string path = uri.Substring(idx + 8); // after "/Videos/"
                int q = path.IndexOf('?');
                if (q >= 0) path = path.Substring(0, q);
                path = Uri.UnescapeDataString(path).Replace('/', Path.DirectorySeparatorChar);
                return path.Length > 0 ? path : null;
            }
            catch { return null; }
        }

        private string ResolveToLocalFile(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            foreach (string root in _allowedRoots)
            {
                if (!Directory.Exists(root)) continue;
                string combined = Path.Combine(root, normalized);
                if (File.Exists(combined))
                    return Path.GetFullPath(combined);
                // Try with folder name stripped (e.g. TopView/1/... under Video/TopView)
                string firstSegment = normalized.Split(Path.DirectorySeparatorChar)[0];
                string rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.Equals(firstSegment, rootName, StringComparison.OrdinalIgnoreCase))
                {
                    int len = firstSegment.Length + 1;
                    if (normalized.Length > len)
                    {
                        string subPath = normalized.Substring(len);
                        combined = Path.Combine(root, subPath);
                        if (File.Exists(combined))
                            return Path.GetFullPath(combined);
                    }
                }
            }
            // Fallback: search by filename only in allowed folders
            string fileName = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(fileName)) return null;
            foreach (string root in _localVideoFolders)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0)
                        return Path.GetFullPath(files[0]);
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private void ServeFile(CoreWebView2WebResourceRequestedEventArgs e, string filePath, string requestUri)
        {
            var fileInfo = new FileInfo(filePath);
            long fileLength = fileInfo.Length;
            string mimeType = "video/mp4";

            string rangeHeader = null;
            try { rangeHeader = e.Request.Headers.GetHeader("Range"); } catch { }
            bool isRange = !string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=");

            long startByte = 0, endByte = fileLength - 1;
            int statusCode = 200;
            string statusText = "OK";
            string responseHeaders = $"Content-Type: {mimeType}\r\nAccept-Ranges: bytes\r\n";

            if (isRange && rangeHeader != null)
            {
                string range = rangeHeader.Substring(6);
                string[] parts = range.Split('-');
                if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
                    long.TryParse(parts[0], out startByte);
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                    long.TryParse(parts[1], out endByte);
                if (startByte < 0) startByte = 0;
                if (endByte >= fileLength) endByte = fileLength - 1;
                if (startByte > endByte) { startByte = 0; endByte = fileLength - 1; }
                long contentLength = endByte - startByte + 1;
                responseHeaders += $"Content-Range: bytes {startByte}-{endByte}/{fileLength}\r\n";
                responseHeaders += $"Content-Length: {contentLength}\r\n";
                statusCode = 206;
                statusText = "Partial Content";
            }
            else
            {
                responseHeaders += $"Content-Length: {fileLength}\r\n";
            }

            responseHeaders += "Cache-Control: max-age=0, no-cache\r\n";

            Stream stream;
            if (isRange)
            {
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.Position = startByte;
                stream = new RangeStream(fs, startByte, endByte);
            }
            else
            {
                stream = File.OpenRead(filePath);
            }

            e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(stream, statusCode, statusText, responseHeaders);
            Logger.Log($"[VIDEO] Served successfully: {requestUri} -> {filePath}");
        }

        private sealed class RangeStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _start, _end;
            private long _position;

            public RangeStream(Stream baseStream, long start, long end)
            {
                _baseStream = baseStream;
                _start = start;
                _end = end;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _end - _start + 1;
            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = Length - _position;
                if (remaining <= 0) return 0;
                if (count > remaining) count = (int)remaining;
                _baseStream.Position = _start + _position;
                int read = _baseStream.Read(buffer, offset, count);
                _position += read;
                return read;
            }

            public override void Flush() => _baseStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _baseStream?.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
