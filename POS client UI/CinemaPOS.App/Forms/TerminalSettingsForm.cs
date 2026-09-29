using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;
using CinemaPOS.Core.Models;
using CinemaPOS.Core.Services;

namespace CinemaPOS.App.Forms
{
    public class TerminalSettingsForm : Form
    {
        private readonly ApiService _api;

        public bool LoggedOut { get; private set; } = false;
        public bool ShiftClosed { get; private set; } = false;

        // Visual Palette (matching Cinema POS design system)
        private static readonly Color HeaderBgColor = ColorTranslator.FromHtml("#0F172A"); // Deep Cinema Slate
        private static readonly Color FormBgColor = ColorTranslator.FromHtml("#F8FAFC");   // Slate 50
        private static readonly Color CardBgColor = Color.White;
        private static readonly Color BorderColor = ColorTranslator.FromHtml("#E2E8F0");   // Slate 200
        private static readonly Color TextPrimaryColor = ColorTranslator.FromHtml("#0F172A");
        private static readonly Color TextMutedColor = ColorTranslator.FromHtml("#64748B");
        private static readonly Color AccentBlueColor = ColorTranslator.FromHtml("#2563EB");
        private static readonly Color CyanBadgeColor = ColorTranslator.FromHtml("#38BDF8");
        private static readonly Color DangerRedColor = ColorTranslator.FromHtml("#DC2626");
        private static readonly Color DangerRedHoverColor = ColorTranslator.FromHtml("#B91C1C");
        private static readonly Color SuccessGreenColor = ColorTranslator.FromHtml("#16A34A");

        public TerminalSettingsForm(ApiService? api = null)
        {
            _api = api ?? LoginForm.Api ?? new ApiService(new HttpClient());

            // Reduce flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Terminal Settings";
            this.ClientSize = new Size(640, 650);
            this.MinimumSize = new Size(640, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = FormBgColor;
            this.Font = new Font("Segoe UI", 9.5f);
            this.ForeColor = TextPrimaryColor;

            // 1. Header Bar (Dock Top)
            var headerPanel = BuildHeaderPanel();

            // 2. Footer Action Bar (Dock Bottom)
            var footerPanel = BuildFooterPanel();

            // 3. Main Content Container (Dock Fill)
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = FormBgColor,
                Padding = new Padding(16, 12, 16, 12)
            };

            // Reverse z-order for WinForms docking
            this.Controls.Add(contentPanel);
            this.Controls.Add(footerPanel);
            this.Controls.Add(headerPanel);

            headerPanel.SendToBack();
            footerPanel.SendToBack();
            contentPanel.BringToFront();

            this.Controls.SetChildIndex(contentPanel, 0);
            this.Controls.SetChildIndex(footerPanel, 1);
            this.Controls.SetChildIndex(headerPanel, 2);

            // Add Cards to Content Panel
            int cardWidth = this.ClientSize.Width - 32;
            int currentY = 12;

            // Section A: Account Information Card
            var accountCard = BuildAccountInfoCard(cardWidth);
            accountCard.Location = new Point(16, currentY);
            accountCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            contentPanel.Controls.Add(accountCard);
            currentY += accountCard.Height + 10;

            // Section B: Keyboard Shortcuts Card
            var shortcutsCard = BuildShortcutsCard(cardWidth);
            shortcutsCard.Location = new Point(16, currentY);
            shortcutsCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            contentPanel.Controls.Add(shortcutsCard);
            currentY += shortcutsCard.Height + 12;

            contentPanel.AutoScrollMinSize = new Size(0, currentY);

            // Keyboard support
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
        }

