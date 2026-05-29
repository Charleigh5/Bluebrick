using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BlueBrick.Services;
using BlueBrick.Simulation;

namespace BlueBrick
{
    /// <summary>
    ///     Provides a fully simulated IDE and SolidWorks development playground so engineers can validate add-ins,
    ///     macros and supporting assets without connecting to a real SolidWorks installation.
    /// </summary>
    public class FrmSandbox : Form
    {
        private readonly FileWorkspaceManager _workspaceManager;
        private readonly MockSolidWorksEnvironment _solidWorksEnvironment;
        private readonly BreakpointManager _breakpointManager;
        private readonly MockSolidWorksExecutor _solidWorksExecutor;
        private readonly UserInteractionSimulator _interactionSimulator;
        private readonly BindingSource _documentsBinding = new BindingSource();
        private readonly BindingSource _apiLogBinding = new BindingSource();
        private readonly BindingSource _fileEventBinding = new BindingSource();
        private CancellationTokenSource? _executionCts;

        private readonly Dictionary<TabPage, string> _tabToPath = new Dictionary<TabPage, string>();
        private readonly Dictionary<string, TabPage> _pathToTab = new Dictionary<string, TabPage>(StringComparer.OrdinalIgnoreCase);

        private TabControl _workspaceTabs = null!;
        private TreeView _fileTree = null!;
        private ListBox _extensionsList = null!;
        private TabControl _editorTabs = null!;
        private TabControl _consoleTabs = null!;
        private ListView _gitStatusView = null!;
        private ComboBox _commandPalette = null!;
        private TextBox _outputConsole = null!;
        private ListBox _debugConsole = null!;
        private ListBox _terminalConsole = null!;
        private ListBox _apiLogList = null!;
        private ListBox _interactionList = null!;
        private ListBox _documentList = null!;
        private Panel _graphicsPanel = null!;
        private PropertyGrid _propertyManager = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _coordinateLabel = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private System.Windows.Forms.Timer _coordinateTimer = null!;
        private Label _environmentStatus = null!;
        private ContextMenuStrip _fileTreeContext = null!;

        public FrmSandbox()
        {
            Text = "SolidWorks Development Sandbox";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1200, 800);

            var sandboxRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlueBrick", "SandboxWorkspace");
            _workspaceManager = new FileWorkspaceManager(sandboxRoot);
            _workspaceManager.SetSynchronizingObject(this);
            _solidWorksEnvironment = new MockSolidWorksEnvironment();
            _breakpointManager = new BreakpointManager();
            _solidWorksExecutor = new MockSolidWorksExecutor(_solidWorksEnvironment, _breakpointManager);
            _interactionSimulator = new UserInteractionSimulator(_solidWorksEnvironment);

            InitializeComponents();
            InitializeDataBindings();
            RefreshFileTree();
            PopulateCommandPalette();
            RefreshGitStatus();
            RefreshInteractionLog();
            UpdateEnvironmentStatus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _workspaceManager.Dispose();
                _coordinateTimer?.Dispose();
                _executionCts?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponents()
        {
            var mainToolStrip = BuildToolStrip();
            Controls.Add(mainToolStrip);

            _workspaceTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, mainToolStrip.Height)
            };

            var ideTab = new TabPage("IDE Workspace") { BackColor = Color.FromArgb(30, 30, 30) };
            ideTab.Controls.Add(BuildIdeWorkspace());

            var solidWorksTab = new TabPage("SolidWorks Simulation") { BackColor = Color.FromArgb(37, 37, 38) };
            solidWorksTab.Controls.Add(BuildSolidWorksWorkspace());

            _workspaceTabs.TabPages.Add(ideTab);
            _workspaceTabs.TabPages.Add(solidWorksTab);

