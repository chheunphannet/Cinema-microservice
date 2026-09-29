using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using CinemaPOS.Core.Models;
using CinemaPOS.Core.Services;
using Microsoft.Extensions.Configuration;

namespace CinemaPOS.App.Forms
{
    public class LoginForm : Form
    {
        // --- Dependencies & Config ---
        private readonly ApiService _apiService;
        private readonly IConfiguration _config;

        // --- State ---
        private string _selectedUsername = "";
        private string _selectedDisplayName = "";
        private Guid? _selectedBranchId = null;
        private string _pinEntry = "";
        private const int PinLength = 4; // Standard 4-digit POS PIN
        private bool _isAuthenticating = false;
        private bool _isInitializing = false;
        private List<StaffUserDto> _allStaff = new();
        private Dictionary<Guid, string> _branchNames = new();

        // --- Controls: Header ---
        private Panel _headerPanel;
        private PictureBox _logoPictureBox;
        private Label _brandTitleLabel;
        private Label _branchLabel;
        private ComboBox _branchComboBox;

        // --- Controls: Left Panel (User Selection) ---
        private Panel _userCardPanel;
        private Label _userPanelTitle;
        private Button _refreshUsersButton;
        private Panel _userListContainer;
        private FlowLayoutPanel _userListFlow;
        private VScrollBar _userListScrollBar;
        private Panel _userLoadingOverlay;
        private Label _userLoadingText;
        private ProgressBar _userProgressBar;
        private Button _selectedUserCardButton = null;

        // --- Controls: Right Panel (PIN & Keypad) ---
        private Panel _pinCardPanel;
        private Label _pinPanelTitle;
        private Panel _pinBoxesContainer;
        private Panel[] _pinBoxPanels;
        private Label[] _pinBoxLabels;
        private TableLayoutPanel _keypadTable;
        private Button _loginSubmitButton;

        // --- Public Session Info (accessible to subsequent screens) ---
        public static LoginResponse CurrentUser { get; private set; }
        public static ShiftOpenResponse CurrentShift { get; private set; }
        public static ApiService Api { get; private set; }
        public static string CurrentBranchName { get; private set; } = "Main Cinema";
        public static LoginForm? CurrentInstance { get; private set; }

        public static void ClearSession()
        {
            if (Api != null)
            {
                Api.JwtToken = string.Empty;
            }
            CurrentUser = null;
            CurrentShift = null;
            CurrentBranchName = "Main Cinema";
        }

        public static void SetSession(LoginResponse? user, ShiftOpenResponse? shift, string branchName = "Main Cinema")
        {
            CurrentUser = user;
            CurrentShift = shift;
            CurrentBranchName = branchName;
            if (user != null && !string.IsNullOrEmpty(user.Token) && Api != null)
            {
                Api.JwtToken = user.Token;
            }
        }

        // --- Design Palette (Inspired by Reference Screenshot & Modern POS UI) ---
        private static readonly Color FormBgColor = ColorTranslator.FromHtml("#EAF0F6"); // Soft grey-blue background
        private static readonly Color CardBgColor = Color.White;
        private static readonly Color TextPrimaryColor = ColorTranslator.FromHtml("#0F172A"); // Dark slate
        private static readonly Color TextMutedColor = ColorTranslator.FromHtml("#64748B"); // Slate grey
        private static readonly Color BorderDefaultColor = ColorTranslator.FromHtml("#CBD5E1"); // Light grey border
        private static readonly Color BorderActiveColor = ColorTranslator.FromHtml("#2563EB"); // Vibrant blue active border
        private static readonly Color SelectedCardBgColor = ColorTranslator.FromHtml("#EFF6FF"); // Ice blue active card
        private static readonly Color GreenButtonColor = ColorTranslator.FromHtml("#16A34A"); // Action green
        private static readonly Color GreenButtonHoverColor = ColorTranslator.FromHtml("#15803D");
        private static readonly Color RedClearColor = ColorTranslator.FromHtml("#DC2626"); // Danger red
        private static readonly Color KeypadHoverColor = ColorTranslator.FromHtml("#F1F5F9");

