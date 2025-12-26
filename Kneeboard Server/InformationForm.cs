using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kneeboard_Server.Navigraph;
using Kneeboard_Server.Navigraph.BGL;


namespace Kneeboard_Server
{
    public partial class InformationForm : Form
    {
        public InformationForm()
        {
            InitializeComponent();
            if (Properties.Settings.Default.autostart == true)
            {
                autostart.Checked = true;
            }
            else
            {
                autostart.Checked = false;
            }

            if (Properties.Settings.Default.simStart == true)
            {
                simStart.Checked = true;
            }
            else
            {
                simStart.Checked = false;
            }

            if (Properties.Settings.Default.minimized == true)
            {
                minimized.Checked = true;
            }
            else
            {
                minimized.Checked = false;
            }

            if (Properties.Settings.Default.exeXmlPath != "")
            {
                folderpathInput.Text = Properties.Settings.Default.exeXmlPath;
            }

            if (Properties.Settings.Default.simbriefId != "")
            {
                SimbriefIdInput.Text = Properties.Settings.Default.simbriefId;
            }

            if (Properties.Settings.Default.vatsimCid != "")
            {
                VatsimCidInput.Text = Properties.Settings.Default.vatsimCid;
            }

            if (Properties.Settings.Default.ivaoVid != "")
            {
                IvaoVidInput.Text = Properties.Settings.Default.ivaoVid;
            }

            // Load max cache size setting (0 = unlimited)
            long maxCacheSize = Properties.Settings.Default.maxCacheSizeMB;
            maxCacheSizeInput.Text = maxCacheSize.ToString();
            UpdateCacheButtonText();

            // Load Navigraph status
            UpdateNavigraphStatus();

            // Load MSFS Navdata status
            UpdateMsfsNavdataStatus();
        }

        private void InformationForm_Load(object sender, EventArgs e)
        {
            if (Owner != null)
                Location = new Point(Owner.Location.X + Owner.Width / 2 - Width / 2,
                    Owner.Location.Y + Owner.Height / 2 - Height / 2);
            Version.Text = "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.Highlight, ButtonBorderStyle.Solid);
        }


        private void chkBackup_CheckChanged(object sender, EventArgs e)
        {
            try
            {
                if (autostart.Checked == true)
                {
                    Properties.Settings.Default.autostart = true;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.autostart = false;
                    Properties.Settings.Default.Save();
                }
            }
            catch
            {

            }
        }

        private void MSFSStart_CheckChanged(object sender, EventArgs e)
        {
            try
            {
                if (simStart.Checked == true && Properties.Settings.Default.exeXmlPath != "")
                {
                    Properties.Settings.Default.simStart = true;
                    Properties.Settings.Default.Save();
                    Kneeboard_Server.WriteExeXML();
                }
                else
                {
                    Properties.Settings.Default.simStart = false;
                    Properties.Settings.Default.Save();
                    Kneeboard_Server.WriteExeXML();
                }
            }
            catch
            {

            }
        }


