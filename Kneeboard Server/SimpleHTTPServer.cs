// MIT License - Copyright (c) 2016 Can Güney Aksakalli
// https://aksakalli.github.io/2014/02/24/simple-http-server-with-csparp.html

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Kneeboard_Server.Kneeboard_Server;

namespace Kneeboard_Server
{
    public class SimpleHTTPServer
    {
        private Kneeboard_Server _kneeboardServer;
        // Load API key: first from secrets.config (via AppSettings), fallback to user settings, then hardcoded default
        private string OPENAIP_API_KEY
        {
            get
            {
                // Try secrets.config first
                string key = ConfigurationManager.AppSettings["OpenAipApiKey"];
                if (!string.IsNullOrEmpty(key))
                    return key;

                // Fallback to user settings
                key = Properties.Settings.Default.openaip;
                if (!string.IsNullOrEmpty(key))
                    return key;

                // Last resort: hardcoded default (from original Settings.Designer.cs)
                return "d41058a9711be731a31a63fab762175c";
            }
        }

        // OpenAIP Cache Configuration
        private static readonly string CACHE_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "openaip");
        private static readonly TimeSpan CACHE_TTL = TimeSpan.FromDays(7);
        private static readonly object _cacheLock = new object();

        // FIR Boundaries Cache
        private static readonly string BOUNDARIES_CACHE_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "boundaries");
        private static readonly TimeSpan BOUNDARIES_CACHE_TTL = TimeSpan.FromHours(24);
        private static string _cachedVatsimBoundaries = null;
        private static DateTime _vatsimBoundariesCacheTime = DateTime.MinValue;
        private static string _cachedIvaoBoundaries = null;
        private static DateTime _ivaoBoundariesCacheTime = DateTime.MinValue;
        private static string _cachedVatspyFirNames = null;
        private static DateTime _vatspyFirNamesCacheTime = DateTime.MinValue;
        private static readonly object _boundariesCacheLock = new object();

        public SimpleHTTPServer(string path, int port, Kneeboard_Server kneeboardServer)
        {
            this._kneeboardServer = kneeboardServer;
            this.Initialize(path, port);
        }

        private readonly string[] _indexFiles = {
            "kneeboard.html",
            "documents.html",
            "map.html",
            "navlog.html",
            "notepad.html",
            "formulas.html",
        };
        private static readonly IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
        #region extension to MIME type list
        {".asf", "video/x-ms-asf"},
        {".asx", "video/x-ms-asf"},
        {".avi", "video/x-msvideo"},
        {".bin", "application/octet-stream"},
        {".cco", "application/x-cocoa"},
        {".crt", "application/x-x509-ca-cert"},
        {".css", "text/css"},
        {".deb", "application/octet-stream"},
        {".der", "application/x-x509-ca-cert"},
        {".dll", "application/octet-stream"},
        {".dmg", "application/octet-stream"},
        {".ear", "application/java-archive"},
        {".eot", "application/octet-stream"},
        {".exe", "application/octet-stream"},
        {".flv", "video/x-flv"},
        {".gif", "image/gif"},
        {".hqx", "application/mac-binhex40"},
        {".htc", "text/x-component"},
        {".htm", "text/html"},
        {".html", "text/html"},
        {".ico", "image/x-icon"},
        {".img", "application/octet-stream"},
        {".iso", "application/octet-stream"},
        {".jar", "application/java-archive"},
        {".jardiff", "application/x-java-archive-diff"},
        {".jng", "image/x-jng"},
        {".jnlp", "application/x-java-jnlp-file"},
        {".jpeg", "image/jpeg"},
        {".jpg", "image/jpeg"},
        {".js", "application/x-javascript"},
        {".mjs", "text/javascript"},
        {".mml", "text/mathml"},
        {".mng", "video/x-mng"},
        {".mov", "video/quicktime"},
        {".mp3", "audio/mpeg"},
        {".mpeg", "video/mpeg"},
        {".mpg", "video/mpeg"},
        {".msi", "application/octet-stream"},
        {".msm", "application/octet-stream"},
        {".msp", "application/octet-stream"},
        {".pdb", "application/x-pilot"},
        {".pdf", "application/pdf"},
        {".pem", "application/x-x509-ca-cert"},
        {".pl", "application/x-perl"},
        {".pm", "application/x-perl"},
        {".png", "image/png"},
        {".prc", "application/x-pilot"},
        {".ra", "audio/x-realaudio"},
        {".rar", "application/x-rar-compressed"},
        {".rpm", "application/x-redhat-package-manager"},
        {".rss", "text/xml"},
        {".run", "application/x-makeself"},
        {".sea", "application/x-sea"},
        {".shtml", "text/html"},
        {".sit", "application/x-stuffit"},
        {".swf", "application/x-shockwave-flash"},
        {".tcl", "application/x-tcl"},
        {".tk", "application/x-tcl"},
        {".txt", "text/plain"},
        {".war", "application/java-archive"},
        {".wbmp", "image/vnd.wap.wbmp"},
        {".wmv", "video/x-ms-wmv"},
        {".xml", "text/xml"},
        {".xpi", "application/x-xpinstall"},
        {".zip", "application/zip"},
        #endregion
    };
        Thread _serverThread;
        private string _rootDirectory;
        HttpListener _listener;
        private int _port;
        public string values = "";
        public long valuesTimestamp = 0;



        public int Port
        {
            get { return _port; }
            private set { }
        }

        /// <summary>
        /// Construct server with given port.
        /// </summary>
        /// <param name="path">Directory path to serve.</param>
        /// <param name="port">Port of the server.</param>
        public SimpleHTTPServer(string path, int port)
        {
            this.Initialize(path, port);
        }



        private volatile bool _isRunning = true;

        /// <summary>
        /// Stop server and dispose all functions.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping server: {ex.Message}");
            }
        }

        private void Listen()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{_port}/");

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Failed to start HTTP listener: {ex.Message}");
                Console.WriteLine("Try running as Administrator or use a different port.");
                return;
            }

            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    // Handle each request in a thread pool thread for better concurrency
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            Process(context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing request: {ex.Message}");
                            try
                            {
                                context.Response.StatusCode = 500;
                                context.Response.OutputStream.Close();
                            }
                            catch { }
                        }
                    });
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    if (!_isRunning) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Server error: {ex.Message}");
                }
            }
        }

        private void ResponseString(HttpListenerContext context, string text)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(text ?? string.Empty);
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}");
            }
            finally
            {
                try { context.Response.OutputStream.Close(); } catch { }
            }
        }

        private void ResponseJson(HttpListenerContext context, string json)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(json ?? "{}");
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending JSON response: {ex.Message}");
            }
            finally
            {
                try { context.Response.OutputStream.Close(); } catch { }
            }
        }

        private void HandleNoaaProxy(HttpListenerContext context, string icaoRaw, bool isTaf)
        {
            string icao = (icaoRaw ?? string.Empty).Trim().ToUpperInvariant();
            if (icao.Length != 4 || !icao.All(char.IsLetter))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.OutputStream.Close();
                return;
            }

            string endpoint = isTaf
                ? $"https://aviationweather.gov/api/data/taf?ids={icao}&format=json"
                : $"https://aviationweather.gov/api/data/metar?ids={icao}&format=json";

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "KneeboardServer/1.0");
                    client.Encoding = Encoding.UTF8;
                    string payload = client.DownloadString(endpoint);
                    byte[] buffer = Encoding.UTF8.GetBytes(payload);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                context.Response.StatusCode = httpResponse != null
                    ? (int)httpResponse.StatusCode
                    : (int)HttpStatusCode.BadGateway;

                string errorPayload = string.Empty;
                if (ex.Response != null)
                {
                    try
                    {
                        using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                        {
                            errorPayload = reader.ReadToEnd();
                        }
                    }
                    catch
                    {
                        errorPayload = string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(errorPayload))
                {
                    errorPayload = "{\"error\":\"Unable to reach NOAA service\"}";
                }

                byte[] buffer = Encoding.UTF8.GetBytes(errorPayload);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }
        private void HandleElevationProxy(HttpListenerContext context)
        {
            var request = context.Request;

            if (request.HttpMethod != "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.OutputStream.Close();
                return;
            }

            string requestBody;
            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading elevation request body: {ex.Message}");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.OutputStream.Close();
                return;
            }

            try
            {
                var outboundRequest = (HttpWebRequest)WebRequest.Create("https://api.open-elevation.com/api/v1/lookup");
                outboundRequest.Method = "POST";
                outboundRequest.ContentType = "application/json";
                outboundRequest.Accept = "application/json";
                outboundRequest.Timeout = 15000; // 15 second timeout for elevation data
                outboundRequest.ReadWriteTimeout = 15000;

                byte[] bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                outboundRequest.ContentLength = bodyBytes.Length;

                using (var requestStream = outboundRequest.GetRequestStream())
                {
                    requestStream.Write(bodyBytes, 0, bodyBytes.Length);
                }

                using (var upstreamResponse = (HttpWebResponse)outboundRequest.GetResponse())
                {
                    context.Response.StatusCode = (int)upstreamResponse.StatusCode;
                    context.Response.ContentType = upstreamResponse.ContentType ?? "application/json";

                    using (var upstreamStream = upstreamResponse.GetResponseStream())
                    {
                        upstreamStream.CopyTo(context.Response.OutputStream);
                    }
                }
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                context.Response.StatusCode = httpResponse != null
                    ? (int)httpResponse.StatusCode
                    : (int)HttpStatusCode.BadGateway;

                Console.WriteLine($"Elevation Proxy Error: {ex.Message}");
                if (httpResponse != null)
                {
                    Console.WriteLine($"Upstream Status: {httpResponse.StatusCode}");
                }

                string payload = "{\"error\":\"Unable to reach elevation service\"}";
                if (httpResponse != null)
                {
                    try
                    {
                        using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                        {
                            payload = reader.ReadToEnd();
                        }
                    }
                    catch
                    {
                        payload = "{\"error\":\"Unable to reach elevation service\"}";
                    }
                }

                var buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private void HandleDfsProxy(HttpListenerContext context)
        {
            var request = context.Request;
            var path = request.Url.AbsolutePath;

            // Extract the tile path (z/x/y.png) from the URL
            var idx = path.IndexOf("/api/dfs/tiles/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.OutputStream.Close();
                return;
            }

            var tilePart = path.Substring(idx + "/api/dfs/tiles/".Length);
            var targetUrl = $"https://secais.dfs.de/static-maps/icao500/tiles/{tilePart}";

            try
            {
                var outboundRequest = (HttpWebRequest)WebRequest.Create(targetUrl);
                outboundRequest.Method = "GET";
                outboundRequest.Accept = "image/png";
                outboundRequest.Timeout = 10000;
                outboundRequest.ReadWriteTimeout = 10000;

                using (var upstreamResponse = (HttpWebResponse)outboundRequest.GetResponse())
                {
                    context.Response.StatusCode = (int)upstreamResponse.StatusCode;
                    context.Response.ContentType = upstreamResponse.ContentType ?? "image/png";

                    using (var upstreamStream = upstreamResponse.GetResponseStream())
                    {
                        upstreamStream.CopyTo(context.Response.OutputStream);
                    }
                }
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                context.Response.StatusCode = httpResponse != null
                    ? (int)httpResponse.StatusCode
                    : (int)HttpStatusCode.BadGateway;

                Console.WriteLine($"DFS Proxy Error: {ex.Message}");
                Console.WriteLine($"Requested URL: {targetUrl}");
                if (httpResponse != null)
                {
                    Console.WriteLine($"Upstream Status: {httpResponse.StatusCode}");
                }

                // Return empty PNG for failed tile requests
                context.Response.ContentType = "image/png";
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private void HandleVatsimBoundariesProxy(HttpListenerContext context)
        {
            try
            {
                // Check memory cache first
                lock (_boundariesCacheLock)
                {
                    if (_cachedVatsimBoundaries != null && (DateTime.Now - _vatsimBoundariesCacheTime) < BOUNDARIES_CACHE_TTL)
                    {
                        context.Response.AddHeader("X-Cache", "HIT");
                        ResponseJson(context, _cachedVatsimBoundaries);
                        return;
                    }
                }

                // Check disk cache
                string diskCachePath = Path.Combine(BOUNDARIES_CACHE_DIR, "vatsim_boundaries.json");
                if (File.Exists(diskCachePath))
                {
                    var fileInfo = new FileInfo(diskCachePath);
                    if ((DateTime.Now - fileInfo.LastWriteTime) < BOUNDARIES_CACHE_TTL)
                    {
                        string cachedData = File.ReadAllText(diskCachePath);
                        lock (_boundariesCacheLock)
                        {
                            _cachedVatsimBoundaries = cachedData;
                            _vatsimBoundariesCacheTime = fileInfo.LastWriteTime;
                        }
                        context.Response.AddHeader("X-Cache", "DISK");
                        Console.WriteLine("VATSIM Boundaries: loaded from disk cache");
                        ResponseJson(context, cachedData);
                        return;
                    }
                }

                // Fetch from GitHub
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.UserAgent, "KneeboardServer/1.0");
                    string boundaries = client.DownloadString("https://raw.githubusercontent.com/vatsimnetwork/vatspy-data-project/master/Boundaries.geojson");

                    lock (_boundariesCacheLock)
                    {
                        _cachedVatsimBoundaries = boundaries;
                        _vatsimBoundariesCacheTime = DateTime.Now;
                    }

                    // Save to disk cache
                    try
                    {
                        Directory.CreateDirectory(BOUNDARIES_CACHE_DIR);
                        File.WriteAllText(diskCachePath, boundaries);
                        Console.WriteLine("VATSIM Boundaries: saved to disk cache");
                    }
                    catch (Exception cacheEx)
                    {
                        Console.WriteLine($"VATSIM Boundaries: failed to save disk cache: {cacheEx.Message}");
                    }

                    context.Response.AddHeader("X-Cache", "MISS");
                    ResponseJson(context, boundaries);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VATSIM Boundaries fetch error: {ex.Message}");

                // Try to return cached data even if expired
                lock (_boundariesCacheLock)
                {
                    if (_cachedVatsimBoundaries != null)
                    {
                        context.Response.AddHeader("X-Cache", "STALE");
                        ResponseJson(context, _cachedVatsimBoundaries);
                        return;
                    }
                }

                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                ResponseJson(context, "{\"error\":\"Unable to fetch VATSIM boundaries\"}");
            }
        }

        private void HandleVatspyFirNamesProxy(HttpListenerContext context)
        {
            try
            {
                // Check memory cache first
                lock (_boundariesCacheLock)
                {
                    if (_cachedVatspyFirNames != null && (DateTime.Now - _vatspyFirNamesCacheTime) < BOUNDARIES_CACHE_TTL)
                    {
                        context.Response.AddHeader("X-Cache", "HIT");
                        ResponseJson(context, _cachedVatspyFirNames);
                        return;
                    }
                }

                // Check disk cache
                string diskCachePath = Path.Combine(BOUNDARIES_CACHE_DIR, "vatspy_firnames.json");
                if (File.Exists(diskCachePath))
                {
                    var fileInfo = new FileInfo(diskCachePath);
                    if ((DateTime.Now - fileInfo.LastWriteTime) < BOUNDARIES_CACHE_TTL)
                    {
                        string cachedData = File.ReadAllText(diskCachePath);
                        lock (_boundariesCacheLock)
                        {
                            _cachedVatspyFirNames = cachedData;
                            _vatspyFirNamesCacheTime = fileInfo.LastWriteTime;
                        }
                        context.Response.AddHeader("X-Cache", "DISK");
                        Console.WriteLine("VATSpy FIR names: loaded from disk cache");
                        ResponseJson(context, cachedData);
                        return;
                    }
                }

                // Fetch VATSpy.dat from GitHub and parse FIR names
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.UserAgent, "KneeboardServer/1.0");
                    string vatspyData = client.DownloadString("https://raw.githubusercontent.com/vatsimnetwork/vatspy-data-project/master/VATSpy.dat");

                    // Parse both [FIRs] and [Airports] sections to extract prefix -> name mappings
                    var firNames = new Dictionary<string, string>();
                    string currentSection = "";

                    foreach (string line in vatspyData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmedLine = line.Trim();

                        if (trimmedLine.StartsWith("["))
                        {
                            currentSection = trimmedLine.ToUpperInvariant();
                            continue;
                        }

                        if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                            continue;

                        var parts = trimmedLine.Split('|');

                        if (currentSection == "[FIRS]" && parts.Length >= 2)
                        {
                            // FIR format: ICAO|Name|Prefix|FIR Boundary ID
                            // Example: EDGG|Langen Radar|EDGG|EDGG
                            string icao = parts[0].Trim();
                            string name = parts[1].Trim();
                            if (!string.IsNullOrEmpty(icao) && !string.IsNullOrEmpty(name))
                            {
                                firNames[icao] = name;
                            }
                        }
                        else if (currentSection == "[AIRPORTS]" && parts.Length >= 5)
                        {
                            // Airport format: ICAO|Name|Lat|Lon|Prefix|FIR|...
                            // Example: KOAK|Oakland Intl CA|37.721292|-122.220717|OAK|KZOA|0
                            // We want to map Prefix (OAK) -> Name (Oakland Intl CA)
                            string name = parts[1].Trim();
                            string prefix = parts[4].Trim();
                            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(name) && !firNames.ContainsKey(prefix))
                            {
                                firNames[prefix] = name;
                            }
                        }
                    }

                    // Convert to JSON
                    var jsonBuilder = new StringBuilder("{");
                    bool first = true;
                    foreach (var kvp in firNames)
                    {
                        if (!first) jsonBuilder.Append(",");
                        // Escape quotes in name
                        string escapedName = kvp.Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        jsonBuilder.AppendFormat("\"{0}\":\"{1}\"", kvp.Key, escapedName);
                        first = false;
                    }
                    jsonBuilder.Append("}");

                    string jsonResult = jsonBuilder.ToString();

                    lock (_boundariesCacheLock)
                    {
                        _cachedVatspyFirNames = jsonResult;
                        _vatspyFirNamesCacheTime = DateTime.Now;
                    }

                    // Save to disk cache
                    try
                    {
                        Directory.CreateDirectory(BOUNDARIES_CACHE_DIR);
                        File.WriteAllText(diskCachePath, jsonResult);
                        Console.WriteLine($"VATSpy FIR names: saved to disk cache");
                    }
                    catch (Exception cacheEx)
                    {
                        Console.WriteLine($"VATSpy FIR names: failed to save disk cache: {cacheEx.Message}");
                    }

                    Console.WriteLine($"VATSpy FIR names loaded: {firNames.Count} entries");
                    context.Response.AddHeader("X-Cache", "MISS");
                    ResponseJson(context, jsonResult);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VATSpy FIR names fetch error: {ex.Message}");

                // Try to return cached data even if expired
                lock (_boundariesCacheLock)
                {
                    if (_cachedVatspyFirNames != null)
                    {
                        context.Response.AddHeader("X-Cache", "STALE");
                        ResponseJson(context, _cachedVatspyFirNames);
                        return;
                    }
                }

                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                ResponseJson(context, "{}");
            }
        }

        private void HandleIvaoBoundariesProxy(HttpListenerContext context)
        {
            try
            {
                // Check memory cache first
                lock (_boundariesCacheLock)
                {
                    if (_cachedIvaoBoundaries != null && (DateTime.Now - _ivaoBoundariesCacheTime) < BOUNDARIES_CACHE_TTL)
                    {
                        context.Response.AddHeader("X-Cache", "HIT");
                        ResponseJson(context, _cachedIvaoBoundaries);
                        return;
                    }
                }

                // Check disk cache
                string diskCachePath = Path.Combine(BOUNDARIES_CACHE_DIR, "ivao_boundaries.json");
                if (File.Exists(diskCachePath))
                {
                    var fileInfo = new FileInfo(diskCachePath);
                    if ((DateTime.Now - fileInfo.LastWriteTime) < BOUNDARIES_CACHE_TTL)
                    {
                        string cachedData = File.ReadAllText(diskCachePath);
                        lock (_boundariesCacheLock)
                        {
                            _cachedIvaoBoundaries = cachedData;
                            _ivaoBoundariesCacheTime = fileInfo.LastWriteTime;
                        }
                        context.Response.AddHeader("X-Cache", "DISK");
                        Console.WriteLine("IVAO Boundaries: loaded from disk cache");
                        ResponseJson(context, cachedData);
                        return;
                    }
                }

                // First, get the directory listing to find the latest file
                string latestFileUrl = GetLatestIvaoFileUrl();
                if (string.IsNullOrEmpty(latestFileUrl))
                {
                    throw new Exception("Could not find IVAO boundaries file");
                }

                // Download and extract the ZIP file
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "KneeboardServer/1.0");
                    byte[] zipData = client.DownloadData(latestFileUrl);

                    string jsonContent = ExtractIvaoJsonFromZip(zipData);
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        throw new Exception("Could not extract JSON from IVAO ZIP file");
                    }

                    lock (_boundariesCacheLock)
                    {
                        _cachedIvaoBoundaries = jsonContent;
                        _ivaoBoundariesCacheTime = DateTime.Now;
                    }

                    // Save to disk cache
                    try
                    {
                        Directory.CreateDirectory(BOUNDARIES_CACHE_DIR);
                        File.WriteAllText(diskCachePath, jsonContent);
                        Console.WriteLine("IVAO Boundaries: saved to disk cache");
                    }
                    catch (Exception cacheEx)
                    {
                        Console.WriteLine($"IVAO Boundaries: failed to save disk cache: {cacheEx.Message}");
                    }

                    context.Response.AddHeader("X-Cache", "MISS");
                    ResponseJson(context, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IVAO Boundaries fetch error: {ex.Message}");

                // Try to return cached data even if expired
                lock (_boundariesCacheLock)
                {
                    if (_cachedIvaoBoundaries != null)
                    {
                        context.Response.AddHeader("X-Cache", "STALE");
                        ResponseJson(context, _cachedIvaoBoundaries);
                        return;
                    }
                }

                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                ResponseJson(context, "{\"error\":\"Unable to fetch IVAO boundaries\"}");
            }
        }

        private string GetLatestIvaoFileUrl()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "KneeboardServer/1.0");
                    string html = client.DownloadString("https://www.littlenavmap.org/downloads/Airspace%20Boundaries/");

                    // Parse HTML to find IVAO ZIP files and get the latest one
                    // Files are named like "IVAO%20ATC%20Positions%2020250801.zip" (URL-encoded)
                    // or "IVAO ATC Positions 20250801.zip" (plain text)
                    var regex = new System.Text.RegularExpressions.Regex(@"href=""(IVAO[^""]+\.zip)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var matches = regex.Matches(html);

                    string latestFile = null;
                    int latestDate = 0;

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        string fileName = match.Groups[1].Value;
                        // Decode URL-encoded filename to extract date
                        string decodedFileName = Uri.UnescapeDataString(fileName);
                        // Extract date from filename (e.g., "20250801")
                        var dateMatch = System.Text.RegularExpressions.Regex.Match(decodedFileName, @"(\d{8})");
                        if (dateMatch.Success)
                        {
                            int fileDate = int.Parse(dateMatch.Groups[1].Value);
                            if (fileDate > latestDate)
                            {
                                latestDate = fileDate;
                                latestFile = fileName; // Keep original (possibly URL-encoded) filename
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(latestFile))
                    {
                        string url;
                        // File may already be URL-encoded in HTML, or not - handle both cases
                        if (latestFile.Contains("%"))
                        {
                            // Already URL-encoded
                            url = "https://www.littlenavmap.org/downloads/Airspace%20Boundaries/" + latestFile;
                        }
                        else
                        {
                            // Needs encoding
                            url = "https://www.littlenavmap.org/downloads/Airspace%20Boundaries/" + Uri.EscapeDataString(latestFile);
                        }
                        Console.WriteLine($"IVAO boundaries URL: {url}");
                        return url;
                    }
                    else
                    {
                        Console.WriteLine("No IVAO files found in directory listing");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting IVAO file list: {ex.Message}");
            }

            return null;
        }

        private string ExtractIvaoJsonFromZip(byte[] zipData)
        {
            try
            {
                using (var zipStream = new MemoryStream(zipData))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var entryStream = entry.Open())
                            using (var reader = new StreamReader(entryStream, Encoding.UTF8))
                            {
                                string rawJson = reader.ReadToEnd();
                                // Pre-filter to only include items with valid map_region
                                return FilterIvaoJson(rawJson);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting IVAO ZIP: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Filters IVAO JSON to only include entries with valid map_region (>2 points)
        /// and extracts only the needed fields (airport_id, position, name, map_region)
        /// with reduced coordinate precision (4 decimal places = ~11m accuracy)
        /// This reduces the data from ~29MB to ~3-5MB
        /// </summary>
        private string FilterIvaoJson(string rawJson)
        {
            try
            {
                var filteredItems = new List<string>();
                int arrayStart = rawJson.IndexOf('[');
                if (arrayStart < 0) return rawJson;

                int depth = 0;
                int itemStart = -1;
                bool inString = false;
                bool escaped = false;

                for (int i = arrayStart; i < rawJson.Length; i++)
                {
                    char c = rawJson[i];

                    if (escaped) { escaped = false; continue; }
                    if (c == '\\' && inString) { escaped = true; continue; }
                    if (c == '"') { inString = !inString; continue; }
                    if (inString) continue;

                    if (c == '{')
                    {
                        if (depth == 1) itemStart = i;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 1 && itemStart >= 0)
                        {
                            string item = rawJson.Substring(itemStart, i - itemStart + 1);
                            // Extract and minimize item if it has valid geometry
                            string minimized = MinimizeIvaoItem(item);
                            if (minimized != null)
                            {
                                filteredItems.Add(minimized);
                            }
                            itemStart = -1;
                        }
                    }
                    else if (c == '[' && depth == 0) depth = 1;
                    else if (c == ']' && depth == 1) break;
                }

                Console.WriteLine($"IVAO filter: {filteredItems.Count} items with valid geometry (minimized)");
                return "[" + string.Join(",", filteredItems) + "]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IVAO filter error: {ex.Message}, returning raw data");
                return rawJson;
            }
        }

        /// <summary>
        /// Extracts only needed fields from IVAO item and reduces coordinate precision
        /// Returns null if item has no valid map_region
        /// </summary>
        private string MinimizeIvaoItem(string jsonItem)
        {
            // Extract airport_id
            string airportId = ExtractJsonStringValue(jsonItem, "airport_id");
            // Extract position
            string position = ExtractJsonStringValue(jsonItem, "position");
            // Extract name
            string name = ExtractJsonStringValue(jsonItem, "name");

            // Extract and minimize map_region coordinates
            int mapRegionIdx = jsonItem.IndexOf("\"map_region\"");
            if (mapRegionIdx < 0) return null;

            int arrayStart = jsonItem.IndexOf('[', mapRegionIdx);
            if (arrayStart < 0) return null;

            // Find end of map_region array
            int depth = 0;
            int arrayEnd = -1;
            for (int i = arrayStart; i < jsonItem.Length; i++)
            {
                if (jsonItem[i] == '[') depth++;
                else if (jsonItem[i] == ']')
                {
                    depth--;
                    if (depth == 0) { arrayEnd = i; break; }
                }
            }
            if (arrayEnd < 0) return null;

            // Parse coordinates and minimize precision
            var coords = new List<string>();
            string mapRegion = jsonItem.Substring(arrayStart, arrayEnd - arrayStart + 1);

            // Extract {lat:..., lng:...} objects
            int objStart = -1;
            depth = 0;
            for (int i = 0; i < mapRegion.Length; i++)
            {
                if (mapRegion[i] == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (mapRegion[i] == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string coordObj = mapRegion.Substring(objStart, i - objStart + 1);
                        double? lat = ExtractJsonNumberValue(coordObj, "lat");
                        double? lng = ExtractJsonNumberValue(coordObj, "lng");
                        if (lat.HasValue && lng.HasValue)
                        {
                            // Round to 4 decimal places (~11m precision)
                            coords.Add($"{{\"lat\":{Math.Round(lat.Value, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)},\"lng\":{Math.Round(lng.Value, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
                        }
                        objStart = -1;
                    }
                }
            }

            // Need at least 3 points for a valid polygon
            if (coords.Count < 3) return null;

            // Build minimized JSON
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            if (!string.IsNullOrEmpty(airportId))
                sb.Append($"\"airport_id\":\"{EscapeJsonString(airportId)}\",");
            if (!string.IsNullOrEmpty(position))
                sb.Append($"\"position\":\"{EscapeJsonString(position)}\",");
            if (!string.IsNullOrEmpty(name))
                sb.Append($"\"name\":\"{EscapeJsonString(name)}\",");
            sb.Append("\"map_region\":[");
            sb.Append(string.Join(",", coords));
            sb.Append("]}");

            return sb.ToString();
        }

        private string ExtractJsonStringValue(string json, string key)
        {
            string searchKey = $"\"{key}\"";
            int keyIdx = json.IndexOf(searchKey);
            if (keyIdx < 0) return null;

            int colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0) return null;

            // Find start quote
            int startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return null;

            // Check if it's null
            string afterColon = json.Substring(colonIdx + 1, Math.Min(10, json.Length - colonIdx - 1)).Trim();
            if (afterColon.StartsWith("null")) return null;

            // Find end quote (handling escapes)
            int endQuote = startQuote + 1;
            while (endQuote < json.Length)
            {
                if (json[endQuote] == '\\') { endQuote += 2; continue; }
                if (json[endQuote] == '"') break;
                endQuote++;
            }
            if (endQuote >= json.Length) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        private double? ExtractJsonNumberValue(string json, string key)
        {
            string searchKey = $"\"{key}\"";
            int keyIdx = json.IndexOf(searchKey);
            if (keyIdx < 0) return null;

            int colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0) return null;

            // Find number start
            int numStart = colonIdx + 1;
            while (numStart < json.Length && (json[numStart] == ' ' || json[numStart] == '\t')) numStart++;

            // Find number end
            int numEnd = numStart;
            while (numEnd < json.Length && (char.IsDigit(json[numEnd]) || json[numEnd] == '.' || json[numEnd] == '-' || json[numEnd] == 'e' || json[numEnd] == 'E' || json[numEnd] == '+'))
                numEnd++;

            if (numEnd <= numStart) return null;

            string numStr = json.Substring(numStart, numEnd - numStart);
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;
            return null;
        }

        private string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private void HandleNominatimProxy(HttpListenerContext context)
        {
            var request = context.Request;
            var queryString = request.Url.Query;

            // Check if query is too short (Nominatim blocks very short queries)
            var queryParam = request.QueryString["q"] ?? string.Empty;
            if (queryParam.Length < 2)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                string payload = "[]"; // Return empty results for very short queries
                var buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                return;
            }

            // Build the target URL for Nominatim
            var targetUrl = $"https://nominatim.openstreetmap.org/search{queryString}";

            try
            {
                var outboundRequest = (HttpWebRequest)WebRequest.Create(targetUrl);
                outboundRequest.Method = "GET";
                outboundRequest.Accept = "application/json";
                outboundRequest.UserAgent = "KneeboardServer/1.0 Flight Planning Application"; // Nominatim requires a descriptive User-Agent
                outboundRequest.Referer = $"http://localhost:{_port}/"; // Add Referer header
                outboundRequest.Timeout = 10000;
                outboundRequest.ReadWriteTimeout = 10000;

                using (var upstreamResponse = (HttpWebResponse)outboundRequest.GetResponse())
                {
                    context.Response.StatusCode = (int)upstreamResponse.StatusCode;
                    context.Response.ContentType = upstreamResponse.ContentType ?? "application/json";

                    using (var upstreamStream = upstreamResponse.GetResponseStream())
                    {
                        upstreamStream.CopyTo(context.Response.OutputStream);
                    }
                }
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                context.Response.StatusCode = httpResponse != null
                    ? (int)httpResponse.StatusCode
                    : (int)HttpStatusCode.BadGateway;

                Console.WriteLine($"Nominatim Proxy Error: {ex.Message}");
                Console.WriteLine($"Requested URL: {targetUrl}");
                if (httpResponse != null)
                {
                    Console.WriteLine($"Upstream Status: {httpResponse.StatusCode}");
                }

                // Return JSON error instead of HTML from Nominatim
                string payload = "{\"error\":\"Nominatim service unavailable or request blocked. Try a more specific search query.\"}";
                var buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        // Static HttpClient for wind proxy - reused for connection pooling
        // AllowAutoRedirect=false because we handle redirects manually (NOAA does cross-protocol redirects)
        private static readonly HttpClient _windHttpClient;

        static SimpleHTTPServer()
        {
            // Configure HttpClient WITHOUT automatic redirect - we handle redirects manually
            // NOAA NOMADS does cross-protocol redirects (HTTPS->HTTP) which .NET refuses to follow automatically
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,  // MANUAL redirect handling
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _windHttpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _windHttpClient.DefaultRequestHeaders.Add("User-Agent", "KneeboardServer/2.0 Flight Planning Application");
            _windHttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        }

        private void HandleWindProxy(HttpListenerContext context)
        {
            var request = context.Request;
            var path = request.Url.AbsolutePath;
            var queryString = request.Url.Query;

            // Extract the GFS path from /api/wind/gfs{date}/gfs_1p00_{run}z.ascii?...
            var idx = path.IndexOf("/api/wind/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.OutputStream.Close();
                return;
            }

            var gfsPart = path.Substring(idx + "/api/wind/".Length);
            var targetUrl = $"https://nomads.ncep.noaa.gov/dods/{gfsPart}{queryString}";

            Console.WriteLine($"[Wind Proxy] Requesting: {targetUrl}");

            try
            {
                // Manual redirect following - NOAA does HTTPS->HTTP redirects which .NET won't auto-follow
                HttpResponseMessage response = null;
                var currentUrl = targetUrl;
                int maxRedirects = 5;

                for (int i = 0; i <= maxRedirects; i++)
                {
                    var requestMsg = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                    var responseTask = _windHttpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead);
                    responseTask.Wait();
                    response = responseTask.Result;

                    // Check for redirect (3xx status codes)
                    int statusCode = (int)response.StatusCode;
                    if (statusCode >= 300 && statusCode < 400)
                    {
                        var redirectUrl = response.Headers.Location;
                        if (redirectUrl == null)
                        {
                            Console.WriteLine($"[Wind Proxy] Redirect {statusCode} without Location header");
                            break;
                        }

                        // Handle relative redirects
                        if (!redirectUrl.IsAbsoluteUri)
                        {
                            redirectUrl = new Uri(new Uri(currentUrl), redirectUrl);
                        }

                        currentUrl = redirectUrl.ToString();
                        Console.WriteLine($"[Wind Proxy] Following {statusCode} redirect to: {currentUrl}");
                        response.Dispose();
                        continue;
                    }

                    break; // Not a redirect, use this response
                }

                Console.WriteLine($"[Wind Proxy] Final Response: {response.StatusCode}, ContentLength: {response.Content.Headers.ContentLength}");

                if (response.IsSuccessStatusCode)
                {
                    var contentTask = response.Content.ReadAsByteArrayAsync();
                    contentTask.Wait();
                    var content = contentTask.Result;

                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/plain";
                    context.Response.ContentLength64 = content.Length;
                    context.Response.OutputStream.Write(content, 0, content.Length);
                }
                else
                {
                    Console.WriteLine($"[Wind Proxy] Non-success status: {response.StatusCode}");
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = "text/plain";
                }
            }
            catch (AggregateException ae)
            {
                var innerEx = ae.InnerException ?? ae;
                Console.WriteLine($"[Wind Proxy] Error: {innerEx.Message}");
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                context.Response.ContentType = "text/plain";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Wind Proxy] Error: {ex.Message}");
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                context.Response.ContentType = "text/plain";
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        // ============================================================================
        // OpenAIP Cache Methods
        // ============================================================================

        /// <summary>
        /// Gets the cache file path for a tile
        /// </summary>
        private static string GetTileCachePath(string tilePart)
        {
            // tilePart is like "10/532/341.png"
            return Path.Combine(CACHE_DIR, "tiles", tilePart.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Gets the cache file path for API data
        /// </summary>
        private static string GetApiCachePath(string endpoint, string lat, string lng, string dist)
        {
            // Sanitize coordinates for filename (replace dots and minus signs)
            string safeLat = lat.Replace(".", "_").Replace("-", "n");
            string safeLng = lng.Replace(".", "_").Replace("-", "n");
            string safeDist = dist.Replace(".", "_");
            string filename = $"{endpoint}_{safeLat}_{safeLng}_{safeDist}.json";
            return Path.Combine(CACHE_DIR, "api", filename);
        }

        /// <summary>
        /// Checks if a cache file exists and is still valid (not expired)
        /// </summary>
        private static bool IsCacheValid(string cachePath)
        {
            if (!File.Exists(cachePath))
                return false;

            var fileInfo = new FileInfo(cachePath);
            return (DateTime.Now - fileInfo.LastWriteTime) < CACHE_TTL;
        }

        /// <summary>
        /// Reads data from cache file
        /// </summary>
        private static byte[] ReadFromCache(string cachePath)
        {
            lock (_cacheLock)
            {
                return File.ReadAllBytes(cachePath);
            }
        }

        /// <summary>
        /// Writes data to cache file, creating directories as needed
        /// Respects max cache size setting (0 = unlimited)
        /// </summary>
        private static void WriteToCache(string cachePath, byte[] data)
        {
            try
            {
                lock (_cacheLock)
                {
                    // Check max cache size before writing
                    long maxSizeMB = Properties.Settings.Default.maxCacheSizeMB;
                    if (maxSizeMB > 0)
                    {
                        long currentSize = GetCacheSizeInternal();
                        long maxSizeBytes = maxSizeMB * 1024 * 1024;

                        // If adding this file would exceed limit, clean up old files first
                        if (currentSize + data.Length > maxSizeBytes)
                        {
                            CleanupOldestCacheFiles(data.Length);
                        }
                    }

                    string directory = Path.GetDirectoryName(cachePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllBytes(cachePath, data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cache write error: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal method to get cache size (assumes lock is held)
        /// </summary>
        private static long GetCacheSizeInternal()
        {
            if (!Directory.Exists(CACHE_DIR))
                return 0;

            return Directory.GetFiles(CACHE_DIR, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }

        /// <summary>
        /// Removes oldest cache files to make room for new data
        /// </summary>
        private static void CleanupOldestCacheFiles(long bytesNeeded)
        {
            if (!Directory.Exists(CACHE_DIR))
                return;

            try
            {
                long maxSizeMB = Properties.Settings.Default.maxCacheSizeMB;
                long maxSizeBytes = maxSizeMB * 1024 * 1024;
                long currentSize = GetCacheSizeInternal();
                long targetSize = maxSizeBytes - bytesNeeded - (10 * 1024 * 1024); // Leave 10MB buffer

                if (currentSize <= targetSize)
                    return;

                // Get all cache files sorted by last access time (oldest first)
                var files = Directory.GetFiles(CACHE_DIR, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastAccessTime)
                    .ToList();

                long bytesFreed = 0;
                long bytesToFree = currentSize - targetSize;

                foreach (var file in files)
                {
                    if (bytesFreed >= bytesToFree)
                        break;

                    try
                    {
                        long fileSize = file.Length;
                        file.Delete();
                        bytesFreed += fileSize;
                    }
                    catch { }
                }

                Console.WriteLine($"Cache cleanup: freed {bytesFreed / 1024 / 1024}MB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cache cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the entire OpenAIP cache directory
        /// </summary>
        public static void ClearOpenAipCache()
        {
            lock (_cacheLock)
            {
                if (Directory.Exists(CACHE_DIR))
                {
                    Directory.Delete(CACHE_DIR, true);
                    Console.WriteLine("OpenAIP cache cleared");
                }
            }
        }

        /// <summary>
        /// Gets the current cache size in bytes
        /// </summary>
        public static long GetCacheSize()
        {
            if (!Directory.Exists(CACHE_DIR))
                return 0;

            return Directory.GetFiles(CACHE_DIR, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }

        private void HandleOpenAipProxy(HttpListenerContext context, string endpoint)
        {
            var request = context.Request;
            var lat = (request.QueryString["lat"] ?? string.Empty).Trim();
            var lng = (request.QueryString["lng"] ?? string.Empty).Trim();
            var dist = (request.QueryString["dist"] ?? string.Empty).Trim();
            bool isTilesEndpoint = endpoint.Equals("tiles", StringComparison.OrdinalIgnoreCase);

            if (!isTilesEndpoint && (string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lng) || string.IsNullOrWhiteSpace(dist)))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.OutputStream.Close();
                return;
            }

            // Determine cache path
            string cachePath = null;
            string tilePart = null;

            if (isTilesEndpoint)
            {
                var path = request.Url.AbsolutePath;
                var idx = path.IndexOf("/api/openaip/tiles/", StringComparison.OrdinalIgnoreCase);
                tilePart = path.Substring(idx + "/api/openaip/tiles/".Length);
                cachePath = GetTileCachePath(tilePart);
            }
            else
            {
                cachePath = GetApiCachePath(endpoint, lat, lng, dist);
            }

            // Check cache first
            if (IsCacheValid(cachePath))
            {
                try
                {
                    byte[] cachedData = ReadFromCache(cachePath);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = isTilesEndpoint ? "image/png" : "application/json";
                    context.Response.ContentLength64 = cachedData.Length;
                    context.Response.AddHeader("X-Cache", "HIT");
                    context.Response.OutputStream.Write(cachedData, 0, cachedData.Length);
                    context.Response.OutputStream.Close();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cache read error: {ex.Message}");
                    // Continue to fetch from upstream
                }
            }

            var builder = new StringBuilder();
            if (!isTilesEndpoint)
            {
                builder.Append("https://api.core.openaip.net/api/")
                       .Append(endpoint)
                       .Append("?page=1&limit=1000&pos=")
                       .Append(Uri.EscapeDataString(lat))
                       .Append("%2C")
                       .Append(Uri.EscapeDataString(lng))
                       .Append("&dist=")
                       .Append(Uri.EscapeDataString(dist))
                       .Append("&sortBy=name&sortDesc=true&approved=true&searchOptLwc=false")
                       .Append("&apiKey=")
                       .Append(Uri.EscapeDataString(OPENAIP_API_KEY));
            }
            else
            {
                // OpenAIP tiles API uses subdomains (a, b, c) for load balancing
                // Rotate through subdomains based on tile coordinates for better distribution
                string[] subdomains = { "a", "b", "c" };
                int subdomainIndex = Math.Abs(tilePart.GetHashCode()) % subdomains.Length;
                string subdomain = subdomains[subdomainIndex];
                builder.Append("https://")
                       .Append(subdomain)
                       .Append(".api.tiles.openaip.net/api/data/openaip/")
                       .Append(tilePart)
                       .Append("?apiKey=")
                       .Append(Uri.EscapeDataString(OPENAIP_API_KEY));
            }

            HttpWebResponse upstreamResponse = null;
            try
            {
                var outboundRequest = (HttpWebRequest)WebRequest.Create(builder.ToString());
                outboundRequest.Method = "GET";
                outboundRequest.Accept = isTilesEndpoint ? "image/png" : "application/json";
                // Always add header for authentication
                outboundRequest.Headers.Add("x-openaip-client-id", OPENAIP_API_KEY);
                outboundRequest.Timeout = 10000; // 10 second timeout
                outboundRequest.ReadWriteTimeout = 10000; // 10 second read/write timeout
                upstreamResponse = (HttpWebResponse)outboundRequest.GetResponse();

                // Read response into memory for caching
                byte[] responseData;
                using (var memoryStream = new MemoryStream())
                {
                    using (var upstreamStream = upstreamResponse.GetResponseStream())
                    {
                        upstreamStream.CopyTo(memoryStream);
                    }
                    responseData = memoryStream.ToArray();
                }

                // Write to cache (async, don't block response)
                if (responseData.Length > 0)
                {
                    ThreadPool.QueueUserWorkItem(_ => WriteToCache(cachePath, responseData));
                }

                context.Response.StatusCode = (int)upstreamResponse.StatusCode;
                context.Response.ContentType = upstreamResponse.ContentType ?? (isTilesEndpoint ? "image/png" : "application/json");
                context.Response.ContentLength64 = responseData.Length;
                context.Response.AddHeader("X-Cache", "MISS");
                context.Response.OutputStream.Write(responseData, 0, responseData.Length);
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                context.Response.StatusCode = httpResponse != null
                    ? (int)httpResponse.StatusCode
                    : (int)HttpStatusCode.BadGateway;

                // Log error details to file AND console
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OpenAIP Proxy Error\n" +
                    $"Message: {ex.Message}\n" +
                    $"URL: {builder.ToString()}\n" +
                    $"Status: {httpResponse?.StatusCode}\n";

                try
                {
                    File.AppendAllText(Path.Combine(_rootDirectory, "openaip_errors.log"), logMessage + "\n");
                }
                catch { }

                Console.WriteLine($"OpenAIP Proxy Error: {ex.Message}");
                Console.WriteLine($"Requested URL: {builder.ToString()}");
                if (httpResponse != null)
                {
                    Console.WriteLine($"Upstream Status: {httpResponse.StatusCode}");
                }

                string payload = "{\"error\":\"Unable to reach openAIP\"}";
                if (httpResponse != null)
                {
                    try
                    {
                        using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                        {
                            payload = reader.ReadToEnd();
                            Console.WriteLine($"Upstream Response: {payload}");
                        }
                    }
                    catch
                    {
                        payload = "{\"error\":\"Unable to reach openAIP\"}";
                    }
                }

                // For tiles, return empty PNG instead of JSON error
                if (isTilesEndpoint)
                {
                    context.Response.ContentType = "image/png";
                    context.Response.ContentLength64 = 0;
                }
                else
                {
                    var buffer = Encoding.UTF8.GetBytes(payload);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            finally
            {
                upstreamResponse?.Close();
                context.Response.OutputStream.Close();
            }
        }

        private void Process(HttpListenerContext context)
        {

            HttpListenerRequest request = context.Request;



            HttpListenerResponse response = context.Response;

            // ✅ Allow CORS
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            // ✅ Handle preflight OPTIONS request
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.OutputStream.Close();
                return;
            }

            // ✅ Handle HEAD requests for file existence checks (avoids 404 console errors)
            if (request.HttpMethod == "HEAD")
            {
                try
                {
                    string headCommand = context.Request.Url.AbsolutePath.Substring(1);
                    // Replace forward slashes with backslashes for Windows path compatibility
                    headCommand = headCommand.Replace('/', '\\');
                    string headFilePath = Path.Combine(_rootDirectory, headCommand);

                    if (System.IO.File.Exists(headFilePath))
                    {
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.ContentType = _mimeTypeMappings.TryGetValue(Path.GetExtension(headFilePath), out string headMime) ? headMime : "application/octet-stream";
                        response.ContentLength64 = new FileInfo(headFilePath).Length;
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                }
                catch (Exception)
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                response.OutputStream.Close();
                return;
            }

            string command = context.Request.Url.AbsolutePath;

            command = command.Substring(1);
            //Console.WriteLine("Command: " + command);

            if (string.IsNullOrEmpty(command))
            {
                foreach (string indexFile in _indexFiles)
                {
                    if (System.IO.File.Exists(Path.Combine(_rootDirectory, indexFile)))
                    {
                        command = indexFile;
                        break;
                    }
                }
            }
            else if (command == "checkServerConnection")
            {
                ResponseString(context, "true");
            }
            else if (command == "setNavlogValues")
            {
                string postedText = GetPostedText(request);
                Console.WriteLine($"[NavlogSync] Server received navlog POST, length: {postedText.Length}");

                // Store the new values with timestamp
                values = postedText;
                valuesTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Properties.Settings.Default.values = values;
                Properties.Settings.Default.valuesTimestamp = valuesTimestamp;
                Properties.Settings.Default.Save();

                Console.WriteLine($"[NavlogSync] Server stored navlog data, new length: {values.Length}, timestamp: {valuesTimestamp}");
                ResponseString(context, valuesTimestamp.ToString());
            }
            else if (command == "synchronizeFlightplan")
            {
                string flightplan = Kneeboard_Server.syncFlightplan();
                if (flightplan != "")
                {
                    ResponseString(context, flightplan);
                    // Don't clear flightplan - keep it available for map auto-load
                }
                else
                {
                    ResponseString(context, "");
                }
            }
            else if (command == "getNavlogValues")
            {
                string navlogData = Properties.Settings.Default.values ?? "";
                long timestamp = Properties.Settings.Default.valuesTimestamp;
                Console.WriteLine($"[NavlogSync] Server sending navlog data, length: {navlogData.Length}, timestamp: {timestamp}");

                // Send data with timestamp as header
                context.Response.Headers.Add("X-Navlog-Timestamp", timestamp.ToString());
                ResponseString(context, navlogData);
            }
            else if (command == "getNavlogTimestamp")
            {
                long timestamp = Properties.Settings.Default.valuesTimestamp;
                ResponseString(context, timestamp.ToString());
            }
            else if (command == "clearNavlogValues")
            {
                Console.WriteLine("[NavlogSync] Server clearing navlog data");
                values = "";
                valuesTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Properties.Settings.Default.values = "";
                Properties.Settings.Default.valuesTimestamp = valuesTimestamp;
                Properties.Settings.Default.Save();
                Console.WriteLine($"[NavlogSync] Server navlog data cleared, timestamp: {valuesTimestamp}");
                ResponseString(context, "OK");
            }
            else if (command == "getFlightplan")
            {
                if (Kneeboard_Server.flightplan != null)
                {
                    ResponseString(context, Kneeboard_Server.flightplan);
                    // Don't clear flightplan - keep it available for map auto-load
                }
                else
                {
                    ResponseString(context, "");
                }
            }
            else if (command == "clearFlightplan")
            {
                // Full clear: removes everything including SimBrief cache
                Kneeboard_Server.flightplan = null;
                Kneeboard_Server.cachedSimbriefTimeGenerated = null;  // Reset SimBrief timestamp cache
                Kneeboard_Server.simbriefOFPData = null;              // Reset OFP data

                // Clear persisted flightplan data
                Kneeboard_Server.ClearPersistedFlightplanData();

                // Also clear navlog data
                Properties.Settings.Default.values = "";
                Properties.Settings.Default.valuesTimestamp = 0;
                Properties.Settings.Default.Save();
                Console.WriteLine("[NavlogSync] Navlog data and SimBrief cache cleared along with flight plan");

                ResponseString(context, "cleared");
            }
            else if (command == "clearLocalFlightplan")
            {
                // Soft clear: only clears navlog but keeps SimBrief cache intact
                // Used when user deletes flightplan via map - they can immediately sync again
                Properties.Settings.Default.values = "";
                Properties.Settings.Default.valuesTimestamp = 0;
                Properties.Settings.Default.Save();

                // Check if server still has a flightplan from SimBrief
                bool hasServerFlightplan = !string.IsNullOrEmpty(Kneeboard_Server.flightplan);
                Console.WriteLine("[NavlogSync] Local navlog cleared, SimBrief cache preserved: " + hasServerFlightplan);

                ResponseJson(context, $"{{\"cleared\":true,\"hasServerFlightplan\":{hasServerFlightplan.ToString().ToLower()}}}");
            }
            else if (command == "hasFlightplan")
            {
                ResponseString(context, Kneeboard_Server.flightplan != null ? "true" : "false");
            }
            else if (command == "hasSimbriefId")
            {
                bool hasId = !string.IsNullOrEmpty(Properties.Settings.Default.simbriefId);
                ResponseJson(context, $"{{\"hasSimbriefId\":{hasId.ToString().ToLower()}}}");
            }
            else if (command == "checkSimbriefUpdate")
            {
                bool updateAvailable = Kneeboard_Server.CheckSimbriefUpdateAvailable();
                ResponseJson(context, $"{{\"updateAvailable\":{updateAvailable.ToString().ToLower()}}}");
            }
            else if (command == "hasServerFlightplan")
            {
                // Check if server has a flightplan loaded (from background sync or previous session)
                bool hasFlightplan = !string.IsNullOrEmpty(Kneeboard_Server.flightplan);
                ResponseJson(context, $"{{\"hasFlightplan\":{hasFlightplan.ToString().ToLower()}}}");
            }
            else if (command == "getFlightplanHash")
            {
                // Optimierter Endpoint: Gibt Hash zurück um unnötige Full-Data-Transfers zu vermeiden
                if (!string.IsNullOrEmpty(Kneeboard_Server.flightplan))
                {
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Kneeboard_Server.flightplan));
                        string hashString = Convert.ToBase64String(hash).Substring(0, 16);
                        ResponseJson(context, $"{{\"exists\":true,\"hash\":\"{hashString}\"}}");
                    }
                }
                else
                {
                    ResponseJson(context, "{\"exists\":false}");
                }
            }
            else if (command.StartsWith("api/metar/", StringComparison.OrdinalIgnoreCase))
            {
                HandleNoaaProxy(context, command.Substring("api/metar/".Length), false);
                return;
            }
            else if (command.StartsWith("api/taf/", StringComparison.OrdinalIgnoreCase))
            {
                HandleNoaaProxy(context, command.Substring("api/taf/".Length), true);
                return;
            }
            else if (command == "api/boundaries/vatsim")
            {
                HandleVatsimBoundariesProxy(context);
                return;
            }
            else if (command == "api/boundaries/ivao")
            {
                HandleIvaoBoundariesProxy(context);
                return;
            }
            else if (command == "api/vatspy/firnames")
            {
                HandleVatspyFirNamesProxy(context);
                return;
            }
            else if (command == "synchronizeControllers")
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string controllers = client.DownloadString("https://data.vatsim.net/v3/vatsim-data.json");
                        ResponseJson(context, controllers);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VATSIM sync error: {ex.Message}");
                    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                    try { context.Response.OutputStream.Close(); } catch { }
                }
                return;
            }
            else if (command == "synchronizeTransceivers")
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string transceivers = client.DownloadString("https://data.vatsim.net/v3/transceivers-data.json");
                        ResponseJson(context, transceivers);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VATSIM transceivers sync error: {ex.Message}");
                    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                    try { context.Response.OutputStream.Close(); } catch { }
                }
                return;
            }
            else if (command == "synchronizeIVAO")
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string controllers = client.DownloadString("https://api.ivao.aero/v2/tracker/whazzup");
                        ResponseJson(context, controllers);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"IVAO sync error: {ex.Message}");
                    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                    try { context.Response.OutputStream.Close(); } catch { }
                }
                return;
            }
            else if (command.StartsWith("api/openaip/airports", StringComparison.OrdinalIgnoreCase))
            {
                HandleOpenAipProxy(context, "airports");
                return;
            }
            else if (command.StartsWith("api/openaip/navaids", StringComparison.OrdinalIgnoreCase))
            {
                HandleOpenAipProxy(context, "navaids");
                return;
            }
            else if (command.StartsWith("api/openaip/reporting-points", StringComparison.OrdinalIgnoreCase))
            {
                HandleOpenAipProxy(context, "reporting-points");
                return;
            }
            else if (command.StartsWith("api/openaip/tiles/", StringComparison.OrdinalIgnoreCase))
            {
                HandleOpenAipProxy(context, "tiles");
                return;
            }
            else if (command == "api/elevation")
            {
                HandleElevationProxy(context);
                return;
            }
            else if (command.StartsWith("api/dfs/tiles/", StringComparison.OrdinalIgnoreCase))
            {
                HandleDfsProxy(context);
                return;
            }
            else if (command.StartsWith("api/nominatim/search", StringComparison.OrdinalIgnoreCase))
            {
                HandleNominatimProxy(context);
                return;
            }
            else if (command.StartsWith("api/wind/", StringComparison.OrdinalIgnoreCase))
            {
                HandleWindProxy(context);
                return;
            }
            else if (command == "getDocumentsList")
            {
                string documentsString = _kneeboardServer.GetFileList();
                ResponseString(context, documentsString);
            }
            else if (command == "getMapLayers")
            {
                try
                {
                    string layersFilePath = Path.Combine(_rootDirectory, "map-layers.json");
                    if (System.IO.File.Exists(layersFilePath))
                    {
                        string layersJson = System.IO.File.ReadAllText(layersFilePath, Encoding.UTF8);
                        ResponseJson(context, layersJson);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        ResponseJson(context, "{\"error\":\"map-layers.json not found\"}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading map layers: {ex.Message}");
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ResponseJson(context, "{\"error\":\"Failed to load map layers\"}");
                }
                return;
            }
            else if (command.StartsWith("getDocumentText"))
            {
                string[] parts = command.Split(new[] { '-' }, 2);
                if (parts.Length < 2)
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentLength64 = 0;
                    context.Response.OutputStream.Close();   // ✅
                    return;
                }

                string documentNameEncoded = parts[1];
                string documentName = Uri.UnescapeDataString(documentNameEncoded);

                Console.WriteLine("Dokument-Request: " + documentName);

                KneeboardFile fileEntry = null;

                fileEntry = _kneeboardServer.filesList
                    .FirstOrDefault(r => string.Equals(r.Name, documentName, StringComparison.OrdinalIgnoreCase));

                if (fileEntry == null && _kneeboardServer.foldersList != null)
                {
                    foreach (var folder in _kneeboardServer.foldersList)
                    {
                        if (folder?.Files == null) continue;
                        fileEntry = folder.Files.FirstOrDefault(r =>
                            string.Equals(r.Name, documentName, StringComparison.OrdinalIgnoreCase));
                        if (fileEntry != null)
                        {
                            break;
                        }
                    }
                }

                if (fileEntry == null)
                {
                    context.Response.StatusCode = 404;
                    context.Response.ContentLength64 = 0;
                    context.Response.OutputStream.Close();   // ✅
                    return;
                }

                byte[] pdfFile;
                try
                {
                    pdfFile = System.IO.File.ReadAllBytes(fileEntry.Path);
                }
                catch (Exception)
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentLength64 = 0;
                    context.Response.OutputStream.Close();   // ✅
                    return;
                }

                // Erfolgspfad
                string base64 = Convert.ToBase64String(pdfFile);

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(base64);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;

                using (System.IO.Stream output = context.Response.OutputStream)
                {
                    output.Write(buffer, 0, buffer.Length);
                }
                return;
            }
            else
            {
                // Serve static files
                string filePath = Path.Combine(_rootDirectory, command.Replace('/', '\\'));

                // Case-insensitive file lookup for Windows compatibility
                if (!File.Exists(filePath))
                {
                    // Try to find file with different case
                    string directory = Path.GetDirectoryName(filePath);
                    string fileName = Path.GetFileName(filePath);
                    if (Directory.Exists(directory))
                    {
                        var match = Directory.GetFiles(directory)
                            .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            filePath = match;
                        }
                    }
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            // Set response headers
                            context.Response.ContentType = _mimeTypeMappings.TryGetValue(Path.GetExtension(filePath), out string mime) ? mime : "application/octet-stream";
                            context.Response.ContentLength64 = input.Length;
                            context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                            context.Response.AddHeader("Last-Modified", File.GetLastWriteTime(filePath).ToString("r"));

                            // Add cache headers for static assets
                            string ext = Path.GetExtension(filePath).ToLowerInvariant();
                            if (ext == ".jpg" || ext == ".png" || ext == ".gif" || ext == ".ico")
                            {
                                context.Response.AddHeader("Cache-Control", "public, max-age=3600");
                            }

                            // Stream file with larger buffer for better performance
                            byte[] buffer = new byte[64 * 1024]; // 64KB buffer
                            int bytesRead;
                            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                context.Response.OutputStream.Write(buffer, 0, bytesRead);
                            }
                        }

                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error serving file {filePath}: {ex.Message}");
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }

            try { context.Response.OutputStream.Close(); } catch { }
        }

        private static string GetPostedText(HttpListenerRequest request)
        {
            string text = "";
            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }
            return text;
        }

        private void Initialize(string path, int port)
        {
            // Increase connection limit for better tile loading performance
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;

            this._rootDirectory = path;
            this._port = port;
            _serverThread = new Thread(this.Listen)
            {
                IsBackground = true
            };
            _serverThread.Start();
        }
    }

}