            Controls.Add(_workspaceTabs);
        }

        private ToolStrip BuildToolStrip()
        {
            var strip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System
            };

            var btnNewFile = new ToolStripButton("New File", null, (_, _) => CreateNewFile()) { ToolTipText = "Create a new file in the workspace" };
            var btnNewFolder = new ToolStripButton("New Folder", null, (_, _) => CreateNewFolder()) { ToolTipText = "Create a new folder in the workspace" };
            var btnSave = new ToolStripButton("Save", null, async (_, _) => await SaveActiveDocumentAsync())
            {
                ToolTipText = "Save the active document"
            };
            var btnRunMacro = new ToolStripButton("Run Macro", null, async (_, _) => await RunMacroAsync());
            var btnRunAddIn = new ToolStripButton("Execute Add-in", null, async (_, _) => await ExecuteAddInAsync());
            var btnRunVba = new ToolStripButton("Run VBA", null, async (_, _) => await RunVbaAsync());
            var btnCancel = new ToolStripButton("Cancel Execution", null, (_, _) => CancelExecution());
            var btnBreakpoint = new ToolStripButton("Toggle Breakpoint", null, (_, _) => ToggleBreakpoint());

            strip.Items.Add(btnNewFile);
            strip.Items.Add(btnNewFolder);
            strip.Items.Add(btnSave);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(btnRunMacro);
            strip.Items.Add(btnRunAddIn);
            strip.Items.Add(btnRunVba);
            strip.Items.Add(btnCancel);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(btnBreakpoint);

            return strip;
        }

        private Control BuildIdeWorkspace()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.BackColor = Color.FromArgb(37, 37, 38);

            layout.Controls.Add(BuildCommandPalettePanel(), 0, 0);

            var bodySplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 280,
                BackColor = Color.FromArgb(37, 37, 38)
            };

            bodySplit.Panel1.Controls.Add(BuildExplorerPanel());
            bodySplit.Panel2.Controls.Add(BuildEditorPanel());

            layout.Controls.Add(bodySplit, 0, 1);

            return layout;
        }

        private Control BuildCommandPalettePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var lblCommand = new Label
            {
                Text = "Command Palette:",
                ForeColor = Color.White,
                AutoSize = true,
                Dock = DockStyle.Left
            };

            _commandPalette = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            var btnExecute = new Button
            {
                Text = "Execute",
                Dock = DockStyle.Right,
                Width = 90
            };
            btnExecute.Click += (_, _) => ExecuteSelectedCommand();

            panel.Controls.Add(btnExecute);
            panel.Controls.Add(_commandPalette);
            panel.Controls.Add(lblCommand);

            return panel;
        }

        private Control BuildExplorerPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

            var explorerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(37, 37, 38)
            };

            var explorerLabel = new Label
            {
                Text = "EXPLORER",
                ForeColor = Color.FromArgb(200, 200, 200),
                Dock = DockStyle.Top,
                Height = 20
            };

            _fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            _fileTree.NodeMouseDoubleClick += (_, e) => OpenFileFromNode(e.Node);

            _fileTreeContext = new ContextMenuStrip();
            _fileTreeContext.Items.Add("New File", null, (_, _) => CreateNewFile());
            _fileTreeContext.Items.Add("New Folder", null, (_, _) => CreateNewFolder());
            _fileTreeContext.Items.Add("Rename", null, (_, _) => RenameSelectedNode());
            _fileTreeContext.Items.Add("Delete", null, (_, _) => DeleteSelectedNode());
            _fileTree.ContextMenuStrip = _fileTreeContext;

            explorerPanel.Controls.Add(_fileTree);
            explorerPanel.Controls.Add(explorerLabel);

            var extensionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(37, 37, 38)
            };

            var extensionsLabel = new Label
            {
                Text = "EXTENSIONS",
                ForeColor = Color.FromArgb(200, 200, 200),
                Dock = DockStyle.Top,
                Height = 20
            };

            _extensionsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            _extensionsList.Items.AddRange(new object[]
            {
                "SolidWorks API IntelliSense",
                "VBA Macro Helper",
                "Add-in Deployment Wizard",
                "Simulation Snapshot Recorder",
                "API Call Visualizer"
            });

            extensionsPanel.Controls.Add(_extensionsList);
            extensionsPanel.Controls.Add(extensionsLabel);

            layout.Controls.Add(explorerPanel, 0, 0);
            layout.Controls.Add(extensionsPanel, 0, 1);

            return layout;
        }

        private Control BuildEditorPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

            var editorSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 650,
                BackColor = Color.FromArgb(37, 37, 38)
            };

            _editorTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(150, 25)
            };
            _editorTabs.DrawItem += EditorTabs_DrawItem;
            _editorTabs.MouseDown += EditorTabs_MouseDown;

            editorSplit.Panel1.Controls.Add(_editorTabs);

            var gitPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(37, 37, 38)
            };

            var gitLabel = new Label
            {
                Text = "GIT",
                ForeColor = Color.FromArgb(200, 200, 200),
                Dock = DockStyle.Top,
                Height = 20
            };

            _gitStatusView = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                View = View.Details,
                FullRowSelect = true
            };
            _gitStatusView.Columns.Add("Status", 80);
            _gitStatusView.Columns.Add("Path", 200);

            var gitRefreshButton = new Button
            {
                Text = "Refresh",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            gitRefreshButton.Click += (_, _) => RefreshGitStatus();

            gitPanel.Controls.Add(_gitStatusView);
            gitPanel.Controls.Add(gitRefreshButton);
            gitPanel.Controls.Add(gitLabel);

            editorSplit.Panel2.Controls.Add(gitPanel);

            layout.Controls.Add(editorSplit, 0, 0);

            _consoleTabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            _terminalConsole = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.LightGreen };
            _debugConsole = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.Gold };
            _outputConsole = new TextBox { Dock = DockStyle.Fill, Multiline = true, BackColor = Color.Black, ForeColor = Color.White, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            _apiLogList = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.DeepSkyBlue };

            _consoleTabs.TabPages.Add(new TabPage("Terminal") { Controls = { _terminalConsole } });
            _consoleTabs.TabPages.Add(new TabPage("Debug Console") { Controls = { _debugConsole } });
            _consoleTabs.TabPages.Add(new TabPage("Output") { Controls = { _outputConsole } });
            _consoleTabs.TabPages.Add(new TabPage("API Log") { Controls = { _apiLogList } });

            layout.Controls.Add(_consoleTabs, 0, 1);

            return layout;
        }

        private Control BuildSolidWorksWorkspace()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                BackColor = Color.FromArgb(37, 37, 38)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            layout.Controls.Add(BuildCommandManager(), 0, 0);
            layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 3);

            layout.Controls.Add(BuildFeatureManagerPanel(), 0, 1);
            layout.Controls.Add(BuildGraphicsPanel(), 1, 1);
            layout.Controls.Add(BuildPropertyTaskPane(), 2, 1);
            layout.Controls.Add(BuildStatusStripPanel(), 0, 2);
            layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 3);

            return layout;
        }

        private Control BuildCommandManager()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal
            };

            tabs.TabPages.Add(CreateCommandTab("Features", new[] { "Extrude", "Cut", "Fillet", "Shell" }));
            tabs.TabPages.Add(CreateCommandTab("Sketch", new[] { "Line", "Circle", "Dimension", "Offset" }));
            tabs.TabPages.Add(CreateCommandTab("Evaluate", new[] { "Measure", "Simulation", "Check" }));
            tabs.TabPages.Add(CreateCommandTab("SolidWorks API", new[] { "Run Macro", "Reload Add-in", "Record Steps" }));

            panel.Controls.Add(tabs);

            return panel;
        }

        private TabPage CreateCommandTab(string name, IEnumerable<string> commands)
        {
            var tab = new TabPage(name) { BackColor = Color.FromArgb(45, 45, 48) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            foreach (var command in commands)
            {
                var button = new Button
                {
                    Text = command,
                    Margin = new Padding(8),
                    Width = 110,
                    Height = 32
                };
                button.Click += (_, _) => _interactionSimulator.SimulateCommand($"{name}/{command}");
                flow.Controls.Add(button);
            }

            tab.Controls.Add(flow);
            return tab;
        }

        private Control BuildFeatureManagerPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(37, 37, 38)
            };

            var label = new Label
            {
                Text = "FeatureManager Design Tree",
                Dock = DockStyle.Top,
                ForeColor = Color.White
            };

            _documentList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            var btnNewPart = new Button { Text = "New Part" };
            btnNewPart.Click += (_, _) => CreateDocument(MockDocumentType.Part);
            var btnNewAssembly = new Button { Text = "New Assembly" };
            btnNewAssembly.Click += (_, _) => CreateDocument(MockDocumentType.Assembly);
            var btnNewDrawing = new Button { Text = "New Drawing" };
            btnNewDrawing.Click += (_, _) => CreateDocument(MockDocumentType.Drawing);
            var btnCloseDoc = new Button { Text = "Close" };
            btnCloseDoc.Click += (_, _) => CloseSelectedDocument();

            toolbar.Controls.AddRange(new Control[] { btnNewPart, btnNewAssembly, btnNewDrawing, btnCloseDoc });

            panel.Controls.Add(_documentList);
            panel.Controls.Add(toolbar);
            panel.Controls.Add(label);

            return panel;
        }

        private Control BuildGraphicsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(10)
            };

            _graphicsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                BorderStyle = BorderStyle.FixedSingle
            };
            _graphicsPanel.Paint += GraphicsPanel_Paint;

            var overlay = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40
            };

            _environmentStatus = new Label
            {
                Text = "Status: Idle",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            overlay.Controls.Add(_environmentStatus);

            panel.Controls.Add(_graphicsPanel);
            panel.Controls.Add(overlay);

            return panel;
        }

        private Control BuildPropertyTaskPane()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            var propertyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(37, 37, 38) };
            var propertyLabel = new Label { Text = "PropertyManager", Dock = DockStyle.Top, ForeColor = Color.White };
            _propertyManager = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                SelectedObject = new SolidWorksSimulationSettings(),
                HelpVisible = false,
                ToolbarVisible = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            propertyPanel.Controls.Add(_propertyManager);
            propertyPanel.Controls.Add(propertyLabel);

            var taskPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(37, 37, 38) };
            var taskLabel = new Label { Text = "Task Pane", Dock = DockStyle.Top, ForeColor = Color.White };
            _interactionList = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };

            var simulateToolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
            var btnSimulateButton = new Button { Text = "Simulate Button" };
            btnSimulateButton.Click += (_, _) => { _interactionSimulator.SimulateButtonClick("CommandButton"); RefreshInteractionLog(); };
            var btnSimulateSelect = new Button { Text = "Simulate Selection" };
            btnSimulateSelect.Click += (_, _) => { _interactionSimulator.SimulateSelection("Face<1>"); RefreshInteractionLog(); };

            simulateToolbar.Controls.Add(btnSimulateButton);
            simulateToolbar.Controls.Add(btnSimulateSelect);

            taskPanel.Controls.Add(_interactionList);
            taskPanel.Controls.Add(simulateToolbar);
            taskPanel.Controls.Add(taskLabel);

            layout.Controls.Add(propertyPanel, 0, 0);
            layout.Controls.Add(taskPanel, 0, 1);

            return layout;
        }

        private Control BuildStatusStripPanel()
        {
            _statusStrip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
            _coordinateLabel = new ToolStripStatusLabel("X:0 Y:0 Z:0");
            _statusLabel = new ToolStripStatusLabel("Ready");
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
            _statusStrip.Items.Add(_coordinateLabel);

            _coordinateTimer = new System.Windows.Forms.Timer { Interval = 800 };
            _coordinateTimer.Tick += (_, _) => UpdateCoordinateDisplay();
            _coordinateTimer.Start();

            return _statusStrip;
        }

        private void InitializeDataBindings()
        {
            _documentsBinding.DataSource = _solidWorksEnvironment.Documents;
            _documentList.DataSource = _documentsBinding;
            _documentList.DisplayMember = nameof(MockDocument.Name);

            _apiLogBinding.DataSource = _solidWorksEnvironment.Logger.Entries;
            _apiLogList.DataSource = _apiLogBinding;

            _fileEventBinding.DataSource = _workspaceManager.PendingEvents;
            _terminalConsole.DataSource = _fileEventBinding;

            _solidWorksEnvironment.ApiCalled += (_, e) =>
            {
                _debugConsole.Items.Insert(0, $"API: {e.Name} ({string.Join(", ", e.Parameters.Select(p => $"{p.Key}={p.Value}"))})");
                LimitListBox(_debugConsole, 200);
            };

            _breakpointManager.BreakpointHit += (_, hit) =>
            {
                _outputConsole.AppendText($"Breakpoint hit in {Path.GetFileName(hit.file)} at line {hit.line}{Environment.NewLine}");
            };

            _solidWorksEnvironment.ApiCalled += (_, _) => UpdateEnvironmentStatus();
        }

        private void PopulateCommandPalette()
        {
            var commands = new List<CommandDefinition>
            {
                new CommandDefinition("Create Sample Part Document", () => CreateDocument(MockDocumentType.Part)),
                new CommandDefinition("Create Sample Assembly", () => CreateDocument(MockDocumentType.Assembly)),
                new CommandDefinition("Create Sample Drawing", () => CreateDocument(MockDocumentType.Drawing)),
                new CommandDefinition("Refresh File Explorer", RefreshFileTree),
                new CommandDefinition("Refresh Git Status", RefreshGitStatus),
                new CommandDefinition("Execute Last Macro", async () => await RunMacroAsync()),
                new CommandDefinition("Execute Last Add-in", async () => await ExecuteAddInAsync()),
                new CommandDefinition("Execute Last VBA", async () => await RunVbaAsync()),
                new CommandDefinition("Clear Interaction Log", () => { _interactionSimulator.Clear(); RefreshInteractionLog(); })
            };

            _commandPalette.DataSource = commands;
            _commandPalette.DisplayMember = nameof(CommandDefinition.Name);
        }

        private void UpdateCoordinateDisplay()
        {
            var rnd = new Random();
            var x = rnd.NextDouble() * 100;
            var y = rnd.NextDouble() * 100;
            var z = rnd.NextDouble() * 100;
            _coordinateLabel.Text = $"X:{x:F2} Y:{y:F2} Z:{z:F2}";
        }

        private void UpdateEnvironmentStatus()
        {
            _environmentStatus.Text = $"Status: {_solidWorksEnvironment.Status}";
            _statusLabel.Text = _solidWorksEnvironment.Status;
        }

        private void RefreshInteractionLog()
        {
            _interactionList.Items.Clear();
            foreach (var item in _interactionSimulator.InteractionLog)
            {
                _interactionList.Items.Add(item);
            }
        }

        private void RefreshGitStatus()
        {
            _gitStatusView.Items.Clear();
            try
            {
                var repoRoot = FindGitRoot();
                if (repoRoot == null)
                {
                    _gitStatusView.Items.Add(new ListViewItem(new[] { "!", "Not a git repository" }));
                    return;
                }

                var info = new ProcessStartInfo("git", "status --short")
                {
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(info);
                if (process == null)
                {
                    _gitStatusView.Items.Add(new ListViewItem(new[] { "!", "Failed to start git" }));
                    return;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                if (string.IsNullOrWhiteSpace(output))
                {
                    _gitStatusView.Items.Add(new ListViewItem(new[] { "✔", "Clean" }));
                }
                else
                {
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var status = line.Length > 2 ? line.Substring(0, 2).Trim() : line;
                        var path = line.Length > 3 ? line.Substring(3) : string.Empty;
                        _gitStatusView.Items.Add(new ListViewItem(new[] { status, path }));
                    }
                }
            }
            catch (Exception ex)
            {
                _gitStatusView.Items.Add(new ListViewItem(new[] { "!", ex.Message }));
            }
        }

        private string? FindGitRoot()
        {
            var directory = new DirectoryInfo(_workspaceManager.WorkspaceRoot);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private void RefreshFileTree()
        {
            _fileTree.BeginUpdate();
            _fileTree.Nodes.Clear();

            var rootNode = new TreeNode(Path.GetFileName(_workspaceManager.WorkspaceRoot))
            {
                Tag = string.Empty,
                ForeColor = Color.White
            };
            _fileTree.Nodes.Add(rootNode);

            foreach (var entry in _workspaceManager.EnumerateFiles().OrderBy(e => e))
            {
                AddNode(rootNode, entry);
            }

            rootNode.Expand();
            _fileTree.EndUpdate();
        }

        private void AddNode(TreeNode root, string relativePath)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar);
            var currentNode = root;

            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var existing = currentNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => string.Equals(n.Text, part, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new TreeNode(part)
                    {
                        ForeColor = Color.White,
                        Tag = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(index + 1)).TrimEnd(Path.DirectorySeparatorChar)
                    };
                    currentNode.Nodes.Add(existing);
                }

                currentNode = existing;
            }
        }

        private async Task SaveActiveDocumentAsync()
        {
            if (_editorTabs.SelectedTab == null)
            {
                return;
            }

            var path = _tabToPath[_editorTabs.SelectedTab];
            if (_editorTabs.SelectedTab.Controls.OfType<RichTextBox>().FirstOrDefault() is { } editor)
            {
                await _workspaceManager.SaveFileAsync(path, editor.Text);
                _outputConsole.AppendText($"Saved {path}{Environment.NewLine}");
            }
        }

        private void CreateNewFile()
        {
            if (!PromptForRelativePath("Enter file name:", out var relativePath))
            {
                return;
            }

            _workspaceManager.CreateFile(relativePath, "// New file created by SolidWorks sandbox\n");
            RefreshFileTree();
        }

        private void CreateNewFolder()
        {
            if (!PromptForRelativePath("Enter folder name:", out var relativePath))
            {
                return;
            }

            _workspaceManager.CreateDirectory(relativePath);
            RefreshFileTree();
        }

        private void RenameSelectedNode()
        {
            if (_fileTree.SelectedNode == null)
            {
                return;
            }

            var current = _fileTree.SelectedNode.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(current))
            {
                return;
            }

            if (PromptForRelativePath("Enter new name:", out var newName))
            {
                var nameOnly = Path.GetFileName(newName);
                _workspaceManager.Rename(current, nameOnly);
                RefreshFileTree();
            }
        }

        private void DeleteSelectedNode()
        {
            if (_fileTree.SelectedNode == null)
            {
                return;
            }

            var path = _fileTree.SelectedNode.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (MessageBox.Show($"Delete {path}?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _workspaceManager.Delete(path);
                RefreshFileTree();
            }
        }

        private async Task RunMacroAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select SolidWorks Macro (.swp)",
                Filter = "SolidWorks Macro (*.swp)|*.swp|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            await ExecuteSolidWorksCommand(() => _solidWorksExecutor.RunMacroAsync(dialog.FileName, GetOrCreateExecutionToken().Token));
        }

        private async Task ExecuteAddInAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select SolidWorks Add-in (.dll)",
                Filter = "Add-in (*.dll)|*.dll|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            await ExecuteSolidWorksCommand(() => _solidWorksExecutor.ExecuteAddInAsync(dialog.FileName, GetOrCreateExecutionToken().Token));
        }

        private async Task RunVbaAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select VBA Script",
                Filter = "VBA Script (*.vba;*.bas)|*.vba;*.bas|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            await ExecuteSolidWorksCommand(() => _solidWorksExecutor.RunVbaAsync(dialog.FileName, GetOrCreateExecutionToken().Token));
        }

        private async Task ExecuteSolidWorksCommand(Func<Task<ApiExecutionResult>> action)
        {
            try
            {
                var result = await action();
                _outputConsole.AppendText($"{result.Command}: {(result.Success ? "Success" : result.Cancelled ? "Cancelled" : result.Message)}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                _outputConsole.AppendText($"Error: {ex.Message}{Environment.NewLine}");
            }
            finally
            {
                _executionCts?.Dispose();
                _executionCts = null;
                UpdateEnvironmentStatus();
            }
        }

        private CancellationTokenSource GetOrCreateExecutionToken()
        {
            _executionCts?.Cancel();
            _executionCts?.Dispose();
            _executionCts = new CancellationTokenSource();
            return _executionCts;
        }

        private void CancelExecution()
        {
            _executionCts?.Cancel();
        }

        private void ToggleBreakpoint()
        {
            if (_editorTabs.SelectedTab == null)
            {
                return;
            }

            var path = _tabToPath[_editorTabs.SelectedTab];
            if (_editorTabs.SelectedTab.Controls.OfType<RichTextBox>().FirstOrDefault() is { } editor)
            {
                var line = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
                _breakpointManager.ToggleBreakpoint(path, line);
                _outputConsole.AppendText($"Breakpoint toggled in {Path.GetFileName(path)} at line {line}{Environment.NewLine}");
            }
        }

        private void CreateDocument(MockDocumentType type)
        {
            var name = $"{type}_{DateTime.Now:HHmmss}";
            var document = _solidWorksEnvironment.CreateDocument(type, name);
            _documentsBinding.ResetBindings(false);
            _propertyManager.SelectedObject = new SolidWorksSimulationSettings { ActiveDocument = document.Name };
        }

        private void CloseSelectedDocument()
        {
            if (_documentList.SelectedItem is MockDocument document)
            {
                _solidWorksEnvironment.CloseDocument(document);
                _documentsBinding.ResetBindings(false);
            }
        }

        private void ExecuteSelectedCommand()
        {
            if (_commandPalette.SelectedItem is CommandDefinition command)
            {
                command.Execute();
            }
        }

        private bool PromptForRelativePath(string prompt, out string value)
        {
            using var dialog = new InputDialog(prompt);
            var result = dialog.ShowDialog(this);
            value = dialog.Value;
            return result == DialogResult.OK && !string.IsNullOrWhiteSpace(value);
        }

        private void OpenFileFromNode(TreeNode node)
        {
            if (node.Tag == null || node.Text.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return;
            }

            var relativePath = node.Tag.ToString() ?? string.Empty;
            if (relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return;
            }

            var absolute = _workspaceManager.ToAbsolutePath(relativePath);
            if (Directory.Exists(absolute))
            {
                return;
            }

            OpenFile(relativePath);
        }

        private void OpenFile(string relativePath)
        {
            if (_pathToTab.TryGetValue(relativePath, out var existingTab))
            {
                _editorTabs.SelectedTab = existingTab;
                return;
            }

            var content = _workspaceManager.ReadFile(relativePath);
            var tab = new TabPage(Path.GetFileName(relativePath))
            {
                BackColor = Color.FromArgb(30, 30, 30)
            };

            var editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                Text = content,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                WordWrap = false,
                BorderStyle = BorderStyle.None
            };

            editor.KeyDown += (sender, args) =>
            {
                if (args.Control && args.KeyCode == Keys.S)
                {
                    _ = SaveActiveDocumentAsync();
                    args.SuppressKeyPress = true;
                }
            };

            tab.Controls.Add(editor);
            _editorTabs.TabPages.Add(tab);
            _editorTabs.SelectedTab = tab;

            _tabToPath[tab] = relativePath;
            _pathToTab[relativePath] = tab;
        }

        private void GraphicsPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(30, 30, 30));

            using var gridPen = new Pen(Color.FromArgb(50, 50, 50));
            for (var x = 0; x < _graphicsPanel.Width; x += 20)
            {
                g.DrawLine(gridPen, x, 0, x, _graphicsPanel.Height);
            }

            for (var y = 0; y < _graphicsPanel.Height; y += 20)
            {
                g.DrawLine(gridPen, 0, y, _graphicsPanel.Width, y);
            }

            using var axisPen = new Pen(Color.DeepSkyBlue, 2);
            g.DrawLine(axisPen, 50, _graphicsPanel.Height - 60, _graphicsPanel.Width - 50, _graphicsPanel.Height - 60);
            g.DrawLine(axisPen, 60, _graphicsPanel.Height - 50, 60, 60);

            using var textBrush = new SolidBrush(Color.White);
            g.DrawString("Mock 3D Model Preview", new Font("Segoe UI", 16, FontStyle.Bold), textBrush, new PointF(80, _graphicsPanel.Height / 2 - 20));
        }

        private void EditorTabs_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = _editorTabs.TabPages[e.Index];
            var rect = e.Bounds;
            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using var background = new SolidBrush(isSelected ? Color.FromArgb(45, 45, 48) : Color.FromArgb(30, 30, 30));
            e.Graphics.FillRectangle(background, rect);
            TextRenderer.DrawText(e.Graphics, tab.Text, e.Font, rect, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            var closeRect = new Rectangle(rect.Right - 20, rect.Top + 5, 15, 15);
            TextRenderer.DrawText(e.Graphics, "x", e.Font, closeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void EditorTabs_MouseDown(object? sender, MouseEventArgs e)
        {
            for (var i = 0; i < _editorTabs.TabCount; i++)
            {
                var rect = _editorTabs.GetTabRect(i);
                var closeRect = new Rectangle(rect.Right - 20, rect.Top + 5, 15, 15);
                if (closeRect.Contains(e.Location))
                {
                    CloseTab(_editorTabs.TabPages[i]);
                    break;
                }
            }
        }

        private void CloseTab(TabPage tab)
        {
            if (_tabToPath.TryGetValue(tab, out var path))
            {
                _tabToPath.Remove(tab);
                _pathToTab.Remove(path);
            }

            _editorTabs.TabPages.Remove(tab);
        }

        private void LimitListBox(ListBox listBox, int maxItems)
        {
            while (listBox.Items.Count > maxItems)
            {
                listBox.Items.RemoveAt(listBox.Items.Count - 1);
            }
        }

        private class CommandDefinition
        {
            private readonly Func<Task> _action;

            public CommandDefinition(string name, Action action)
            {
                Name = name;
                _action = () =>
                {
                    action();
                    return Task.CompletedTask;
                };
            }

            public CommandDefinition(string name, Func<Task> action)
            {
                Name = name;
                _action = action;
            }

            public string Name { get; }

            public async void Execute()
            {
                try
                {
                    await _action();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Command Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private class InputDialog : Form
        {
            private readonly TextBox _input;

            public InputDialog(string prompt)
            {
                Text = prompt;
                Width = 400;
                Height = 150;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 30 };
                _input = new TextBox { Dock = DockStyle.Top };

                var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
                panel.Controls.Add(ok);
                panel.Controls.Add(cancel);

                Controls.Add(panel);
                Controls.Add(_input);
                Controls.Add(label);
            }

            public string Value => _input.Text;
        }
    }

    public class SolidWorksSimulationSettings
    {
        [Category("Simulation")]
        [DisplayName("Active Document")]
        public string? ActiveDocument { get; set; }

        [Category("Simulation")]
        [DisplayName("Graphics Quality")]
        public int GraphicsQuality { get; set; } = 85;

        [Category("Simulation")]
        [DisplayName("Show Mates")]
        public bool ShowMates { get; set; } = true;

        [Category("Simulation")]
        [DisplayName("Enable Collisions")]
        public bool EnableCollisions { get; set; } = true;

        [Category("Simulation")]
        [DisplayName("Playback Speed")]
        public double PlaybackSpeed { get; set; } = 1.0;
    }
}
