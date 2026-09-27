using System.ComponentModel;

namespace SteamIdleManager;

public sealed class MainForm : Form
{
    private readonly SteamService _steam = new();

    private readonly Label _connectionLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _connectButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _aboutButton = new();

    private readonly TextBox _searchBox = new();
    private readonly Button _addAppButton = new();
    private readonly Button _selectVisibleButton = new();
    private readonly Button _clearSelectionButton = new();

    private readonly ComboBox _profileCombo = new();
    private readonly Button _loadProfileButton = new();
    private readonly Button _saveProfileButton = new();
    private readonly Button _deleteProfileButton = new();
    private readonly CheckBox _favoritesOnlyCheck = new();
    private readonly CheckBox _recentOnlyCheck = new();
    private readonly CheckBox _minimizeToTrayCheck = new();

    private readonly DataGridView _grid = new();
    private readonly Label _libraryLabel = new();
    private readonly Label _selectedLabel = new();

    private readonly CheckBox _indefiniteCheck = new();
    private readonly NumericUpDown _durationMinutes = new();
    private readonly CheckBox _presenceCheck = new();
    private readonly ComboBox _publicGameCombo = new();

    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();

    private readonly Label _sessionLabel = new();
    private readonly Label _elapsedLabel = new();

    private readonly ListView _activeList = new();

    private readonly System.Windows.Forms.Timer _sessionTimer = new();

    private readonly List<GameEntry> _library = new();
    private readonly HashSet<uint> _selected = new();

    private DateTimeOffset? _idleStartedAt;
    private DateTimeOffset? _plannedEndAt;
    private QrDialog? _qrDialog;
    private bool _rebuildingGrid;
    private string? _currentAccountName;
    private bool _reconnecting;

    private AccountPreferences _preferences = new();
    private readonly HashSet<uint> _favorites = new();
    private readonly List<uint> _recent = new();

    private readonly NotifyIcon _trayIcon = new();
    private bool _trayTipShown;
    private bool _exitRequested;

    private AppSettings _appSettings = new();
    private bool _autoConnectStarted;
    private bool _autoStartAttempted;

    public MainForm()
    {
        Text = "Steam Idle Manager v1.0.2";
        Icon = LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 650);
        Size = new Size(1050, 760);

        AppLogger.Info("Main window initializing.");

        _appSettings = AppSettingsStore.Load();

        BuildUi();
        ConfigureTray();
        WireEvents();

        SetStatus("Connect Steam and choose QR, credentials, or a saved account.");

        _steam.GuardCodeProvider = PromptGuardCodeAsync;

        RefreshGrid();
        RefreshButtons();

        _sessionTimer.Interval = 1000;
        _sessionTimer.Tick += (_, _) => UpdateSessionTimer();
        _sessionTimer.Start();

