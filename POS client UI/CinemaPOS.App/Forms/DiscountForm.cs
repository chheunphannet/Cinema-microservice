using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CinemaPOS.Core.Models;
using CinemaPOS.Core.Services;

namespace CinemaPOS.App.Forms
{
    public class DiscountForm : Form
    {
        private readonly ApiService _api;
        private readonly decimal _subTotal;

        private TextBox _txtVoucherCode;
        private TextBox _txtManualDiscount;
        private Label _lblDiscountSummary;

        public decimal AppliedDiscountAmount { get; private set; } = 0;
        public string? AppliedVoucherCode { get; private set; }

        public DiscountForm(ApiService api, decimal subTotal)
        {
            _api = api;
            _subTotal = subTotal;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Apply Order Discount / Promo Code";
            this.Size = new Size(440, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            this.Font = new Font("Segoe UI", 11f);

            var lblHeader = new Label
            {
                Text = "Apply Discount",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(lblHeader);

            // Voucher section
            var lblVoucher = new Label { Text = "Voucher / Promo Code:", Location = new Point(20, 65), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            this.Controls.Add(lblVoucher);

            _txtVoucherCode = new TextBox { Text = "CINEMA2026", Location = new Point(20, 92), Width = 260, Font = new Font("Segoe UI", 12f) };
            this.Controls.Add(_txtVoucherCode);

            var btnApplyVoucher = new Button
            {
                Text = "VERIFY",
                Location = new Point(290, 90),
                Size = new Size(110, 36),
                BackColor = ColorTranslator.FromHtml("#2563EB"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyVoucher.FlatAppearance.BorderSize = 0;
            btnApplyVoucher.Click += async (s, e) => await ValidateVoucherAsync();
            this.Controls.Add(btnApplyVoucher);

            // Manual discount section
            var lblManual = new Label { Text = "Or Manual Discount ($):", Location = new Point(20, 150), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            this.Controls.Add(lblManual);

            _txtManualDiscount = new TextBox { Text = "0.00", Location = new Point(20, 177), Width = 380, Font = new Font("Segoe UI", 12f) };
            _txtManualDiscount.TextChanged += (s, e) =>
            {
                if (decimal.TryParse(_txtManualDiscount.Text, out decimal val))
                {
                    AppliedDiscountAmount = Math.Min(_subTotal, Math.Max(0, val));
                    AppliedVoucherCode = null;
                    UpdateSummary();
                }
            };
            this.Controls.Add(_txtManualDiscount);

            // Summary
            _lblDiscountSummary = new Label
            {
                Text = $"Current Subtotal: ${_subTotal:F2}\nApplied Discount: -$0.00",
                Location = new Point(20, 230),
                Size = new Size(380, 50),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#16A34A")
            };
            this.Controls.Add(_lblDiscountSummary);

            // Buttons
            var btnConfirm = new Button
            {
                Text = "✓ APPLY DISCOUNT",
                Location = new Point(20, 300),
                Size = new Size(185, 46),
                BackColor = ColorTranslator.FromHtml("#16A34A"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) => this.DialogResult = DialogResult.OK;
            this.Controls.Add(btnConfirm);

            var btnCancel = new Button
            {
                Text = "CANCEL",
                Location = new Point(215, 300),
                Size = new Size(185, 46),
                BackColor = ColorTranslator.FromHtml("#64748B"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);
        }

        private async Task ValidateVoucherAsync()
        {
            string code = _txtVoucherCode.Text.Trim();
            if (string.IsNullOrEmpty(code)) return;

            try
            {
                var req = new VoucherValidateRequest { Code = code };
                var res = await _api.PostAsync<VoucherValidateRequest, VoucherValidateResponse>("/api/v1/loyalty/vouchers/validate", req);
                if (res != null && res.IsValid)
                {
                    decimal disc = res.DiscountType == "Percentage" ? (_subTotal * (res.DiscountValue / 100m)) : res.DiscountValue;
                    if (res.MaxDiscountAmount > 0 && disc > res.MaxDiscountAmount) disc = res.MaxDiscountAmount;

                    AppliedDiscountAmount = Math.Min(_subTotal, Math.Max(0, disc));
                    AppliedVoucherCode = code;
                    _txtManualDiscount.Text = AppliedDiscountAmount.ToString("F2");
                    MessageBox.Show($"Voucher '{code}' Applied!\n{res.Description}", "Voucher Validated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Invalid or expired voucher code.", "Invalid Voucher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch
            {
                // Offline fallback voucher check
                if (code.Equals("CINEMA2026", StringComparison.OrdinalIgnoreCase))
                {
                    AppliedDiscountAmount = Math.Min(_subTotal, 5.00m);
                    AppliedVoucherCode = code;
                    _txtManualDiscount.Text = AppliedDiscountAmount.ToString("F2");
                    MessageBox.Show("Voucher 'CINEMA2026' Applied! ($5.00 Grand Opening Discount)", "Voucher Validated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Could not validate voucher code.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            _lblDiscountSummary.Text = $"Current Subtotal: ${_subTotal:F2}\nApplied Discount: -${AppliedDiscountAmount:F2}";
        }
    }
}
