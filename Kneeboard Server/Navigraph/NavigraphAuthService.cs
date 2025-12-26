using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kneeboard_Server.Navigraph
{
    /// <summary>
    /// Handles OAuth 2.0 Device Authorization Flow for Navigraph API
    /// </summary>
    public class NavigraphAuthService : IDisposable
    {
        #region Constants

        private const string DEVICE_AUTH_ENDPOINT = "https://identity.api.navigraph.com/connect/deviceauthorization";
        private const string TOKEN_ENDPOINT = "https://identity.api.navigraph.com/connect/token";
        private const string USERINFO_ENDPOINT = "https://identity.api.navigraph.com/connect/userinfo";
        private const string SCOPES = "openid offline_access fmsdata";

        #endregion

        #region Fields

        private readonly HttpClient _httpClient;
        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;
        private string _codeVerifier;
        private CancellationTokenSource _pollCancellation;
        private bool _disposed;

        // Client credentials - should be loaded from config
        private string _clientId;
        private string _clientSecret;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the user is currently authenticated
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry;

        /// <summary>
        /// Current access token (null if not authenticated)
        /// </summary>
        public string AccessToken => IsAuthenticated ? _accessToken : null;

        /// <summary>
        /// Username of authenticated user
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Whether client credentials are configured
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(_clientId);

        #endregion

        #region Events

        /// <summary>
        /// Raised when device code is received (show to user)
        /// </summary>
        public event Action<DeviceCodeResponse> OnDeviceCodeReceived;

        /// <summary>
        /// Raised when authentication completes (success or failure)
        /// </summary>
        public event Action<bool, string> OnAuthenticationComplete;

        /// <summary>
        /// Raised when authentication status changes
        /// </summary>
        public event Action<bool> OnAuthStatusChanged;

        #endregion

        #region Constructor

        public NavigraphAuthService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KneeboardServer/2.0");
            LoadClientCredentials();
            LoadTokens();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configure client credentials
        /// </summary>
        public void Configure(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            SaveClientCredentials();
        }

        /// <summary>
        /// Start the device authorization flow
        /// </summary>
        public async Task<DeviceCodeResponse> StartDeviceAuthFlowAsync()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Navigraph Client ID not configured. Please set up your Navigraph developer credentials.");
            }

            // Generate PKCE code verifier and challenge
            _codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(_codeVerifier);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret ?? ""),
                new KeyValuePair<string, string>("scope", SCOPES),
                new KeyValuePair<string, string>("code_challenge", codeChallenge),
                new KeyValuePair<string, string>("code_challenge_method", "S256")
            });

            try
            {
                var response = await _httpClient.PostAsync(DEVICE_AUTH_ENDPOINT, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Navigraph] Device auth failed: {responseContent}");
                    throw new Exception($"Device authorization failed: {response.StatusCode}");
                }

                var json = JObject.Parse(responseContent);
                var deviceCode = new DeviceCodeResponse
                {
                    DeviceCode = json["device_code"]?.ToString(),
                    UserCode = json["user_code"]?.ToString(),
                    VerificationUri = json["verification_uri"]?.ToString(),
                    VerificationUriComplete = json["verification_uri_complete"]?.ToString(),
                    ExpiresIn = json["expires_in"]?.Value<int>() ?? 600,
                    Interval = json["interval"]?.Value<int>() ?? 5
                };

                Console.WriteLine($"[Navigraph] Device code received. User code: {deviceCode.UserCode}");
                OnDeviceCodeReceived?.Invoke(deviceCode);

                return deviceCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Device auth error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Poll for token after user completes authorization
        /// </summary>
        public async Task<bool> PollForTokenAsync(string deviceCode, int interval)
        {
            _pollCancellation = new CancellationTokenSource();
            int currentInterval = interval;
            int maxAttempts = 120; // ~10 minutes with 5s interval
            int attempts = 0;

            while (!_pollCancellation.Token.IsCancellationRequested && attempts < maxAttempts)
            {
                await Task.Delay(currentInterval * 1000, _pollCancellation.Token);
                attempts++;

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret ?? ""),
                    new KeyValuePair<string, string>("code_verifier", _codeVerifier)
                });

                try
                {
                    var response = await _httpClient.PostAsync(TOKEN_ENDPOINT, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseContent);

                    if (response.IsSuccessStatusCode)
                    {
                        // Success!
                        _accessToken = json["access_token"]?.ToString();
                        _refreshToken = json["refresh_token"]?.ToString();
                        int expiresIn = json["expires_in"]?.Value<int>() ?? 3600;
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 1 minute buffer

                        SaveTokens();
                        await FetchUsernameAsync();

                        Console.WriteLine($"[Navigraph] Authentication successful! User: {Username}");
                        OnAuthenticationComplete?.Invoke(true, null);
                        OnAuthStatusChanged?.Invoke(true);
                        return true;
                    }
                    else
                    {
                        string error = json["error"]?.ToString();

                        switch (error)
                        {
                            case "authorization_pending":
                                // User hasn't completed authorization yet, continue polling
                                continue;

                            case "slow_down":
                                // Increase interval
                                currentInterval += 5;
                                continue;

                            case "access_denied":
                                Console.WriteLine("[Navigraph] User denied authorization");
                                OnAuthenticationComplete?.Invoke(false, "Authorization denied by user");
                                return false;

                            case "expired_token":
                                Console.WriteLine("[Navigraph] Device code expired");
                                OnAuthenticationComplete?.Invoke(false, "Authorization code expired. Please try again.");
                                return false;

                            default:
                                Console.WriteLine($"[Navigraph] Token error: {error}");
                                OnAuthenticationComplete?.Invoke(false, error);
                                return false;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("[Navigraph] Polling cancelled");
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Navigraph] Polling error: {ex.Message}");
                    // Continue polling on network errors
                }
            }

            OnAuthenticationComplete?.Invoke(false, "Timeout waiting for authorization");
            return false;
        }

        /// <summary>
        /// Cancel ongoing polling
        /// </summary>
        public void CancelPolling()
        {
            _pollCancellation?.Cancel();
        }

        /// <summary>
        /// Refresh the access token using refresh token
        /// </summary>
        public async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                Console.WriteLine("[Navigraph] No refresh token available");
                return false;
            }

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret ?? ""),
                new KeyValuePair<string, string>("refresh_token", _refreshToken)
            });

            try
            {
                var response = await _httpClient.PostAsync(TOKEN_ENDPOINT, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(responseContent);
                    _accessToken = json["access_token"]?.ToString();

                    // Navigraph may return a new refresh token
                    string newRefreshToken = json["refresh_token"]?.ToString();
                    if (!string.IsNullOrEmpty(newRefreshToken))
                    {
                        _refreshToken = newRefreshToken;
                    }

                    int expiresIn = json["expires_in"]?.Value<int>() ?? 3600;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                    SaveTokens();
                    Console.WriteLine("[Navigraph] Token refreshed successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[Navigraph] Token refresh failed: {responseContent}");
                    // Clear invalid tokens
                    Logout();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Token refresh error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensure we have a valid access token (refresh if needed)
        /// </summary>
        public async Task<bool> EnsureValidTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                return false;
            }

            // If token expires in less than 5 minutes, refresh
            if (DateTime.UtcNow.AddMinutes(5) >= _tokenExpiry)
            {
                return await RefreshAccessTokenAsync();
            }

            return true;
        }

        /// <summary>
        /// Log out and clear tokens
        /// </summary>
        public void Logout()
        {
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;
            Username = null;

            // Clear from settings
            Properties.Settings.Default.NavigraphAccessToken = "";
            Properties.Settings.Default.NavigraphRefreshToken = "";
            Properties.Settings.Default.NavigraphTokenExpiry = "";
            Properties.Settings.Default.NavigraphUsername = "";
            Properties.Settings.Default.Save();

            Console.WriteLine("[Navigraph] Logged out");
            OnAuthStatusChanged?.Invoke(false);
        }

        /// <summary>
        /// Get current authentication status
        /// </summary>
        public NavigraphStatus GetStatus()
        {
            return new NavigraphStatus
            {
                Authenticated = IsAuthenticated,
                HasSubscription = IsAuthenticated, // If authenticated, has at least basic subscription
                Username = Username
            };
        }

        #endregion

        #region Private Methods

        private async Task FetchUsernameAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, USERINFO_ENDPOINT);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    Username = json["preferred_username"]?.ToString() ?? json["name"]?.ToString();

                    Properties.Settings.Default.NavigraphUsername = Username;
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to fetch username: {ex.Message}");
            }
        }

        private void SaveTokens()
        {
            try
            {
                Properties.Settings.Default.NavigraphAccessToken = _accessToken ?? "";
                Properties.Settings.Default.NavigraphRefreshToken = _refreshToken ?? "";
                Properties.Settings.Default.NavigraphTokenExpiry = _tokenExpiry.ToString("o");
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to save tokens: {ex.Message}");
            }
        }

        private void LoadTokens()
        {
            try
            {
                _accessToken = Properties.Settings.Default.NavigraphAccessToken;
                _refreshToken = Properties.Settings.Default.NavigraphRefreshToken;
                Username = Properties.Settings.Default.NavigraphUsername;

                string expiryStr = Properties.Settings.Default.NavigraphTokenExpiry;
                if (!string.IsNullOrEmpty(expiryStr) && DateTime.TryParse(expiryStr, out DateTime expiry))
                {
                    _tokenExpiry = expiry;
                }

                if (IsAuthenticated)
                {
                    Console.WriteLine($"[Navigraph] Loaded saved session for: {Username}");
                }
                else if (!string.IsNullOrEmpty(_refreshToken))
                {
                    // Token expired but we have refresh token - try to refresh
                    Console.WriteLine("[Navigraph] Access token expired, attempting auto-refresh...");
                    _ = Task.Run(async () =>
                    {
                        bool success = await RefreshAccessTokenAsync();
                        if (success)
                        {
                            Console.WriteLine($"[Navigraph] Auto-refresh successful for: {Username}");
                            OnAuthStatusChanged?.Invoke(true);
                        }
                        else
                        {
                            Console.WriteLine("[Navigraph] Auto-refresh failed, user needs to re-login");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to load tokens: {ex.Message}");
            }
        }

        private void SaveClientCredentials()
        {
            try
            {
                Properties.Settings.Default.NavigraphClientId = _clientId ?? "";
                Properties.Settings.Default.NavigraphClientSecret = _clientSecret ?? "";
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Failed to save credentials: {ex.Message}");
            }
        }

        private void LoadClientCredentials()
        {
            try
            {
                // First, try to load from secrets.config (app-embedded credentials)
                var secretsConfig = ConfigurationManager.OpenMappedExeConfiguration(
                    new ExeConfigurationFileMap { ExeConfigFilename = "secrets.config" },
                    ConfigurationUserLevel.None);

                if (secretsConfig.AppSettings.Settings["NavigraphClientId"] != null)
                {
                    _clientId = secretsConfig.AppSettings.Settings["NavigraphClientId"].Value;
                    _clientSecret = secretsConfig.AppSettings.Settings["NavigraphClientSecret"]?.Value ?? "";
                    Console.WriteLine("[Navigraph] Loaded client credentials from secrets.config");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph] Could not load secrets.config: {ex.Message}");
            }

            try
            {
                // Fallback to user settings (for development/testing)
                _clientId = Properties.Settings.Default.NavigraphClientId;
                _clientSecret = Properties.Settings.Default.NavigraphClientSecret;
            }
            catch
            {
                // Settings may not exist yet
                _clientId = "";
                _clientSecret = "";
            }
        }

        /// <summary>
        /// Generate a 43-character code verifier for PKCE
        /// </summary>
        private string GenerateCodeVerifier()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[32];
                rng.GetBytes(bytes);
                return Base64UrlEncode(bytes);
            }
        }

        /// <summary>
        /// Generate SHA256 code challenge from verifier
        /// </summary>
        private string GenerateCodeChallenge(string verifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
                return Base64UrlEncode(hash);
            }
        }

        /// <summary>
        /// URL-safe Base64 encoding
        /// </summary>
        private string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _pollCancellation?.Cancel();
                _pollCancellation?.Dispose();
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
