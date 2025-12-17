using AutoUpdaterDotNET;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static Kneeboard_Server.Simbrief;
using static Kneeboard_Server.Waypoints;

using Path = System.IO.Path;

namespace Kneeboard_Server
{
    public partial class Kneeboard_Server : Form
    {
        private bool _dragging = false;
        private Point _start_point = new Point(0, 0);
        public static string folderpath = "";
        public static string communityFolderPath = "";
        public string port = "815";
        public static int filesShowed = 0;
        public static string flightplan;
        public static string simbriefOFPData;

        // Enum to track the source of the last imported flightplan
        private enum FlightplanSource { None, SimBrief, LocalPLN }
        private static FlightplanSource lastFlightplanSource = FlightplanSource.None;
        bool serverRun = Properties.Settings.Default.serverRun;
        SimpleHTTPServer myServer;
        bool imagesProzessing = false;
        public List<KneeboardFolder> foldersList = new List<KneeboardFolder>();
        public List<KneeboardFile> filesList = new List<KneeboardFile>();

        public class KneeboardFile
        {
            public string Name { get; set; }
            public int Pages { get; set; }
            public string Path { get; set; }
            public KneeboardFile()
            {
            }
            public KneeboardFile(string name, int pages, string path)
            {
                Name = name;
                Pages = pages;
                Path = path;
            }
        }

        public class KneeboardFolder
        {
            public string Name { get; set; }
            public List<KneeboardFile> Files { get; set; } = new List<KneeboardFile>();
            public KneeboardFolder()
            {
            }
            public KneeboardFolder(string name, List<KneeboardFile> files)
            {
                Name = name;
                Files = files ?? new List<KneeboardFile>();
            }
        }

        private class DocumentState
        {
            public List<KneeboardFile> Files { get; set; } = new List<KneeboardFile>();
            public List<KneeboardFolder> Folders { get; set; } = new List<KneeboardFolder>();
        }

        private void LoadDocumentState()
        {
            try
            {
                string serialized = Properties.Settings.Default.documentState;
                if (string.IsNullOrWhiteSpace(serialized))
                {
                    filesList = filesList ?? new List<KneeboardFile>();
                    foldersList = foldersList ?? new List<KneeboardFolder>();
                    return;
                }

                DocumentState snapshot = JsonConvert.DeserializeObject<DocumentState>(serialized);
                filesList = snapshot?.Files ?? new List<KneeboardFile>();
                foldersList = snapshot?.Folders ?? new List<KneeboardFolder>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load document state: " + ex.Message);
                filesList = filesList ?? new List<KneeboardFile>();
                foldersList = foldersList ?? new List<KneeboardFolder>();
            }
        }

        private void SaveDocumentState()
        {
            try
            {
                DocumentState snapshot = new DocumentState
                {
                    Files = filesList ?? new List<KneeboardFile>(),
                    Folders = foldersList ?? new List<KneeboardFolder>()
                };

                Properties.Settings.Default.documentState = JsonConvert.SerializeObject(snapshot);
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save document state: " + ex.Message);
            }
        }

        private void EnsureDefaultManualsExist()
        {
            try
            {
                // Check if manuals have already been initialized
                // If yes, skip this process (respects user's decision to remove manuals)
                if (Properties.Settings.Default.manualsInitialized)
                {
                    return;
                }

                string manualsPath = Path.Combine(folderpath, "data", "manuals");

                if (!Directory.Exists(manualsPath))
                {
                    Console.WriteLine("Manuals directory does not exist: " + manualsPath);
                    // Mark as initialized even if directory doesn't exist to avoid repeated checks
                    Properties.Settings.Default.manualsInitialized = true;
                    Properties.Settings.Default.Save();
                    return;
                }

                // Find all PDF files in the manuals directory
                string[] manualFiles = Directory.GetFiles(manualsPath, "*.pdf", SearchOption.TopDirectoryOnly);

                if (manualFiles.Length == 0)
                {
                    Console.WriteLine("No manual PDF files found in: " + manualsPath);
                    // Mark as initialized to avoid repeated checks
                    Properties.Settings.Default.manualsInitialized = true;
                    Properties.Settings.Default.Save();
                    return;
                }

                bool stateChanged = false;

                foreach (string manualPath in manualFiles)
                {
                    string manualName = Path.GetFileNameWithoutExtension(manualPath);

                    // Check if manual already exists in the list
                    bool alreadyExists = filesList.Any(f =>
                        f.Name.Equals(manualName, StringComparison.OrdinalIgnoreCase) ||
                        f.Path.Equals(manualPath, StringComparison.OrdinalIgnoreCase));

                    if (!alreadyExists)
                    {
                        // Add manual to the documents list
                        KneeboardFile manualFile = new KneeboardFile(
                            name: manualName,
                            pages: 0,
                            path: manualPath
                        );

                        filesList.Add(manualFile);
                        stateChanged = true;
                        Console.WriteLine("Added manual to documents list: " + manualName);
                    }
                }

                // Mark manuals as initialized (first run completed)
                Properties.Settings.Default.manualsInitialized = true;
                Properties.Settings.Default.Save();

                // Save document state if any manuals were added
                if (stateChanged)
                {
                    SaveDocumentState();
                    Console.WriteLine("Document state saved with new manuals");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ensuring default manuals exist: " + ex.Message);
            }
        }


        public Kneeboard_Server()
        {
            InitializeComponent();

            //delete hover color
            loadButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            addFileButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            addFolderButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            deleteFileButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            deleteFolderButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            saveButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            editButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;

            //Update message event - Fully automatic update configuration
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.DownloadPath = System.IO.Path.GetTempPath();
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.ForcedDownload;
            AutoUpdater.Start("https://github.com/G-Simulation/G-Sim-Kneeboard-Server/releases/latest/download/Kneeboard_version.xml");
        }

        //Update check

        bool updateAvailable = false;

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    UpdateMessage.Visible = true;
                    UpdateMessage.Text = $" Downloading update {args.CurrentVersion}...";
                    UpdateMessage.BackColor = Color.Orange;
                    updateAvailable = true;

                    // Automatically download and install the update
                    try
                    {
                        if (AutoUpdater.DownloadUpdate(args))
                        {
                            // Update downloaded successfully, application will restart
                            Application.Exit();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Auto-update failed: {ex.Message}");
                        UpdateMessage.Text = $" Update {args.CurrentVersion} available. Click here for download.";
                        UpdateMessage.BackColor = Color.Red;
                    }
                }
                else
                {
                    UpdateMessage.Visible = false;
                    statusBox.Text = "Status: Server is running...";
                    statusBox.BackColor = SystemColors.MenuHighlight;
                    updateAvailable = false;
                }
            }
            else
            {
                // Fehler beim Update-Check (z.B. keine Internetverbindung)
                Console.WriteLine($"Update check failed: {args.Error.Message}");
                UpdateMessage.Visible = false;
                statusBox.Text = "Status: Server is running...";
                updateAvailable = false;
            }
        }

        /// <summary>
        /// Returns the SimBrief API URL based on whether user entered a numeric ID or username
        /// </summary>
        private static string GetSimbriefApiUrl()
        {
            string input = Properties.Settings.Default.simbriefId;
            string baseUrl = "https://www.simbrief.com/api/xml.fetcher.php?";

            // Check if input is numeric (Pilot ID) or alphanumeric (Username)
            if (int.TryParse(input, out _))
            {
                return baseUrl + "userid=" + input;
            }
            else
            {
                return baseUrl + "username=" + input;
            }
        }

        private readonly int tolerance = 16;
        private const int WM_NCHITTEST = 132;
        private const int HTBOTTOMRIGHT = 17;
        private System.Drawing.Rectangle sizeGripRectangle;

        /// <summary>
        /// Add application to Startup of windows
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="path"></param>