        public LoginForm(IConfiguration config)
        {
            CurrentInstance = this;
            _config = config;
            var httpClient = new HttpClient();
            _apiService = new ApiService(httpClient);
            Api = _apiService;

            // Reduce flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            InitializeComponent();
            this.Load += async (s, e) => await InitializeDataAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Cinema POS - Cashier Terminal Login";
            this.Size = new Size(980, 680);
            this.MinimumSize = new Size(980, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = FormBgColor;
            this.Font = new Font("Segoe UI", 11f);
            this.ForeColor = TextPrimaryColor;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            BuildHeader();
            BuildLeftUserSelectionPanel();
            BuildRightPinPanel();
        }

        private void BuildHeader()
        {
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.White,
                Padding = new Padding(24, 0, 24, 0)
            };
            _headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderDefaultColor, 1);
                e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            };

            // Logo & Brand Title
            _logoPictureBox = new PictureBox
            {
                Location = new Point(24, 11),
                Size = new Size(42, 42),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            string logoPath = FindLogoPath();
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    _logoPictureBox.Image = Image.FromFile(logoPath);
                }
                catch
                {
                    try
                    {
                        using var imgSharp = SixLabors.ImageSharp.Image.Load(logoPath);
                        using var ms = new System.IO.MemoryStream();
                        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(imgSharp, ms);
                        ms.Position = 0;
                        _logoPictureBox.Image = new Bitmap(ms);
                    }
                    catch { }
                }
            }
            _headerPanel.Controls.Add(_logoPictureBox);

            _brandTitleLabel = new Label
            {
                Text = "CINEMA POS",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                AutoSize = true,
                Location = new Point(74, 18)
            };
            _headerPanel.Controls.Add(_brandTitleLabel);

            // Branch selector
            _branchComboBox = new ComboBox
            {
                Location = new Point(660, 16),
                Width = 280,
                Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = "Name",
                ValueMember = "BranchId",
                Font = new Font("Segoe UI", 10.5f)
            };
            _branchComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing) return;
                if (_branchComboBox.SelectedItem is BranchDto branch)
                {
                    _selectedBranchId = branch.BranchId;
                    FilterAndDisplayUsers();
                }
            };
            _headerPanel.Controls.Add(_branchComboBox);

            _branchLabel = new Label
            {
                Text = "Branch:",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextMutedColor,
                AutoSize = true,
                Location = new Point(580, 20)
            };
            _headerPanel.Controls.Add(_branchLabel);

            this.Controls.Add(_headerPanel);
        }

        private void BuildLeftUserSelectionPanel()
        {
            _userCardPanel = new Panel
            {
                Location = new Point(24, 84),
                Size = new Size(434, 530),
                BackColor = CardBgColor
            };
            _userCardPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderDefaultColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _userCardPanel.Width - 1, _userCardPanel.Height - 1);
            };

            // Section Title
            _userPanelTitle = new Label
            {
                Text = "Select a User",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                Location = new Point(20, 18),
                AutoSize = true
            };
            _userCardPanel.Controls.Add(_userPanelTitle);

            // Refresh Button (⟳)
            _refreshUsersButton = new Button
            {
                Text = "⟳ Refresh",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = BorderActiveColor,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(314, 16),
                Size = new Size(100, 32),
                Cursor = Cursors.Hand
            };
            _refreshUsersButton.FlatAppearance.BorderColor = BorderDefaultColor;

            _refreshUsersButton.Click += async (s, e) =>
            {
                _refreshUsersButton.Enabled = false;
                _refreshUsersButton.Text = "⟳ Loading...";
                try
                {
                    await RefreshAllAsync();
                }
                finally
                {
                    _refreshUsersButton.Text = "⟳ Refresh";
                    _refreshUsersButton.Enabled = true;
                }
            };
            _userCardPanel.Controls.Add(_refreshUsersButton);

            // User list container (holds flow panel and dedicated vertical scrollbar)
            _userListContainer = new Panel
            {
                Location = new Point(14, 60),
                Size = new Size(406, 450),
                BackColor = CardBgColor
            };
            _userListContainer.MouseWheel += OnUserListMouseWheel;

            // Flow panel for user cards (sized to leave room for the right scrollbar)
            _userListFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 0),
                Width = 382,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = CardBgColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _userListFlow.MouseWheel += OnUserListMouseWheel;
            _userListContainer.Controls.Add(_userListFlow);

            // Dedicated vertical scrollbar permanently positioned on the right
            _userListScrollBar = new VScrollBar
            {
                Location = new Point(386, 0),
                Size = new Size(18, 450),
                Minimum = 0,
                SmallChange = 32,
                Enabled = false
            };
            _userListScrollBar.ValueChanged += (s, e) =>
            {
                _userListFlow.Top = -_userListScrollBar.Value;
            };
            _userListContainer.Controls.Add(_userListScrollBar);

            _userCardPanel.Controls.Add(_userListContainer);

            // Loading Overlay for Left Panel
            _userLoadingOverlay = new Panel
            {
                Location = new Point(14, 60),
                Size = new Size(406, 450),
                BackColor = Color.White,
                Visible = false
            };

            _userProgressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Size = new Size(240, 8),
                Location = new Point(83, 180)
            };
            _userLoadingOverlay.Controls.Add(_userProgressBar);

            _userLoadingText = new Label
            {
                Text = "Fetching staff members from API...",
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TextMutedColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 200),
                Size = new Size(366, 40)
            };
            _userLoadingOverlay.Controls.Add(_userLoadingText);

            _userCardPanel.Controls.Add(_userLoadingOverlay);
            _userLoadingOverlay.BringToFront();

            this.Controls.Add(_userCardPanel);
        }

        private void BuildRightPinPanel()
        {
            _pinCardPanel = new Panel
            {
                Location = new Point(482, 84),
                Size = new Size(474, 530),
                BackColor = CardBgColor
            };
            _pinCardPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderDefaultColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _pinCardPanel.Width - 1, _pinCardPanel.Height - 1);
            };

            // Section Title
            _pinPanelTitle = new Label
            {
                Text = "Pin Code",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 18),
                Size = new Size(474, 30)
            };
            _pinCardPanel.Controls.Add(_pinPanelTitle);

            // PIN Digit Boxes Container
            _pinBoxesContainer = new Panel
            {
                Location = new Point(87, 56),
                Size = new Size(300, 60),
                BackColor = CardBgColor
            };

            _pinBoxPanels = new Panel[PinLength];
            _pinBoxLabels = new Label[PinLength];

            int boxWidth = 54;
            int boxHeight = 54;
            int gap = 16;
            int startX = (300 - (PinLength * boxWidth + (PinLength - 1) * gap)) / 2;

            for (int i = 0; i < PinLength; i++)
            {
                int index = i;
                var boxPanel = new Panel
                {
                    Location = new Point(startX + i * (boxWidth + gap), 3),
                    Size = new Size(boxWidth, boxHeight),
                    BackColor = Color.White
                };

                boxPanel.Paint += (s, e) =>
                {
                    bool isFilled = index < _pinEntry.Length;
                    bool isNext = index == _pinEntry.Length;
                    Color borderColor = isFilled || isNext ? BorderActiveColor : BorderDefaultColor;
                    int borderWidth = isFilled || isNext ? 2 : 1;

                    using var pen = new Pen(borderColor, borderWidth);
                    e.Graphics.DrawRectangle(pen, 0, 0, boxPanel.Width - 1, boxPanel.Height - 1);
                };

                var boxLabel = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 26f, FontStyle.Bold),
                    ForeColor = TextPrimaryColor,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent
                };

                boxPanel.Controls.Add(boxLabel);
                _pinBoxPanels[i] = boxPanel;
                _pinBoxLabels[i] = boxLabel;
                _pinBoxesContainer.Controls.Add(boxPanel);
            }
            _pinCardPanel.Controls.Add(_pinBoxesContainer);

            // Keypad Grid
            _keypadTable = new TableLayoutPanel
            {
                Location = new Point(67, 126),
                Size = new Size(340, 310),
                ColumnCount = 3,
                RowCount = 4,
                BackColor = CardBgColor
            };

            for (int c = 0; c < 3; c++)
                _keypadTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int r = 0; r < 4; r++)
                _keypadTable.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            string[] keypadKeys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", "<<", "0", "C" };
            int keyIdx = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var btn = CreateKeypadButton(keypadKeys[keyIdx]);
                    _keypadTable.Controls.Add(btn, col, row);
                    keyIdx++;
                }
            }
            _pinCardPanel.Controls.Add(_keypadTable);

            // Submit Button: LOG IN
            _loginSubmitButton = new Button
            {
                Text = "➜  LOG IN",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = GreenButtonColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(67, 452),
                Size = new Size(340, 52),
                Cursor = Cursors.Hand
            };
            _loginSubmitButton.FlatAppearance.BorderSize = 0;
            _loginSubmitButton.FlatAppearance.MouseOverBackColor = GreenButtonHoverColor;
            _loginSubmitButton.Click += async (s, e) => await PerformLoginAsync();
            _pinCardPanel.Controls.Add(_loginSubmitButton);

            this.Controls.Add(_pinCardPanel);
        }

        private Button CreateKeypadButton(string keyText)
        {
            var btn = new Button
            {
                Text = keyText,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4),
                BackColor = Color.White,
                ForeColor = TextPrimaryColor,
                Font = new Font("Segoe UI", 20f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = BorderDefaultColor;
            btn.FlatAppearance.MouseOverBackColor = KeypadHoverColor;

            if (keyText == "<<" || keyText == "⌫")
            {
                btn.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
                btn.ForeColor = TextMutedColor;
            }
            else if (keyText == "C")
            {
                btn.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
                btn.ForeColor = RedClearColor;
            }

            btn.Click += async (s, e) => await OnKeypadPressed(keyText);
            return btn;
        }

        private async Task OnKeypadPressed(string key)
        {
            if (_isAuthenticating) return;

            if (key == "C")
            {
                _pinEntry = "";
                UpdatePinBoxes();
            }
            else if (key == "<<" || key == "⌫")
            {
                if (_pinEntry.Length > 0)
                {
                    _pinEntry = _pinEntry.Substring(0, _pinEntry.Length - 1);
                    UpdatePinBoxes();
                }
            }
            else
            {
                if (_pinEntry.Length < PinLength)
                {
                    _pinEntry += key;
                    UpdatePinBoxes();

                    // If full 4-digit PIN entered, automatically attempt login if user is chosen
                    if (_pinEntry.Length == PinLength && !string.IsNullOrEmpty(_selectedUsername))
                    {
                        await Task.Delay(150); // Brief visual feedback
                        await PerformLoginAsync();
                    }
                }
            }
        }

        private void UpdatePinBoxes()
        {
            for (int i = 0; i < PinLength; i++)
            {
                if (i < _pinEntry.Length)
                {
                    _pinBoxLabels[i].Text = "✱"; // Prominent box asterisk
                    _pinBoxLabels[i].ForeColor = TextPrimaryColor;
                }
                else
                {
                    _pinBoxLabels[i].Text = "";
                }
                _pinBoxPanels[i].Invalidate(); // Redraw borders
            }
        }

        private async Task InitializeDataAsync()
        {
            ShowLoading(true, "Loading staff and branches...");
            _isInitializing = true;
            _apiService.JwtToken = null; // Always fetch public users unauthenticated

            // 1. Fetch branches from Catalog API
            try
            {
                var branches = await _apiService.GetAsync<List<BranchDto>>("/api/v1/catalog/branches");
                var allBranchesItem = new BranchDto
                {
                    BranchId = Guid.Empty,
                    Name = "All Branches",
                    Code = "ALL",
                    IsActive = true
                };

                var branchList = new List<BranchDto> { allBranchesItem };

                if (branches != null && branches.Count > 0)
                {
                    _branchNames.Clear();
                    foreach (var b in branches)
                    {
                        _branchNames[b.BranchId] = b.Name;
                    }
                    branchList.AddRange(branches.Where(b => b.IsActive));
                }

                _branchComboBox.DataSource = branchList;
                _branchComboBox.DisplayMember = "Name";
                _branchComboBox.ValueMember = "BranchId";

                // Default selection: All Branches (shows all users)
                _selectedBranchId = Guid.Empty;
                _branchComboBox.SelectedItem = allBranchesItem;
            }
            catch (Exception)
            {
                _branchComboBox.Items.Add("All Branches");
                _branchComboBox.SelectedIndex = 0;
            }

            // 2. Fetch all staff from API once
            try
            {
                var users = await _apiService.GetAsync<List<StaffUserDto>>("/api/v1/identity/users");
                _allStaff = users ?? new List<StaffUserDto>();
            }
            catch (Exception)
            {
                _allStaff = new List<StaffUserDto>();
            }

            _isInitializing = false;
            ShowLoading(false);
            FilterAndDisplayUsers();
        }

        private async Task RefreshAllAsync()
        {
            ShowLoading(true, "Refreshing branches and staff...");
            _apiService.JwtToken = null; // Ensure unauthenticated fetch so no branch filtering occurs

            try
            {
                // Reload branches
                var freshBranches = await _apiService.GetAsync<List<BranchDto>>("/api/v1/catalog/branches");
                if (freshBranches != null && freshBranches.Count > 0)
                {
                    _branchNames.Clear();
                    foreach (var b in freshBranches)
                    {
                        _branchNames[b.BranchId] = b.Name;
                    }

                    var currentSelected = _selectedBranchId;
                    var allBranchesItem = new BranchDto { BranchId = Guid.Empty, Name = "All Branches", Code = "ALL", IsActive = true };
                    var branchList = new List<BranchDto> { allBranchesItem };
                    branchList.AddRange(freshBranches.Where(b => b.IsActive));

                    _isInitializing = true;
                    _branchComboBox.DataSource = branchList;
                    _branchComboBox.DisplayMember = "Name";
                    _branchComboBox.ValueMember = "BranchId";

                    var match = branchList.FirstOrDefault(b => b.BranchId == currentSelected)
                             ?? allBranchesItem;
                    _selectedBranchId = match.BranchId;
                    _branchComboBox.SelectedItem = match;
                    _isInitializing = false;
                }

                // Reload all staff from API (cache-busting is active)
                var users = await _apiService.GetAsync<List<StaffUserDto>>("/api/v1/identity/users");
                _allStaff = users ?? new List<StaffUserDto>();

                ShowLoading(false);
                FilterAndDisplayUsers();
            }
            catch (Exception)
            {
                ShowLoading(false);
                ShowNoUsersMessage("Could not reload staff list. Check server connection.");
            }
        }

        private void FilterAndDisplayUsers()
        {
            _userListFlow.SuspendLayout();
            _userListFlow.Controls.Clear();
            _selectedUserCardButton = null;
            _selectedUsername = "";
            _selectedDisplayName = "";
            _pinEntry = "";
            UpdatePinBoxes();

            bool isSpecificBranch = _selectedBranchId.HasValue && _selectedBranchId.Value != Guid.Empty;
            var filtered = isSpecificBranch
                ? _allStaff.Where(u =>
                    (u.BranchId.HasValue && u.BranchId.Value == _selectedBranchId.Value) ||
                    IsSystemWideUser(u)
                ).ToList()
                : _allStaff.ToList();

            if (filtered.Count > 0)
            {
                foreach (var staff in filtered)
                {
                    var cardBtn = CreateStaffCard(staff);
                    _userListFlow.Controls.Add(cardBtn);
                }

                var firstBtn = _userListFlow.Controls.OfType<Button>().FirstOrDefault();
                if (firstBtn != null)
                {
                    SelectUser(firstBtn);
                }
            }
            else
            {
                ShowNoUsersMessage("No active staff found for this branch.");
            }

            _userListFlow.ResumeLayout(true);
            _userListFlow.PerformLayout();
            UpdateUserListScroll();
            _userListFlow.Invalidate();
        }

        private void UpdateUserListScroll()
        {
            _userListFlow.PerformLayout();
            int totalHeight = _userListFlow.Height;
            int visibleHeight = _userListContainer.Height;

            if (totalHeight > visibleHeight)
            {
                _userListScrollBar.Enabled = true;
                _userListScrollBar.LargeChange = Math.Max(1, visibleHeight / 4);
                _userListScrollBar.SmallChange = 32;
                _userListScrollBar.Maximum = totalHeight - visibleHeight + _userListScrollBar.LargeChange - 1;
                _userListScrollBar.Value = Math.Clamp(_userListScrollBar.Value, 0, Math.Max(0, _userListScrollBar.Maximum - _userListScrollBar.LargeChange + 1));
            }
            else
            {
                _userListScrollBar.Enabled = false;
                _userListScrollBar.Value = 0;
            }
            _userListFlow.Top = -_userListScrollBar.Value;
        }

        private void OnUserListMouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_userListScrollBar.Enabled) return;
            int deltaSteps = e.Delta / 120;
            int maxVal = Math.Max(0, _userListScrollBar.Maximum - _userListScrollBar.LargeChange + 1);
            int target = Math.Clamp(_userListScrollBar.Value - deltaSteps * _userListScrollBar.SmallChange, 0, maxVal);
            _userListScrollBar.Value = target;
        }

        private Button CreateStaffCard(StaffUserDto staff)
        {
            var btn = new Button
            {
                Width = 376,
                Height = 64,
                Margin = new Padding(1, 4, 1, 4),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag = staff
            };

            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = BorderDefaultColor;
            btn.FlatAppearance.MouseOverBackColor = KeypadHoverColor;
            btn.MouseWheel += OnUserListMouseWheel;

            // Custom paint for rich display: avatar circle, name, username & role badge
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                bool isSelected = btn == _selectedUserCardButton;
                if (isSelected)
                {
                    using var bgBrush = new SolidBrush(SelectedCardBgColor);
                    e.Graphics.FillRectangle(bgBrush, btn.ClientRectangle);

                    using var borderPen = new Pen(BorderActiveColor, 2);
                    e.Graphics.DrawRectangle(borderPen, 1, 1, btn.Width - 3, btn.Height - 3);
                }

                // Avatar Circle
                var avatarRect = new Rectangle(12, 12, 40, 40);
                using var avatarBrush = new SolidBrush(isSelected ? ColorTranslator.FromHtml("#DBEAFE") : ColorTranslator.FromHtml("#F1F5F9"));
                e.Graphics.FillEllipse(avatarBrush, avatarRect);

                // Initials
                string initials = GetInitials(staff.DisplayName ?? staff.Username);
                using var initialFont = new Font("Segoe UI", 11f, FontStyle.Bold);
                using var initialBrush = new SolidBrush(isSelected ? BorderActiveColor : TextMutedColor);
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(initials, initialFont, initialBrush, avatarRect, sfCenter);

                // Display Name
                string name = staff.DisplayName ?? staff.Username;
                using var nameFont = new Font("Segoe UI", 12f, FontStyle.Bold);
                using var nameBrush = new SolidBrush(TextPrimaryColor);
                e.Graphics.DrawString(name, nameFont, nameBrush, 60, 11);

                // Username, Role & Branch Tag
                string roleText = FormatRole(staff.Role);
                string branchTag = "";
                if (staff.BranchId.HasValue && _branchNames.TryGetValue(staff.BranchId.Value, out var bName))
                {
                    branchTag = $"  •  {bName}";
                }
                else if (!staff.BranchId.HasValue || staff.BranchId == Guid.Empty || IsSystemWideUser(staff))
                {
                    branchTag = "  •  System-wide (All Branches)";
                }

                string detail = $"@{staff.Username}  •  {roleText}{branchTag}";
                using var detailFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                using var detailBrush = new SolidBrush(TextMutedColor);
                var detailRect = new RectangleF(60, 35, btn.Width - 92, 22);
                using var sfDetail = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                e.Graphics.DrawString(detail, detailFont, detailBrush, detailRect, sfDetail);

                // Right checkmark if selected
                if (isSelected)
                {
                    using var checkFont = new Font("Segoe UI", 12f, FontStyle.Bold);
                    using var checkBrush = new SolidBrush(BorderActiveColor);
                    e.Graphics.DrawString("✓", checkFont, checkBrush, btn.Width - 28, 20);
                }
            };

            btn.Click += (s, e) => SelectUser(btn);
            return btn;
        }

        private void SelectUser(Button btn)
        {
            var prev = _selectedUserCardButton;
            _selectedUserCardButton = btn;

            if (prev != null) prev.Invalidate();
            btn.Invalidate();

            if (btn.Tag is StaffUserDto staff)
            {
                _selectedUsername = staff.Username;
                _selectedDisplayName = staff.DisplayName ?? staff.Username;
            }
            else
            {
                _selectedUsername = btn.Text;
                _selectedDisplayName = btn.Text;
            }

            _pinEntry = "";
            UpdatePinBoxes();
        }

        private void ShowLoading(bool visible, string message = "")
        {
            _userLoadingText.Text = message;
            _userLoadingOverlay.Visible = visible;
            if (visible)
            {
                _userLoadingOverlay.BringToFront();
            }
        }

        private void ShowNoUsersMessage(string message)
        {
            var label = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 11f),
                ForeColor = TextMutedColor,
                AutoSize = true,
                Margin = new Padding(12, 20, 12, 12)
            };
            _userListFlow.Controls.Add(label);

            // Manual username textbox fallback
            var manualLabel = new Label
            {
                Text = "Type username manually:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                AutoSize = true,
                Margin = new Padding(12, 10, 12, 4)
            };
            _userListFlow.Controls.Add(manualLabel);

            var manualBox = new TextBox
            {
                Width = 360,
                Font = new Font("Segoe UI", 12f),
                Margin = new Padding(12, 0, 12, 8)
            };
            manualBox.TextChanged += (s, e) =>
            {
                _selectedUsername = manualBox.Text.Trim();
                _selectedDisplayName = _selectedUsername;
            };
            _userListFlow.Controls.Add(manualBox);
        }

        private async Task PerformLoginAsync()
        {
            if (_isAuthenticating) return;

            if (string.IsNullOrEmpty(_selectedUsername))
            {
                MessageBox.Show("Please select a user from the list first.", "User Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_pinEntry))
            {
                MessageBox.Show("Please enter your PIN code.", "PIN Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isAuthenticating = true;
            _loginSubmitButton.Text = "AUTHENTICATING...";
            _loginSubmitButton.Enabled = false;

            try
            {
                var loginReq = new LoginRequest
                {
                    Username = _selectedUsername,
                    PinOrPassword = _pinEntry
                };

                LoginResponse loginRes;
                try
                {
                    loginRes = await _apiService.PostAsync<LoginRequest, LoginResponse>("/api/v1/identity/login", loginReq);
                }
                catch (Exception apiEx)
                {
                    // If server returns error, show message
                    MessageBox.Show($"Authentication failed: {apiEx.Message}\nCheck your PIN and try again.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _pinEntry = "";
                    UpdatePinBoxes();
                    return;
                }

                _apiService.JwtToken = loginRes.Token;
                CurrentUser = loginRes;
                if (loginRes.BranchId != Guid.Empty && _branchNames.TryGetValue(loginRes.BranchId, out var bName))
                {
                    CurrentBranchName = bName;
                }
                else if (_selectedBranchId.HasValue && _branchNames.TryGetValue(_selectedBranchId.Value, out var selBName))
                {
                    CurrentBranchName = selBName;
                }

                // Prompt for starting cash drawer float
                string floatInput = PromptForOpeningFloat(loginRes.DisplayName ?? loginRes.Username);
                if (decimal.TryParse(floatInput, out decimal floatAmount))
                {
                    Guid effectiveBranchId = loginRes.BranchId != Guid.Empty
                        ? loginRes.BranchId
                        : ((_selectedBranchId.HasValue && _selectedBranchId.Value != Guid.Empty)
                            ? _selectedBranchId.Value
                            : (_branchNames.Keys.Count > 0 ? _branchNames.Keys.First() : Guid.Empty));

                    var shiftReq = new ShiftOpenRequest
                    {
                        BranchId = effectiveBranchId,
                        CashierId = loginRes.UserId,
                        TerminalCode = "POS-01",
                        OpeningFloat = floatAmount
                    };

                    ShiftOpenResponse shiftRes;
                    try
                    {
                        shiftRes = await _apiService.PostAsync<ShiftOpenRequest, ShiftOpenResponse>("/api/v1/pos/shifts/open", shiftReq);
                    }
                    catch
                    {
                        // Fallback shift object if offline
                        shiftRes = new ShiftOpenResponse
                        {
                            ShiftId = Guid.NewGuid(),
                            Status = "open",
                            TerminalCode = "POS-01",
                            OpeningFloat = floatAmount
                        };
                    }

                    CurrentShift = shiftRes;

                    // Phase 1 Reset: Clear UI state
                    _pinEntry = "";
                    UpdatePinBoxes();
                    
                    // Phase 2 transition:
                    this.Hide();
                    new MainPosForm().Show();
                }
                else
                {
                    // User cancelled float dialog
                    _pinEntry = "";
                    UpdatePinBoxes();
                }
            }
            finally
            {
                _isAuthenticating = false;
                _loginSubmitButton.Text = "➜  LOG IN";
                _loginSubmitButton.Enabled = true;
            }
        }

        private string PromptForOpeningFloat(string staffName)
        {
            using var floatDialog = new Form
            {
                Text = "Shift Start - Cash Drawer Float",
                BackColor = FormBgColor,
                Size = new Size(420, 260),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 12f)
            };

            var iconLabel = new Label
            {
                Text = "💵",
                Font = new Font("Segoe UI", 26f),
                Location = new Point(24, 20),
                AutoSize = true
            };
            floatDialog.Controls.Add(iconLabel);

            var titleLabel = new Label
            {
                Text = $"Cash Float for {staffName}:",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                Location = new Point(78, 26),
                AutoSize = true
            };
            floatDialog.Controls.Add(titleLabel);

            var infoLabel = new Label
            {
                Text = "Count and confirm starting cash in drawer:",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextMutedColor,
                Location = new Point(26, 76),
                AutoSize = true
            };
            floatDialog.Controls.Add(infoLabel);

            var floatTextBox = new TextBox
            {
                Text = "100.00",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(26, 102),
                Width = 352,
                Height = 42
            };
            floatDialog.Controls.Add(floatTextBox);

            var confirmBtn = new Button
            {
                Text = "✓ CONFIRM FLOAT",
                DialogResult = DialogResult.OK,
                Location = new Point(208, 160),
                Size = new Size(170, 46),
                BackColor = GreenButtonColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            confirmBtn.FlatAppearance.BorderSize = 0;
            floatDialog.Controls.Add(confirmBtn);

            var cancelBtn = new Button
            {
                Text = "CANCEL",
                DialogResult = DialogResult.Cancel,
                Location = new Point(26, 160),
                Size = new Size(170, 46),
                BackColor = ColorTranslator.FromHtml("#64748B"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            cancelBtn.FlatAppearance.BorderSize = 0;
            floatDialog.Controls.Add(cancelBtn);

            floatDialog.AcceptButton = confirmBtn;
            floatDialog.CancelButton = cancelBtn;

            return floatDialog.ShowDialog() == DialogResult.OK ? floatTextBox.Text.Trim() : "";
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "U";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpperInvariant();
        }

        private static string FormatRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Staff";
            return role.Replace("_", " ").ToUpperInvariant();
        }

        private static bool IsSystemWideUser(StaffUserDto staff)
        {
            if (staff == null) return false;
            // No assigned branch or Guid.Empty means system-wide (all branches)
            if (!staff.BranchId.HasValue || staff.BranchId.Value == Guid.Empty) return true;
            // Super admin or system admin roles have system-wide access across all branches
            var r = staff.Role?.ToLowerInvariant() ?? "";
            return r.Contains("super_admin") || r.Contains("system_admin") || r == "admin";
        }

        public void ResetLoginState()
        {
            _pinEntry = "";
            UpdatePinBoxes();
            if (_selectedUserCardButton != null)
            {
                var prev = _selectedUserCardButton;
                _selectedUserCardButton = null;
                prev.Invalidate();
            }
            _selectedUsername = "";
            _selectedDisplayName = "";
            _isAuthenticating = false;
            if (_loginSubmitButton != null)
            {
                _loginSubmitButton.Text = "➜  LOG IN";
                _loginSubmitButton.Enabled = true;
            }
        }

        public static string FindLogoPath()
        {
            string[] fileNames = new[]
            {
                "cinema_logo.png",
                "kisspng-madcap-software-inc-art-film-ticket-5b587243f08318.3650538515325230759851.jpg"
            };

            foreach (var fileName in fileNames)
            {
                string[] candidates = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo", fileName),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "logo", fileName),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logo", fileName),
                    @"C:\Users\chheu\Desktop\Cinema-microservice\POS client UI\logo\" + fileName
                };

                foreach (var path in candidates)
                {
                    if (System.IO.File.Exists(path)) return path;
                }
            }
            return "";
        }
    }
}
