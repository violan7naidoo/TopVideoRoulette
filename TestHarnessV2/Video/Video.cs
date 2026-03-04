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

        public Video(Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));

            // Search paths for local videos.
            // - App-relative: .\Videos\TopView, .\Videos\SideView, .\Videos\Oblique View, .\Videos\ObliqueView
            // - Absolute: D:\Users\saleesha\Desktop\Videos\{TopView,SideView,Oblique View,ObliqueView}
            _localVideoFolders = new[]
            {
                Path.Combine(Application.StartupPath, "Videos", "TopView"),
                Path.Combine(Application.StartupPath, "Videos", "SideView"),
                Path.Combine(Application.StartupPath, "Videos", "Oblique View"),
                Path.Combine(Application.StartupPath, "Videos", "ObliqueView"),
                @"D:\Users\saleesha\Desktop\Videos\TopView",
                @"D:\Users\saleesha\Desktop\Videos\SideView",
                @"D:\Users\saleesha\Desktop\Videos\Oblique View",
                @"D:\Users\saleesha\Desktop\Videos\ObliqueView"
            };

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