        public static void WriteExeXML()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Alle möglichen exe.xml Pfade für MSFS 2020 und 2024 (Store und Steam)
            var exeXmlPaths = new List<string>
            {
                // MSFS 2024 Store
                Path.Combine(localAppData, @"Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\exe.xml"),
                // MSFS 2024 Steam
                Path.Combine(roamingAppData, @"Microsoft Flight Simulator 2024\exe.xml"),
                // MSFS 2020 Store
                Path.Combine(localAppData, @"Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\exe.xml"),
                // MSFS 2020 Steam
                Path.Combine(roamingAppData, @"Microsoft Flight Simulator\exe.xml")
            };

            // Falls ein benutzerdefinierter Pfad gesetzt ist, diesen zuerst prüfen
            if (!string.IsNullOrEmpty(Properties.Settings.Default.exeXmlPath) &&
                Properties.Settings.Default.exeXmlPath != "Path to exe.xml")
            {
                exeXmlPaths.Insert(0, Properties.Settings.Default.exeXmlPath);
            }

            foreach (string exeXmlPath in exeXmlPaths)
            {
                if (System.IO.File.Exists(exeXmlPath))
                {
                    try
                    {
                        var doc = XDocument.Load(exeXmlPath);

                        // Prüfe ob Kneeboard Server bereits eingetragen ist
                        bool alreadyExists = doc.Descendants("Launch.Addon")
                            .Any(e => (string)e.Element("Name") == "Kneeboard Server");

                        if (alreadyExists)
                        {
                            // Entferne existierenden Eintrag wenn simStart deaktiviert
                            if (Properties.Settings.Default.simStart == false)
                            {
                                doc.Descendants().Elements("Launch.Addon")
                                    .Where(x => x.Element("Name")?.Value == "Kneeboard Server")
                                    .Remove();
                                doc.Save(exeXmlPath);
                            }
                        }
                        else
                        {
                            // Füge neuen Eintrag hinzu wenn simStart aktiviert
                            if (Properties.Settings.Default.simStart == true)
                            {
                                var newElement = new XElement("Launch.Addon",
                                    new XElement("Name", "Kneeboard Server"),
                                    new XElement("Disabled", "false"),
                                    new XElement("Path", AppDomain.CurrentDomain.BaseDirectory + "Kneeboard Server.exe"));
                                doc.Element("SimBase.Document").Add(newElement);
                                doc.Save(exeXmlPath);
                            }
                        }

                        Console.WriteLine($"Processed exe.xml: {exeXmlPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing exe.xml at {exeXmlPath}: {ex.Message}");
                    }
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    base.WndProc(ref m);
                    var hitPoint = this.PointToClient(new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16));
                    if (sizeGripRectangle.Contains(hitPoint))
                        m.Result = new IntPtr(HTBOTTOMRIGHT);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            var region = new Region(new System.Drawing.Rectangle(0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height));
            sizeGripRectangle = new System.Drawing.Rectangle(this.ClientRectangle.Width - tolerance, this.ClientRectangle.Height - tolerance, tolerance, tolerance);
            region.Exclude(sizeGripRectangle);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.Highlight, ButtonBorderStyle.Solid);
        }


        private void KneeboardServer_Load(object sender, EventArgs e)
        {
            folderpath = AppDomain.CurrentDomain.BaseDirectory;
            //MessageBox.Show(folderpath);
            LoadDocumentState();
            EnsureDefaultManualsExist();

            if (Properties.Settings.Default.firstAutoStart == false)
            {
                DialogResult autsartQuestion = MessageBox.Show("Do you want to start the Kneeboard Server automatically with the Microsoft Flight Simulator 2020/2024??", "Start with MSFS?", MessageBoxButtons.YesNoCancel);
                if (autsartQuestion == DialogResult.Yes)
                {
                    Properties.Settings.Default.simStart = true;
                    Properties.Settings.Default.firstAutoStart = true;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.simStart = false;
                    Properties.Settings.Default.firstAutoStart = true;
                    Properties.Settings.Default.Save();
                }
            }

            if (Properties.Settings.Default.firstSimbriefAsk == false)
            {
                DialogResult simbriefQuestion = MessageBox.Show("Do you want to enter your Simbrief Pilot ID or Username now?\n\nYou can find your Pilot ID at: simbrief.com -> Account Settings -> Pilot ID\nAlternatively, you can use your Simbrief username.", "Simbrief Integration", MessageBoxButtons.YesNo);
                if (simbriefQuestion == DialogResult.Yes)
                {
                    EnterFilename simbriefDialog = new EnterFilename();
                    simbriefDialog.Text = "Enter Simbrief Pilot ID or Username";
                    simbriefDialog.textBox1.Text = "";

                    if (simbriefDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        if (!string.IsNullOrWhiteSpace(simbriefDialog.textBox1.Text))
                        {
                            Properties.Settings.Default.simbriefId = simbriefDialog.textBox1.Text.Trim();
                        }
                    }
                }
                Properties.Settings.Default.firstSimbriefAsk = true;
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.simStart == true)
            {
                WriteExeXML();
            }

            if (Properties.Settings.Default.minimized == true)
            {
                if (WindowState == FormWindowState.Normal)
                {
                    WindowState = FormWindowState.Minimized;
                }
                else if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Minimized;
                }
                notifyIcon.ShowBalloonTip(1000);
            }

            myServer = new SimpleHTTPServer(folderpath + @"\data", Convert.ToInt32(port), this);
            Console.WriteLine("Server is running on this port: " + myServer.Port.ToString());
            statusBox.BackColor = SystemColors.MenuHighlight;
            statusBox.Text = "Status: Server is running...";
            serverRun = true;
            UpdateFileList();
        }

        private void Close_Click(object sender, EventArgs e)
        {
            if (serverRun == true)
            {
                serverRun = false;
                myServer.Stop();
                statusBox.BackColor = SystemColors.MenuHighlight;
                statusBox.Text = "Status: Server is not running...";
                Properties.Settings.Default.serverRun = true;
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default.serverRun = false;
                Properties.Settings.Default.Save();
            }
            Environment.Exit(0);
        }

        private void KneeboardServer_MouseDown(object sender, MouseEventArgs e)
        {
            _dragging = true;
            _start_point = new Point(e.X, e.Y);
        }

        private void KneeboardServer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - this._start_point.X, p.Y - this._start_point.Y);
            }
        }

        private void KneeboardServer_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        private void Maximize_Click(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                WindowState = FormWindowState.Maximized;
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
        }

        private void Minimize_Click(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                WindowState = FormWindowState.Minimized;
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Minimized;
            }
            notifyIcon.ShowBalloonTip(1000);
        }

        public void UpdateFileList()
        {
            treeView1.Nodes.Clear();

            foreach (KneeboardFile file in filesList)
            {
                var fileNode = treeView1.Nodes.Add("📄 " + file.Name);
                fileNode.Tag = "file";
                fileNode.Name = file.Name;
            }

            foreach (KneeboardFolder folder in foldersList)
            {
                System.Windows.Forms.TreeNode folderNode = treeView1.Nodes.Add("📁 " + folder.Name);
                folderNode.Tag = "folder";
                folderNode.Name = folder.Name;

                foreach (KneeboardFile folderFile in folder.Files)
                {
                    var subFileNode = folderNode.Nodes.Add("📄 " + folderFile.Name);
                    subFileNode.Tag = "file";
                    subFileNode.Name = folderFile.Name;
                }
            }
            CreateImages();
            treeView1.ExpandAll();
        }

        String ReplaceGermanUmlauts(String s)
        {
            String t = s;
            t = t.Replace("ä", "ae");
            t = t.Replace("ö", "oe");
            t = t.Replace("ü", "ue");
            t = t.Replace("Ä", "Ae");
            t = t.Replace("Ö", "Oe");
            t = t.Replace("Ü", "Ue");
            t = t.Replace("ß", "ss");
            return t;
        }

        bool imagesCreated = false;

        // Helper-Methode für bereinigten Dateinamen
        private string GetCleanName(string name)
        {
            return Regex.Replace(name, "[^0-9A-Za-z]+", "_");
        }

        // Helper-Methode für Dateiendungs-Prüfung
        private bool IsPdfFile(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTextFile(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
        }

        private void CreateTextImage(string textFilePath, string outputPath)
        {
            try
            {
                Directory.CreateDirectory(outputPath);
                string cleanFilename = GetCleanName(Path.GetFileNameWithoutExtension(textFilePath));
                string text = System.IO.File.ReadAllText(textFilePath);

                // Bildgröße (A4-ähnlich bei 150 DPI)
                int width = 1240;
                int height = 1754;
                int margin = 60;
                int lineHeight = 28;

                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    // Schriftart
                    using (var font = new Font("Segoe UI", 14, FontStyle.Regular))
                    using (var brush = new SolidBrush(Color.Black))
                    {
                        // Text umbrechen
                        string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        int y = margin;
                        int pageNum = 1;
                        int maxLines = (height - 2 * margin) / lineHeight;
                        int lineCount = 0;

                        foreach (string line in lines)
                        {
                            // Zeile ggf. umbrechen
                            string remainingText = line;
                            while (!string.IsNullOrEmpty(remainingText))
                            {
                                // Messe wie viel Text in eine Zeile passt
                                int charsFit = remainingText.Length;
                                SizeF size = graphics.MeasureString(remainingText, font);

                                while (size.Width > width - 2 * margin && charsFit > 1)
                                {
                                    charsFit--;
                                    size = graphics.MeasureString(remainingText.Substring(0, charsFit), font);
                                }

                                string lineToDraw = remainingText.Substring(0, charsFit);
                                remainingText = charsFit < remainingText.Length ? remainingText.Substring(charsFit) : "";

                                graphics.DrawString(lineToDraw, font, brush, margin, y);
                                y += lineHeight;
                                lineCount++;

                                // Neue Seite wenn voll
                                if (lineCount >= maxLines && (!string.IsNullOrEmpty(remainingText) || lines.Length > lineCount))
                                {
                                    // Speichere aktuelle Seite
                                    string pagePath = Path.Combine(outputPath, $"{cleanFilename}_{pageNum}.png");
                                    bitmap.Save(pagePath, System.Drawing.Imaging.ImageFormat.Png);
                                    pageNum++;

                                    // Neue Seite vorbereiten
                                    graphics.Clear(Color.White);
                                    y = margin;
                                    lineCount = 0;
                                }
                            }
                        }

                        // Letzte Seite speichern
                        if (lineCount > 0 || pageNum == 1)
                        {
                            string pagePath = Path.Combine(outputPath, $"{cleanFilename}_{pageNum}.png");
                            bitmap.Save(pagePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }

                Console.WriteLine($"Text file converted to images: {textFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating text image: {ex.Message}");
            }
        }

        private void CleanupOrphanedImages()
        {
            try
            {
                string imagesPath = $@"{folderpath}\data\images";
                if (!Directory.Exists(imagesPath))
                    return;

                // Sammle alle gültigen Ordnernamen aus filesList und foldersList
                var validFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Einzelne Dateien
                foreach (var file in filesList)
                {
                    validFolders.Add(GetCleanName(Path.GetFileNameWithoutExtension(file.Path)));
                }

                // Ordner und deren Dateien
                foreach (var folder in foldersList)
                {
                    validFolders.Add(GetCleanName(folder.Name));
                }

                // Lösche Ordner die nicht mehr in der Liste sind
                foreach (var dir in Directory.GetDirectories(imagesPath))
                {
                    string dirName = Path.GetFileName(dir);

                    // Prüfe ob es ein Hauptordner (aus foldersList) ist
                    var mainFolder = foldersList.FirstOrDefault(f =>
                        GetCleanName(f.Name).Equals(dirName, StringComparison.OrdinalIgnoreCase));

                    if (mainFolder != null)
                    {
                        // Für Hauptordner: prüfe Unterordner
                        var validSubFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var file in mainFolder.Files)
                        {
                            validSubFolders.Add(GetCleanName(Path.GetFileNameWithoutExtension(file.Path)));
                        }

                        // Lösche verwaiste Unterordner
                        foreach (var subDir in Directory.GetDirectories(dir))
                        {
                            string subDirName = Path.GetFileName(subDir);
                            if (!validSubFolders.Contains(subDirName))
                            {
                                try
                                {
                                    Directory.Delete(subDir, true);
                                    Console.WriteLine($"Deleted orphaned subfolder: {subDir}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error deleting subfolder: {ex.Message}");
                                }
                            }
                        }
                    }
                    else if (!validFolders.Contains(dirName))
                    {
                        // Kein Hauptordner und nicht in filesList -> löschen
                        try
                        {
                            Directory.Delete(dir, true);
                            Console.WriteLine($"Deleted orphaned folder: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting folder: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        public async void CreateImages()
        {
            // Verhindere parallele Ausführung - prüfe ob bereits eine Instanz läuft
            if (imagesProzessing)
            {
                Console.WriteLine("Image creation already in progress - skipping duplicate call");
                return;
            }

            imagesCreated = false;
            string imagesBasePath = $@"{folderpath}\data\images";

            try
            {
                // Lösche verwaiste Bilder-Ordner
                CleanupOrphanedImages();

                // Zähle Gesamtanzahl der zu verarbeitenden Dateien
                int totalFiles = 0;
                int processedFiles = 0;

                // Zähle einzelne Dateien
                foreach (var file in filesList)
                {
                    string cleanName = GetCleanName(Path.GetFileNameWithoutExtension(file.Path));
                    string targetPath = $@"{imagesBasePath}\{cleanName}";
                    if ((IsPdfFile(file.Path) || IsTextFile(file.Path)) && !Directory.Exists(targetPath))
                        totalFiles++;
                }

                // Zähle Dateien in Ordnern
                foreach (var folder in foldersList)
                {
                    if (folder.Name == "data") continue;
                    string cleanFolderName = GetCleanName(folder.Name);
                    string folderImagesPath = $@"{imagesBasePath}\{cleanFolderName}";
                    foreach (var file in folder.Files)
                    {
                        string cleanFileName = GetCleanName(Path.GetFileNameWithoutExtension(file.Path));
                        string targetPath = $@"{folderImagesPath}\{cleanFileName}";
                        if ((IsPdfFile(file.Path) || IsTextFile(file.Path)) && !Directory.Exists(targetPath))
                            totalFiles++;
                    }
                }

                // Zeige Fortschritt in UpdateMessage wenn Dateien zu verarbeiten sind
                if (totalFiles > 0)
                {
                    UpdateMessage.Visible = true;
                    UpdateMessage.BackColor = SystemColors.MenuHighlight;
                }

                // Verarbeite einzelne Dateien
                foreach (var file in filesList)
                {
                    string cleanName = GetCleanName(Path.GetFileNameWithoutExtension(file.Path));
                    string targetPath = $@"{imagesBasePath}\{cleanName}";

                    if (IsPdfFile(file.Path))
                    {
                        if (!Directory.Exists(targetPath))
                        {
                            imagesProzessing = true;
                            processedFiles++;
                            UpdateMessage.Text = $" Creating images ({processedFiles}/{totalFiles}): {Path.GetFileName(file.Path)}";
                            await Task.Run(() => createImage(file.Path, targetPath));
                            imagesCreated = true;
                        }
                    }
                    else if (IsImageFile(file.Path))
                    {
                        Directory.CreateDirectory(targetPath);
                        string destFile = $@"{targetPath}\{cleanName}_1.png";
                        System.IO.File.Copy(file.Path, destFile, true);
                    }
                    else if (IsTextFile(file.Path))
                    {
                        if (!Directory.Exists(targetPath))
                        {
                            imagesProzessing = true;
                            processedFiles++;
                            UpdateMessage.Text = $" Creating images ({processedFiles}/{totalFiles}): {Path.GetFileName(file.Path)}";
                            await Task.Run(() => CreateTextImage(file.Path, targetPath));
                            imagesCreated = true;
                        }
                    }
                }

                // Verarbeite Ordner
                foreach (var folder in foldersList)
                {
                    if (folder.Name == "data") continue;

                    string cleanFolderName = GetCleanName(folder.Name);
                    string folderImagesPath = $@"{imagesBasePath}\{cleanFolderName}";

                    if (!Directory.Exists(folderImagesPath))
                    {
                        Directory.CreateDirectory(folderImagesPath);
                        await Task.Delay(1000);
                    }

                    foreach (var file in folder.Files)
                    {
                        string cleanFileName = GetCleanName(Path.GetFileNameWithoutExtension(file.Path));
                        string targetPath = $@"{folderImagesPath}\{cleanFileName}";

                        if (IsPdfFile(file.Path))
                        {
                            if (!Directory.Exists(targetPath))
                            {
                                imagesProzessing = true;
                                processedFiles++;
                                UpdateMessage.Text = $" Creating images ({processedFiles}/{totalFiles}): {Path.GetFileName(file.Path)}";
                                await Task.Run(() => createImage(file.Path, targetPath));
                                imagesCreated = true;
                                Console.WriteLine($"Images successfully created: {file.Path}");
                            }
                        }
                        else if (IsImageFile(file.Path))
                        {
                            Directory.CreateDirectory(targetPath);
                            string destFile = $@"{targetPath}\{cleanFileName}_1.png";
                            System.IO.File.Copy(file.Path, destFile, true);
                        }
                        else if (IsTextFile(file.Path))
                        {
                            if (!Directory.Exists(targetPath))
                            {
                                imagesProzessing = true;
                                processedFiles++;
                                UpdateMessage.Text = $" Creating images ({processedFiles}/{totalFiles}): {Path.GetFileName(file.Path)}";
                                await Task.Run(() => CreateTextImage(file.Path, targetPath));
                                imagesCreated = true;
                                Console.WriteLine($"Text images created: {file.Path}");
                            }
                        }
                    }
                }

                // Verstecke UpdateMessage nach Abschluss (außer wenn Update verfügbar)
                if (!updateAvailable)
                {
                    UpdateMessage.Visible = false;
                }
                statusBox.Text = imagesCreated ? "Status: Images updated. Server is running..." : "Status: Server is running...";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in CreateImages: {e.Message}");
            }
            finally
            {
                imagesProzessing = false;
            }
        }


        void createImage(string file, string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                System.Threading.Thread.Sleep(1000);

                string cleanFilename = GetCleanName(Path.GetFileNameWithoutExtension(file));

                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(file))
                {
                    for (int i = 0; i < pdfDoc.PageCount; i++)
                    {
                        using (var img = RenderPage(pdfDoc, i))
                        {
                            string outputPath = Path.Combine(path, $"{cleanFilename}_{i + 1}.png");
                            img.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }

                Console.WriteLine($"PDF images created successfully: {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating PDF images: {ex.Message}");
                MessageBox.Show($"Error creating PDF images: {ex.Message}", "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public System.Drawing.Image RenderPage(PdfiumViewer.PdfDocument doc, int page)
        {
            // Render at 300 DPI for high quality
            const int dpi = 300;
            var pageSize = doc.PageSizes[page];
            int width = (int)(pageSize.Width / 72.0 * dpi);
            int height = (int)(pageSize.Height / 72.0 * dpi);

            // Render with transparent background (PdfRenderFlags.Transparent)
            using (var image = doc.Render(page, width, height, dpi, dpi, PdfiumViewer.PdfRenderFlags.Transparent))
            {
                // Convert to 32bpp ARGB to ensure transparency is preserved
                var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.DrawImage(image, 0, 0, width, height);
                }
                return result;
            }
        }



        public static bool simbriefDownloaded = false;

        public static void Restart()
        {
            Application.Restart();
            Environment.Exit(0);
        }


        private void KneeboardServer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serverRun == true)
            {
                serverRun = false;
                //myServer.Stop();
                statusBox.Text = "Status: Server is not running! Plese select a working folder.";
                statusBox.BackColor = Color.IndianRed;
            }
            Application.Exit();
        }

        private void AddFileButton_Click(object sender, EventArgs e)
        {
            bool dataChanged = false;
            try
            {
                if (treeView1.SelectedNode != null)
                {
                    //MessageBox.Show("selected");
                    System.Windows.Forms.OpenFileDialog openDialog = new System.Windows.Forms.OpenFileDialog
                    {
                        Title = "Select A File",
                        Filter = "Documents (*.pdf;*.jpg;*.png;*.txt)|*.pdf;*.jpg;*.png;*.txt|PDF Files (*.pdf)|*.pdf|Images (*.jpg;*.png)|*.jpg;*.png|Text Files (*.txt)|*.txt"
                    };
                    if (openDialog.ShowDialog() == DialogResult.OK)
                    {
                        string fileName = openDialog.FileName;

                        bool containsItem = filesList.Any(item => item.Path == Path.GetFullPath(openDialog.FileName));

                        string fileType = Path.GetExtension(openDialog.FileName);
                        if (!containsItem)
                        {

                            foreach (KneeboardFolder folder in foldersList.Where(x => x.Name == treeView1.SelectedNode.Name))
                            {
                                //MessageBox.Show("found");s

                                KneeboardFile file = new KneeboardFile(name: Path.GetFileNameWithoutExtension(openDialog.FileName), pages: 0, path: fileName);
                                folder.Files.Add(file);
                                dataChanged = true;
                            }
                        }
                        else
                        {
                            MessageBox.Show("A file with the same name is already in the directory!");
                        }
                    }

                }
                else
                {
                    System.Windows.Forms.OpenFileDialog openDialog = new System.Windows.Forms.OpenFileDialog
                    {
                        Title = "Select A File",
                        Filter = "Documents (*.pdf;*.jpg;*.png;*.txt)|*.pdf;*.jpg;*.png;*.txt|PDF Files (*.pdf)|*.pdf|Images (*.jpg;*.png)|*.jpg;*.png|Text Files (*.txt)|*.txt"
                    };
                    if (openDialog.ShowDialog() == DialogResult.OK)
                    {
                        string fileName = openDialog.FileName;

                        bool containsItem = filesList.Any(item => item.Path == Path.GetFullPath(openDialog.FileName));

                        string fileType = Path.GetExtension(openDialog.FileName);
                        if (!containsItem)
                        {
                            KneeboardFile file = new KneeboardFile(name: Path.GetFileNameWithoutExtension(openDialog.FileName), pages: 0, path: fileName);
                            filesList.Add(file);
                            dataChanged = true;
                        }
                        else
                        {
                            MessageBox.Show("A file with the same name is already in the directory!");
                        }
                    }
                }
                UpdateFileList();
                if (dataChanged)
                {
                    SaveDocumentState();
                }
            }
            catch (Exception e2)
            {
                Console.WriteLine(e2.Message);
            }

        }

        private void DeleteFileButton_Click(object sender, EventArgs e)
        {
            bool dataChanged = false;
            if (treeView1.SelectedNode != null)
            {
                if (treeView1.SelectedNode.Parent == null)
                {
                    if (filesList != null)
                    {
                        int removed = filesList.RemoveAll(x => x.Name == treeView1.SelectedNode.Name);
                        if (removed > 0)
                            dataChanged = true;
                    }
                } //parent
                else
                {

                    var item = foldersList?.SingleOrDefault(x => x.Name == treeView1.SelectedNode.Parent.Name);
                    if (item != null)
                    {
                        int removed = item.Files.RemoveAll(x => x.Name == treeView1.SelectedNode.Name);
                        if (removed > 0)
                            dataChanged = true;

                    }
                } //child
            }
            if (dataChanged)
            {
                UpdateFileList();
                SaveDocumentState();
            }
        }

        public string GetFileList()
        {
            string documentsString = "";

            // Debug-Ausgaben
            if ( filesList == null)
                throw new NullReferenceException("filesList ist null");
            if (foldersList == null)
                throw new NullReferenceException("foldersList ist null");

            foreach (KneeboardFile file in filesList)
            {
                if (file == null)
                    throw new NullReferenceException("Ein Element in filesList ist null");
                if (file.Name == null)
                    throw new NullReferenceException("Ein KneeboardFile.Name ist null");

                documentsString += $"\"{file.Name}\"";
            }

            foreach (KneeboardFolder folder in foldersList)
            {
                if (folder == null)
                    throw new NullReferenceException("Ein Element in foldersList ist null");
                if (folder.Files == null)
                    throw new NullReferenceException($"folder.Files ist null im Ordner {folder.Name}");
                if (folder.Name == null)
                    throw new NullReferenceException("folder.Name ist null");

                foreach (KneeboardFile file2 in folder.Files)
                {
                    if (file2 == null)
                        throw new NullReferenceException($"Ein Dateiobjekt in Ordner {folder.Name} ist null");
                    if (file2.Name == null)
                        throw new NullReferenceException($"Eine Datei in Ordner {folder.Name} hat keinen Namen");

                    documentsString += $"\"{folder.Name}\\{file2.Name}\"";
                }
            }

            return documentsString;
        }

        private void Information_Click(object sender, EventArgs e)
        {
            InformationForm InformationForm = new InformationForm();
            InformationForm.ShowDialog();
        }

        private void Kneeboard_Server_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            UpdateFileList();
            Console.WriteLine("Update file list");
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            if (folderpath != "")
            {
                if (serverRun == true)
                {
                    System.Diagnostics.Process.Start("microsoft-edge:http://localhost:815/navigationlog.html");
                }
            }
            else
            {
                MessageBox.Show("Please select a document directory!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (imagesProzessing == false)
            {
                System.Windows.Forms.SaveFileDialog savefile = new System.Windows.Forms.SaveFileDialog
                {
                    // set a default file name
                    FileName = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")
                };
                // set filters - this can be done in properties as well
                savefile.Filter = "Kneeboard navlog files (*.knf)|*.knf|All files (*.*)|*.*";
                savefile.DefaultExt = "knf";
                savefile.AddExtension = true;
                if (savefile.ShowDialog() == DialogResult.OK)
                {
                    using (StreamWriter sw2 = new StreamWriter(savefile.FileName))
                    {
                        sw2.WriteLine(myServer.values);
                        sw2.Close();
                    }
                }
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            if (imagesProzessing == false)
            {
                if (Properties.Settings.Default.simbriefId != "")
                {
                    DialogResult dialogResult = MessageBox.Show("Do you like to import latest Simbrief OFP?", "Simbrief import", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        try
                        {
                            if (Directory.Exists(folderpath + @"\data\images\Simbrief"))
                            {
                                try
                                {
                                    Directory.Delete(folderpath + @"\data\images\Simbrief", true);
                                }
                                catch
                                {
                                }
                            }
                            var m_strFilePath = GetSimbriefApiUrl();
                            string xmlStr;
                            using (var wc = new WebClient())
                            {
                                xmlStr = wc.DownloadString(m_strFilePath);
                            }
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(xmlStr);

                            // OFP-Daten deserialisieren und speichern
                            using (StringReader stringReader = new StringReader(xmlStr))
                            {
                                XmlSerializer ofpSerializer = new XmlSerializer(typeof(Simbrief.OFP));
                                Simbrief.OFP ofpData = (Simbrief.OFP)ofpSerializer.Deserialize(stringReader);
                                simbriefOFPData = Newtonsoft.Json.JsonConvert.SerializeObject(ofpData);
                                Console.WriteLine("[OFP Debug] OFP deserialized successfully, JSON length: " + (simbriefOFPData != null ? simbriefOFPData.Length.ToString() : "null"));
                            }

                            var plnNode = xmlDoc.DocumentElement.SelectSingleNode("/OFP/fms_downloads/mfs/link");
                            var ofpNode = xmlDoc.DocumentElement.SelectSingleNode("/OFP/files/pdf/link");

                            if (plnNode == null || ofpNode == null)
                            {
                                MessageBox.Show("Could not retrieve flight plan from SimBrief. Please check your Pilot ID and ensure you have a flight plan generated on SimBrief.", "SimBrief Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            string simbriefPLN = plnNode.InnerText;
                            string simbriefOFP = ofpNode.InnerText;

                            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                            {
                                using (System.Net.WebClient client = new System.Net.WebClient())
                                {
                                    client.DownloadFile(new Uri("https://www.simbrief.com/ofp/flightplans/" + simbriefPLN), folderpath + @"\data\simbrief.pln");
                                    Properties.Settings.Default.communityFolderPath = "";
                                    Properties.Settings.Default.Save();
                                }
                            }
                            using (XmlReader reader = XmlReader.Create(new FileStream(folderpath + @"\data\simbrief.pln", FileMode.Open), new XmlReaderSettings() { CloseInput = true }))
                            {
                                XmlSerializer serializer = new XmlSerializer(typeof(SimBaseDocument));
                                SimBaseDocument waypoints = (SimBaseDocument)serializer.Deserialize(reader);
                                // Kombiniertes Objekt mit PLN und OFP-Daten senden
                                Console.WriteLine("[OFP Debug] Creating combined data - simbriefOFPData is " + (simbriefOFPData != null ? "NOT null, length: " + simbriefOFPData.Length : "NULL"));
                                var combinedData = new
                                {
                                    pln = waypoints,
                                    ofp = simbriefOFPData != null ? Newtonsoft.Json.JsonConvert.DeserializeObject(simbriefOFPData) : null
                                };
                                flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                                Console.WriteLine("[OFP Debug] Combined flightplan created, length: " + (flightplan != null ? flightplan.Length.ToString() : "null"));
                                reader.Close();
                            }

                            if (!Directory.Exists(folderpath + @"\Simbrief"))
                            {
                                Directory.CreateDirectory(folderpath + @"\Simbrief");
                            }

                            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                            {
                                using (System.Net.WebClient client = new System.Net.WebClient())
                                {
                                    client.DownloadFile(new Uri("https://www.simbrief.com/ofp/flightplans/" + simbriefOFP), folderpath + @"\Simbrief\OFP.pdf");
                                    simbriefDownloaded = true;
                                }
                            }

                            lastFlightplanSource = FlightplanSource.SimBrief;
                            MessageBox.Show("SimBrief flight plan imported successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (System.Net.WebException webEx)
                        {
                            Console.WriteLine($"SimBrief download error: {webEx.Message}");
                            MessageBox.Show($"Could not connect to SimBrief. Please check your internet connection.\n\nError: {webEx.Message}", "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (XmlException xmlEx)
                        {
                            Console.WriteLine($"SimBrief XML parsing error: {xmlEx.Message}");
                            MessageBox.Show($"Invalid response from SimBrief. Please ensure you have a valid flight plan generated.\n\nError: {xmlEx.Message}", "SimBrief Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"SimBrief import error: {ex.Message}");
                            MessageBox.Show($"Failed to import SimBrief flight plan.\n\nError: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else if (dialogResult == DialogResult.No)
                    {
                        System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog
                        {
                            Filter = "Kneeboard files (*.knf;*.pln;*.flightplan)|*.knf;*.pln;*.flightplan|All files (*.*)|*.*"
                        };
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            Console.WriteLine(Path.GetExtension(openFileDialog1.FileName));
                            if (Path.GetExtension(openFileDialog1.FileName) == ".knf" || Path.GetExtension(openFileDialog1.FileName) == ".KNF")
                            {
                                // Create a StreamReader from a FileStream
                                using (StreamReader reader = new StreamReader(new FileStream(openFileDialog1.FileName, FileMode.Open)))
                                {
                                    string line;
                                    // Read line by line
                                    while ((line = reader.ReadLine()) != null)
                                    {
                                        myServer.values = line;
                                        Console.WriteLine(line);
                                        Properties.Settings.Default.values = line;
                                        Properties.Settings.Default.Save();
                                    }
                                    reader.Close();
                                }
                            }
                            else if (Path.GetExtension(openFileDialog1.FileName) == ".pln" || Path.GetExtension(openFileDialog1.FileName) == ".PLN")
                            {
                                try
                                {
                                    communityFolderPath = openFileDialog1.FileName;
                                    Properties.Settings.Default.communityFolderPath = communityFolderPath;
                                    Properties.Settings.Default.Save();
                                    using (XmlReader reader = XmlReader.Create(new FileStream(communityFolderPath, FileMode.Open), new XmlReaderSettings() { CloseInput = true }))
                                    {
                                        XmlSerializer serializer = new XmlSerializer(typeof(SimBaseDocument));
                                        SimBaseDocument waypoints = (SimBaseDocument)serializer.Deserialize(reader);
                                        var combinedData = new { pln = waypoints, ofp = (object)null };
                                        flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                                        lastFlightplanSource = FlightplanSource.LocalPLN;
                                        reader.Close();
                                    }
                                }
                                catch (Exception e2)
                                {
                                    Console.WriteLine(e2.Message);
                                }
                            }
                            else if (Path.GetExtension(openFileDialog1.FileName) == ".flightplan" || Path.GetExtension(openFileDialog1.FileName) == ".FLIGHTPLAN")
                            {
                                try
                                {
                                    communityFolderPath = openFileDialog1.FileName;
                                    Properties.Settings.Default.communityFolderPath = communityFolderPath;
                                    Properties.Settings.Default.Save();
                                    SimBaseDocument waypoints = ParseSkyDemonFlightplan(openFileDialog1.FileName);
                                    var combinedData = new { pln = waypoints, ofp = (object)null };
                                    flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                                    lastFlightplanSource = FlightplanSource.LocalPLN;
                                }
                                catch (Exception e2)
                                {
                                    Console.WriteLine(e2.Message);
                                    MessageBox.Show("Error loading SkyDemon flightplan: " + e2.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                }
                else
                {
                    System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog
                    {
                        Filter = "Kneeboard files (*.knf;*.pln;*.flightplan)|*.knf;*.pln;*.flightplan|All files (*.*)|*.*"
                    };
                    if (openFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Console.WriteLine(Path.GetExtension(openFileDialog1.FileName));
                        if (Path.GetExtension(openFileDialog1.FileName) == ".knf" || Path.GetExtension(openFileDialog1.FileName) == ".KNF")
                        {
                            // Create a StreamReader from a FileStream
                            using (StreamReader reader = new StreamReader(new FileStream(openFileDialog1.FileName, FileMode.Open)))
                            {
                                string line;
                                // Read line by line
                                while ((line = reader.ReadLine()) != null)
                                {
                                    myServer.values = line;
                                    Console.WriteLine(line);
                                    Properties.Settings.Default.values = line;
                                    Properties.Settings.Default.Save();
                                }
                                reader.Close();
                            }
                        }
                        else if (Path.GetExtension(openFileDialog1.FileName) == ".pln" || Path.GetExtension(openFileDialog1.FileName) == ".PLN")
                        {
                            try
                            {
                                communityFolderPath = openFileDialog1.FileName;
                                Properties.Settings.Default.communityFolderPath = communityFolderPath;
                                Properties.Settings.Default.Save();
                                using (XmlReader reader = XmlReader.Create(new FileStream(communityFolderPath, FileMode.Open), new XmlReaderSettings() { CloseInput = true }))
                                {
                                    XmlSerializer serializer = new XmlSerializer(typeof(SimBaseDocument));
                                    SimBaseDocument waypoints = (SimBaseDocument)serializer.Deserialize(reader);
                                    var combinedData = new { pln = waypoints, ofp = (object)null };
                                    flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                                    lastFlightplanSource = FlightplanSource.LocalPLN;
                                    reader.Close();
                                }
                            }
                            catch (Exception e2)
                            {
                                Console.WriteLine(e2.Message);
                            }
                        }
                        else if (Path.GetExtension(openFileDialog1.FileName) == ".flightplan" || Path.GetExtension(openFileDialog1.FileName) == ".FLIGHTPLAN")
                        {
                            try
                            {
                                communityFolderPath = openFileDialog1.FileName;
                                Properties.Settings.Default.communityFolderPath = communityFolderPath;
                                Properties.Settings.Default.Save();
                                SimBaseDocument waypoints = ParseSkyDemonFlightplan(openFileDialog1.FileName);
                                var combinedData = new { pln = waypoints, ofp = (object)null };
                                flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                                lastFlightplanSource = FlightplanSource.LocalPLN;
                            }
                            catch (Exception e2)
                            {
                                Console.WriteLine(e2.Message);
                                MessageBox.Show("Error loading SkyDemon flightplan: " + e2.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }



        private void AddFolderButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Add a folder", addFolderButton);
            addFolderButton.BackColor = Color.White;
        }

        private void DeleteFolderButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Delete a folder", deleteFolderButton);
            deleteFolderButton.BackColor = Color.White;
        }

        private void AddFileButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Upload a document", addFileButton);
            addFileButton.BackColor = Color.White;
        }

        private void DeleteFileButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Delete a document", deleteFileButton);
            deleteFileButton.BackColor = Color.White;
        }

        private void LoadButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Open a navlog/flightplan file", loadButton);
            loadButton.BackColor = Color.White;
        }

        private void SaveButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Save a navlog file", saveButton);
            saveButton.BackColor = Color.White;
        }

        private void EditButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Show browser kneeboard", editButton);
            editButton.BackColor = Color.White;
        }

        private void Information_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("About", information);
        }

        private void MinimizeButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Minimize", minimizeButton);
        }

        private void MaximizeButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Maximize", maximizeButton);
        }

        private void CloseButton_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Close", closeButton);
        }

        private void label1_MouseHover(object sender, EventArgs e)
        {
            MyToolTip.Show("Recreate images", label1);
        }

        private SimBaseDocument ParseSkyDemonFlightplan(string filePath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filePath);

            SimBaseDocument result = new SimBaseDocument
            {
                Type = "SkyDemon",
                version = "1.0",
                Descr = "SkyDemon Flightplan Import"
            };

            var flightPlan = new SimBaseDocumentFlightPlanFlightPlan
            {
                FPType = "VFR",
                RouteType = "Direct"
            };

            // Get the root element - SkyDemon uses different root element names
            XmlElement root = doc.DocumentElement;

            // Try to find the Route element
            XmlNode routeNode = root.SelectSingleNode("//PrimaryRoute") ?? root.SelectSingleNode("//Route");

            if (routeNode == null)
            {
                throw new Exception("No Route element found in SkyDemon flightplan");
            }

            // Get departure and destination from Route attributes
            string startCoord = routeNode.Attributes?["Start"]?.Value ?? "";
            string departure = "";
            string destination = "";

            // Parse waypoints - SkyDemon uses RhumbLineRoute elements
            XmlNodeList waypointNodes = routeNode.SelectNodes("RhumbLineRoute");
            if (waypointNodes == null || waypointNodes.Count == 0)
            {
                waypointNodes = routeNode.SelectNodes("RhumLine") ?? routeNode.SelectNodes("Waypoint");
            }

            var waypointList = new List<SimBaseDocumentFlightPlanFlightPlanATCWaypoint>();

            // Add start point as first waypoint
            if (!string.IsNullOrEmpty(startCoord))
            {
                var startCoords = ParseSkyDemonCoordinatePair(startCoord);
                if (startCoords.lat != 0 || startCoords.lon != 0)
                {
                    departure = "START";
                    var startWaypoint = new SimBaseDocumentFlightPlanFlightPlanATCWaypoint
                    {
                        id = "START",
                        ATCWaypointType = "Airport",
                        WorldPosition = FormatWorldPosition(startCoords.lat, startCoords.lon, 0),
                        ICAO = new SimBaseDocumentFlightPlanFlightPlanATCWaypointICAO
                        {
                            ICAOIdent = "START"
                        }
                    };
                    waypointList.Add(startWaypoint);
                    flightPlan.DepartureLLA = FormatWorldPosition(startCoords.lat, startCoords.lon, 0);
                }
            }

            int wpIndex = 1;
            if (waypointNodes != null)
            {
                foreach (XmlNode wpNode in waypointNodes)
                {
                    // Get the "To" attribute which contains coordinates like "N542254.35 E0131427.70"
                    string toCoord = wpNode.Attributes?["To"]?.Value ?? "";

                    if (string.IsNullOrEmpty(toCoord))
                        continue;

                    // Parse coordinate pair
                    var coords = ParseSkyDemonCoordinatePair(toCoord);
                    double lat = coords.lat;
                    double lon = coords.lon;

                    if (lat == 0 && lon == 0)
                        continue;

                    // Generate waypoint name
                    string wpName = "WP" + wpIndex;
                    string toType = wpNode.Attributes?["ToType"]?.Value ?? "Unknown";
                    if (toType == "Aerodrome")
                    {
                        wpName = "DEST";
                    }
                    else if (toType == "Town")
                    {
                        wpName = "TOWN" + wpIndex;
                    }

                    // Create waypoint in MSFS format
                    var waypoint = new SimBaseDocumentFlightPlanFlightPlanATCWaypoint
                    {
                        id = wpName,
                        ATCWaypointType = toType == "Aerodrome" ? "Airport" : "User",
                        WorldPosition = FormatWorldPosition(lat, lon, 0),
                        ICAO = new SimBaseDocumentFlightPlanFlightPlanATCWaypointICAO
                        {
                            ICAOIdent = wpName
                        }
                    };

                    waypointList.Add(waypoint);

                    // Update destination with each waypoint (last one will be final)
                    destination = wpName;
                    flightPlan.DestinationLLA = FormatWorldPosition(lat, lon, 0);

                    wpIndex++;
                }
            }

            flightPlan.DepartureID = departure;
            flightPlan.DepartureName = departure;
            flightPlan.DestinationID = destination;
            flightPlan.DestinationName = destination;
            flightPlan.Title = $"{departure} to {destination}";
            flightPlan.ATCWaypoint = waypointList.ToArray();

            // Try to get cruising altitude
            string altStr = routeNode.Attributes?["Level"]?.Value ??
                           root.SelectSingleNode("//CruiseAltitude")?.InnerText ?? "3000";
            if (decimal.TryParse(altStr, out decimal cruisingAlt))
            {
                flightPlan.CruisingAlt = cruisingAlt;
            }

            result.FlightPlanFlightPlan = flightPlan;
            return result;
        }

        // Parse a coordinate pair like "N542302.10 E0131932.65"
        private (double lat, double lon) ParseSkyDemonCoordinatePair(string coordPair)
        {
            if (string.IsNullOrEmpty(coordPair))
                return (0, 0);

            // Split by space
            var parts = coordPair.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return (0, 0);

            double lat = ParseSkyDemonCoordinate(parts[0]);
            double lon = ParseSkyDemonCoordinate(parts[1]);

            return (lat, lon);
        }

        // Parse a single coordinate like "N542302.10" (format: Nddmmss.ss or Edddmmss.ss)
        private double ParseSkyDemonCoordinate(string coord)
        {
            if (string.IsNullOrEmpty(coord))
                return 0;

            coord = coord.Trim().ToUpper();
            double sign = 1;
            bool isLongitude = false;

            // Determine sign and type based on direction
            if (coord.StartsWith("S") || coord.StartsWith("W"))
            {
                sign = -1;
            }
            if (coord.StartsWith("E") || coord.StartsWith("W"))
            {
                isLongitude = true;
            }

            // Remove direction letter
            coord = coord.Substring(1);

            // Format is ddmmss.ss for latitude or dddmmss.ss for longitude
            // N542302.10 = N 54° 23' 02.10" (latitude: 2 digit degrees)
            // E0131932.65 = E 013° 19' 32.65" (longitude: 3 digit degrees)

            try
            {
                // Latitude has 2 digit degrees, Longitude has 3 digit degrees
                int degDigits = isLongitude ? 3 : 2;

                string degStr = coord.Substring(0, degDigits);
                string minStr = coord.Substring(degDigits, 2);
                string secStr = coord.Substring(degDigits + 2);

                double degrees = double.Parse(degStr, System.Globalization.CultureInfo.InvariantCulture);
                double minutes = double.Parse(minStr, System.Globalization.CultureInfo.InvariantCulture);
                double seconds = double.Parse(secStr, System.Globalization.CultureInfo.InvariantCulture);

                return sign * (degrees + minutes / 60.0 + seconds / 3600.0);
            }
            catch
            {
                return 0;
            }
        }

        private string FormatWorldPosition(double lat, double lon, double alt)
        {
            // Format as MSFS WorldPosition: "N47° 30' 15.00",E9° 45' 30.00",+000500.00"
            string latDir = lat >= 0 ? "N" : "S";
            string lonDir = lon >= 0 ? "E" : "W";

            lat = Math.Abs(lat);
            lon = Math.Abs(lon);

            int latDeg = (int)lat;
            int latMin = (int)((lat - latDeg) * 60);
            double latSec = ((lat - latDeg) * 60 - latMin) * 60;

            int lonDeg = (int)lon;
            int lonMin = (int)((lon - lonDeg) * 60);
            double lonSec = ((lon - lonDeg) * 60 - lonMin) * 60;

            // Use InvariantCulture to ensure dots as decimal separators
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}{1}° {2}' {3:F2}\",{4}{5}° {6}' {7:F2}\",+{8:000000.00}",
                latDir, latDeg, latMin, latSec, lonDir, lonDeg, lonMin, lonSec, alt);
        }

        public static string syncFlightplan()
        {
            // Clear cache to force fresh load from source
            flightplan = null;

            // Decision based on last import source
            if (lastFlightplanSource == FlightplanSource.LocalPLN &&
                Properties.Settings.Default.communityFolderPath != "" &&
                System.IO.File.Exists(Properties.Settings.Default.communityFolderPath))
            {
                // Load local PLN file with consistent format
                using (XmlReader reader = XmlReader.Create(new FileStream(Properties.Settings.Default.communityFolderPath, FileMode.Open), new XmlReaderSettings() { CloseInput = true }))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(SimBaseDocument));
                    SimBaseDocument waypoints = (SimBaseDocument)serializer.Deserialize(reader);
                    var combinedData = new { pln = waypoints, ofp = (object)null };
                    flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                    reader.Close();
                }
                Console.WriteLine(flightplan);
            }
            else if (lastFlightplanSource == FlightplanSource.SimBrief ||
                     (lastFlightplanSource == FlightplanSource.None && Properties.Settings.Default.simbriefId != ""))
                {
                    try
                    {
                        var m_strFilePath = GetSimbriefApiUrl();
                        string xmlStr;
                        using (var wc = new WebClient())
                        {
                            xmlStr = wc.DownloadString(m_strFilePath);
                        }
                        var xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(xmlStr);

                        // OFP-Daten deserialisieren und speichern
                        using (StringReader stringReader = new StringReader(xmlStr))
                        {
                            XmlSerializer ofpSerializer = new XmlSerializer(typeof(Simbrief.OFP));
                            Simbrief.OFP ofpData = (Simbrief.OFP)ofpSerializer.Deserialize(stringReader);
                            simbriefOFPData = Newtonsoft.Json.JsonConvert.SerializeObject(ofpData);
                            Console.WriteLine("[OFP Debug syncFlightplan] OFP deserialized, JSON length: " + (simbriefOFPData != null ? simbriefOFPData.Length.ToString() : "null"));
                        }

                        var plnNode = xmlDoc.DocumentElement.SelectSingleNode("/OFP/fms_downloads/mfs/link");
                        var ofpNode = xmlDoc.DocumentElement.SelectSingleNode("/OFP/files/pdf/link");

                        if (plnNode == null || ofpNode == null)
                        {
                            Console.WriteLine("SimBrief: Could not retrieve flight plan data from XML response");
                            return flightplan;
                        }

                        string value = plnNode.InnerText;
                        string simbriefOFPLink = ofpNode.InnerText;

                        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                        {
                            using (System.Net.WebClient client = new System.Net.WebClient())
                            {
                                client.DownloadFile(new Uri("https://www.simbrief.com/ofp/flightplans/" + value), folderpath + @"\data\simbrief.pln");
                                communityFolderPath = "";
                            }
                        }
                        using (XmlReader reader = XmlReader.Create(new FileStream(folderpath + @"\data\simbrief.pln", FileMode.Open), new XmlReaderSettings() { CloseInput = true }))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(SimBaseDocument));
                            SimBaseDocument waypoints = (SimBaseDocument)serializer.Deserialize(reader);
                            // Kombiniertes Objekt mit PLN und OFP-Daten senden
                            Console.WriteLine("[OFP Debug syncFlightplan] Creating combined data - simbriefOFPData is " + (simbriefOFPData != null ? "NOT null, length: " + simbriefOFPData.Length : "NULL"));
                            var combinedData = new
                            {
                                pln = waypoints,
                                ofp = simbriefOFPData != null ? Newtonsoft.Json.JsonConvert.DeserializeObject(simbriefOFPData) : null
                            };
                            flightplan = Newtonsoft.Json.JsonConvert.SerializeObject(combinedData);
                            lastFlightplanSource = FlightplanSource.SimBrief;
                            Console.WriteLine("[OFP Debug syncFlightplan] Combined flightplan created, length: " + (flightplan != null ? flightplan.Length.ToString() : "null"));
                            reader.Close();
                        }
                    }
                    catch (System.Net.WebException webEx)
                    {
                        Console.WriteLine($"SimBrief sync error (network): {webEx.Message}");
                    }
                    catch (XmlException xmlEx)
                    {
                        Console.WriteLine($"SimBrief sync error (XML parsing): {xmlEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SimBrief sync error: {ex.Message}");
                    }
            }
            return flightplan;
        }

        private void label1_Click(object sender, EventArgs e)
        {
            string[] filesPath = System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "data/");
            foreach (string filePath in filesPath)
            {
                string fileName = System.IO.Path.GetFileName(filePath).ToLower();
                if (fileName.StartsWith("kneeboard_manual"))
                {
                    System.Diagnostics.Process.Start(AppDomain.CurrentDomain.BaseDirectory + "data/manuals/Kneeboard_Manual_EN.pdf");
                    Console.WriteLine("open: " + fileName);
                }
            }
        }

        private void Kneeboard_Server_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(!foldersList.Any(x => x.Name == "newFolder"))
            {
                foldersList.Add(new KneeboardFolder(name: "newFolder", files: new List<KneeboardFile>()));
                UpdateFileList();
                SaveDocumentState();
            }
            else
            {
                MessageBox.Show("A new folder already exists, please rename the newFolder", "Warning");
            }
        }

        private void deleteFolderButton_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null)
            {
                return;
            }

            if (treeView1.SelectedNode.Parent == null)
            {
                if (foldersList.Any(item => item.Name == treeView1.SelectedNode.Name))
                {
                    DialogResult dialogResult = MessageBox.Show("Are you sure you like to delete this folder?", "Delete folder", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        foldersList.RemoveAll(x => x.Name == treeView1.SelectedNode.Name);
                        UpdateFileList();
                        SaveDocumentState();
                        return;
                    }
                    else if (dialogResult == DialogResult.No)
                    {
                        return;
                    }
                }
            }
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void addFolderButton_Enter(object sender, EventArgs e)
        {
            addFolderButton.BackColor = Color.White;
        }

        private void addFolderButton_Leave(object sender, EventArgs e)
        {
            addFolderButton.BackColor = Color.White;
        }

        private void deleteFolderButton_Enter(object sender, EventArgs e)
        {
            deleteFolderButton.BackColor = Color.White;
        }

        private void deleteFolderButton_Leave(object sender, EventArgs e)
        {
            deleteFolderButton.BackColor = Color.White;
        }

        private void addFileButton_Enter(object sender, EventArgs e)
        {
            addFileButton.BackColor = Color.White;
        }

        private void addFileButton_Leave(object sender, EventArgs e)
        {
            addFileButton.BackColor = Color.White;
        }

        private void deleteFileButton_Enter(object sender, EventArgs e)
        {
            deleteFileButton.BackColor = Color.White;
        }

        private void deleteFileButton_Leave(object sender, EventArgs e)
        {
            deleteFileButton.BackColor = Color.White;
        }

        private void loadButton_Enter(object sender, EventArgs e)
        {
            loadButton.BackColor = Color.White;
        }

        private void loadButton_Leave(object sender, EventArgs e)
        {
            loadButton.BackColor = Color.White;
        }

        private async void label1_Click_1(object sender, EventArgs e)
        {
            if (imagesProzessing == false)
            {
                statusBox.Text = "Status: Delete images...";
                System.IO.DirectoryInfo di = new DirectoryInfo(folderpath + "/data/images");

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
                await Task.Delay(2000);
                UpdateFileList();
            }
        }

        private void statusBox_Click(object sender, EventArgs e)
        {
            // Auto-update handles downloads automatically
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null)
                return;

            // Bei Dateien: Original-Quelldatei öffnen
            if (treeView1.SelectedNode.Tag?.ToString() == "file")
            {
                string fileName = treeView1.SelectedNode.Name;
                string folderName = treeView1.SelectedNode.Parent?.Name;

                // Finde die Datei in den Listen
                KneeboardFile fileEntry = null;

                if (folderName != null)
                {
                    // Datei in einem Ordner
                    var folder = foldersList.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    if (folder != null && folder.Files != null)
                    {
                        fileEntry = folder.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    }
                }
                else
                {
                    // Einzelne Datei
                    fileEntry = filesList.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                }

                if (fileEntry != null && System.IO.File.Exists(fileEntry.Path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(fileEntry.Path);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler beim Öffnen der Datei: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Datei nicht gefunden oder existiert nicht.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            // Bei Ordnern: umbenennen
            if (treeView1.SelectedNode.Tag?.ToString() == "folder" && treeView1.SelectedNode.Parent == null)
            {
                EnterFilename testDialog = new EnterFilename();
                string tempFilename = treeView1.SelectedNode.Name;
                testDialog.textBox1.Text = tempFilename;
                testDialog.textBox1.SelectAll();

                // Show testDialog as a modal dialog and determine if DialogResult = OK.
                if (testDialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (testDialog.textBox1.Text != "" && testDialog.textBox1.Text != tempFilename)
                    {
                        if (foldersList.Any(item => item.Name == treeView1.SelectedNode.Name))
                        {
                            foldersList.Where(x => x.Name == treeView1.SelectedNode.Name).ToList().ForEach(b => b.Name = testDialog.textBox1.Text);
                            UpdateFileList();
                            SaveDocumentState();
                        }
                    }
                }
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://localhost:815/manuals/Kneeboard_Manual_EN.html");
        }
    }
}
                