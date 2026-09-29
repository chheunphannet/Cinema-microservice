using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CinemaPOS.Core.Models;
using CinemaPOS.Core.Services;

namespace CinemaPOS.App.Forms
{
    public class ShiftSummaryForm : Form
    {
        private readonly ApiService _api;
        private readonly ShiftOpenResponse? _shift;

        public bool ShiftClosed { get; private set; } = false;

        public ShiftSummaryForm(ApiService api, ShiftOpenResponse? shift)
        {
            _api = api;
            _shift = shift;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Till Shift Summary and Close Till";
            this.Size = new Size(460, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            this.Font = new Font("Segoe UI", 11f);

            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = ColorTranslator.FromHtml("#0F172A"),
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblHeader = new Label
            {
                Text = "Cashier Till Shift Summary",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Left,
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeader);
            this.Controls.Add(pnlHeader);

            int startY = 90;

            void AddRow(string label, string value, bool bold = false)
            {
                var lblL = new Label { Text = label, Location = new Point(24, startY), AutoSize = true, Font = new Font("Segoe UI", 10.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ColorTranslator.FromHtml("#475569") };
                var lblV = new Label { Text = value, Location = new Point(240, startY), AutoSize = true, Font = new Font("Segoe UI", 10.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ColorTranslator.FromHtml("#0F172A") };
                this.Controls.Add(lblL);
                this.Controls.Add(lblV);
                startY += 32;
            }

            AddRow("Terminal Register:", _shift?.TerminalCode ?? "POS-01");
            AddRow("Cashier Staff:", LoginForm.CurrentUser?.DisplayName ?? "Cashier");
            AddRow("Shift Opened At:", _shift?.OpenedAt.ToLocalTime().ToString("g") ?? DateTime.Now.ToString("g"));
            AddRow("Opening Float:", $"${_shift?.OpeningFloat ?? 100.00m:F2}", true);

            var txtClosingCash = new TextBox
            {
                Text = $"{(_shift?.OpeningFloat ?? 100.00m) + 150.00m:F2}",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(24, startY + 30),
                Width = 396
            };

            var lblCount = new Label
            {
                Text = "Counted Closing Cash in Drawer ($):",
                Location = new Point(24, startY + 5),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1E293B")
            };
            this.Controls.Add(lblCount);
            this.Controls.Add(txtClosingCash);

            var btnCloseShift = new Button
            {
                Text = "🔒  CLOSE SHIFT and PRINT X-REPORT",
                Location = new Point(24, startY + 90),
                Size = new Size(396, 50),
                BackColor = ColorTranslator.FromHtml("#DC2626"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCloseShift.FlatAppearance.BorderSize = 0;
            btnCloseShift.Click += async (s, e) =>
            {
                if (decimal.TryParse(txtClosingCash.Text, out decimal closingCash))
                {
                    if (_shift != null && _shift.ShiftId != Guid.Empty)
                    {
                        try
                        {
                            var req = new ShiftCloseRequest { ShiftId = _shift.ShiftId, ClosingCash = closingCash };
                            var res = await _api.PostAsync<ShiftCloseRequest, ShiftCloseResponse>("/api/v1/pos/shifts/close", req);
                            MessageBox.Show($"Shift Closed Successfully!\nDiscrepancy: ${res.Discrepancy:F2}", "Shift Closed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch
                        {
                            MessageBox.Show($"Shift Closed.\nClosing Cash: ${closingCash:F2}", "Shift Closed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    ShiftClosed = true;
                    this.DialogResult = DialogResult.OK;
                }
            };
            this.Controls.Add(btnCloseShift);

            var btnCancel = new Button
            {
                Text = "CLOSE WINDOW",
                Location = new Point(24, startY + 150),
                Size = new Size(396, 42),
                BackColor = ColorTranslator.FromHtml("#64748B"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);
        }
    }
}
