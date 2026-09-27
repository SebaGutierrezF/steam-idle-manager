namespace SteamIdleManager;

public sealed class AppIdDialog : Form
{
    private readonly TextBox _input = new();

    public uint? AppId { get; private set; }

    public AppIdDialog()
    {
        Text = "Add AppID";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 135);

        var label = new Label
        {
            Text = "Steam AppID:",
            AutoSize = true,
            Location = new Point(18, 20),
        };

        _input.Location = new Point(18, 45);
        _input.Width = 320;

        var ok = new Button
        {
            Text = "Add",
            DialogResult = DialogResult.None,
            Location = new Point(182, 88),
            Width = 75,
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(263, 88),
            Width = 75,
        };

        ok.Click += (_, _) =>
        {
            if (
                uint.TryParse(_input.Text.Trim(), out var appId)
                && appId > 0
            )
            {
                AppId = appId;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            MessageBox.Show(
                this,
                "Enter a valid positive numeric AppID.",
                "Invalid AppID",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        };

        Controls.AddRange(new Control[] { label, _input, ok, cancel });

        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public sealed class QrDialog : Form
{
    private readonly PictureBox _qrPicture = new();
    private readonly Label _infoLabel = new();

    public QrDialog(byte[] pngBytes)
    {
        Text = "Steam QR Login";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 630);
        BackColor = Color.White;

        _infoLabel.Text =
            "Scan this QR with the Steam mobile app and approve the sign-in.";
        _infoLabel.Dock = DockStyle.Top;
        _infoLabel.Height = 64;
        _infoLabel.TextAlign = ContentAlignment.MiddleCenter;
        _infoLabel.Font = new Font(Font.FontFamily, 10f, FontStyle.Regular);

        _qrPicture.Dock = DockStyle.Fill;
        _qrPicture.SizeMode = PictureBoxSizeMode.CenterImage;
        _qrPicture.BackColor = Color.White;
        _qrPicture.Margin = new Padding(20);

        Controls.Add(_qrPicture);
        Controls.Add(_infoLabel);

        UpdateQr(pngBytes);
    }

    public void UpdateQr(byte[] pngBytes)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateQr(pngBytes)));
            return;
        }

        Image? newImage = null;

        try
        {
            using var stream = new MemoryStream(pngBytes);
            using var decoded = Image.FromStream(stream);

            // Clone it because Image.FromStream keeps a dependency on the stream.
            newImage = new Bitmap(decoded);
        }
        catch
        {
            newImage?.Dispose();
            return;
        }

        var oldImage = _qrPicture.Image;
        _qrPicture.Image = newImage;
        oldImage?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var image = _qrPicture.Image;
            _qrPicture.Image = null;
            image?.Dispose();
        }

        base.Dispose(disposing);
    }
}


public sealed class LoginDialog : Form
{
    private readonly TabControl _tabs = new();

    private readonly CheckBox _qrRemember = new();

    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly CheckBox _credentialsRemember = new();
    private readonly CheckBox _preferGuardCode = new();

    private readonly ComboBox _savedAccounts = new();
    private readonly Button _forgetSaved = new();

    private readonly Button _connect = new();
    private readonly Button _cancel = new();

    public SteamLoginRequest? Request { get; private set; }

    public LoginDialog()
    {
        Text = "Connect Steam";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 390);

        _tabs.Dock = DockStyle.Top;
        _tabs.Height = 300;

        BuildQrTab();
        BuildCredentialsTab();
        BuildSavedTab();

        _connect.Text = "Connect";
        _connect.Width = 95;
        _connect.Location = new Point(310, 325);

        _cancel.Text = "Cancel";
        _cancel.Width = 95;
        _cancel.Location = new Point(410, 325);
        _cancel.DialogResult = DialogResult.Cancel;

        _connect.Click += (_, _) => Submit();
        _tabs.SelectedIndexChanged += (_, _) => RefreshConnectButton();
        _savedAccounts.SelectedIndexChanged += (_, _) => RefreshConnectButton();

        Controls.Add(_tabs);
        Controls.Add(_connect);
        Controls.Add(_cancel);