        Shown += async (_, _) =>
        {
            await Task.Delay(150);

            if (_appSettings.StartMinimized)
            {
                BeginInvoke(new Action(HideToTray));
            }

            TryAutoConnect();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12),
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        Controls.Add(root);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
        };

        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        _connectionLabel.Text = "● Disconnected";
        _connectionLabel.Font = new Font(Font, FontStyle.Bold);
        _connectionLabel.Dock = DockStyle.Fill;
        _connectionLabel.TextAlign = ContentAlignment.MiddleLeft;

        _libraryLabel.Text = "Library: 0";
        _libraryLabel.Dock = DockStyle.Fill;
        _libraryLabel.TextAlign = ContentAlignment.MiddleLeft;

        _statusLabel.Text = "Ready.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;

        _aboutButton.Text = "About";
        _aboutButton.Dock = DockStyle.Fill;
        _aboutButton.Margin = new Padding(4, 9, 4, 9);

        _settingsButton.Text = "Settings";
        _settingsButton.Dock = DockStyle.Fill;
        _settingsButton.Margin = new Padding(4, 9, 4, 9);

        _connectButton.Text = "Connect Steam";
        _connectButton.Dock = DockStyle.Fill;
        _connectButton.Margin = new Padding(4, 9, 4, 9);

        top.Controls.Add(_connectionLabel, 0, 0);
        top.Controls.Add(_libraryLabel, 1, 0);
        top.Controls.Add(_statusLabel, 2, 0);
        top.Controls.Add(_aboutButton, 3, 0);
        top.Controls.Add(_settingsButton, 4, 0);
        top.Controls.Add(_connectButton, 5, 0);

        root.Controls.Add(top, 0, 0);

        var tools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
        };

        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        _searchBox.PlaceholderText = "Search by name or AppID...";
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.Margin = new Padding(0, 8, 8, 8);

        _addAppButton.Text = "+ Add AppID";
        _addAppButton.Dock = DockStyle.Fill;
        _addAppButton.Margin = new Padding(4, 7, 4, 7);

        _selectVisibleButton.Text = "Select visible";
        _selectVisibleButton.Dock = DockStyle.Fill;
        _selectVisibleButton.Margin = new Padding(4, 7, 4, 7);

        _clearSelectionButton.Text = "Clear";
        _clearSelectionButton.Dock = DockStyle.Fill;
        _clearSelectionButton.Margin = new Padding(4, 7, 4, 7);

        _selectedLabel.Text = "Selected: 0 / 32";
        _selectedLabel.Dock = DockStyle.Fill;
        _selectedLabel.TextAlign = ContentAlignment.MiddleRight;

        tools.Controls.Add(_searchBox, 0, 0);
        tools.Controls.Add(_addAppButton, 1, 0);
        tools.Controls.Add(_selectVisibleButton, 2, 0);
        tools.Controls.Add(_clearSelectionButton, 3, 0);
        tools.Controls.Add(_selectedLabel, 5, 0);

        root.Controls.Add(tools, 0, 1);

        var profileBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
        };

        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        profileBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        var profileLabel = new Label
        {
            Text = "Profile",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _profileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileCombo.Dock = DockStyle.Fill;
        _profileCombo.Margin = new Padding(0, 7, 6, 7);

        _loadProfileButton.Text = "Load";
        _loadProfileButton.Dock = DockStyle.Fill;
        _loadProfileButton.Margin = new Padding(3, 6, 3, 6);

        _saveProfileButton.Text = "Save";
        _saveProfileButton.Dock = DockStyle.Fill;
        _saveProfileButton.Margin = new Padding(3, 6, 3, 6);

        _deleteProfileButton.Text = "Delete";
        _deleteProfileButton.Dock = DockStyle.Fill;
        _deleteProfileButton.Margin = new Padding(3, 6, 3, 6);

        _favoritesOnlyCheck.Text = "★ Favorites";
        _favoritesOnlyCheck.Dock = DockStyle.Fill;

        _recentOnlyCheck.Text = "Recent";
        _recentOnlyCheck.Dock = DockStyle.Fill;

        _minimizeToTrayCheck.Text = "Minimize to tray";
        _minimizeToTrayCheck.Dock = DockStyle.Fill;

        profileBar.Controls.Add(profileLabel, 0, 0);
        profileBar.Controls.Add(_profileCombo, 1, 0);
        profileBar.Controls.Add(_loadProfileButton, 2, 0);
        profileBar.Controls.Add(_saveProfileButton, 3, 0);
        profileBar.Controls.Add(_deleteProfileButton, 4, 0);
        profileBar.Controls.Add(_favoritesOnlyCheck, 5, 0);
        profileBar.Controls.Add(_recentOnlyCheck, 6, 0);
        profileBar.Controls.Add(_minimizeToTrayCheck, 7, 0);

        root.Controls.Add(profileBar, 0, 2);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 3);

        var activeGroup = new GroupBox
        {
            Text = "Active session",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };

        _activeList.Dock = DockStyle.Fill;
        _activeList.View = View.Details;
        _activeList.FullRowSelect = true;
        _activeList.GridLines = true;
        _activeList.HideSelection = false;

        _activeList.Columns.Add("AppID", 105);
        _activeList.Columns.Add("Game", 360);
        _activeList.Columns.Add("State", 120);
        _activeList.Columns.Add("Elapsed", 105);

        activeGroup.Controls.Add(_activeList);
        root.Controls.Add(activeGroup, 0, 4);

        var config = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 0),
        };

        config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        for (var i = 0; i < 3; i++)
        {
            config.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        }

        var durationLabel = new Label
        {
            Text = "Duration (minutes)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _durationMinutes.Minimum = 1;
        _durationMinutes.Maximum = 100000;
        _durationMinutes.Value = 60;
        _durationMinutes.Dock = DockStyle.Fill;
        _durationMinutes.Margin = new Padding(4, 7, 20, 7);

        _indefiniteCheck.Text = "Indefinite";
        _indefiniteCheck.Checked = true;
        _indefiniteCheck.Dock = DockStyle.Fill;

        _presenceCheck.Text = "Show as playing publicly (Online)";
        _presenceCheck.Checked = false;
        _presenceCheck.Dock = DockStyle.Fill;

        var publicLabel = new Label
        {
            Text = "Public game",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _publicGameCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _publicGameCombo.Dock = DockStyle.Fill;
        _publicGameCombo.Enabled = false;
        _publicGameCombo.Margin = new Padding(4, 7, 20, 7);

        _startButton.Text = "▶ START IDLE";
        _startButton.Dock = DockStyle.Fill;
        _startButton.Font = new Font(Font, FontStyle.Bold);
        _startButton.Margin = new Padding(4, 5, 20, 5);

        _stopButton.Text = "■ STOP ALL";
        _stopButton.Dock = DockStyle.Fill;
        _stopButton.Margin = new Padding(4, 5, 0, 5);

        config.Controls.Add(durationLabel, 0, 0);
        config.Controls.Add(_durationMinutes, 1, 0);
        config.Controls.Add(_indefiniteCheck, 2, 0);
        config.Controls.Add(_presenceCheck, 3, 0);

        config.Controls.Add(publicLabel, 0, 1);
        config.Controls.Add(_publicGameCombo, 1, 1);
        config.Controls.Add(_startButton, 2, 1);
        config.Controls.Add(_stopButton, 3, 1);

        _sessionLabel.Text = "Session: idle";
        _sessionLabel.Dock = DockStyle.Fill;
        _sessionLabel.TextAlign = ContentAlignment.MiddleLeft;

        _elapsedLabel.Text = "Elapsed: 00:00:00";
        _elapsedLabel.Dock = DockStyle.Fill;
        _elapsedLabel.TextAlign = ContentAlignment.MiddleLeft;

        config.Controls.Add(_sessionLabel, 0, 2);
        config.SetColumnSpan(_sessionLabel, 2);
        config.Controls.Add(_elapsedLabel, 2, 2);
        config.SetColumnSpan(_elapsedLabel, 2);

        root.Controls.Add(config, 0, 5);

        var footer = new Label
        {
            Text = "Steam Idle Manager v1.0.2 — branded release build. Logs and application data are available from About.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        root.Controls.Add(footer, 0, 6);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;

        _grid.Columns.Add(
            new DataGridViewCheckBoxColumn
            {
                Name = "Favorite",
                HeaderText = "★",
                Width = 42,
            }
        );

        _grid.Columns.Add(
            new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "Idle",
                Width = 55,
            }
        );

        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "AppId",
                HeaderText = "AppID",
                DataPropertyName = "AppId",
                Width = 110,
                ReadOnly = true,
            }
        );

        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Game",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
            }
        );

        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "Source",
                HeaderText = "Source",
                DataPropertyName = "Source",
                Width = 140,
                ReadOnly = true,
            }
        );
    }

    private void WireEvents()
    {
        _aboutButton.Click += (_, _) =>
        {
            using var about = new AboutDialog();
            about.ShowDialog(this);
        };

        _settingsButton.Click += (_, _) => OpenSettings();

        _connectButton.Click += (_, _) =>
        {
            if (_steam.IsLoggedOn || _steam.IsConnected)
            {
                _steam.Disconnect();
                return;
            }

            using var login = new LoginDialog();

            if (
                login.ShowDialog(this) != DialogResult.OK
                || login.Request is null
            )
            {
                return;
            }

            _connectButton.Enabled = false;
            _steam.Connect(login.Request);
        };

        _searchBox.TextChanged += (_, _) => RefreshGrid();

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _grid.CellValueChanged += (_, e) =>
        {
            if (_rebuildingGrid || e.RowIndex < 0)
            {
                return;
            }

            if (_grid.Rows[e.RowIndex].Tag is not GameEntry game)
            {
                return;
            }

            if (e.ColumnIndex == _grid.Columns["Favorite"].Index)
            {
                var favorite =
                    _grid.Rows[e.RowIndex].Cells["Favorite"].Value as bool?
                    ?? false;

                if (favorite)
                {
                    _favorites.Add(game.AppId);
                }
                else
                {
                    _favorites.Remove(game.AppId);
                }

                SavePreferences();
                return;
            }

            if (e.ColumnIndex != _grid.Columns["Selected"].Index)
            {
                return;
            }

            var selected =
                _grid.Rows[e.RowIndex].Cells["Selected"].Value as bool?
                ?? false;

            if (selected)
            {
                if (_selected.Count >= SteamService.MaxIdleApps)
                {
                    _grid.Rows[e.RowIndex].Cells["Selected"].Value = false;

                    MessageBox.Show(
                        this,
                        $"You can select at most {SteamService.MaxIdleApps} games at once.",
                        "Selection limit",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    return;
                }

                _selected.Add(game.AppId);
            }
            else
            {
                _selected.Remove(game.AppId);
            }

            RefreshSelectionUi();
        };

        _addAppButton.Click += async (_, _) => await AddManualAppAsync();

        _selectVisibleButton.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (_selected.Count >= SteamService.MaxIdleApps)
                {
                    break;
                }

                if (row.Tag is GameEntry game)
                {
                    _selected.Add(game.AppId);
                }
            }

            RefreshGrid();
            RefreshSelectionUi();
        };

        _clearSelectionButton.Click += (_, _) =>
        {
            _selected.Clear();
            RefreshGrid();
            RefreshSelectionUi();
        };

        _favoritesOnlyCheck.CheckedChanged += (_, _) => RefreshGrid();
        _recentOnlyCheck.CheckedChanged += (_, _) => RefreshGrid();

        _minimizeToTrayCheck.CheckedChanged += (_, _) =>
        {
            _preferences.MinimizeToTray = _minimizeToTrayCheck.Checked;
            SavePreferences();
        };

        _loadProfileButton.Click += (_, _) => LoadSelectedProfile();
        _saveProfileButton.Click += (_, _) => SaveCurrentProfile();
        _deleteProfileButton.Click += (_, _) => DeleteSelectedProfile();

        _presenceCheck.CheckedChanged += (_, _) =>
        {
            _publicGameCombo.Enabled = _presenceCheck.Checked;
            RefreshPublicGameCombo();
        };

        _indefiniteCheck.CheckedChanged += (_, _) =>
        {
            _durationMinutes.Enabled = !_indefiniteCheck.Checked;
        };

        _startButton.Click += (_, _) => StartIdle();
        _stopButton.Click += (_, _) => StopIdle();

        _steam.StatusChanged += message =>
            Ui(() => SetStatus(message));

        _steam.ErrorOccurred += message =>
            Ui(() =>
            {
                AppLogger.Error(message);
                SetStatus(message);

                MessageBox.Show(
                    this,
                    message
                    + "\n\nA diagnostic entry was written to the application log.",
                    "Steam Idle Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                RefreshButtons();
            });

        _steam.QrReady += pngBytes =>
            Ui(() => ShowOrUpdateQr(pngBytes));

        _steam.QrApproved += () =>
            Ui(() =>
            {
                _qrDialog?.Close();
                _qrDialog = null;
            });

        _steam.AccountChanged += accountName =>
            Ui(() =>
            {
                _currentAccountName = accountName;
                _library.Clear();
                _selected.Clear();

                LoadPreferences(accountName);

                var cached = LibraryCache.Load(accountName);

                if (cached.Count > 0)
                {
                    MergeLibrary(cached);
                    SetStatus(
                        $"Loaded {cached.Count} cached games for {accountName}; refreshing from Steam..."
                    );
                }
                else
                {
                    _libraryLabel.Text = "Library: 0";
                    RefreshGrid();
                    RefreshSelectionUi();
                }
            });

        _steam.ReconnectStateChanged += (reconnecting, attempt) =>
            Ui(() =>
            {
                _reconnecting = reconnecting;

                if (reconnecting)
                {
                    _connectionLabel.Text = attempt > 0
                        ? $"● Reconnecting ({attempt})"
                        : "● Reconnecting";

                    _connectButton.Text = "Reconnecting...";
                }

                RefreshButtons();
            });

        _steam.LoginStateChanged += loggedIn =>
            Ui(() =>
            {
                if (loggedIn)
                {
                    _reconnecting = false;
                    _connectionLabel.Text = "● Connected";
                    _connectButton.Text = "Disconnect";
                }
                else if (!_reconnecting)
                {
                    _connectionLabel.Text = "● Disconnected";
                    _connectButton.Text = "Connect Steam";
                }

                RefreshButtons();

                if (loggedIn)
                {
                    BeginInvoke(new Action(TryAutoStartProfile));
                }
            });

        _steam.LibraryUpdated += games =>
            Ui(() =>
            {
                MergeLibrary(games);
                RefreshGrid();
                RefreshSelectionUi();
            });

        _steam.IdleStateChanged += active =>
            Ui(() =>
            {
                if (!active)
                {
                    _idleStartedAt = null;
                    _plannedEndAt = null;
                }

                RefreshButtons();
                UpdateSessionTimer();
                RefreshActiveSessionList();
            });

        Resize += (_, _) =>
        {
            if (
                WindowState == FormWindowState.Minimized
                && _minimizeToTrayCheck.Checked
            )
            {
                HideToTray();
            }
        };

        FormClosing += (_, e) =>
        {
            AppLogger.Info("Main window closing.");

            if (!_exitRequested)
            {
                _exitRequested = true;
            }

            _sessionTimer.Stop();
            _trayIcon.Visible = false;

            try
            {
                _steam.StopIdle();
                _steam.Dispose();
            }
            catch
            {
                // Best effort on app close.
            }

            _trayIcon.Dispose();
        };
    }

    private Task<string> PromptGuardCodeAsync(GuardCodePrompt prompt)
    {
        var tcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        Ui(() =>
        {
            using var dialog = new GuardCodeDialog(prompt);

            var result = dialog.ShowDialog(this);

            tcs.TrySetResult(
                result == DialogResult.OK
                    ? dialog.Code
                    : string.Empty
            );
        });

        return tcs.Task;
    }

    private void ShowOrUpdateQr(byte[] pngBytes)
    {
        if (_qrDialog is null || _qrDialog.IsDisposed)
        {
            _qrDialog = new QrDialog(pngBytes);
            _qrDialog.Show(this);
        }
        else
        {
            _qrDialog.UpdateQr(pngBytes);
            _qrDialog.Activate();
        }
    }

    private void MergeLibrary(IEnumerable<GameEntry> games)
    {
        var merged = _library.ToDictionary(g => g.AppId);

        foreach (var game in games)
        {
            if (game.AppId == 0)
            {
                continue;
            }

            if (merged.TryGetValue(game.AppId, out var existing))
            {
                if (!string.IsNullOrWhiteSpace(game.Name))
                {
                    existing.Name = game.Name;
                }

                if (!string.IsNullOrWhiteSpace(game.Type))
                {
                    existing.Type = game.Type;
                }

                if (!string.IsNullOrWhiteSpace(game.Source))
                {
                    existing.Source = game.Source;
                }
            }
            else
            {
                merged[game.AppId] = game;
            }
        }

        _library.Clear();
        _library.AddRange(
            merged.Values.OrderBy(
                g => g.Name,
                StringComparer.OrdinalIgnoreCase
            )
        );

        if (!string.IsNullOrWhiteSpace(_currentAccountName))
        {
            LibraryCache.Save(_currentAccountName!, _library);
        }
        _libraryLabel.Text = $"Library: {_library.Count}";
    }

    private IEnumerable<GameEntry> FilteredGames()
    {
        IEnumerable<GameEntry> games = _library;

        if (_favoritesOnlyCheck.Checked)
        {
            games = games.Where(g => _favorites.Contains(g.AppId));
        }

        if (_recentOnlyCheck.Checked)
        {
            games = games.Where(g => _recent.Contains(g.AppId));
        }

        var query = _searchBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            games = games.Where(
                g =>
                    g.Name.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || g.AppId.ToString().Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        if (_recentOnlyCheck.Checked)
        {
            var order = _recent
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            games = games.OrderBy(
                g => order.TryGetValue(g.AppId, out var index)
                    ? index
                    : int.MaxValue
            );
        }

        return games;
    }

    private void RefreshGrid()
    {
        _rebuildingGrid = true;

        try
        {
            _grid.Rows.Clear();

            foreach (var game in FilteredGames())
            {
                var index = _grid.Rows.Add(
                    _favorites.Contains(game.AppId),
                    _selected.Contains(game.AppId),
                    game.AppId,
                    game.Name,
                    game.Source
                );

                _grid.Rows[index].Tag = game;
            }
        }
        finally
        {
            _rebuildingGrid = false;
        }
    }

    private void RefreshSelectionUi()
    {
        _selectedLabel.Text =
            $"Selected: {_selected.Count} / {SteamService.MaxIdleApps}";

        RefreshPublicGameCombo();
        RefreshButtons();
    }

    private void RefreshPublicGameCombo()
    {
        var previous = (
            _publicGameCombo.SelectedItem as GameEntry
        )?.AppId;

        var selectedGames = _library
            .Where(g => _selected.Contains(g.AppId))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _publicGameCombo.BeginUpdate();
        _publicGameCombo.Items.Clear();

        foreach (var game in selectedGames)
        {
            _publicGameCombo.Items.Add(game);
        }

        if (_publicGameCombo.Items.Count > 0)
        {
            var matching = selectedGames.FindIndex(
                g => g.AppId == previous
            );

            _publicGameCombo.SelectedIndex = matching >= 0
                ? matching
                : 0;
        }

        _publicGameCombo.EndUpdate();
    }

    private async Task AddManualAppAsync()
    {
        using var dialog = new AppIdDialog();

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var appId = dialog.AppId!.Value;

        SetStatus($"Resolving AppID {appId}...");

        var game = await _steam.ResolveAppAsync(appId);

        if (game is null)
        {
            game = new GameEntry
            {
                AppId = appId,
                Name = $"App {appId}",
                Source = "Manual",
            };
        }

        MergeLibrary(new[] { game });
        _selected.Add(appId);

        _searchBox.Text = string.Empty;
        RefreshGrid();
        RefreshSelectionUi();

        SetStatus($"Added {game.Name} ({game.AppId}).");
    }

    private void StartIdle()
    {
        if (!_steam.IsLoggedOn)
        {
            MessageBox.Show(
                this,
                "Connect to Steam first.",
                "Not connected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            return;
        }

        if (_selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one game.",
                "Nothing selected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            return;
        }

        uint? preferred = null;

        if (_presenceCheck.Checked)
        {
            preferred = (
                _publicGameCombo.SelectedItem as GameEntry
            )?.AppId;

            if (!preferred.HasValue)
            {
                MessageBox.Show(
                    this,
                    "Choose which selected game should be the preferred public game.",
                    "Public game",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
            }
        }

        try
        {
            _steam.StartIdle(
                _selected.ToList(),
                _presenceCheck.Checked,
                preferred
            );

            _idleStartedAt = DateTimeOffset.Now;

            _plannedEndAt = _indefiniteCheck.Checked
                ? null
                : _idleStartedAt.Value.AddMinutes(
                    (double)_durationMinutes.Value
                );

            AddRecentGames(_selected);

            UpdateSessionTimer();
            RefreshButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Unable to start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private void StopIdle()
    {
        _steam.StopIdle();
        _idleStartedAt = null;
        _plannedEndAt = null;
        UpdateSessionTimer();
        RefreshButtons();
    }

    private void UpdateSessionTimer()
    {
        if (!_steam.IsIdling || !_idleStartedAt.HasValue)
        {
            _sessionLabel.Text = "Session: idle";
            _elapsedLabel.Text = "Elapsed: 00:00:00";
            RefreshActiveSessionList();
            return;
        }

        var now = DateTimeOffset.Now;
        var elapsed = now - _idleStartedAt.Value;

        _sessionLabel.Text = _steam.IsReconnecting
            ? $"Session: {_steam.ActiveAppIds.Count} game(s) | reconnecting..."
            : $"Session: {_steam.ActiveAppIds.Count} game(s) active";

        _elapsedLabel.Text = $"Elapsed: {FormatDuration(elapsed)}";

        if (_plannedEndAt.HasValue)
        {
            var remaining = _plannedEndAt.Value - now;

            if (remaining <= TimeSpan.Zero)
            {
                StopIdle();
                SetStatus("Duration reached. Idle stopped automatically.");
                return;
            }

            _elapsedLabel.Text +=
                $" | Remaining: {FormatDuration(remaining)}";
        }
        else
        {
            _elapsedLabel.Text += " | Indefinite";
        }

        RefreshActiveSessionList();
    }

    private void RefreshButtons()
    {
        _connectButton.Enabled = !_reconnecting;
        _settingsButton.Enabled = !_reconnecting;
        _aboutButton.Enabled = true;

        var idling = _steam.IsIdling;

        _startButton.Enabled =
            _steam.IsLoggedOn
            && _selected.Count > 0
            && !idling;

        _stopButton.Enabled = idling;

        _addAppButton.Enabled = !idling;
        _selectVisibleButton.Enabled = !idling;
        _clearSelectionButton.Enabled = !idling;
        _grid.Enabled = !idling;
        _presenceCheck.Enabled = !idling;
        _publicGameCombo.Enabled =
            !idling && _presenceCheck.Checked;
        _indefiniteCheck.Enabled = !idling;
        _durationMinutes.Enabled =
            !idling && !_indefiniteCheck.Checked;

        _profileCombo.Enabled = !idling;
        _loadProfileButton.Enabled =
            !idling && _profileCombo.SelectedItem is IdleProfile;
        _saveProfileButton.Enabled = !idling;
        _deleteProfileButton.Enabled =
            !idling && _profileCombo.SelectedItem is IdleProfile;
        _favoritesOnlyCheck.Enabled = !idling;
        _recentOnlyCheck.Enabled = !idling;
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsDialog(_appSettings);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var newSettings = dialog.ResultSettings;

        if (
            newSettings.StartWithWindows
            != WindowsStartupManager.IsEnabled()
        )
        {
            var ok = WindowsStartupManager.SetEnabled(
                newSettings.StartWithWindows
            );

            if (!ok)
            {
                MessageBox.Show(
                    this,
                    "Windows startup setting could not be changed.",
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                newSettings.StartWithWindows =
                    WindowsStartupManager.IsEnabled();
            }
        }

        _appSettings = newSettings;
        AppSettingsStore.Save(_appSettings);

        SetStatus("Settings saved.");
    }

    private void TryAutoConnect()
    {
        if (
            _autoConnectStarted
            || !_appSettings.AutoConnectEnabled
            || string.IsNullOrWhiteSpace(_appSettings.AutoConnectAccount)
            || _steam.IsConnected
            || _steam.IsLoggedOn
        )
        {
            return;
        }

        var exists = SecureSessionStore.ListAccounts().Any(
            a => string.Equals(
                a.AccountName,
                _appSettings.AutoConnectAccount,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (!exists)
        {
            SetStatus(
                $"Auto-connect skipped: saved account '{_appSettings.AutoConnectAccount}' was not found."
            );
            return;
        }

        _autoConnectStarted = true;

        SetStatus(
            $"Auto-connecting saved account {_appSettings.AutoConnectAccount}..."
        );

        _steam.Connect(
            new SteamLoginRequest
            {
                Method = SteamLoginMethod.SavedSession,
                Username = _appSettings.AutoConnectAccount,
                RememberSession = true,
            }
        );
    }

    private void TryAutoStartProfile()
    {
        if (
            _autoStartAttempted
            || !_appSettings.AutoConnectEnabled
            || !_appSettings.AutoStartProfileEnabled
            || string.IsNullOrWhiteSpace(_currentAccountName)
            || !string.Equals(
                _currentAccountName,
                _appSettings.AutoConnectAccount,
                StringComparison.OrdinalIgnoreCase
            )
            || string.IsNullOrWhiteSpace(
                _appSettings.AutoStartProfileName
            )
        )
        {
            return;
        }

        _autoStartAttempted = true;

        var profile = _preferences.Profiles.FirstOrDefault(
            p => string.Equals(
                p.Name,
                _appSettings.AutoStartProfileName,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (profile is null)
        {
            SetStatus(
                $"Auto-start skipped: profile '{_appSettings.AutoStartProfileName}' was not found."
            );
            return;
        }

        ApplyProfile(profile);

        BeginInvoke(
            new Action(
                async () =>
                {
                    await Task.Delay(400);

                    if (!_steam.IsLoggedOn || _steam.IsIdling)
                    {
                        return;
                    }

                    SetStatus(
                        $"Auto-starting profile '{profile.Name}'..."
                    );

                    StartIdle();
                }
            )
        );
    }

    private void ApplyProfile(IdleProfile profile)
    {
        _selected.Clear();

        foreach (
            var appId in profile.AppIds
                .Distinct()
                .Take(SteamService.MaxIdleApps)
        )
        {
            _selected.Add(appId);
        }

        _indefiniteCheck.Checked = profile.Indefinite;

        _durationMinutes.Value = Math.Clamp(
            profile.DurationMinutes,
            (int)_durationMinutes.Minimum,
            (int)_durationMinutes.Maximum
        );

        _presenceCheck.Checked = profile.PublicPresence;

        RefreshGrid();
        RefreshSelectionUi();

        if (
            profile.PublicPresence
            && profile.PreferredPublicAppId.HasValue
        )
        {
            for (var i = 0; i < _publicGameCombo.Items.Count; i++)
            {
                if (
                    _publicGameCombo.Items[i] is GameEntry game
                    && game.AppId
                        == profile.PreferredPublicAppId.Value
                )
                {
                    _publicGameCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void RefreshActiveSessionList()
    {
        _activeList.BeginUpdate();

        try
        {
            _activeList.Items.Clear();

            if (!_steam.IsIdling)
            {
                return;
            }

            var elapsed = _idleStartedAt.HasValue
                ? DateTimeOffset.Now - _idleStartedAt.Value
                : TimeSpan.Zero;

            var elapsedText = FormatDuration(elapsed);

            var state = _steam.IsReconnecting
                ? "Reconnecting"
                : "Idling";

            foreach (var appId in _steam.ActiveAppIds)
            {
                var game = _library.FirstOrDefault(
                    g => g.AppId == appId
                );

                var name = game?.Name ?? $"App {appId}";

                var item = new ListViewItem(appId.ToString());

                item.SubItems.Add(name);
                item.SubItems.Add(state);
                item.SubItems.Add(elapsedText);

                _activeList.Items.Add(item);
            }
        }
        finally
        {
            _activeList.EndUpdate();
        }
    }

    private void LoadPreferences(string accountName)
    {
        _preferences = PreferencesStore.Load(accountName);

        _favorites.Clear();

        foreach (var appId in _preferences.FavoriteAppIds)
        {
            _favorites.Add(appId);
        }

        _recent.Clear();
        _recent.AddRange(_preferences.RecentAppIds);

        _minimizeToTrayCheck.Checked = _preferences.MinimizeToTray;

        RefreshProfiles();
        RefreshGrid();
        RefreshSelectionUi();
    }

    private void SavePreferences()
    {
        if (string.IsNullOrWhiteSpace(_currentAccountName))
        {
            return;
        }

        _preferences.FavoriteAppIds = _favorites
            .OrderBy(id => id)
            .ToList();

        _preferences.RecentAppIds = _recent
            .Distinct()
            .Take(30)
            .ToList();

        _preferences.MinimizeToTray =
            _minimizeToTrayCheck.Checked;

        PreferencesStore.Save(
            _currentAccountName!,
            _preferences
        );
    }

    private void RefreshProfiles(string? selectName = null)
    {
        var selectedName =
            selectName
            ?? (_profileCombo.SelectedItem as IdleProfile)?.Name;

        _profileCombo.BeginUpdate();
        _profileCombo.Items.Clear();

        foreach (
            var profile in _preferences.Profiles
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        )
        {
            _profileCombo.Items.Add(profile);
        }

        _profileCombo.DisplayMember = nameof(IdleProfile.Name);

        if (_profileCombo.Items.Count > 0)
        {
            var index = -1;

            for (var i = 0; i < _profileCombo.Items.Count; i++)
            {
                if (
                    _profileCombo.Items[i] is IdleProfile profile
                    && string.Equals(
                        profile.Name,
                        selectedName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    index = i;
                    break;
                }
            }

            _profileCombo.SelectedIndex = index >= 0 ? index : 0;
        }

        _profileCombo.EndUpdate();
        RefreshButtons();
    }

    private void SaveCurrentProfile()
    {
        if (_selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one game before saving a profile.",
                "Profile",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var currentName =
            (_profileCombo.SelectedItem as IdleProfile)?.Name
            ?? string.Empty;

        using var dialog = new ProfileNameDialog(currentName);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var name = dialog.ProfileName;

        uint? preferred = null;

        if (_presenceCheck.Checked)
        {
            preferred =
                (_publicGameCombo.SelectedItem as GameEntry)?.AppId;
        }

        var profile = new IdleProfile
        {
            Name = name,
            AppIds = _selected
                .Take(SteamService.MaxIdleApps)
                .ToList(),
            Indefinite = _indefiniteCheck.Checked,
            DurationMinutes = (int)_durationMinutes.Value,
            PublicPresence = _presenceCheck.Checked,
            PreferredPublicAppId = preferred,
        };

        _preferences.Profiles.RemoveAll(
            p => string.Equals(
                p.Name,
                name,
                StringComparison.OrdinalIgnoreCase
            )
        );

        _preferences.Profiles.Add(profile);

        SavePreferences();
        RefreshProfiles(name);

        SetStatus($"Profile '{name}' saved.");
    }

    private void LoadSelectedProfile()
    {
        if (_profileCombo.SelectedItem is not IdleProfile profile)
        {
            return;
        }

        ApplyProfile(profile);

        SetStatus($"Profile '{profile.Name}' loaded.");
    }

    private void DeleteSelectedProfile()
    {
        if (_profileCombo.SelectedItem is not IdleProfile profile)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Delete profile '{profile.Name}'?",
            "Delete profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _preferences.Profiles.RemoveAll(
            p => string.Equals(
                p.Name,
                profile.Name,
                StringComparison.OrdinalIgnoreCase
            )
        );

        SavePreferences();
        RefreshProfiles();

        SetStatus($"Profile '{profile.Name}' deleted.");
    }

    private void AddRecentGames(IEnumerable<uint> appIds)
    {
        foreach (var appId in appIds.Reverse())
        {
            _recent.Remove(appId);
            _recent.Insert(0, appId);
        }

        if (_recent.Count > 30)
        {
            _recent.RemoveRange(30, _recent.Count - 30);
        }

        SavePreferences();
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(
                Application.ExecutablePath
            ) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void ConfigureTray()
    {
        _trayIcon.Icon = Icon ?? LoadApplicationIcon();
        _trayIcon.Text = "Steam Idle Manager";
        _trayIcon.Visible = true;

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open");
        var stopItem = new ToolStripMenuItem("STOP ALL");
        var exitItem = new ToolStripMenuItem("Exit");

        openItem.Click += (_, _) => RestoreFromTray();

        stopItem.Click += (_, _) =>
        {
            if (_steam.IsIdling)
            {
                StopIdle();
            }
        };

        exitItem.Click += (_, _) =>
        {
            _exitRequested = true;
            RestoreFromTray();
            Close();
        };

        menu.Items.Add(openItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        menu.Opening += (_, _) =>
        {
            stopItem.Enabled = _steam.IsIdling;
        };

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void HideToTray()
    {
        Hide();

        if (!_trayTipShown)
        {
            _trayTipShown = true;

            _trayIcon.BalloonTipTitle = "Steam Idle Manager";
            _trayIcon.BalloonTipText =
                _steam.IsIdling
                    ? $"{_steam.ActiveAppIds.Count} game(s) continue idling in the background."
                    : "Steam Idle Manager is still running.";

            _trayIcon.ShowBalloonTip(2500);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
        AppLogger.Info(message);
    }

    private void Ui(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch
            {
                // Window is closing.
            }

            return;
        }

        action();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