        private void minimized_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (minimized.Checked == true)
                {
                    Properties.Settings.Default.minimized = true;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.minimized = false;
                    Properties.Settings.Default.Save();
                }
            }
            catch
            {

            }
        }

        private void folderpathInput_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog1 = new OpenFileDialog
                {
                    Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
                };

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (folderpathInput.Text != openFileDialog1.FileName && (openFileDialog1.FileName.EndsWith("exe.xml") == true))
                    {
                        folderpathInput.Text = openFileDialog1.FileName;
                        Properties.Settings.Default.exeXmlPath = folderpathInput.Text;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            catch
            {

            }
        }

        private void SimbriefIdInput_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.simbriefId = SimbriefIdInput.Text;
            Properties.Settings.Default.Save();
            // Restart background SimBrief sync with new ID
            Kneeboard_Server.StartBackgroundSimbriefSync();
        }

        private void VatsimCidInput_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.vatsimCid = VatsimCidInput.Text;
            Properties.Settings.Default.Save();
        }

        private void IvaoVidInput_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ivaoVid = IvaoVidInput.Text;
            Properties.Settings.Default.Save();
        }

        private void ClearCacheButton_Click(object sender, EventArgs e)
        {
            try
            {
                SimpleHTTPServer.ClearOpenAipCache();
                SimpleHTTPServer.ClearBoundariesCache();
                UpdateCacheButtonText();
                MessageBox.Show("Cache wurde geleert (OpenAIP + Boundaries).", "Cache",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Leeren des Cache: {ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MaxCacheSizeInput_TextChanged(object sender, EventArgs e)
        {
            if (long.TryParse(maxCacheSizeInput.Text, out long size) && size >= 0)
            {
                Properties.Settings.Default.maxCacheSizeMB = size;
                Properties.Settings.Default.Save();
            }
        }

        private void UpdateCacheButtonText()
        {
            try
            {
                long cacheSizeBytes = SimpleHTTPServer.GetCacheSize();
                double cacheSizeMB = cacheSizeBytes / (1024.0 * 1024.0);
                clearCacheButton.Text = $"Clear Cache ({cacheSizeMB:F1} MB)";
            }
            catch
            {
                clearCacheButton.Text = "Clear OpenAIP Cache";
            }
        }

        #region MSFS Navdata

        private void UpdateMsfsNavdataStatus()
        {
            try
            {
                var versions = MsfsNavdataService.DetectInstalledVersions();

                if (versions.Count == 0)
                {
                    msfsNavdataStatusLabel.Text = "Kein MSFS";
                    msfsNavdataStatusLabel.ForeColor = Color.Gray;
                    importNavdataButton.Enabled = false;
                    deleteNavdataButton.Enabled = false;
                }
                else
                {
                    // Check if we have MSFS 2020 (which uses BGL files)
                    bool hasMsfs2020 = versions.Contains(MsfsVersion.MSFS2020);
                    bool hasMsfs2024 = versions.Contains(MsfsVersion.MSFS2024);

                    // IMMER Reload-Button aktivieren wenn MSFS 2024 vorhanden!
                    if (hasMsfs2024)
                    {
                        deleteNavdataButton.Enabled = true;
                    }

                    if (hasMsfs2024 && !hasMsfs2020)
                    {
                        // Only MSFS 2024 - uses SimConnect, no indexing needed
                        var db = Kneeboard_Server.NavdataDB;
                        if (db != null && db.AirportCount > 0)
                        {
                            msfsNavdataStatusLabel.Text = "Indexiert";
                        }
                        else
                        {
                            msfsNavdataStatusLabel.Text = "Nicht indexiert";
                        }
                        msfsNavdataStatusLabel.ForeColor = (db != null && db.AirportCount > 0) ? Color.Green : Color.Orange;
                        importNavdataButton.Enabled = false;
                        importNavdataButton.Text = "N/A";
                    }
                    else if (Properties.Settings.Default.navdataIndexed && Properties.Settings.Default.navdataAirportCount > 0)
                    {
                        // MSFS 2020 with indexed BGL data
                        msfsNavdataStatusLabel.Text = "Indexiert";
                        msfsNavdataStatusLabel.ForeColor = Color.Green;
                        importNavdataButton.Enabled = true;
                        importNavdataButton.Text = "Re-Import";
                    }
                    else if (hasMsfs2020)
                    {
                        // MSFS 2020 but not indexed yet - Reload trotzdem aktiv wenn MSFS 2024 da
                        msfsNavdataStatusLabel.Text = "Nicht indexiert";
                        msfsNavdataStatusLabel.ForeColor = Color.Orange;
                        importNavdataButton.Enabled = true;
                        importNavdataButton.Text = "Import Navdata";
                    }
                    else
                    {
                        msfsNavdataStatusLabel.Text = "Kein BGL-Support";
                        msfsNavdataStatusLabel.ForeColor = Color.Gray;
                        importNavdataButton.Enabled = false;
                        deleteNavdataButton.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MSFS Navdata UI] Status update error: {ex.Message}");
                msfsNavdataStatusLabel.Text = "Error";
                msfsNavdataStatusLabel.ForeColor = Color.Red;
            }
        }

        private async void ImportNavdataButton_Click(object sender, EventArgs e)
        {
            importNavdataButton.Enabled = false;
            deleteNavdataButton.Enabled = false;
            msfsNavdataStatusLabel.Text = "Indexiere...";
            msfsNavdataStatusLabel.ForeColor = Color.Orange;

            try
            {
                var versions = MsfsNavdataService.DetectInstalledVersions();
                Console.WriteLine($"[MSFS Navdata UI] Found {versions.Count} MSFS versions: {string.Join(", ", versions)}");

                int totalAirports = 0;
                var messages = new System.Text.StringBuilder();

                foreach (var version in versions)
                {
                    msfsNavdataStatusLabel.Text = $"Indexiere {version}...";
                    Application.DoEvents();

                    int versionAirports = 0;
                    string versionStatus = "";

                    await Task.Run(() =>
                    {
                        using (var service = new MsfsNavdataService(version))
                        {
                            Console.WriteLine($"[MSFS Navdata UI] {version}: Path={service.NavdataPath}");
                            Console.WriteLine($"[MSFS Navdata UI] {version}: IsAvailable={service.IsAvailable}");
                            Console.WriteLine($"[MSFS Navdata UI] {version}: RequiresSimConnect={service.RequiresSimConnect}");

                            if (service.RequiresSimConnect)
                            {
                                // MSFS 2024 uses SimConnect for procedures, not BGL files
                                versionStatus = "verwendet SimConnect (Live-Abfrage)";
                                Console.WriteLine($"[MSFS Navdata UI] {version}: Skipping BGL indexing - uses SimConnect");
                            }
                            else if (service.IsAvailable)
                            {
                                service.IndexNavdata();
                                versionAirports = service.IndexedAirportCount;
                                versionStatus = $"{versionAirports:N0} Airports";
                                Console.WriteLine($"[MSFS Navdata UI] {version}: Indexed {versionAirports} airports");
                            }
                            else
                            {
                                versionStatus = "Pfad nicht gefunden";
                                Console.WriteLine($"[MSFS Navdata UI] {version}: Path not available");
                            }
                        }
                    });

                    totalAirports += versionAirports;
                    messages.AppendLine($"• {version}: {versionStatus}");
                }

                Properties.Settings.Default.navdataIndexed = true;
                Properties.Settings.Default.navdataAirportCount = totalAirports;
                Properties.Settings.Default.Save();

                UpdateMsfsNavdataStatus();

                string resultMessage = $"Navdata Import abgeschlossen!\n\n{messages}\nGesamt: {totalAirports:N0} Airports indexiert";
                if (versions.Any(v => v == MsfsVersion.MSFS2024))
                {
                    resultMessage += "\n\nHinweis: MSFS 2024 verwendet SimConnect für\nSID/STAR-Daten (keine BGL-Indexierung nötig).";
                }

                MessageBox.Show(resultMessage, "MSFS Navdata", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MSFS Navdata UI] Error: {ex}");
                MessageBox.Show($"Fehler beim Indexieren: {ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateMsfsNavdataStatus();
            }
            finally
            {
                importNavdataButton.Enabled = true;
            }
        }

        private async void DeleteNavdataButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[InformationForm] DeleteNavdataButton_Click called!");
            if (MessageBox.Show("Navdata neu laden?\n\nDie bestehende Datenbank wird gelöscht und alle SID/STAR-Daten werden neu vom Simulator geladen (~2 Min).\n\nSimConnect muss verbunden sein!",
                "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                deleteNavdataButton.Enabled = false;
                importNavdataButton.Enabled = false;
                msfsNavdataStatusLabel.Text = "Lade neu...";
                msfsNavdataStatusLabel.ForeColor = Color.Orange;

                try
                {
                    var progress = new Progress<(string message, int current, int total)>(p =>
                    {
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                msfsNavdataStatusLabel.Text = p.message;
                                Application.DoEvents();
                            }));
                        }
                        else
                        {
                            msfsNavdataStatusLabel.Text = p.message;
                            Application.DoEvents();
                        }
                    });

                    await Task.Run(async () =>
                    {
                        await Kneeboard_Server.ReloadNavdataDatabaseAsync(progress);
                    });

                    // Update status
                    var db = Kneeboard_Server.NavdataDB;
                    if (db != null && db.AirportCount > 0)
                    {
                        Properties.Settings.Default.navdataIndexed = true;
                        Properties.Settings.Default.navdataAirportCount = db.AirportCount;
                        Properties.Settings.Default.Save();

                        msfsNavdataStatusLabel.Text = "Indexiert";
                        msfsNavdataStatusLabel.ForeColor = Color.Green;
                        MessageBox.Show($"Navdata erfolgreich geladen!\n\n{db.AirportCount} Airports",
                            "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        msfsNavdataStatusLabel.Text = "Fehler";
                        msfsNavdataStatusLabel.ForeColor = Color.Red;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NavdataDB] Error reloading: {ex.Message}");
                    msfsNavdataStatusLabel.Text = "Fehler";
                    msfsNavdataStatusLabel.ForeColor = Color.Red;
                    MessageBox.Show($"Fehler beim Laden der Navdata:\n{ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    deleteNavdataButton.Enabled = true;
                    importNavdataButton.Enabled = true;
                    UpdateMsfsNavdataStatus();
                }
            }
        }

        #endregion

        #region Navigraph Integration

        private NavigraphAuthService _navigraphAuth;

        private void UpdateNavigraphStatus()
        {
            try
            {
                _navigraphAuth = SimpleHTTPServer.GetNavigraphAuth();

                if (_navigraphAuth == null || !_navigraphAuth.IsAuthenticated)
                {
                    navigraphStatusLabel.Text = "Not logged in";
                    navigraphStatusLabel.ForeColor = System.Drawing.Color.Gray;
                    navigraphLoginButton.Text = "Login";
                }
                else
                {
                    string username = _navigraphAuth.Username ?? "Connected";
                    var dataService = SimpleHTTPServer.GetNavigraphData();
                    string airac = dataService?.CurrentAiracCycle ?? "";

                    navigraphStatusLabel.Text = $"{username}" + (string.IsNullOrEmpty(airac) ? "" : $" (AIRAC {airac})");
                    navigraphStatusLabel.ForeColor = System.Drawing.Color.Green;
                    navigraphLoginButton.Text = "Logout";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph UI] Status update error: {ex.Message}");
                navigraphStatusLabel.Text = "Error";
                navigraphStatusLabel.ForeColor = System.Drawing.Color.Red;
            }
        }

        private async void NavigraphLoginButton_Click(object sender, EventArgs e)
        {
            try
            {
                _navigraphAuth = SimpleHTTPServer.GetNavigraphAuth();

                if (_navigraphAuth == null)
                {
                    MessageBox.Show("Navigraph service not available.\n\nNavigraph integration coming soon.",
                        "Navigraph", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // If already authenticated, logout
                if (_navigraphAuth.IsAuthenticated)
                {
                    _navigraphAuth.Logout();
                    UpdateNavigraphStatus();
                    MessageBox.Show("Logged out from Navigraph.", "Navigraph",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Start device auth flow
                navigraphLoginButton.Enabled = false;
                navigraphLoginButton.Text = "Starting...";

                var deviceCode = await _navigraphAuth.StartDeviceAuthFlowAsync();

                // Show verification dialog
                string message = $"Please go to:\n\n{deviceCode.VerificationUri}\n\n" +
                    $"And enter code: {deviceCode.UserCode}\n\n" +
                    $"Click OK when done, or Cancel to abort.";

                // Copy code to clipboard
                try
                {
                    Clipboard.SetText(deviceCode.UserCode);
                    message += "\n(Code copied to clipboard)";
                }
                catch { }

                // Open browser
                try
                {
                    System.Diagnostics.Process.Start(deviceCode.VerificationUriComplete ?? deviceCode.VerificationUri);
                }
                catch { }

                navigraphLoginButton.Text = $"Code: {deviceCode.UserCode}";
                navigraphStatusLabel.Text = "Waiting for authorization...";
                navigraphStatusLabel.ForeColor = System.Drawing.Color.Orange;

                // Poll for authorization (waits until user completes or timeout)
                bool success = await _navigraphAuth.PollForTokenAsync(deviceCode.DeviceCode, deviceCode.Interval);

                if (success)
                {
                    // Download navdata
                    navigraphStatusLabel.Text = "Downloading navdata...";
                    var dataService = SimpleHTTPServer.GetNavigraphData();
                    await dataService.CheckAndDownloadUpdatesAsync();

                    UpdateNavigraphStatus();
                    MessageBox.Show($"Successfully logged in as {_navigraphAuth.Username}!", "Navigraph",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    UpdateNavigraphStatus();
                    MessageBox.Show("Authentication failed or was cancelled.", "Navigraph",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigraph UI] Login error: {ex.Message}");
                MessageBox.Show($"Login error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                navigraphLoginButton.Enabled = true;
                UpdateNavigraphStatus();
            }
        }

        #endregion

    }
}