        private Panel BuildHeaderPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = HeaderBgColor,
                Padding = new Padding(16, 0, 16, 0)
            };

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#1E293B"), 1);
                e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
            };

            var iconLabel = new Label
            {
                Text = "⚙️",
                Font = new Font("Segoe UI Emoji", 13f),
                Location = new Point(16, 12),
                Size = new Size(28, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };
            pnl.Controls.Add(iconLabel);

            var titleLabel = new Label
            {
                Text = "Terminal Settings",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(48, 14),
                Size = new Size(260, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            pnl.Controls.Add(titleLabel);

            var btnCloseX = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextMutedColor,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 32),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnCloseX.FlatAppearance.BorderSize = 0;
            btnCloseX.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1E293B");
            btnCloseX.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            pnl.Controls.Add(btnCloseX);

            void PositionCloseButton()
            {
                btnCloseX.Location = new Point(pnl.Width - 44, 10);
            }
            pnl.Resize += (s, e) => PositionCloseButton();
            PositionCloseButton();

            return pnl;
        }

        private Panel BuildAccountInfoCard(int width)
        {
            var card = new Panel
            {
                Width = width,
                Height = 138,
                BackColor = CardBgColor
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);

                // Divider line between card header and body
                using var pDiv = new Pen(BorderColor, 1);
                e.Graphics.DrawLine(pDiv, 14, 34, card.Width - 14, 34);
            };

            // Card Header Title
            var lblTitle = new Label
            {
                Text = "👤  CASHIER SESSION",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                Location = new Point(14, 8),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            card.Controls.Add(lblTitle);

            // Account Data Elements
            var currentShift = LoginForm.CurrentShift;
            var currentUser = LoginForm.CurrentUser;

            string cashierName = currentUser?.DisplayName ?? (currentUser?.Username ?? "Cashier Staff");
            string username = currentUser?.Username ?? "cashier";
            string roles = (currentUser?.Roles != null && currentUser.Roles.Count > 0)
                ? string.Join(", ", currentUser.Roles.Select(r => r.Replace("_", " ").ToUpperInvariant()))
                : "CASHIER";
            string branchName = LoginForm.CurrentBranchName ?? "Main Cinema";
            string branchIdShort;
            if (currentUser != null && currentUser.BranchId != Guid.Empty)
            {
                branchIdShort = "#" + currentUser.BranchId.ToString().Substring(0, 8).ToUpperInvariant();
            }
            else if (currentShift != null && currentShift.BranchId != Guid.Empty)
            {
                branchIdShort = "#" + currentShift.BranchId.ToString().Substring(0, 8).ToUpperInvariant();
            }
            else
            {
                branchIdShort = "#HQ-01";
            }
            string terminal = currentShift?.TerminalCode ?? "POS-01";
            string shiftStart = currentShift != null
                ? currentShift.OpenedAt.ToLocalTime().ToString("MMM dd, yyyy  hh:mm tt")
                : DateTime.Now.ToString("MMM dd, yyyy  hh:mm tt");
            string openingFloat = $"${(currentShift?.OpeningFloat ?? 100.00m):F2}";

            // Column layout
            int col1X = 14;
            int col2X = Math.Max(280, (card.Width / 2) + 6);
            int startY = 40;

            void AddDataPair(int x, int y, string label, string value, Color valueColor, bool isBold = true)
            {
                var lblK = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 8.25f),
                    ForeColor = TextMutedColor,
                    Location = new Point(x, y),
                    Size = new Size(95, 18),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseMnemonic = false
                };
                var lblV = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", 8.75f, isBold ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = valueColor,
                    Location = new Point(x + 98, y - 1),
                    Size = new Size(160, 20),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseMnemonic = false,
                    AutoEllipsis = true
                };
                card.Controls.Add(lblK);
                card.Controls.Add(lblV);
            }

            // Left column rows
            AddDataPair(col1X, startY, "Cashier Name:", cashierName, TextPrimaryColor, true);
            AddDataPair(col1X, startY + 23, "User Account:", "@" + username, TextMutedColor, false);
            AddDataPair(col1X, startY + 46, "Assigned Role:", roles, AccentBlueColor, true);
            AddDataPair(col1X, startY + 69, "Opening Float:", openingFloat, SuccessGreenColor, true);

            // Right column rows
            AddDataPair(col2X, startY, "Branch Location:", branchName, TextPrimaryColor, true);
            AddDataPair(col2X, startY + 23, "Branch ID:", branchIdShort, TextMutedColor, false);
            AddDataPair(col2X, startY + 46, "Terminal Till:", terminal, TextPrimaryColor, true);
            AddDataPair(col2X, startY + 69, "Shift Started:", shiftStart, TextMutedColor, false);

            return card;
        }

        private Panel BuildShortcutsCard(int width)
        {
            var card = new Panel
            {
                Width = width,
                Height = 320,
                BackColor = CardBgColor
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);

                // Divider line below header
                using var pDiv = new Pen(BorderColor, 1);
                e.Graphics.DrawLine(pDiv, 14, 32, card.Width - 14, 32);
            };

            var lblTitle = new Label
            {
                Text = "⌨️  KEYBOARD SHORTCUTS",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextPrimaryColor,
                Location = new Point(14, 8),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            card.Controls.Add(lblTitle);

            // Shortcuts table data (11 global hotkeys without '&')
            var shortcuts = new (string Key, string Action, string Description)[]
            {
                ("F1",  "Quick Search",        "Focus movie title, booking or customer search input"),
                ("F2",  "Bookings",            "Switch to Reservations view and lookup booking records"),
                ("F3",  "Discounts",           "Open promo voucher code and cart discount dialog"),
                ("F4",  "F and B Menu",        "Browse and add snacks, drinks and combos to cart"),
                ("F5",  "Seat Map",            "Switch to auditorium seat layout for current showtime"),
                ("F8",  "Terminal Settings",   "Open cashier session info and hotkey guide"),
                ("F9",  "Hold Seats",          "Lock and synchronize selected seats before checkout"),
                ("F10", "Movies",              "Return to main movie catalogue and schedule browser"),
                ("F11", "Void Cart",           "Clear all items from current cart and release seat holds"),
                ("F12", "Pay / Checkout",      "Proceed directly to payment modal and print receipt"),
                ("Esc", "Close / Cancel",      "Close active modal dialog or clear current selection")
            };

            int yOffset = 40;
            int rowHeight = 24;

            for (int i = 0; i < shortcuts.Length; i++)
            {
                var item = shortcuts[i];
                bool isAlt = (i % 2 == 1);

                var rowPanel = new Panel
                {
                    Location = new Point(10, yOffset),
                    Size = new Size(card.Width - 20, rowHeight),
                    BackColor = isAlt ? ColorTranslator.FromHtml("#F8FAFC") : Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                // Key Badge (dark pill)
                var pnlKey = new Panel
                {
                    Location = new Point(4, 2),
                    Size = new Size(42, 20),
                    BackColor = ColorTranslator.FromHtml("#1E293B")
                };
                pnlKey.Paint += (s, e) =>
                {
                    using var p = new Pen(ColorTranslator.FromHtml("#334155"), 1);
                    e.Graphics.DrawRectangle(p, 0, 0, pnlKey.Width - 1, pnlKey.Height - 1);
                };
                var lblKey = new Label
                {
                    Text = item.Key,
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = CyanBadgeColor,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    UseMnemonic = false
                };
                pnlKey.Controls.Add(lblKey);
                rowPanel.Controls.Add(pnlKey);

                // Action Label
                var lblAction = new Label
                {
                    Text = item.Action,
                    Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                    ForeColor = TextPrimaryColor,
                    Location = new Point(52, 2),
                    Size = new Size(120, 18),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseMnemonic = false
                };
                rowPanel.Controls.Add(lblAction);

                // Description Label
                var lblDesc = new Label
                {
                    Text = item.Description,
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = TextMutedColor,
                    Location = new Point(176, 2),
                    Size = new Size(rowPanel.Width - 180, 18),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseMnemonic = false,
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                rowPanel.Controls.Add(lblDesc);

                card.Controls.Add(rowPanel);
                yOffset += rowHeight + 1;
            }

            return card;
        }

        private Panel BuildFooterPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 8)
            };

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnl.Width, 0);
            };

            // 1. Logout Button
            var btnLogout = new Button
            {
                Text = "🚪  Log Out",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = DangerRedColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(115, 36),
                Location = new Point(16, 8),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = DangerRedHoverColor;
            btnLogout.Click += (s, e) => HandleLogout();
            pnl.Controls.Add(btnLogout);

            // 2. Close Shift Till Button
            var btnCloseShift = new Button
            {
                Text = "🔒  Close Shift Till...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = HeaderBgColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(155, 36),
                Location = new Point(138, 8),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnCloseShift.FlatAppearance.BorderSize = 0;
            btnCloseShift.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1E293B");
            btnCloseShift.Click += (s, e) => HandleCloseShift();
            pnl.Controls.Add(btnCloseShift);

            // 3. Dismiss / Close Button
            var btnClose = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = TextMutedColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(90, 36),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#475569");
            btnClose.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            pnl.Controls.Add(btnClose);

            void PositionButtons()
            {
                btnClose.Location = new Point(pnl.Width - 106, 8);
            }
            pnl.Resize += (s, e) => PositionButtons();
            PositionButtons();

            this.CancelButton = btnClose;

            return pnl;
        }

        private void HandleLogout()
        {
            var result = MessageBox.Show(
                this,
                "Are you sure you want to log out of this terminal session?\n\nAny unsaved transactions will be discarded.",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                LoggedOut = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void HandleCloseShift()
        {
            using var shiftForm = new ShiftSummaryForm(_api, LoginForm.CurrentShift);
            if (shiftForm.ShowDialog(this) == DialogResult.OK && shiftForm.ShiftClosed)
            {
                ShiftClosed = true;
                LoggedOut = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
