using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CinemaPOS.Core.Models;

namespace CinemaPOS.App.Controls
{
    public class CartControl : UserControl
    {
        private DataGridView _grid;
        private Label _subtotalLabel;
        private Label _discountLabel;
        private Label _taxLabel;
        private Label _totalLabel;
        private Label _timerLabel;
        private Panel _emptyCartPanel;
        private System.Windows.Forms.Timer _holdTimer;
        private DateTime _holdExpiry;

        private List<CartItem> _items = new();
        public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

        public event EventHandler? CartChanged;
        public event EventHandler<CartItem>? ItemRemoved;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public decimal DiscountAmount { get; set; } = 0;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public decimal TaxRate { get; set; } = 0.10m; // 10% VAT / Sales Tax

        public decimal SubTotal => _items.Sum(i => i.Quantity * i.UnitPrice);
        public decimal TaxableSubTotal => Math.Max(0, SubTotal - DiscountAmount);
        public decimal TaxAmount => Math.Round(TaxableSubTotal * TaxRate, 2);
        public decimal TotalAmount => TaxableSubTotal + TaxAmount;

        public CartControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            this.Size = new Size(350, 600);

            // Hold Timer Bar Header
            _timerLabel = new Label
            {
                Text = "⏱️ Seat Hold: --:--",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ColorTranslator.FromHtml("#FEF3C7") // Light Amber
            };
            this.Controls.Add(_timerLabel);

            // Total Summary Card Panel (Bottom)
            var totalCard = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Dock = DockStyle.Bottom,
                Height = 165,
                Padding = new Padding(15, 10, 15, 10)
            };
            
