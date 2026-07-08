using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("MidiToMove")]
[assembly: System.Reflection.AssemblyDescription("Accessible standard MIDI to Ableton Move and Note bundle converter")]
[assembly: System.Reflection.AssemblyCompany("Andre Louis")]
[assembly: System.Reflection.AssemblyProduct("MidiToMove")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) Andre Louis")]
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.3")]

namespace MidiToMove
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                Environment.ExitCode = CommandLineRunner.Run(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal static class CommandLineRunner
    {
        public static int Run(string[] args)
        {
            try
            {
                var settings = AppSettings.Load();
                var outputFolder = "";
                var input = new List<string>();
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i].Trim().Trim('"');
                    if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) || arg.Equals("-o", StringComparison.OrdinalIgnoreCase))
                    {
                        if (++i >= args.Length) throw new ArgumentException("--output requires a folder.");
                        outputFolder = args[i].Trim().Trim('"');
                    }
                    else if (arg.Equals("--scenes", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("--scenes is not supported. Move sets always use up to eight scenes.");
                    }
                    else if (arg.Equals("--no-drum-preference", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.PreferDrumsOnTrack1 = false;
                    }
                    else
                    {
                        input.Add(arg);
                    }
                }

                var files = ExpandInputs(input);
                if (files.Count == 0) throw new ArgumentException("No MIDI files were supplied.");
                if (outputFolder.Length > 0) Directory.CreateDirectory(outputFolder);
                var failed = 0;
                foreach (var file in files)
                {
                    try
                    {
                        var midi = MidiFileReader.Read(file);
                        var parts = midi.BuildExportParts(settings.PreferDrumsOnTrack1).Take(4).ToList();
                        if (parts.Count == 0) throw new InvalidDataException("No MIDI notes were found.");
                        var folder = outputFolder.Length > 0 ? outputFolder : Path.GetDirectoryName(file);
                        var outputPath = UniquePath(Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + ".ablbundle"));
                        var result = MoveBundleWriter.Write(file, outputPath, midi, parts, MoveBundleWriter.MoveSceneLimit);
                        Console.WriteLine(result.ToMessage());
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.Error.WriteLine("{0}: {1}", file, ex.Message);
                    }
                }
                return failed == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        private static List<string> ExpandInputs(List<string> input)
        {
            var files = new List<string>();
            foreach (var item in input)
            {
                if (Directory.Exists(item))
                {
                    files.AddRange(Directory.GetFiles(item, "*.mid"));
                    files.AddRange(Directory.GetFiles(item, "*.midi"));
                }
                else if (File.Exists(item))
                {
                    files.Add(item);
                }
            }
            return files.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var i = 2; i < 10000; i++)
            {
                var candidate = Path.Combine(dir, name + " " + i.ToString(CultureInfo.InvariantCulture) + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not create a unique output file name.");
        }
    }

    internal sealed class MainForm : Form
    {
        private const string AppName = "MidiToMove";
        private const string Version = "1.3";
        private const string ProjectUrl = "https://github.com/OnjLouis/MidiToMove";
        private readonly AppSettings settings = AppSettings.Load();
        private readonly ListView resultsList;
        private readonly Label statusLabel;
        private readonly List<string> resultLines = new List<string>();
        private readonly Timer updateCheckTimer;
        private bool automaticUpdateCheckStartedThisRun;

        private static readonly string HelpText =
@"MidiToMove

Purpose
MidiToMove converts standard MIDI files into Ableton Move or Ableton Note .ablbundle files.

Keyboard
Ctrl+O: Open one or more MIDI files.
Ctrl+F: Open a folder and process every .mid and .midi file in that folder.
Ctrl+Comma: Open Preferences.
Ctrl+F1: Open the project page on GitHub.
F1: Show this help.
F4: Review results.
Alt+F4: Close the program.

Updates
Help > Check for Updates checks GitHub Releases.
Help > Version History shows the latest GitHub release notes.
Help > Project on GitHub opens the project page.
Help > Donate opens onj.me/donate if you want to support development.
Preferences > Updates controls automatic checks and quiet update installs.

Output
Source MIDI files are never overwritten.
Existing output bundles are never overwritten; a number is added when needed.
Settings are stored in MidiToMove.ini next to the program.

Track Selection
Move supports four tracks.
If a MIDI file has more than four usable parts, MidiToMove asks which parts to export.
Use Space to check or clear a part.
Use Move up, Move down, Ctrl+Up, or Ctrl+Down to set the order.
Use Assign Track 1 through Assign Track 4 to choose the destination Move track. Several MIDI parts can share the same Move track and will be merged.
When the drum preference is on, MIDI channel 10 parts are combined and placed first by default.

Scenes
Each Move clip is 16 bars long in 4/4, which is 64 beats.
Long MIDI files are split into scene 1, scene 2, and so on, up to Move's eight-scene limit.
If a MIDI file is longer than eight scenes, MidiToMove writes the first eight scenes and reports that the source was longer.

Sounds
Generated tracks contain no instrument device.
After importing the bundle to Move, choose sounds on Move or in Ableton.

Current Export
Tempo, time signature, track names, notes, note lengths, and velocities are exported.
Sustain pedal messages, CC64, are converted into longer note lengths where needed.
Other controller automation is intentionally not exported in this first build.";

        public MainForm()
        {
            Text = AppName;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 460);
            Size = new Size(900, 540);
            KeyPreview = true;
            AccessibleName = AppName;
            AccessibleDescription = "Accessible MIDI to Ableton Move bundle converter.";

            MainMenuStrip = BuildMenu();
            Controls.Add(MainMenuStrip);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 10, 12, 12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            root.Controls.Add(buttons, 0, 0);

            var openFilesButton = CreateButton("&Open MIDI File(s)...", "Open MIDI files", "Choose one or more MIDI files to convert.");
            openFilesButton.Click += delegate { OpenFiles(); };
            buttons.Controls.Add(openFilesButton);

            var openFolderButton = CreateButton("Open &Folder...", "Open folder", "Choose a folder and convert every MIDI file in it.");
            openFolderButton.Click += delegate { OpenFolder(); };
            buttons.Controls.Add(openFolderButton);

            var reviewButton = CreateButton("&Review Results", "Review results", "Review the selected result, or all results if none is selected.");
            reviewButton.Click += delegate { ReviewResults(); };
            buttons.Controls.Add(reviewButton);

            var helpButton = CreateButton("Help", "Help", "Open built-in MidiToMove help.");
            helpButton.Click += delegate { ShowHelp(); };
            buttons.Controls.Add(helpButton);

            var preferencesButton = CreateButton("&Preferences...", "Preferences", "Choose output and conversion defaults.");
            preferencesButton.Click += delegate { ShowPreferences(); };
            buttons.Controls.Add(preferencesButton);

            statusLabel = new Label
            {
                Text = "Choose MIDI files or a folder to convert.",
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 8),
                AccessibleRole = AccessibleRole.StaticText,
                AccessibleName = "Status"
            };
            root.Controls.Add(statusLabel, 0, 1);

            resultsList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = true,
                AccessibleRole = AccessibleRole.Table,
                AccessibleName = "Conversion results"
            };
            resultsList.Columns.Add("Source", 360);
            resultsList.Columns.Add("Result", 480);
            resultsList.DoubleClick += delegate { ReviewResults(); };
            root.Controls.Add(resultsList, 0, 2);

            KeyDown += MainForm_KeyDown;
            updateCheckTimer = new Timer();
            updateCheckTimer.Interval = 60 * 60 * 1000;
            updateCheckTimer.Tick += delegate { CheckAutomaticUpdateSchedule(); };
            StartAutomaticUpdateChecks();
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { AccessibleName = "Menu bar" };
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(new ToolStripMenuItem("&Open MIDI File(s)...", null, delegate { OpenFiles(); }, Keys.Control | Keys.O));
            file.DropDownItems.Add(new ToolStripMenuItem("Open &Folder...", null, delegate { OpenFolder(); }, Keys.Control | Keys.F));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, delegate { Close(); }));

            var options = new ToolStripMenuItem("&Options");
            options.DropDownItems.Add(new ToolStripMenuItem("&Preferences...", null, delegate { ShowPreferences(); }, Keys.Control | Keys.Oemcomma));

            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add(new ToolStripMenuItem("&Check for Updates...", null, delegate { CheckForUpdates(true, true); }, Keys.Shift | Keys.F1));
            help.DropDownItems.Add(new ToolStripMenuItem("&Version History...", null, delegate { ShowVersionHistoryDialog(); }));
            help.DropDownItems.Add(new ToolStripMenuItem("&Project on GitHub", null, delegate { OpenProjectPage(); }, Keys.Control | Keys.F1));
            help.DropDownItems.Add(new ToolStripMenuItem("&Donate...", null, delegate { OpenDonatePage(); }));
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add(new ToolStripMenuItem("MidiToMove &Help", null, delegate { ShowHelp(); }, Keys.F1));
            help.DropDownItems.Add(new ToolStripMenuItem("&About MidiToMove", null, delegate { ShowAbout(); }));

            menu.Items.Add(file);
            menu.Items.Add(options);
            menu.Items.Add(help);
            return menu;
        }

        private static Button CreateButton(string text, string accessibleName, string description)
        {
            return new Button { Text = text, AutoSize = true, AccessibleRole = AccessibleRole.PushButton, AccessibleName = accessibleName, AccessibleDescription = description };
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1) { ShowHelp(); e.Handled = true; }
            else if (e.KeyCode == Keys.F4 && !e.Alt) { ReviewResults(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Oemcomma) { ShowPreferences(); e.Handled = true; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F1)) { OpenProjectPage(); return true; }
            if (keyData == (Keys.Control | Keys.Oemcomma)) { ShowPreferences(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open MIDI file or files";
                dialog.Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi|All files (*.*)|*.*";
                dialog.Multiselect = true;
                if (Directory.Exists(settings.LastInputFolder)) dialog.InitialDirectory = settings.LastInputFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (dialog.FileNames.Length > 0) settings.LastInputFolder = Path.GetDirectoryName(dialog.FileNames[0]);
                ProcessPaths(new List<string>(dialog.FileNames));
            }
        }

        private void OpenFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder containing MIDI files.";
                if (Directory.Exists(settings.LastInputFolder)) dialog.SelectedPath = settings.LastInputFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.LastInputFolder = dialog.SelectedPath;
                var files = Directory.GetFiles(dialog.SelectedPath, "*.mid").Concat(Directory.GetFiles(dialog.SelectedPath, "*.midi")).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
                ProcessPaths(files);
            }
        }

        private void ProcessPaths(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                MessageBox.Show(this, "No MIDI files were found.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var output = settings.ToOutputSettings();
            if (settings.AskForOutputLocationAfterInput)
            {
                using (var dialog = new OutputLocationForm(settings, paths.Count))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    output = dialog.Output;
                    settings.ApplyOutputSettings(output);
                    settings.Save();
                }
            }
            else if (output.Mode == OutputMode.SingleFolder && !Directory.Exists(output.Folder))
            {
                MessageBox.Show(this, "Choose an output folder in Preferences, or turn on the option to ask where to save after choosing input.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            else
            {
                settings.Save();
            }

            resultsList.Items.Clear();
            resultLines.Clear();
            statusLabel.Text = "Converting " + paths.Count + " file(s).";
            Application.DoEvents();

            var started = Stopwatch.StartNew();
            var ok = 0;
            var failed = 0;

            foreach (var path in paths)
            {
                try
                {
                    var midi = MidiFileReader.Read(path);
                    var parts = midi.BuildExportParts(settings.PreferDrumsOnTrack1);
                    if (parts.Count == 0) throw new InvalidDataException("No MIDI notes were found.");
                    var selected = parts.Count <= 4 && !settings.AlwaysShowTrackMapping ? parts.Take(4).ToList() : ChooseParts(path, parts);
                    if (selected == null || selected.Count == 0)
                    {
                        AddResult(path, "Skipped.", false);
                        continue;
                    }

                    var outputFolder = output.Mode == OutputMode.AlongsideSourceFiles ? Path.GetDirectoryName(path) : output.Folder;
                    Directory.CreateDirectory(outputFolder);
                    var outputPath = UniquePath(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(path) + ".ablbundle"));
                    var result = MoveBundleWriter.Write(path, outputPath, midi, selected, MoveBundleWriter.MoveSceneLimit);
                    AddResult(path, result.ToMessage(), false);
                    ok++;
                }
                catch (Exception ex)
                {
                    AddResult(path, "Failed: " + ex.Message, true);
                    failed++;
                }
            }

            started.Stop();
            statusLabel.Text = string.Format(CultureInfo.InvariantCulture, "Finished in {0}. {1} converted; {2} failed.", FormatDuration(started.Elapsed), ok, failed);
            if (failed > 0) MessageBox.Show(this, statusLabel.Text + Environment.NewLine + "Failed files are at the top of the results list.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private List<ExportPart> ChooseParts(string path, List<ExportPart> parts)
        {
            using (var dialog = new PartSelectionForm(path, parts))
            {
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedParts : null;
            }
        }

        private void AddResult(string source, string result, bool failed)
        {
            var line = Path.GetFileName(source) + ": " + result;
            resultLines.Add(line);
            var item = new ListViewItem(Path.GetFileName(source));
            item.SubItems.Add(result);
            item.Tag = line;
            if (failed)
            {
                item.BackColor = Color.MistyRose;
                resultsList.Items.Insert(0, item);
            }
            else
            {
                resultsList.Items.Add(item);
            }
        }

        private void ReviewResults()
        {
            if (resultsList.Items.Count == 0)
            {
                MessageBox.Show(this, "There are no results to review yet.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = resultsList.SelectedItems.Cast<ListViewItem>().Select(i => Convert.ToString(i.Tag)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var lines = selected.Count == 1 ? selected : resultsList.Items.Cast<ListViewItem>().Select(i => Convert.ToString(i.Tag)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            ShowTextDialog("MidiToMove Results", string.Join(Environment.NewLine + Environment.NewLine, lines.ToArray()));
        }

        private void ShowHelp() { ShowTextDialog("MidiToMove Help", HelpText); }

        private void ShowAbout()
        {
            ShowTextDialog("About MidiToMove", "MidiToMove " + Version + Environment.NewLine + Environment.NewLine + "Accessible standard MIDI to Ableton Move and Note bundle converter." + Environment.NewLine + Environment.NewLine + "Project page:" + Environment.NewLine + ProjectUrl + Environment.NewLine + Environment.NewLine + "Created by Andre Louis with Codex.");
        }

        private void ShowPreferences()
        {
            using (var dialog = new PreferencesForm(settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SaveSettingsNonFatal();
                    StartAutomaticUpdateChecks();
                    statusLabel.Text = "Preferences saved.";
                }
            }
        }

        private void CheckForUpdates(bool showUpToDate, bool showErrors)
        {
            try
            {
                UseWaitCursor = true;
                var releases = UpdateService.FetchReleases(ProjectUrl, Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(ProjectUrl, Version);
                var latest = release == null ? string.Empty : (release.tag_name ?? string.Empty).Trim();
                System.Version current;
                System.Version remote;
                if (System.Version.TryParse(Version, out current) && System.Version.TryParse(latest.TrimStart('v', 'V'), out remote) && remote > current)
                {
                    if (settings.InstallUpdatesQuietly && TryStartUpdate(release, true)) return;
                    ShowUpdateAvailableDialog(release, latest, UpdateService.BuildReleaseNotes(releases, current, remote, Version));
                    return;
                }
                if (showUpToDate) MessageBox.Show(this, "MidiToMove is up to date. Current version: " + Version + ".", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (showErrors) MessageBox.Show(this, "Could not check for updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally { UseWaitCursor = false; }
        }

        private void StartAutomaticUpdateChecks()
        {
            updateCheckTimer.Stop();
            CheckAutomaticUpdateSchedule();
            if (UpdateService.AutomaticUpdateInterval(settings.UpdateCheckFrequency).HasValue) updateCheckTimer.Start();
        }

        private void CheckAutomaticUpdateSchedule()
        {
            var frequency = UpdateService.NormalizeUpdateCheckFrequency(settings.UpdateCheckFrequency);
            if (frequency == "Never") return;
            if (frequency == "Startup")
            {
                if (!automaticUpdateCheckStartedThisRun)
                {
                    automaticUpdateCheckStartedThisRun = true;
                    BeginSilentAutomaticUpdateCheck(false);
                }
                return;
            }
            var interval = UpdateService.AutomaticUpdateInterval(frequency);
            DateTime last;
            if (interval.HasValue && (!DateTime.TryParse(settings.LastAutomaticUpdateCheckUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out last) || DateTime.UtcNow - last >= interval.Value))
            {
                settings.LastAutomaticUpdateCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                SaveSettingsNonFatal();
                BeginSilentAutomaticUpdateCheck(true);
            }
        }

        private void BeginSilentAutomaticUpdateCheck(bool recorded)
        {
            Task.Factory.StartNew(delegate
            {
                try
                {
                    if (!recorded) BeginInvoke((MethodInvoker)delegate { settings.LastAutomaticUpdateCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); SaveSettingsNonFatal(); });
                    var releases = UpdateService.FetchReleases(ProjectUrl, Version);
                    var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(ProjectUrl, Version);
                    var latest = release == null ? string.Empty : (release.tag_name ?? string.Empty).Trim();
                    System.Version current;
                    System.Version remote;
                    if (!System.Version.TryParse(Version, out current) || !System.Version.TryParse(latest.TrimStart('v', 'V'), out remote) || remote <= current) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!IsDisposed)
                        {
                            if (settings.InstallUpdatesQuietly && TryStartUpdate(release, false)) return;
                            ShowUpdateAvailableDialog(release, latest, UpdateService.BuildReleaseNotes(releases, current, remote, Version));
                        }
                    });
                }
                catch { }
            });
        }

        private void ShowUpdateAvailableDialog(GitHubReleaseInfo release, string latest, string releaseNotes)
        {
            var releaseUrl = release == null || string.IsNullOrWhiteSpace(release.html_url) ? ProjectUrl + "/releases" : release.html_url;
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            using (var dialog = new Form())
            {
                dialog.Text = "Update available";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 720;
                dialog.Height = 520;
                dialog.AccessibleName = "Update available";
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Top, Text = "MidiToMove " + latest + " is available.", Padding = new Padding(0, 0, 0, 8) }, 0, 0);
                layout.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = UpdateService.FormatReleaseNotesForDialog(releaseNotes, "No release notes were provided for this update."), AccessibleName = "Release notes" }, 0, 1);
                var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0) };
                if (zipAsset != null)
                {
                    var install = new Button { Text = "&Download and install", AutoSize = true, AccessibleName = "Download and install update" };
                    install.Click += delegate { dialog.DialogResult = DialogResult.OK; dialog.Close(); StartUpdate(zipAsset.browser_download_url); };
                    buttons.Controls.Add(install);
                    dialog.AcceptButton = install;
                }
                var releaseButton = new Button { Text = "Open &release page", AutoSize = true };
                releaseButton.Click += delegate { Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true }); };
                var later = new Button { Text = "&Later", DialogResult = DialogResult.Cancel, AutoSize = true };
                buttons.Controls.Add(releaseButton);
                buttons.Controls.Add(later);
                dialog.CancelButton = later;
                layout.Controls.Add(buttons, 0, 2);
                dialog.Controls.Add(layout);
                dialog.ShowDialog(this);
            }
        }

        private void ShowVersionHistoryDialog()
        {
            try
            {
                UseWaitCursor = true;
                var releases = UpdateService.FetchReleases(ProjectUrl, Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(ProjectUrl, Version);
                var version = release == null ? Version : (release.tag_name ?? Version).Trim().TrimStart('v', 'V');
                var notes = UpdateService.FormatReleaseNotesForDialog(release == null ? string.Empty : release.body, "No release notes were provided for this update.");
                ShowTextDialog("Version History - " + version, "Latest release: " + version + Environment.NewLine + Environment.NewLine + notes);
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not check updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Version History", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { UseWaitCursor = false; }
        }

        private void OpenDonatePage()
        {
            OpenExternalPage("https://onj.me/donate", "Could not open the donation page.");
        }

        private void OpenProjectPage()
        {
            OpenExternalPage(ProjectUrl, "Could not open the project page.");
        }

        private void OpenExternalPage(string url, string errorTitle)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, errorTitle + Environment.NewLine + Environment.NewLine + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool TryStartUpdate(GitHubReleaseInfo release, bool showErrors)
        {
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            if (zipAsset == null || string.IsNullOrWhiteSpace(zipAsset.browser_download_url))
            {
                if (showErrors) MessageBox.Show(this, "This GitHub release does not include a downloadable ZIP package. Please open the release page instead.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            StartUpdate(zipAsset.browser_download_url);
            return true;
        }

        private void StartUpdate(string zipUrl)
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var exePath = Application.ExecutablePath;
                var updaterTempDir = UpdateService.GetUpdaterTempDirectory(appDir);
                var scriptPath = Path.Combine(updaterTempDir, "MidiToMoveUpdater-" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(scriptPath, UpdateService.BuildUpdaterScript(zipUrl, appDir, exePath, updaterTempDir, Process.GetCurrentProcess().Id, Version));
                Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"", UseShellExecute = false, CreateNoWindow = true });
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not start updater", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void SaveSettingsNonFatal()
        {
            try { settings.Save(); }
            catch (Exception ex) { statusLabel.Text = "Settings could not be saved. " + ex.Message; }
        }

        private static void ShowTextDialog(string title, string text)
        {
            using (var dialog = new TextReviewForm(title, text)) dialog.ShowDialog();
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var i = 2; i < 10000; i++)
            {
                var candidate = Path.Combine(dir, name + " " + i.ToString(CultureInfo.InvariantCulture) + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not create a unique output file name.");
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 1) return span.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";
            return span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds";
        }

    }

    internal enum OutputMode { AlongsideSourceFiles, SingleFolder }

    internal sealed class OutputSettings
    {
        public OutputMode Mode;
        public string Folder;
    }

    internal sealed class AppSettings
    {
        public OutputMode OutputMode = OutputMode.AlongsideSourceFiles;
        public string OutputFolder = "";
        public string LastInputFolder = "";
        public bool AskForOutputLocationAfterInput = true;
        public bool PreferDrumsOnTrack1 = true;
        public bool AlwaysShowTrackMapping = false;
        public string UpdateCheckFrequency = "Startup";
        public bool InstallUpdatesQuietly = false;
        public string LastAutomaticUpdateCheckUtc = "";

        public static string IniPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MidiToMove.ini"); }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(IniPath)) return settings;
            foreach (var raw in File.ReadAllLines(IniPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("[") || line.StartsWith(";")) continue;
                var index = line.IndexOf('=');
                if (index <= 0) continue;
                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim();
                bool b;
                if (key.Equals("OutputMode", StringComparison.OrdinalIgnoreCase)) settings.OutputMode = value.Equals("SingleFolder", StringComparison.OrdinalIgnoreCase) ? OutputMode.SingleFolder : OutputMode.AlongsideSourceFiles;
                else if (key.Equals("OutputFolder", StringComparison.OrdinalIgnoreCase)) settings.OutputFolder = value;
                else if (key.Equals("LastInputFolder", StringComparison.OrdinalIgnoreCase)) settings.LastInputFolder = value;
                else if (key.Equals("AskForOutputLocationAfterInput", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out b)) settings.AskForOutputLocationAfterInput = b;
                else if (key.Equals("PreferDrumsOnTrack1", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out b)) settings.PreferDrumsOnTrack1 = b;
                else if (key.Equals("AlwaysShowTrackMapping", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out b)) settings.AlwaysShowTrackMapping = b;
                else if (key.Equals("UpdateCheckFrequency", StringComparison.OrdinalIgnoreCase)) settings.UpdateCheckFrequency = UpdateService.NormalizeUpdateCheckFrequency(value);
                else if (key.Equals("InstallUpdatesQuietly", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out b)) settings.InstallUpdatesQuietly = b;
                else if (key.Equals("LastAutomaticUpdateCheckUtc", StringComparison.OrdinalIgnoreCase)) settings.LastAutomaticUpdateCheckUtc = value;
            }
            return settings;
        }

        public void Save()
        {
            var lines = new[]
            {
                "[Settings]",
                "OutputMode=" + (OutputMode == OutputMode.SingleFolder ? "SingleFolder" : "AlongsideSourceFiles"),
                "OutputFolder=" + OutputFolder,
                "LastInputFolder=" + LastInputFolder,
                "AskForOutputLocationAfterInput=" + AskForOutputLocationAfterInput,
                "PreferDrumsOnTrack1=" + PreferDrumsOnTrack1,
                "AlwaysShowTrackMapping=" + AlwaysShowTrackMapping,
                "UpdateCheckFrequency=" + UpdateService.NormalizeUpdateCheckFrequency(UpdateCheckFrequency),
                "InstallUpdatesQuietly=" + InstallUpdatesQuietly,
                "LastAutomaticUpdateCheckUtc=" + LastAutomaticUpdateCheckUtc
            };
            File.WriteAllLines(IniPath, lines, Encoding.ASCII);
        }

        public OutputSettings ToOutputSettings()
        {
            return new OutputSettings { Mode = OutputMode, Folder = OutputFolder };
        }

        public void ApplyOutputSettings(OutputSettings output)
        {
            OutputMode = output.Mode;
            OutputFolder = output.Folder ?? "";
        }
    }

    internal sealed class OutputLocationForm : Form
    {
        private readonly RadioButton alongsideRadio;
        private readonly RadioButton singleFolderRadio;
        private readonly TextBox folderTextBox;
        private readonly Button browseButton;
        public OutputSettings Output { get; private set; }

        public OutputLocationForm(AppSettings settings, int fileCount)
        {
            Text = "Output Location";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 240);
            MinimizeBox = false;
            MaximizeBox = false;
            AccessibleName = "Output location";

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
            Controls.Add(layout);

            layout.Controls.Add(new Label { Text = "Choose where to save " + fileCount + " Move bundle file(s).", AutoSize = true, MaximumSize = new Size(560, 0), AccessibleRole = AccessibleRole.StaticText });
            alongsideRadio = new RadioButton { Text = "&Save each bundle next to its source MIDI file", AutoSize = true, Checked = settings.OutputMode == OutputMode.AlongsideSourceFiles, AccessibleName = "Save each bundle next to its source MIDI file" };
            layout.Controls.Add(alongsideRadio);
            singleFolderRadio = new RadioButton { Text = "Save all bundles in &one folder", AutoSize = true, Checked = settings.OutputMode == OutputMode.SingleFolder, AccessibleName = "Save all bundles in one folder" };
            singleFolderRadio.CheckedChanged += delegate { UpdateFolderControls(); };
            layout.Controls.Add(singleFolderRadio);

            var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            row.Controls.Add(new Label { Text = "Output folder:", AutoSize = true, Padding = new Padding(0, 5, 4, 0) });
            folderTextBox = new TextBox { Width = 400, Text = settings.OutputFolder, AccessibleName = "Output folder" };
            row.Controls.Add(folderTextBox);
            browseButton = new Button { Text = "&Browse...", AutoSize = true, AccessibleName = "Browse for output folder" };
            browseButton.Click += delegate { Browse(); };
            row.Controls.Add(browseButton);
            layout.Controls.Add(row);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            ok.Click += delegate
            {
                var folder = folderTextBox.Text.Trim().Trim('"');
                if (singleFolderRadio.Checked && folder.Length == 0)
                {
                    MessageBox.Show(this, "Choose an output folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }
                Output = new OutputSettings { Mode = singleFolderRadio.Checked ? OutputMode.SingleFolder : OutputMode.AlongsideSourceFiles, Folder = folder };
            };

            UpdateFolderControls();
        }

        private void Browse()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(folderTextBox.Text.Trim().Trim('"'))) dialog.SelectedPath = folderTextBox.Text.Trim().Trim('"');
                if (dialog.ShowDialog(this) == DialogResult.OK) folderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void UpdateFolderControls()
        {
            folderTextBox.Enabled = singleFolderRadio.Checked;
            browseButton.Enabled = singleFolderRadio.Checked;
        }
    }

    internal sealed class PreferencesForm : Form
    {
        private readonly AppSettings settings;
        private readonly RadioButton outputAlongsideRadio;
        private readonly RadioButton outputSingleFolderRadio;
        private readonly TextBox outputFolderTextBox;
        private readonly Button outputBrowseButton;
        private readonly CheckBox askOutputCheckBox;
        private readonly CheckBox preferDrumsCheckBox;
        private readonly CheckBox alwaysShowTrackMappingCheckBox;
        private readonly ComboBox updateCheckFrequencyBox;
        private readonly CheckBox installUpdatesQuietlyCheckBox;

        public PreferencesForm(AppSettings settings)
        {
            this.settings = settings;
            Text = "Preferences";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 430);
            MinimumSize = new Size(560, 360);
            AccessibleName = "Preferences";

            var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Preference tabs" };
            Controls.Add(tabs);

            var outputTab = new TabPage("Output Defaults") { AccessibleName = "Output Defaults" };
            tabs.TabPages.Add(outputTab);
            var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(12) };
            outputTab.Controls.Add(outputPanel);
            outputAlongsideRadio = new RadioButton { Text = "&Save each bundle next to its source MIDI file", AutoSize = true, Checked = settings.OutputMode == OutputMode.AlongsideSourceFiles };
            outputPanel.Controls.Add(outputAlongsideRadio);
            outputSingleFolderRadio = new RadioButton { Text = "Save all bundles in &one folder", AutoSize = true, Checked = settings.OutputMode == OutputMode.SingleFolder };
            outputSingleFolderRadio.CheckedChanged += delegate { UpdateOutputFolderControls(); };
            outputPanel.Controls.Add(outputSingleFolderRadio);
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(22, 8, 0, 8) };
            row.Controls.Add(new Label { Text = "Output folder:", AutoSize = true, Padding = new Padding(0, 5, 4, 0) });
            outputFolderTextBox = new TextBox { Width = 360, Text = settings.OutputFolder, AccessibleName = "Output folder path" };
            row.Controls.Add(outputFolderTextBox);
            outputBrowseButton = new Button { Text = "Browse...", AutoSize = true, AccessibleName = "Browse for output folder" };
            outputBrowseButton.Click += delegate { BrowseOutputFolder(); };
            row.Controls.Add(outputBrowseButton);
            outputPanel.Controls.Add(row);
            askOutputCheckBox = CreateCheckBox("&Ask where to save after choosing input", settings.AskForOutputLocationAfterInput, "When checked, MidiToMove asks for output choices after you choose MIDI files. When unchecked, saved output defaults are used immediately.");
            outputPanel.Controls.Add(askOutputCheckBox);

            var conversionTab = new TabPage("Conversion") { AccessibleName = "Conversion" };
            tabs.TabPages.Add(conversionTab);
            var conversionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(12) };
            conversionTab.Controls.Add(conversionPanel);
            preferDrumsCheckBox = CreateCheckBox("Combine MIDI channel 10 drum parts and place them first", settings.PreferDrumsOnTrack1, "When checked, channel 10 drum parts are combined and suggested as Move track 1.");
            conversionPanel.Controls.Add(preferDrumsCheckBox);
            alwaysShowTrackMappingCheckBox = CreateCheckBox("Always ask how MIDI parts map to Move tracks", settings.AlwaysShowTrackMapping, "When checked, MidiToMove opens the track mapping dialog even when the MIDI file has four or fewer parts.");
            conversionPanel.Controls.Add(alwaysShowTrackMappingCheckBox);

            var updatesTab = new TabPage("Updates") { AccessibleName = "Updates" };
            tabs.TabPages.Add(updatesTab);
            var updatesPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(12) };
            updatesTab.Controls.Add(updatesPanel);
            updatesPanel.Controls.Add(new Label { Text = "Check GitHub Releases for updates:", AutoSize = true, AccessibleRole = AccessibleRole.StaticText });
            updateCheckFrequencyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, AccessibleRole = AccessibleRole.ComboBox, AccessibleName = "Check GitHub Releases for updates" };
            updateCheckFrequencyBox.Items.AddRange(UpdateFrequencyLabels());
            updateCheckFrequencyBox.SelectedIndex = UpdateFrequencyIndex(settings.UpdateCheckFrequency);
            updatesPanel.Controls.Add(updateCheckFrequencyBox);
            installUpdatesQuietlyCheckBox = CreateCheckBox("Download and install updates &quietly when available", settings.InstallUpdatesQuietly, "When checked, MidiToMove downloads and installs a release ZIP without first showing release notes.");
            updatesPanel.Controls.Add(installUpdatesQuietlyCheckBox);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10) };
            Controls.Add(buttons);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += delegate { SaveSettings(); };
            UpdateOutputFolderControls();
        }

        private static CheckBox CreateCheckBox(string text, bool isChecked, string description)
        {
            return new CheckBox { Text = text, Checked = isChecked, AutoSize = true, AccessibleRole = AccessibleRole.CheckButton, AccessibleName = text.Replace("&", string.Empty), AccessibleDescription = description, Margin = new Padding(3, 3, 3, 6) };
        }

        private void BrowseOutputFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var folder = outputFolderTextBox.Text.Trim().Trim('"');
                if (Directory.Exists(folder)) dialog.SelectedPath = folder;
                if (dialog.ShowDialog(this) == DialogResult.OK) outputFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void SaveSettings()
        {
            settings.OutputMode = outputSingleFolderRadio.Checked ? OutputMode.SingleFolder : OutputMode.AlongsideSourceFiles;
            settings.OutputFolder = outputFolderTextBox.Text.Trim().Trim('"');
            settings.AskForOutputLocationAfterInput = askOutputCheckBox.Checked;
            settings.PreferDrumsOnTrack1 = preferDrumsCheckBox.Checked;
            settings.AlwaysShowTrackMapping = alwaysShowTrackMappingCheckBox.Checked;
            settings.UpdateCheckFrequency = UpdateFrequencyFromIndex(updateCheckFrequencyBox.SelectedIndex);
            settings.InstallUpdatesQuietly = installUpdatesQuietlyCheckBox.Checked;
        }

        private void UpdateOutputFolderControls()
        {
            outputFolderTextBox.Enabled = outputSingleFolderRadio.Checked;
            outputBrowseButton.Enabled = outputSingleFolderRadio.Checked;
        }

        private static object[] UpdateFrequencyLabels()
        {
            return new object[] { "At startup", "Every hour", "Every 6 hours", "Every 12 hours", "Daily", "Weekly", "Never" };
        }

        private static int UpdateFrequencyIndex(string value)
        {
            switch (UpdateService.NormalizeUpdateCheckFrequency(value))
            {
                case "Hourly": return 1;
                case "6Hours": return 2;
                case "12Hours": return 3;
                case "Daily": return 4;
                case "Weekly": return 5;
                case "Never": return 6;
                default: return 0;
            }
        }

        private static string UpdateFrequencyFromIndex(int index)
        {
            switch (index)
            {
                case 1: return "Hourly";
                case 2: return "6Hours";
                case 3: return "12Hours";
                case 4: return "Daily";
                case 5: return "Weekly";
                case 6: return "Never";
                default: return "Startup";
            }
        }
    }

    internal sealed class PartSelectionForm : Form
    {
        private readonly ListView list;
        private readonly List<ExportPart> parts;
        public List<ExportPart> SelectedParts { get; private set; }

        public PartSelectionForm(string midiPath, List<ExportPart> parts)
        {
            this.parts = parts;
            Text = "Choose Move Tracks";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(780, 520);
            AccessibleName = "Choose Move tracks";
            KeyPreview = true;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            root.Controls.Add(new Label { Text = "Choose MIDI parts and assign each one to a Move track. More than one MIDI part can use the same Move track and will be merged.", AutoSize = true, MaximumSize = new Size(720, 0) }, 0, 0);

            list = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, AccessibleName = "MIDI parts" };
            list.Columns.Add("Use", 70);
            list.Columns.Add("Move track", 90);
            list.Columns.Add("MIDI part", 360);
            list.Columns.Add("Notes", 80);
            list.Columns.Add("Length", 100);
            list.ItemChecked += List_ItemChecked;
            root.Controls.Add(list, 0, 1);

            foreach (var part in parts)
            {
                var item = new ListViewItem("");
                item.SubItems.Add("");
                item.SubItems.Add(part.DisplayName);
                item.SubItems.Add(part.Notes.Count.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(part.EndBeat.ToString("0.##", CultureInfo.InvariantCulture) + " beats");
                item.Tag = part;
                item.Checked = list.CheckedItems.Count < 4;
                list.Items.Add(item);
            }
            RefreshMoveTrackLabels();

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var up = new Button { Text = "Move &Up", AutoSize = true, AccessibleName = "Move up" };
            up.Click += delegate { MoveSelected(-1); };
            buttons.Controls.Add(up);
            var down = new Button { Text = "Move &Down", AutoSize = true, AccessibleName = "Move down" };
            down.Click += delegate { MoveSelected(1); };
            buttons.Controls.Add(down);
            for (var track = 1; track <= 4; track++)
            {
                var moveTrack = track;
                var assign = new Button { Text = "Assign Track &" + moveTrack.ToString(CultureInfo.InvariantCulture), AutoSize = true, AccessibleName = "Assign selected MIDI parts to Move track " + moveTrack.ToString(CultureInfo.InvariantCulture) };
                assign.Click += delegate { AssignSelected(moveTrack); };
                buttons.Controls.Add(assign);
            }
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            ok.Click += delegate { AcceptSelection(); };
            buttons.Controls.Add(ok);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
            AcceptButton = ok;
            CancelButton = cancel;

            list.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Up) { MoveSelected(-1); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.Down) { MoveSelected(1); e.Handled = true; }
            };
        }

        private void List_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Checked && GetAssignedTrack(e.Item) == 0)
            {
                SetAssignedTrack(e.Item, GetDefaultTrackForNewSelection());
            }
            RefreshMoveTrackLabels();
        }

        private int GetDefaultTrackForNewSelection()
        {
            for (var track = 1; track <= 4; track++)
            {
                if (!list.Items.Cast<ListViewItem>().Any(i => i.Checked && GetAssignedTrack(i) == track)) return track;
            }
            return 4;
        }

        private void AssignSelected(int moveTrack)
        {
            if (list.SelectedItems.Count == 0) return;
            foreach (ListViewItem item in list.SelectedItems)
            {
                item.Checked = true;
                SetAssignedTrack(item, moveTrack);
            }
            RefreshMoveTrackLabels();
        }

        private void MoveSelected(int direction)
        {
            if (list.SelectedItems.Count == 0) return;
            var item = list.SelectedItems[0];
            var oldIndex = item.Index;
            var newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= list.Items.Count) return;
            list.Items.RemoveAt(oldIndex);
            list.Items.Insert(newIndex, item);
            item.Selected = true;
            item.Focused = true;
            RefreshMoveTrackLabels();
        }

        private void RefreshMoveTrackLabels()
        {
            var next = 1;
            foreach (ListViewItem item in list.Items)
            {
                if (item.Checked && GetAssignedTrack(item) == 0) SetAssignedTrack(item, Math.Min(4, next));
                item.SubItems[1].Text = item.Checked ? GetAssignedTrack(item).ToString(CultureInfo.InvariantCulture) : "";
                if (item.Checked) next = Math.Max(next, GetAssignedTrack(item) + 1);
            }
        }

        private void AcceptSelection()
        {
            var checkedItems = list.Items.Cast<ListViewItem>().Where(i => i.Checked).ToList();
            SelectedParts = MergeAssignedParts(checkedItems);
            if (SelectedParts.Count == 0)
            {
                MessageBox.Show(this, "Choose at least one MIDI part.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }

        private static int GetAssignedTrack(ListViewItem item)
        {
            int moveTrack;
            return int.TryParse(item.SubItems[1].Text, out moveTrack) ? moveTrack : 0;
        }

        private static void SetAssignedTrack(ListViewItem item, int moveTrack)
        {
            item.SubItems[1].Text = Math.Max(1, Math.Min(4, moveTrack)).ToString(CultureInfo.InvariantCulture);
        }

        private static List<ExportPart> MergeAssignedParts(List<ListViewItem> checkedItems)
        {
            var output = new List<ExportPart>();
            foreach (var group in checkedItems.GroupBy(GetAssignedTrack).Where(g => g.Key >= 1 && g.Key <= 4).OrderBy(g => g.Key))
            {
                var parts = group.Select(i => (ExportPart)i.Tag).ToList();
                if (parts.Count == 1)
                {
                    output.Add(parts[0]);
                    continue;
                }

                var names = parts.Select(p => p.SourceTrackName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var channels = parts.Select(p => p.Channel).Distinct().OrderBy(c => c).Select(c => c.ToString(CultureInfo.InvariantCulture)).ToList();
                var name = names.Count == 0 ? "Merged Track " + group.Key.ToString(CultureInfo.InvariantCulture) : string.Join(" + ", names.ToArray());
                output.Add(new ExportPart
                {
                    DisplayName = name + ", merged channels " + string.Join(", ", channels.ToArray()),
                    SourceTrackName = name,
                    Channel = parts[0].Channel,
                    Notes = parts.SelectMany(p => p.Notes).OrderBy(n => n.StartBeat).ThenBy(n => n.NoteNumber).ToList()
                });
            }
            return output;
        }
    }

    internal sealed class TextReviewForm : Form
    {
        public TextReviewForm(string title, string text)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 560);
            AccessibleName = title;
            var textBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = NormalizeLineEndings(text), AccessibleName = title + " text" };
            Controls.Add(textBox);
            var close = new Button { Dock = DockStyle.Bottom, Text = "Close", DialogResult = DialogResult.OK, AccessibleName = "Close" };
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        private static string NormalizeLineEndings(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }
    }

    internal sealed class GitHubReleaseInfo
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public string body { get; set; }
        public List<GitHubReleaseAsset> assets { get; set; }
    }

    internal sealed class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal static class UpdateService
    {
        public static GitHubReleaseInfo FetchLatestRelease(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
            {
                return new JavaScriptSerializer().Deserialize<GitHubReleaseInfo>(client.DownloadString(ApiUrl(projectUrl) + "/releases/latest"));
            }
        }

        public static List<GitHubReleaseInfo> FetchReleases(string projectUrl, string version)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var client = CreateGitHubClient(version))
            {
                return new JavaScriptSerializer().Deserialize<List<GitHubReleaseInfo>>(client.DownloadString(ApiUrl(projectUrl) + "/releases?per_page=100")) ?? new List<GitHubReleaseInfo>();
            }
        }

        public static GitHubReleaseInfo LatestVersionedRelease(IEnumerable<GitHubReleaseInfo> releases)
        {
            return (releases ?? new List<GitHubReleaseInfo>()).Select(r => new { Release = r, Version = ReleaseVersion(r) }).Where(i => i.Version != null).OrderByDescending(i => i.Version).Select(i => i.Release).FirstOrDefault();
        }

        public static GitHubReleaseAsset FindPortableZipAsset(GitHubReleaseInfo release)
        {
            if (release == null) return null;
            return (release.assets ?? new List<GitHubReleaseAsset>()).Where(a => a != null && !string.IsNullOrWhiteSpace(a.browser_download_url) && !string.IsNullOrWhiteSpace(a.name))
                .Where(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(a => a.name.IndexOf("midi", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault();
        }

        public static string BuildReleaseNotes(IEnumerable<GitHubReleaseInfo> releases, System.Version current, System.Version latest, string currentVersion)
        {
            var newer = (releases ?? new List<GitHubReleaseInfo>()).Select(r => new { Release = r, Version = ReleaseVersion(r) }).Where(i => i.Version != null && i.Version > current && i.Version <= latest).OrderByDescending(i => i.Version).ToList();
            var builder = new StringBuilder();
            builder.AppendLine("Your version: " + currentVersion);
            builder.AppendLine("New version: " + latest);
            builder.AppendLine();
            builder.AppendLine("Changes between " + currentVersion + " and " + latest);
            builder.AppendLine();
            if (newer.Count == 0) builder.AppendLine("No release notes were provided for this update.");
            foreach (var item in newer)
            {
                builder.AppendLine(item.Release.tag_name);
                builder.AppendLine(FormatReleaseNotesForDialog(item.Release.body, "No release notes were provided for this update."));
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string FormatReleaseNotesForDialog(string text, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(text)) return emptyText;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine).Trim();
        }

        public static string NormalizeUpdateCheckFrequency(string value)
        {
            if (string.Equals(value, "Hour", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Hourly", StringComparison.OrdinalIgnoreCase)) return "Hourly";
            if (string.Equals(value, "6Hours", StringComparison.OrdinalIgnoreCase)) return "6Hours";
            if (string.Equals(value, "12Hours", StringComparison.OrdinalIgnoreCase)) return "12Hours";
            if (string.Equals(value, "Daily", StringComparison.OrdinalIgnoreCase)) return "Daily";
            if (string.Equals(value, "Weekly", StringComparison.OrdinalIgnoreCase)) return "Weekly";
            if (string.Equals(value, "Never", StringComparison.OrdinalIgnoreCase)) return "Never";
            return "Startup";
        }

        public static TimeSpan? AutomaticUpdateInterval(string frequency)
        {
            switch (NormalizeUpdateCheckFrequency(frequency))
            {
                case "Hourly": return TimeSpan.FromHours(1);
                case "6Hours": return TimeSpan.FromHours(6);
                case "12Hours": return TimeSpan.FromHours(12);
                case "Daily": return TimeSpan.FromDays(1);
                case "Weekly": return TimeSpan.FromDays(7);
                default: return null;
            }
        }

        public static string GetUpdaterTempDirectory(string appDir)
        {
            var candidates = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData)) candidates.Add(Path.Combine(localAppData, "Temp"));
            candidates.Add(Path.GetTempPath());
            candidates.Add(Path.Combine(appDir, "Update Temp"));
            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
                    Directory.CreateDirectory(fullPath);
                    return fullPath;
                }
                catch { }
            }
            throw new InvalidOperationException("Could not create a temporary folder for the updater.");
        }

        public static string BuildUpdaterScript(string zipUrl, string targetDir, string exePath, string tempDir, int processId, string version)
        {
            return
                "$ErrorActionPreference = 'Stop'\r\n" +
                "Add-Type -AssemblyName System.Windows.Forms\r\n" +
                "$zipUrl = " + PowerShellQuote(zipUrl) + "\r\n" +
                "$userAgent = " + PowerShellQuote("MidiToMove " + version) + "\r\n" +
                "$target = " + PowerShellQuote(targetDir) + "\r\n" +
                "$exe = " + PowerShellQuote(exePath) + "\r\n" +
                "$tempBase = " + PowerShellQuote(tempDir) + "\r\n" +
                "$pidToWait = " + processId.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "try {\r\n" +
                "  [System.IO.Directory]::CreateDirectory($tempBase) | Out-Null\r\n" +
                "  $root = Join-Path $tempBase ('MidiToMoveUpdate_' + [guid]::NewGuid().ToString('N'))\r\n" +
                "  $zip = Join-Path $root 'update.zip'\r\n" +
                "  $stage = Join-Path $root 'stage'\r\n" +
                "  [System.IO.Directory]::CreateDirectory($root) | Out-Null\r\n" +
                "  [System.IO.Directory]::CreateDirectory($stage) | Out-Null\r\n" +
                "  Invoke-WebRequest -Uri $zipUrl -OutFile $zip -UseBasicParsing -UserAgent $userAgent\r\n" +
                "  Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force\r\n" +
                "  $source = $stage\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'MidiToMove.exe'))) {\r\n" +
                "    $candidate = Get-ChildItem -LiteralPath $stage -Recurse -Filter 'MidiToMove.exe' -File | Select-Object -First 1\r\n" +
                "    if ($candidate) { $source = $candidate.DirectoryName }\r\n" +
                "  }\r\n" +
                "  if (-not (Test-Path -LiteralPath (Join-Path $source 'MidiToMove.exe'))) { throw 'The downloaded ZIP does not contain MidiToMove.exe.' }\r\n" +
                "  Get-Process -Id $pidToWait -ErrorAction SilentlyContinue | Wait-Process\r\n" +
                "  Get-ChildItem -LiteralPath $source -Force | ForEach-Object {\r\n" +
                "    if ($_.name -ieq 'MidiToMove.ini' -or $_.name -ieq 'MidiToMove failures.log') { return }\r\n" +
                "    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.name) -Recurse -Force\r\n" +
                "  }\r\n" +
                "  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "  Start-Process -FilePath $exe\r\n" +
                "} catch {\r\n" +
                "  [System.Windows.Forms.MessageBox]::Show('MidiToMove update failed:' + [Environment]::NewLine + [Environment]::NewLine + $_.Exception.Message, 'MidiToMove updater', 'OK', 'Error') | Out-Null\r\n" +
                "}\r\n" +
                "Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n";
        }

        private static WebClient CreateGitHubClient(string version)
        {
            var client = new WebClient();
            client.Headers.Add("User-Agent", "MidiToMove " + version);
            return client;
        }

        private static string ApiUrl(string projectUrl) { return projectUrl.Replace("https://github.com/", "https://api.github.com/repos/"); }

        private static System.Version ReleaseVersion(GitHubReleaseInfo release)
        {
            if (release == null || string.IsNullOrWhiteSpace(release.tag_name)) return null;
            System.Version version;
            return System.Version.TryParse(release.tag_name.Trim().TrimStart('v', 'V'), out version) ? version : null;
        }

        private static string PowerShellQuote(string value) { return "'" + (value ?? string.Empty).Replace("'", "''") + "'"; }
    }

    internal sealed class MidiFile
    {
        public int TicksPerQuarter = 480;
        public double Tempo = 120.0;
        public int TimeSignatureNumerator = 4;
        public int TimeSignatureDenominator = 4;
        public readonly List<MidiTrack> Tracks = new List<MidiTrack>();

        public List<ExportPart> BuildExportParts(bool combineDrums)
        {
            var parts = new List<ExportPart>();
            foreach (var track in Tracks)
            {
                foreach (var group in track.Notes.GroupBy(n => n.Channel).OrderBy(g => g.Key))
                {
                    var name = string.IsNullOrWhiteSpace(track.Name) ? "Track " + track.Index.ToString(CultureInfo.InvariantCulture) : track.Name.Trim();
                    var display = name + ", channel " + group.Key.ToString(CultureInfo.InvariantCulture);
                    parts.Add(new ExportPart { DisplayName = display, Channel = group.Key, SourceTrackName = name, Notes = group.OrderBy(n => n.StartBeat).ToList() });
                }
            }

            parts = MergeMatchingParts(parts);

            if (combineDrums)
            {
                var drums = parts.Where(p => p.Channel == 10).ToList();
                if (drums.Count > 1)
                {
                    foreach (var drum in drums) parts.Remove(drum);
                    parts.Insert(0, new ExportPart
                    {
                        DisplayName = "Drums, MIDI channel 10 combined",
                        Channel = 10,
                        SourceTrackName = "Drums",
                        Notes = drums.SelectMany(p => p.Notes).OrderBy(n => n.StartBeat).ToList()
                    });
                }
                else if (drums.Count == 1)
                {
                    parts.Remove(drums[0]);
                    drums[0].DisplayName = "Drums, MIDI channel 10";
                    drums[0].SourceTrackName = "Drums";
                    parts.Insert(0, drums[0]);
                }
            }

            return parts.Where(p => p.Notes.Count > 0).OrderBy(p => combineDrums && p.Channel == 10 ? 0 : 1).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<ExportPart> MergeMatchingParts(List<ExportPart> parts)
        {
            return parts
                .GroupBy(p => new { Name = NormalizePartName(p.SourceTrackName), p.Channel })
                .Select(g =>
                {
                    var ordered = g.ToList();
                    if (ordered.Count == 1) return ordered[0];
                    var first = ordered[0];
                    return new ExportPart
                    {
                        DisplayName = first.SourceTrackName + ", channel " + first.Channel.ToString(CultureInfo.InvariantCulture) + " (" + ordered.Count.ToString(CultureInfo.InvariantCulture) + " MIDI tracks combined)",
                        Channel = first.Channel,
                        SourceTrackName = first.SourceTrackName,
                        Notes = ordered.SelectMany(p => p.Notes).OrderBy(n => n.StartBeat).ToList()
                    };
                })
                .ToList();
        }

        private static string NormalizePartName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "" : name.Trim().ToUpperInvariant();
        }
    }

    internal sealed class MidiTrack
    {
        public int Index;
        public string Name = "";
        public readonly List<MidiNote> Notes = new List<MidiNote>();
        public readonly List<MidiSustainEvent> SustainEvents = new List<MidiSustainEvent>();
    }

    internal sealed class MidiNote
    {
        public int Channel;
        public int NoteNumber;
        public double StartBeat;
        public double DurationBeat;
        public int Velocity;
    }

    internal sealed class MidiSustainEvent
    {
        public int Channel;
        public double Beat;
        public bool IsDown;
    }

    internal sealed class ExportPart
    {
        public string DisplayName;
        public string SourceTrackName;
        public int Channel;
        public List<MidiNote> Notes = new List<MidiNote>();
        public double EndBeat { get { return Notes.Count == 0 ? 0 : Notes.Max(n => n.StartBeat + n.DurationBeat); } }
    }

    internal static class MidiFileReader
    {
        public static MidiFile Read(string path)
        {
            var data = File.ReadAllBytes(path);
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                if (ReadAscii(reader, 4) != "MThd") throw new InvalidDataException("Not a MIDI file.");
                var headerLength = ReadInt32(reader);
                var format = ReadInt16(reader);
                var trackCount = ReadInt16(reader);
                var division = ReadInt16(reader);
                if ((division & 0x8000) != 0) throw new InvalidDataException("SMPTE time division MIDI files are not supported yet.");
                if (headerLength > 6) reader.ReadBytes(headerLength - 6);
                var midi = new MidiFile { TicksPerQuarter = division };
                for (var i = 0; i < trackCount; i++)
                {
                    if (ReadAscii(reader, 4) != "MTrk") throw new InvalidDataException("Missing MIDI track chunk.");
                    var length = ReadInt32(reader);
                    var chunk = reader.ReadBytes(length);
                    var track = ParseTrack(chunk, i + 1, midi, format);
                    midi.Tracks.Add(track);
                }
                foreach (var track in midi.Tracks) ApplySustainPedal(track);
                return midi;
            }
        }

        private static MidiTrack ParseTrack(byte[] chunk, int index, MidiFile midi, int format)
        {
            var track = new MidiTrack { Index = index };
            var open = new Dictionary<string, Stack<Tuple<long, int>>>();
            using (var ms = new MemoryStream(chunk))
            using (var reader = new BinaryReader(ms))
            {
                long tick = 0;
                int running = 0;
                while (ms.Position < ms.Length)
                {
                    tick += ReadVariable(reader);
                    var first = reader.ReadByte();
                    int status;
                    int data1;
                    if (first < 0x80)
                    {
                        if (running == 0) throw new InvalidDataException("MIDI running status appeared before a status byte.");
                        status = running;
                        data1 = first;
                    }
                    else
                    {
                        status = first;
                        data1 = -1;
                        if (status < 0xF0) running = status;
                    }

                    if (status == 0xFF)
                    {
                        var type = reader.ReadByte();
                        var length = ReadVariable(reader);
                        var payload = reader.ReadBytes((int)length);
                        if (type == 0x03 && payload.Length > 0 && string.IsNullOrWhiteSpace(track.Name)) track.Name = DecodeText(payload);
                        else if (type == 0x51 && payload.Length == 3 && midi.Tempo <= 120.0)
                        {
                            var micros = (payload[0] << 16) | (payload[1] << 8) | payload[2];
                            if (micros > 0) midi.Tempo = 60000000.0 / micros;
                        }
                        else if (type == 0x58 && payload.Length >= 2)
                        {
                            midi.TimeSignatureNumerator = payload[0];
                            midi.TimeSignatureDenominator = 1 << payload[1];
                        }
                        continue;
                    }

                    if (status == 0xF0 || status == 0xF7)
                    {
                        var length = ReadVariable(reader);
                        reader.ReadBytes((int)length);
                        continue;
                    }

                    var command = status & 0xF0;
                    var channel = (status & 0x0F) + 1;
                    var p1 = data1 >= 0 ? data1 : reader.ReadByte();
                    var p2 = (command == 0xC0 || command == 0xD0) ? 0 : reader.ReadByte();
                    if (command == 0xB0 && p1 == 64)
                    {
                        track.SustainEvents.Add(new MidiSustainEvent
                        {
                            Channel = channel,
                            Beat = tick / (double)midi.TicksPerQuarter,
                            IsDown = p2 >= 64
                        });
                    }
                    if (command == 0x90 && p2 > 0)
                    {
                        var key = channel.ToString(CultureInfo.InvariantCulture) + ":" + p1.ToString(CultureInfo.InvariantCulture);
                        Stack<Tuple<long, int>> stack;
                        if (!open.TryGetValue(key, out stack)) open[key] = stack = new Stack<Tuple<long, int>>();
                        stack.Push(Tuple.Create(tick, p2));
                    }
                    else if (command == 0x80 || (command == 0x90 && p2 == 0))
                    {
                        var key = channel.ToString(CultureInfo.InvariantCulture) + ":" + p1.ToString(CultureInfo.InvariantCulture);
                        Stack<Tuple<long, int>> stack;
                        if (open.TryGetValue(key, out stack) && stack.Count > 0)
                        {
                            var start = stack.Pop();
                            var duration = Math.Max(1, tick - start.Item1);
                            track.Notes.Add(new MidiNote
                            {
                                Channel = channel,
                                NoteNumber = p1,
                                StartBeat = start.Item1 / (double)midi.TicksPerQuarter,
                                DurationBeat = duration / (double)midi.TicksPerQuarter,
                                Velocity = start.Item2
                            });
                        }
                    }
                }
            }
            return track;
        }

        private static void ApplySustainPedal(MidiTrack track)
        {
            if (track.SustainEvents.Count == 0 || track.Notes.Count == 0) return;

            foreach (var channelGroup in track.SustainEvents.GroupBy(e => e.Channel))
            {
                var channel = channelGroup.OrderBy(e => e.Beat).ToList();
                var notes = track.Notes.Where(n => n.Channel == channelGroup.Key).OrderBy(n => n.StartBeat).ToList();
                foreach (var note in notes)
                {
                    var physicalEnd = note.StartBeat + note.DurationBeat;
                    if (!IsSustainDownAt(channel, physicalEnd)) continue;

                    var release = NextSustainRelease(channel, physicalEnd);
                    if (!release.HasValue) continue;

                    var nextSamePitch = notes
                        .Where(n => n.NoteNumber == note.NoteNumber && n.StartBeat > note.StartBeat)
                        .Select(n => (double?)n.StartBeat)
                        .FirstOrDefault();
                    var sustainedEnd = release.Value;
                    if (nextSamePitch.HasValue && nextSamePitch.Value < sustainedEnd) sustainedEnd = nextSamePitch.Value;
                    if (sustainedEnd > physicalEnd) note.DurationBeat = sustainedEnd - note.StartBeat;
                }
            }
        }

        private static bool IsSustainDownAt(List<MidiSustainEvent> events, double beat)
        {
            bool down = false;
            foreach (var sustainEvent in events)
            {
                if (sustainEvent.Beat > beat) break;
                down = sustainEvent.IsDown;
            }
            return down;
        }

        private static double? NextSustainRelease(List<MidiSustainEvent> events, double beat)
        {
            foreach (var sustainEvent in events)
            {
                if (sustainEvent.Beat > beat && !sustainEvent.IsDown) return sustainEvent.Beat;
            }
            return null;
        }

        private static string DecodeText(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.IndexOf('\uFFFD') >= 0 ? Encoding.Default.GetString(bytes) : text;
        }

        private static string ReadAscii(BinaryReader reader, int count) { return Encoding.ASCII.GetString(reader.ReadBytes(count)); }
        private static int ReadInt16(BinaryReader reader) { var b = reader.ReadBytes(2); return (b[0] << 8) | b[1]; }
        private static int ReadInt32(BinaryReader reader) { var b = reader.ReadBytes(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }

        private static long ReadVariable(BinaryReader reader)
        {
            long value = 0;
            byte b;
            do
            {
                b = reader.ReadByte();
                value = (value << 7) | (uint)(b & 0x7F);
            } while ((b & 0x80) != 0);
            return value;
        }
    }

    internal sealed class ConvertResult
    {
        public string OutputPath;
        public int TrackCount;
        public int SceneCount;
        public int SourceSceneCount;
        public int NoteCount;

        public string ToMessage()
        {
            var message = string.Format(CultureInfo.InvariantCulture, "Saved: {0}. Tracks: {1}; scenes: {2}; notes: {3}.", OutputPath, TrackCount, SceneCount, NoteCount);
            if (SourceSceneCount > SceneCount)
            {
                message += string.Format(CultureInfo.InvariantCulture, " Source is about {0} scenes long; only the first {1} Move scenes were written.", SourceSceneCount, SceneCount);
            }
            return message;
        }
    }

    internal static class MoveBundleWriter
    {
        public const int MoveSceneLimit = 8;
        private const double BarsPerScene = 16.0;

        public static ConvertResult Write(string sourcePath, string outputPath, MidiFile midi, List<ExportPart> parts, int maximumScenes)
        {
            maximumScenes = Math.Max(1, Math.Min(MoveSceneLimit, maximumScenes));
            var clipBeats = GetClipBeats(midi);
            int scenesUsed;
            int notesWritten;
            var song = BuildSong(sourcePath, midi, parts, maximumScenes, clipBeats, out scenesUsed, out notesWritten);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 };
            var json = serializer.Serialize(song);

            StoredZipWriter.WriteSingleFile(outputPath, "Song.abl", new UTF8Encoding(false).GetBytes(json));

            var maxBeat = parts.Count == 0 ? 0 : parts.Max(p => p.EndBeat);
            var sourceSceneCount = Math.Max(1, (int)Math.Ceiling(maxBeat / clipBeats));
            return new ConvertResult { OutputPath = outputPath, TrackCount = parts.Count, SceneCount = scenesUsed, SourceSceneCount = sourceSceneCount, NoteCount = notesWritten };
        }

        private static Dictionary<string, object> BuildSong(string sourcePath, MidiFile midi, List<ExportPart> parts, int maximumScenes, double clipBeats, out int scenesUsed, out int notesWritten)
        {
            notesWritten = 0;
            var maxBeat = parts.Count == 0 ? 0 : parts.Max(p => p.EndBeat);
            var barBeats = GetBarBeats(midi);
            scenesUsed = Math.Max(1, Math.Min(maximumScenes, (int)Math.Ceiling(maxBeat / clipBeats)));
            var tracks = new List<object>();
            var colors = new[] { 1, 6, 14, 16 };
            for (var i = 0; i < 4; i++)
            {
                var part = i < parts.Count ? parts[i] : null;
                tracks.Add(BuildTrack(part, i, colors[i], scenesUsed, barBeats, clipBeats, ref notesWritten));
            }

            var scenes = new List<object>();
            for (var i = 0; i < MoveSceneLimit; i++) scenes.Add(new Dictionary<string, object> { { "name", i < scenesUsed ? "Scene " + (i + 1).ToString(CultureInfo.InvariantCulture) : "" }, { "color", null } });

            return new Dictionary<string, object>
            {
                { "$schema", "http://tech.ableton.com/schema/song/1.8.3/song.json" },
                { "stepEditorResolution", "1/16" },
                { "tempo", midi.Tempo },
                { "globalGrooveAmount", 0.0 },
                { "timeSignature", new Dictionary<string, object> { { "upper", Math.Max(1, midi.TimeSignatureNumerator) }, { "lower", Math.Max(1, midi.TimeSignatureDenominator) } } },
                { "rootNote", 0 },
                { "scale", "major" },
                { "melodicLayout", "chromatic" },
                { "tracks", tracks },
                { "returnTracks", new List<object>() },
                { "masterTrack", new Dictionary<string, object> { { "color", 23 }, { "devices", new List<object>() }, { "mixer", new Dictionary<string, object> { { "pan", 0.0 }, { "volume", 0.0 } } } } },
                { "scenes", scenes },
                { "grooves", new List<object> { BuildSwing16thsGroove() } },
                { "metadata", new Dictionary<string, object> { { "usedFeatures", new List<object>() } } }
            };
        }

        private static double GetBarBeats(MidiFile midi)
        {
            if (midi == null || midi.TimeSignatureNumerator <= 0 || midi.TimeSignatureDenominator <= 0) return 4.0;
            return Math.Max(1.0, midi.TimeSignatureNumerator * (4.0 / midi.TimeSignatureDenominator));
        }

        private static double GetClipBeats(MidiFile midi)
        {
            return Math.Max(1.0, GetBarBeats(midi) * BarsPerScene);
        }

        private static Dictionary<string, object> BuildSwing16thsGroove()
        {
            var events = new List<object>();
            var times = new[] { 0.0, 1.0 / 3.0, 0.5, 5.0 / 6.0, 1.0, 4.0 / 3.0, 1.5, 11.0 / 6.0, 2.0, 7.0 / 3.0, 2.5, 17.0 / 6.0, 3.0, 10.0 / 3.0, 3.5, 23.0 / 6.0 };
            foreach (var time in times) events.Add(new Dictionary<string, object> { { "time", time } });
            return new Dictionary<string, object>
            {
                { "id", 1 },
                { "name", "Swing 16ths" },
                { "base", "1/16" },
                { "loop", new Dictionary<string, object> { { "start", 0.0 }, { "end", 4.0 } } },
                { "events", events }
            };
        }

        private static Dictionary<string, object> BuildTrack(ExportPart part, int index, int color, int scenesUsed, double barBeats, double clipBeats, ref int notesWritten)
        {
            var clipSlots = new List<object>();
            for (var scene = 0; scene < MoveSceneLimit; scene++)
            {
                object clip = null;
                if (part != null && scene < scenesUsed)
                {
                    var start = scene * clipBeats;
                    var notes = new List<object>();
                    foreach (var note in part.Notes.Where(n => n.StartBeat < start + clipBeats && n.StartBeat + n.DurationBeat > start).OrderBy(n => n.StartBeat))
                    {
                        var localStart = Math.Max(0.0, note.StartBeat - start);
                        var noteEnd = note.StartBeat + note.DurationBeat;
                        var localEnd = Math.Min(clipBeats, noteEnd - start);
                        var duration = localEnd - localStart;
                        if (duration <= 0) continue;
                        notes.Add(new Dictionary<string, object>
                        {
                            { "noteNumber", Math.Max(0, Math.Min(127, note.NoteNumber)) },
                            { "startTime", localStart },
                            { "duration", duration },
                            { "velocity", Math.Max(1, Math.Min(127, note.Velocity)) },
                            { "offVelocity", 0.0 }
                        });
                    }
                    if (notes.Count > 0)
                    {
                        NormalizeSamePitchOverlaps(notes);
                        notesWritten += notes.Count;
                        var clipLength = GetClipLength(notes, barBeats, clipBeats);
                        clip = new Dictionary<string, object>
                        {
                            { "isPlaying", scene == 0 },
                            { "name", part.SourceTrackName ?? "" },
                            { "color", color },
                            { "isEnabled", true },
                            { "region", new Dictionary<string, object> { { "start", 0.0 }, { "end", clipLength }, { "loop", new Dictionary<string, object> { { "start", 0.0 }, { "end", clipLength }, { "isEnabled", true } } } } },
                            { "grooveId", 1 },
                            { "stepEditorScrollPosition", 0.0 },
                            { "notes", notes },
                            { "envelopes", new List<object>() }
                        };
                    }
                }
                clipSlots.Add(new Dictionary<string, object> { { "hasStop", true }, { "clip", clip } });
            }

            return new Dictionary<string, object>
            {
                { "kind", "midi" },
                { "name", part == null ? "" : part.SourceTrackName },
                { "color", color },
                { "clipSlots", clipSlots },
                { "isArmed", false },
                { "isNoteRepeatOn", false },
                { "noteRepeatRate", "1/16" },
                { "noteRepeatArpeggio", new Dictionary<string, object> { { "style", "chordRepeat" } } },
                { "uiOctaveIndex", 2 },
                { "devices", new List<object> { BuildDisabledInstrument(index) } },
                { "mixer", new Dictionary<string, object> { { "pan", 0.0 }, { "solo-cue", false }, { "speakerOn", true }, { "volume", 0.0 }, { "sends", new List<object>() } } }
            };
        }

        private static void NormalizeSamePitchOverlaps(List<object> notes)
        {
            var remove = new HashSet<Dictionary<string, object>>();
            var byPitch = notes.Cast<Dictionary<string, object>>()
                .GroupBy(n => Convert.ToInt32(n["noteNumber"], CultureInfo.InvariantCulture));

            foreach (var group in byPitch)
            {
                var ordered = group
                    .OrderBy(n => Convert.ToDouble(n["startTime"], CultureInfo.InvariantCulture))
                    .ThenByDescending(n => Convert.ToDouble(n["duration"], CultureInfo.InvariantCulture))
                    .ToList();

                for (var i = 0; i < ordered.Count - 1; i++)
                {
                    var current = ordered[i];
                    if (remove.Contains(current)) continue;
                    var next = ordered[i + 1];
                    var start = Convert.ToDouble(current["startTime"], CultureInfo.InvariantCulture);
                    var duration = Convert.ToDouble(current["duration"], CultureInfo.InvariantCulture);
                    var nextStart = Convert.ToDouble(next["startTime"], CultureInfo.InvariantCulture);
                    if (start + duration <= nextStart) continue;

                    var newDuration = nextStart - start;
                    if (newDuration >= 0.001)
                    {
                        current["duration"] = newDuration;
                    }
                    else
                    {
                        remove.Add(current);
                    }
                }
            }

            if (remove.Count == 0) return;
            for (var i = notes.Count - 1; i >= 0; i--)
            {
                var note = notes[i] as Dictionary<string, object>;
                if (note != null && remove.Contains(note)) notes.RemoveAt(i);
            }
        }

        private static double GetClipLength(List<object> notes, double barBeats, double clipBeats)
        {
            var maxEnd = 0.0;
            foreach (Dictionary<string, object> note in notes)
            {
                var start = Convert.ToDouble(note["startTime"], CultureInfo.InvariantCulture);
                var duration = Convert.ToDouble(note["duration"], CultureInfo.InvariantCulture);
                maxEnd = Math.Max(maxEnd, start + duration);
            }

            var unit = barBeats > 0 ? barBeats : 4.0;
            var rounded = Math.Ceiling(Math.Max(0.0001, maxEnd - 0.000001) / unit) * unit;
            return Math.Max(unit, Math.Min(clipBeats, rounded));
        }

        private static Dictionary<string, object> BuildDisabledInstrument(int index)
        {
            var presets = new[]
            {
                new[] { "ableton:/packs/abl-core-library/Track%20Presets/Brass/Four%20Brass.json", "Four Brass" },
                new[] { "ableton:/packs/abl-core-library/Track%20Presets/Brass/Neo%20Jab.json", "Neo Jab" },
                new[] { "ableton:/packs/abl-core-library/Track%20Presets/Synth%20Pluck/Bridge%20Pluck.json", "Bridge Pluck" },
                new[] { "ableton:/packs/abl-core-library/Track%20Presets/Rhythmic/Ecstatic%20Pluck.json", "Ecstatic Pluck" }
            };
            var preset = presets[Math.Max(0, Math.Min(3, index))];
            return new Dictionary<string, object>
            {
                { "presetUri", preset[0] },
                { "kind", "instrumentRack" },
                { "name", preset[1] },
                { "lockId", 1001 },
                { "lockSeal", 0 },
                { "parameters", new Dictionary<string, object>
                    {
                        { "Enabled", false },
                        { "Macro0", 0.0 },
                        { "Macro1", 0.0 },
                        { "Macro2", 0.0 },
                        { "Macro3", 0.0 },
                        { "Macro4", 0.0 },
                        { "Macro5", 0.0 },
                        { "Macro6", 0.0 },
                        { "Macro7", 0.0 }
                    }
                },
                { "chains", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "name", "" },
                            { "color", 0 },
                            { "devices", new List<object> { BuildSilentInstrumentRack() } },
                            { "mixer", new Dictionary<string, object> { { "pan", 0.0 }, { "solo-cue", false }, { "speakerOn", true }, { "volume", 0.0 }, { "sends", new List<object>() } } }
                        }
                    }
                }
            };
        }

        private static Dictionary<string, object> BuildSilentInstrumentRack()
        {
            return new Dictionary<string, object>
            {
                { "presetUri", null },
                { "kind", "instrumentRack" },
                { "name", "Silent Instrument" },
                { "lockId", 2001 },
                { "lockSeal", 0 },
                { "parameters", new Dictionary<string, object>
                    {
                        { "Enabled", false },
                        { "Macro0", 0.0 },
                        { "Macro1", 0.0 },
                        { "Macro2", 0.0 },
                        { "Macro3", 0.0 },
                        { "Macro4", 0.0 },
                        { "Macro5", 0.0 },
                        { "Macro6", 0.0 },
                        { "Macro7", 0.0 }
                    }
                },
                { "chains", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "name", "" },
                            { "color", 0 },
                            { "devices", new List<object> { BuildSilentWavetable() } },
                            { "mixer", new Dictionary<string, object> { { "pan", 0.0 }, { "solo-cue", false }, { "speakerOn", true }, { "volume", 0.0 }, { "sends", new List<object>() } } }
                        }
                    }
                }
            };
        }

        private static Dictionary<string, object> BuildSilentWavetable()
        {
            return new Dictionary<string, object>
            {
                { "presetUri", null },
                { "kind", "wavetable" },
                { "name", "Wavetable" },
                { "parameters", new Dictionary<string, object>
                    {
                        { "Enabled", false },
                        { "HiQ", false },
                        { "MonoPoly", "Poly" },
                        { "PolyVoices", "4" },
                        { "Voice_Global_Glide", 0.0 },
                        { "Voice_Global_Transpose", 0.0 },
                        { "Voice_Oscillator1_Gain", 0.0 },
                        { "Voice_Oscillator1_On", true },
                        { "Voice_Oscillator2_Gain", 0.0 },
                        { "Voice_Oscillator2_On", false }
                    }
                },
                { "deviceData", new Dictionary<string, object>
                    {
                        { "spriteUri1", null },
                        { "spriteUri2", null },
                        { "modulations", new Dictionary<string, object>() }
                    }
                }
            };
        }
    }

    internal static class StoredZipWriter
    {
        public static void WriteSingleFile(string zipPath, string entryName, byte[] data)
        {
            var nameBytes = Encoding.UTF8.GetBytes(entryName);
            var crc = Crc32(data);
            using (var fs = File.Create(zipPath))
            using (var writer = new BinaryWriter(fs))
            {
                WriteUInt32(writer, 0x04034b50);
                WriteUInt16(writer, 20);
                WriteUInt16(writer, 0x0800);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt32(writer, crc);
                WriteUInt32(writer, (uint)data.Length);
                WriteUInt32(writer, (uint)data.Length);
                WriteUInt16(writer, (ushort)nameBytes.Length);
                WriteUInt16(writer, 0);
                writer.Write(nameBytes);
                writer.Write(data);

                var centralOffset = fs.Position;
                WriteUInt32(writer, 0x02014b50);
                WriteUInt16(writer, 20);
                WriteUInt16(writer, 20);
                WriteUInt16(writer, 0x0800);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt32(writer, crc);
                WriteUInt32(writer, (uint)data.Length);
                WriteUInt32(writer, (uint)data.Length);
                WriteUInt16(writer, (ushort)nameBytes.Length);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt32(writer, 0);
                WriteUInt32(writer, 0);
                writer.Write(nameBytes);
                var centralSize = fs.Position - centralOffset;

                WriteUInt32(writer, 0x06054b50);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 0);
                WriteUInt16(writer, 1);
                WriteUInt16(writer, 1);
                WriteUInt32(writer, (uint)centralSize);
                WriteUInt32(writer, (uint)centralOffset);
                WriteUInt16(writer, 0);
            }
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xffffffff;
            foreach (var b in data)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
            return ~crc;
        }

        private static void WriteUInt16(BinaryWriter writer, ushort value)
        {
            writer.Write(value);
        }

        private static void WriteUInt32(BinaryWriter writer, uint value)
        {
            writer.Write(value);
        }
    }
}