        AcceptButton = _connect;
        CancelButton = _cancel;

        RefreshSavedAccounts();
        RefreshConnectButton();
    }

    private void BuildQrTab()
    {
        var tab = new TabPage("QR");

        var title = new Label
        {
            Text = "Sign in with the Steam mobile app",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(22, 25),
        };

        var info = new Label
        {
            Text =
                "The app will show a QR code. Scan it with Steam and approve the sign-in.",
            AutoSize = false,
            Location = new Point(22, 58),
            Size = new Size(450, 55),
        };

        _qrRemember.Text = "Remember this account on this Windows user";
        _qrRemember.Checked = true;
        _qrRemember.AutoSize = true;
        _qrRemember.Location = new Point(22, 125);

        tab.Controls.AddRange(new Control[] { title, info, _qrRemember });
        _tabs.TabPages.Add(tab);
    }

    private void BuildCredentialsTab()
    {
        var tab = new TabPage("Username / Password");

        var userLabel = new Label
        {
            Text = "Steam account name",
            AutoSize = true,
            Location = new Point(22, 22),
        };

        _username.Location = new Point(22, 45);
        _username.Width = 445;

        var passLabel = new Label
        {
            Text = "Password",
            AutoSize = true,
            Location = new Point(22, 82),
        };

        _password.Location = new Point(22, 105);
        _password.Width = 445;
        _password.UseSystemPasswordChar = true;

        _preferGuardCode.Text =
            "Prefer Steam Guard code entry (SDA / authenticator) instead of mobile approval";
        _preferGuardCode.Checked = true;
        _preferGuardCode.AutoSize = true;
        _preferGuardCode.Location = new Point(22, 148);

        _credentialsRemember.Text =
            "Remember session securely (password is never stored)";
        _credentialsRemember.Checked = true;
        _credentialsRemember.AutoSize = true;
        _credentialsRemember.Location = new Point(22, 180);

        var hint = new Label
        {
            Text =
                "Steam Guard may ask for a code from Steam Desktop Authenticator, another authenticator app, or email.",
            AutoSize = false,
            Location = new Point(22, 212),
            Size = new Size(450, 45),
        };

        tab.Controls.AddRange(
            new Control[]
            {
                userLabel,
                _username,
                passLabel,
                _password,
                _preferGuardCode,
                _credentialsRemember,
                hint,
            }
        );

        _tabs.TabPages.Add(tab);
    }

    private void BuildSavedTab()
    {
        var tab = new TabPage("Saved account");

        var title = new Label
        {
            Text = "Saved sessions",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(22, 25),
        };

        var info = new Label
        {
            Text =
                "Refresh tokens are encrypted with Windows DPAPI for the current Windows user. Passwords are never saved.",
            AutoSize = false,
            Location = new Point(22, 55),
            Size = new Size(450, 55),
        };

        _savedAccounts.DropDownStyle = ComboBoxStyle.DropDownList;
        _savedAccounts.Location = new Point(22, 120);
        _savedAccounts.Width = 330;

        _forgetSaved.Text = "Forget";
        _forgetSaved.Location = new Point(360, 118);
        _forgetSaved.Width = 105;
        _forgetSaved.Click += (_, _) =>
        {
            if (_savedAccounts.SelectedItem is not string account)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"Forget the saved session for '{account}'?",
                "Forget saved account",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (answer != DialogResult.Yes)
            {
                return;
            }

            SecureSessionStore.DeleteSession(account);
            RefreshSavedAccounts();
        };

        var note = new Label
        {
            Text =
                "A saved session avoids QR/password prompts until Steam expires or revokes that refresh token.",
            AutoSize = false,
            Location = new Point(22, 165),
            Size = new Size(450, 55),
        };

        tab.Controls.AddRange(
            new Control[]
            {
                title,
                info,
                _savedAccounts,
                _forgetSaved,
                note,
            }
        );

        _tabs.TabPages.Add(tab);
    }

    private void RefreshSavedAccounts()
    {
        var previous = _savedAccounts.SelectedItem as string;

        _savedAccounts.Items.Clear();

        foreach (var account in SecureSessionStore.ListAccounts())
        {
            _savedAccounts.Items.Add(account.AccountName);
        }

        if (_savedAccounts.Items.Count > 0)
        {
            var index = previous is null
                ? 0
                : _savedAccounts.Items.IndexOf(previous);

            _savedAccounts.SelectedIndex = index >= 0 ? index : 0;
        }

        _forgetSaved.Enabled = _savedAccounts.Items.Count > 0;
        RefreshConnectButton();
    }

    private void RefreshConnectButton()
    {
        _connect.Enabled = _tabs.SelectedIndex switch
        {
            0 => true,
            1 =>
                !string.IsNullOrWhiteSpace(_username.Text)
                && !string.IsNullOrWhiteSpace(_password.Text),
            2 => _savedAccounts.SelectedItem is string,
            _ => false,
        };
    }

    private void Submit()
    {
        if (_tabs.SelectedIndex == 0)
        {
            Request = new SteamLoginRequest
            {
                Method = SteamLoginMethod.Qr,
                RememberSession = _qrRemember.Checked,
            };
        }
        else if (_tabs.SelectedIndex == 1)
        {
            if (
                string.IsNullOrWhiteSpace(_username.Text)
                || string.IsNullOrWhiteSpace(_password.Text)
            )
            {
                MessageBox.Show(
                    this,
                    "Enter your Steam account name and password.",
                    "Credentials required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            Request = new SteamLoginRequest
            {
                Method = SteamLoginMethod.Credentials,
                Username = _username.Text.Trim(),
                Password = _password.Text,
                RememberSession = _credentialsRemember.Checked,
                PreferGuardCode = _preferGuardCode.Checked,
            };

            // The request object will only exist in memory. The password is never
            // written to disk by Steam Idle Manager.
        }
        else
        {
            if (_savedAccounts.SelectedItem is not string account)
            {
                return;
            }

            Request = new SteamLoginRequest
            {
                Method = SteamLoginMethod.SavedSession,
                Username = account,
                RememberSession = true,
            };
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}

public sealed class GuardCodeDialog : Form
{
    private readonly TextBox _code = new();

    public string Code => _code.Text.Trim();

    public GuardCodeDialog(GuardCodePrompt prompt)
    {
        Text = prompt.IsEmailCode
            ? "Steam Guard Email Code"
            : "Steam Guard Authenticator Code";

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(430, 190);

        var headline = new Label
        {
            Text = prompt.IsEmailCode
                ? $"Enter the Steam Guard code sent to:\n{prompt.Email}"
                : "Enter the current Steam Guard code from your authenticator or Steam Desktop Authenticator.",
            AutoSize = false,
            Location = new Point(18, 18),
            Size = new Size(390, 60),
        };

        if (prompt.PreviousCodeWasIncorrect)
        {
            headline.Text += "\nThe previous code was rejected. Enter a new code.";
        }

        _code.Location = new Point(18, 88);
        _code.Width = 390;
        _code.CharacterCasing = CharacterCasing.Upper;

        var ok = new Button
        {
            Text = "Continue",
            Location = new Point(232, 132),
            Width = 85,
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Location = new Point(323, 132),
            Width = 85,
            DialogResult = DialogResult.Cancel,
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Code))
            {
                MessageBox.Show(
                    this,
                    "Enter the Steam Guard code.",
                    "Code required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[] { headline, _code, ok, cancel });

        AcceptButton = ok;
        CancelButton = cancel;
    }
}


public sealed class ProfileNameDialog : Form
{
    private readonly TextBox _nameBox = new();

    public string ProfileName => _nameBox.Text.Trim();

    public ProfileNameDialog(string initialName = "")
    {
        Text = "Save Idle Profile";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(390, 150);

        var label = new Label
        {
            Text = "Profile name:",
            AutoSize = true,
            Location = new Point(18, 20),
        };

        _nameBox.Location = new Point(18, 46);
        _nameBox.Width = 350;
        _nameBox.Text = initialName;

        var ok = new Button
        {
            Text = "Save",
            Location = new Point(202, 96),
            Width = 80,
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Location = new Point(288, 96),
            Width = 80,
            DialogResult = DialogResult.Cancel,
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                MessageBox.Show(
                    this,
                    "Enter a profile name.",
                    "Profile name",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[] { label, _nameBox, ok, cancel });

        AcceptButton = ok;
        CancelButton = cancel;
    }
}


public sealed class SettingsDialog : Form
{
    private readonly CheckBox _autoConnect = new();
    private readonly ComboBox _accountCombo = new();

    private readonly CheckBox _autoStartProfile = new();
    private readonly ComboBox _profileCombo = new();

    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _startMinimized = new();

    public AppSettings ResultSettings { get; private set; }

    public SettingsDialog(AppSettings current)
    {
        Text = "Steam Idle Manager Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 330);

        ResultSettings = new AppSettings
        {
            AutoConnectEnabled = current.AutoConnectEnabled,
            AutoConnectAccount = current.AutoConnectAccount,
            AutoStartProfileEnabled = current.AutoStartProfileEnabled,
            AutoStartProfileName = current.AutoStartProfileName,
            StartWithWindows = current.StartWithWindows,
            StartMinimized = current.StartMinimized,
        };

        var y = 22;

        _autoConnect.Text = "Auto-connect saved Steam account on launch";
        _autoConnect.AutoSize = true;
        _autoConnect.Location = new Point(22, y);
        _autoConnect.Checked = current.AutoConnectEnabled;

        y += 32;

        var accountLabel = new Label
        {
            Text = "Saved account:",
            AutoSize = true,
            Location = new Point(42, y + 4),
        };

        _accountCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _accountCombo.Location = new Point(145, y);
        _accountCombo.Width = 330;

        foreach (var account in SecureSessionStore.ListAccounts())
        {
            _accountCombo.Items.Add(account.AccountName);
        }

        if (_accountCombo.Items.Count > 0)
        {
            var index = _accountCombo.Items.IndexOf(current.AutoConnectAccount);
            _accountCombo.SelectedIndex = index >= 0 ? index : 0;
        }

        y += 42;

        _autoStartProfile.Text = "Auto-start an idle profile after auto-connect";
        _autoStartProfile.AutoSize = true;
        _autoStartProfile.Location = new Point(22, y);
        _autoStartProfile.Checked = current.AutoStartProfileEnabled;

        y += 32;

        var profileLabel = new Label
        {
            Text = "Profile:",
            AutoSize = true,
            Location = new Point(42, y + 4),
        };

        _profileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileCombo.Location = new Point(145, y);
        _profileCombo.Width = 330;

        y += 48;

        _startWithWindows.Text = "Start Steam Idle Manager with Windows";
        _startWithWindows.AutoSize = true;
        _startWithWindows.Location = new Point(22, y);
        _startWithWindows.Checked = current.StartWithWindows;

        y += 32;

        _startMinimized.Text = "Start minimized to system tray";
        _startMinimized.AutoSize = true;
        _startMinimized.Location = new Point(22, y);
        _startMinimized.Checked = current.StartMinimized;

        var save = new Button
        {
            Text = "Save",
            Width = 85,
            Location = new Point(320, 275),
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Width = 85,
            Location = new Point(410, 275),
            DialogResult = DialogResult.Cancel,
        };

        _autoConnect.CheckedChanged += (_, _) => RefreshEnabledState();
        _autoStartProfile.CheckedChanged += (_, _) => RefreshEnabledState();
        _accountCombo.SelectedIndexChanged += (_, _) =>
        {
            RefreshProfiles(current.AutoStartProfileName);
        };

        save.Click += (_, _) =>
        {
            var account = _accountCombo.SelectedItem as string ?? string.Empty;
            var profile = _profileCombo.SelectedItem as string ?? string.Empty;

            if (_autoConnect.Checked && string.IsNullOrWhiteSpace(account))
            {
                MessageBox.Show(
                    this,
                    "Choose a saved account for auto-connect.",
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            if (
                _autoStartProfile.Checked
                && (
                    !_autoConnect.Checked
                    || string.IsNullOrWhiteSpace(profile)
                )
            )
            {
                MessageBox.Show(
                    this,
                    "Auto-start profile requires auto-connect and a valid profile.",
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            ResultSettings = new AppSettings
            {
                AutoConnectEnabled = _autoConnect.Checked,
                AutoConnectAccount = account,
                AutoStartProfileEnabled = _autoStartProfile.Checked,
                AutoStartProfileName = profile,
                StartWithWindows = _startWithWindows.Checked,
                StartMinimized = _startMinimized.Checked,
            };

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(
            new Control[]
            {
                _autoConnect,
                accountLabel,
                _accountCombo,
                _autoStartProfile,
                profileLabel,
                _profileCombo,
                _startWithWindows,
                _startMinimized,
                save,
                cancel,
            }
        );

        AcceptButton = save;
        CancelButton = cancel;

        RefreshProfiles(current.AutoStartProfileName);
        RefreshEnabledState();
    }

    private void RefreshProfiles(string preferred = "")
    {
        var account = _accountCombo.SelectedItem as string ?? string.Empty;

        _profileCombo.Items.Clear();

        if (!string.IsNullOrWhiteSpace(account))
        {
            var prefs = PreferencesStore.Load(account);

            foreach (
                var profile in prefs.Profiles.OrderBy(
                    p => p.Name,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                _profileCombo.Items.Add(profile.Name);
            }
        }

        if (_profileCombo.Items.Count > 0)
        {
            var index = _profileCombo.Items.IndexOf(preferred);
            _profileCombo.SelectedIndex = index >= 0 ? index : 0;
        }
    }

    private void RefreshEnabledState()
    {
        _accountCombo.Enabled = _autoConnect.Checked;

        _autoStartProfile.Enabled = _autoConnect.Checked;

        _profileCombo.Enabled =
            _autoConnect.Checked
            && _autoStartProfile.Checked;
    }
}


public sealed class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = "About Steam Idle Manager";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 410);

        var title = new Label
        {
            Text = "Steam Idle Manager",
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 20),
        };

        var version = new Label
        {
            Text = "Version 1.0.2",
            AutoSize = true,
            Location = new Point(24, 58),
        };

        var description = new Label
        {
            Text =
                "Manage Steam idle sessions for selected AppIDs with multi-game "
                + "idling, private/public presence, saved accounts, profiles, "
                + "automatic reconnect and system-tray operation.",
            AutoSize = false,
            Location = new Point(24, 88),
            Size = new Size(510, 62),
        };

        var privacy = new Label
        {
            Text =
                "Saved Steam refresh tokens are protected with Windows DPAPI "
                + "for the current Windows user. Steam passwords are never stored.",
            AutoSize = false,
            Location = new Point(24, 155),
            Size = new Size(510, 48),
        };

        var disclaimer = new Label
        {
            Text =
                "Independent utility. Not affiliated with, endorsed by, or "
                + "sponsored by Valve Corporation or Steam.",
            AutoSize = false,
            Location = new Point(24, 208),
            Size = new Size(510, 42),
        };

        var thirdParty = new Label
        {
            Text =
                "Third-party components: SteamKit2, QRCoder, and Microsoft "
                + "System.Security.Cryptography.ProtectedData. See "
                + "THIRD-PARTY-NOTICES.txt in the release package.",
            AutoSize = false,
            Location = new Point(24, 255),
            Size = new Size(510, 54),
        };

        var logs = new Button
        {
            Text = "Open Logs Folder",
            Width = 135,
            Location = new Point(24, 340),
        };

        var data = new Button
        {
            Text = "Open App Data",
            Width = 125,
            Location = new Point(168, 340),
        };

        var close = new Button
        {
            Text = "Close",
            Width = 90,
            Location = new Point(444, 340),
            DialogResult = DialogResult.OK,
        };

        logs.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppLogger.LogDirectory);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppLogger.LogDirectory,
                    UseShellExecute = true,
                }
            );
        };

        data.Click += (_, _) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "SteamIdleManager"
            );

            Directory.CreateDirectory(path);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                }
            );
        };

        Controls.AddRange(
            new Control[]
            {
                title,
                version,
                description,
                privacy,
                disclaimer,
                thirdParty,
                logs,
                data,
                close,
            }
        );

        AcceptButton = close;
        CancelButton = close;
    }
}