            var borderLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };
            totalCard.Controls.Add(borderLine);
            
            _subtotalLabel = new Label 
            { 
                Text = "Sub Total: $0.00", 
                Location = new Point(15, 15), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = ColorTranslator.FromHtml("#475569")
            };
            
            _discountLabel = new Label 
            { 
                Text = "Discount: -$0.00", 
                Location = new Point(15, 42), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), 
                ForeColor = ColorTranslator.FromHtml("#DC2626") 
            };
            
            _taxLabel = new Label 
            { 
                Text = "Tax (10%): $0.00", 
                Location = new Point(15, 70), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = ColorTranslator.FromHtml("#475569")
            };
            
            _totalLabel = new Label 
            { 
                Text = "TOTAL: $0.00", 
                Location = new Point(15, 105), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 18f, FontStyle.Bold), 
                ForeColor = ColorTranslator.FromHtml("#0F172A") 
            };

            totalCard.Controls.Add(_subtotalLabel);
            totalCard.Controls.Add(_discountLabel);
            totalCard.Controls.Add(_taxLabel);
            totalCard.Controls.Add(_totalLabel);

            this.Controls.Add(totalCard);

            // Grid for Cart Line Items
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                ColumnHeadersVisible = true,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = ColorTranslator.FromHtml("#F1F5F9"),
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                    ForeColor = ColorTranslator.FromHtml("#475569"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Padding = new Padding(5)
                },
                DefaultCellStyle = new DataGridViewCellStyle 
                { 
                    Font = new Font("Segoe UI", 10f), 
                    Padding = new Padding(5, 8, 5, 8), 
                    SelectionBackColor = ColorTranslator.FromHtml("#EFF6FF"), 
                    SelectionForeColor = ColorTranslator.FromHtml("#0F172A"),
                    BackColor = Color.White,
                    ForeColor = ColorTranslator.FromHtml("#0F172A")
                },
                RowTemplate = new DataGridViewRow { Height = 46 }
            };
            
            _grid.Columns.Add("Desc", "Item Description");
            _grid.Columns.Add("QtyPrice", "Qty");
            _grid.Columns.Add("Total", "Price");

            var delCol = new DataGridViewButtonColumn
            {
                Name = "DeleteCol",
                HeaderText = "",
                Text = "✕",
                UseColumnTextForButtonValue = true,
                Width = 42,
                FlatStyle = FlatStyle.Flat
            };
            delCol.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#DC2626");
            delCol.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#DC2626");
            delCol.DefaultCellStyle.SelectionBackColor = Color.White;
            delCol.DefaultCellStyle.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            delCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(delCol);

            _grid.Columns[0].FillWeight = 52;
            _grid.Columns[1].FillWeight = 24;
            _grid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns[2].FillWeight = 24;
            _grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

            _grid.CellClick += Grid_CellClick;

            // Context menu to remove line items
            var contextMenu = new ContextMenuStrip();
            var removeMenuItem = new ToolStripMenuItem("❌ Remove Selected Item");
            removeMenuItem.Click += (s, e) => RemoveSelectedRow();
            contextMenu.Items.Add(removeMenuItem);
            _grid.ContextMenuStrip = contextMenu;

            this.Controls.Add(_grid);

            // Empty cart placeholder panel
            _emptyCartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            var lblEmptyIcon = new Label
            {
                Text = "🛒",
                Font = new Font("Segoe UI Emoji", 28f),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 60,
                ForeColor = ColorTranslator.FromHtml("#CBD5E1")
            };
            var lblEmptyTitle = new Label
            {
                Text = "Cart is Empty",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = ColorTranslator.FromHtml("#64748B")
            };
            var lblEmptyHint = new Label
            {
                Text = "Select a movie showtime to pick seats\nor choose concessions from F&B",
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill,
                ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                Padding = new Padding(20, 4, 20, 0),
                UseMnemonic = false
            };
            _emptyCartPanel.Controls.Add(lblEmptyHint);
            _emptyCartPanel.Controls.Add(lblEmptyTitle);
            _emptyCartPanel.Controls.Add(lblEmptyIcon);
            this.Controls.Add(_emptyCartPanel);

            _timerLabel.SendToBack();
            totalCard.SendToBack();
            _grid.BringToFront();
            _emptyCartPanel.BringToFront();

            _grid.Visible = false;
            _emptyCartPanel.Visible = true;

            _holdTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _holdTimer.Tick += HoldTimer_Tick;
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["DeleteCol"]?.Index)
            {
                RemoveItemAtIndex(e.RowIndex);
            }
        }

        private void HoldTimer_Tick(object? sender, EventArgs e)
        {
            var remaining = _holdExpiry - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                _timerLabel.Text = $"⏱️ Hold Timer: {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
                if (remaining.TotalMinutes < 2)
                {
                    _timerLabel.BackColor = ColorTranslator.FromHtml("#FEE2E2"); // Light red alert
                    _timerLabel.ForeColor = ColorTranslator.FromHtml("#DC2626");
                }
                else
                {
                    _timerLabel.BackColor = ColorTranslator.FromHtml("#FEF3C7");
                    _timerLabel.ForeColor = ColorTranslator.FromHtml("#0F172A");
                }
            }
            else
            {
                _timerLabel.Text = "⏱️ Seat Hold: EXPIRED";
                _timerLabel.BackColor = ColorTranslator.FromHtml("#FEE2E2");
                _timerLabel.ForeColor = ColorTranslator.FromHtml("#DC2626");
                _holdTimer.Stop();
            }
        }

        public void StartHoldTimer(DateTime expiry)
        {
            _holdExpiry = expiry;
            _holdTimer.Start();
        }

        public void StopHoldTimer()
        {
            _holdTimer.Stop();
            _timerLabel.Text = "⏱️ Seat Hold: --:--";
            _timerLabel.BackColor = ColorTranslator.FromHtml("#FEF3C7");
            _timerLabel.ForeColor = ColorTranslator.FromHtml("#0F172A");
        }

        public void AddItem(Guid id, string description, int quantity, decimal unitPrice, string type)
        {
            var existing = _items.FirstOrDefault(i => i.Id == id && i.Type == type);
            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                _items.Add(new CartItem { Id = id, Description = description, Quantity = quantity, UnitPrice = unitPrice, Type = type });
            }
            RefreshGrid();
            CartChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveItem(Guid id, string type)
        {
            var item = _items.FirstOrDefault(i => i.Id == id && i.Type == type);
            if (item != null)
            {
                _items.Remove(item);
                RefreshGrid();
                ItemRemoved?.Invoke(this, item);
                CartChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveItemAtIndex(int idx)
        {
            if (idx >= 0 && idx < _items.Count)
            {
                var removed = _items[idx];
                _items.RemoveAt(idx);
                RefreshGrid();
                ItemRemoved?.Invoke(this, removed);
                CartChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RemoveSelectedRow()
        {
            if (_grid.SelectedRows.Count > 0)
            {
                int idx = _grid.SelectedRows[0].Index;
                RemoveItemAtIndex(idx);
            }
        }

        public void DecrementItem(Guid id, string type, int count = 1)
        {
            var existing = _items.FirstOrDefault(i => i.Id == id && i.Type == type);
            if (existing != null)
            {
                existing.Quantity -= count;
                if (existing.Quantity <= 0)
                {
                    _items.Remove(existing);
                    ItemRemoved?.Invoke(this, existing);
                }
                RefreshGrid();
                CartChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int GetItemQuantity(Guid id, string type)
        {
            return _items.FirstOrDefault(i => i.Id == id && i.Type == type)?.Quantity ?? 0;
        }

        public void ClearTicketItems(bool notifyRemoved = true)
        {
            var removed = _items.Where(i => i.Type == "ticket").ToList();
            _items.RemoveAll(i => i.Type == "ticket");
            RefreshGrid();
            StopHoldTimer();
            if (notifyRemoved)
            {
                foreach (var r in removed)
                {
                    ItemRemoved?.Invoke(this, r);
                }
            }
            CartChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearCart(bool notifyRemoved = true)
        {
            var allRemoved = _items.ToList();
            _items.Clear();
            DiscountAmount = 0;
            RefreshGrid();
            StopHoldTimer();
            if (notifyRemoved)
            {
                foreach (var r in allRemoved)
                {
                    ItemRemoved?.Invoke(this, r);
                }
            }
            CartChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshGrid()
        {
            _grid.Rows.Clear();
            decimal subTotal = 0;

            if (_emptyCartPanel != null)
            {
                _emptyCartPanel.Visible = (_items.Count == 0);
                _grid.Visible = (_items.Count > 0);
            }

            foreach (var item in _items)
            {
                decimal rowTotal = item.Quantity * item.UnitPrice;
                subTotal += rowTotal;
                
                _grid.Rows.Add(item.Description, $"{item.Quantity} @ ${item.UnitPrice:F2}", $"${rowTotal:F2}");
            }

            decimal taxableSubTotal = Math.Max(0, subTotal - DiscountAmount);
            decimal tax = Math.Round(taxableSubTotal * TaxRate, 2);
            decimal total = taxableSubTotal + tax;

            _subtotalLabel.Text = $"Sub Total: ${subTotal:F2}";
            _discountLabel.Text = $"Discount: -${DiscountAmount:F2}";
            _taxLabel.Text = $"Tax (10%): ${tax:F2}";
            _totalLabel.Text = $"TOTAL: ${total:F2}";
            _totalLabel.ForeColor = total > 0 ? ColorTranslator.FromHtml("#16A34A") : ColorTranslator.FromHtml("#0F172A");
        }

        public List<OrderLineDto> GetOrderLines()
        {
            return _items.Select(i => new OrderLineDto
            {
                ProductId = i.Type == "fnb" ? i.Id : (Guid?)null,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineType = i.Type
            }).ToList();
        }
    }
}
