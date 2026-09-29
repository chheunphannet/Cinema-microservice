using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CinemaPOS.Core.Models;

namespace CinemaPOS.App.Controls
{
    public class SeatMapControl : Panel
    {
        private SeatMapResponseDto? _seatMap;
        private Dictionary<Guid, SeatDto> _seats = new();
        private Dictionary<Guid, Rectangle> _seatRects = new();
        private List<Guid> _selectedSeatIds = new();
        private Guid? _hoveredSeatId = null;

        public event EventHandler<SeatDto>? SeatClicked;

        private int _seatWidth = 35;
        private int _seatHeight = 35;
        private int _seatMargin = 5;
        private const int ScreenBarHeight = 36;
        
        // Modern Design System Colors (User-Configured Palette)
        public static readonly Color AvailableColor = ColorTranslator.FromHtml("#991B1B"); // Dark Red
        public static readonly Color BookedColor = ColorTranslator.FromHtml("#CBD5E1");    // Light Gray
        public static readonly Color VipColor = ColorTranslator.FromHtml("#EAB308");       // Gold
        public static readonly Color SelectedColor = ColorTranslator.FromHtml("#16A34A");  // Green
        public static readonly Color HeldColor = ColorTranslator.FromHtml("#38BDF8");      // Light Blue
        public static readonly Color BlockedColor = ColorTranslator.FromHtml("#64748B");   // Slate Grey
        public static readonly Color ScreenColor = ColorTranslator.FromHtml("#0F172A");    // Dark Navy Slate
        public static readonly Color HoverOutlineColor = ColorTranslator.FromHtml("#FACC15"); // Golden Yellow

        public SeatMapControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            AutoScroll = true;
            this.MouseMove += SeatMapControl_MouseMove;
            this.MouseLeave += (s, e) => { _hoveredSeatId = null; Invalidate(); };
        }

        public void LoadSeatMap(SeatMapResponseDto seatMap)
        {
            _seatMap = seatMap;
            _seats = seatMap.Seats?.ToDictionary(s => s.SeatId) ?? new Dictionary<Guid, SeatDto>();
            _selectedSeatIds.Clear();
            _hoveredSeatId = null;
            CalculateLayout();
            Invalidate();
        }

        public void SetSeatSelected(Guid seatId, bool selected)
        {
            if (selected && !_selectedSeatIds.Contains(seatId))
            {
                _selectedSeatIds.Add(seatId);
            }
            else if (!selected)
            {
                _selectedSeatIds.Remove(seatId);
            }
            Invalidate();
        }
        
        public void ClearSelectedSeats()
        {
            _selectedSeatIds.Clear();
            Invalidate();
        }

        private void CalculateLayout()
        {
            _seatRects.Clear();
            if (_seatMap == null || _seats.Count == 0) return;

            var rows = _seatMap.Seats.Select(s => s.Row).Distinct().OrderBy(r => r).ToList();
            if (rows.Count == 0) return;

            int maxSeatsInRow = _seatMap.Seats.GroupBy(s => s.Row).Max(g => g.Count());
            if (maxSeatsInRow <= 0) return;

            int clientW = this.ClientSize.Width > 0 ? this.ClientSize.Width : 660;

            // Space required for left and right row labels (24px letter + 6px gap each = 30px each) + safety margins
            int rowLabelMargin = 30;
            int minSidePadding = 16;
            int reservedWidth = (rowLabelMargin + minSidePadding) * 2; // 92px

            int availableForSeats = Math.Max(260, clientW - reservedWidth);

            // Compute seat slot (seat width + margin) so all seats fit cleanly without horizontal scrolling
            int maxSlot = (availableForSeats + 5) / maxSeatsInRow;
            int slot = Math.Clamp(maxSlot, 24, 40);

            _seatMargin = slot >= 36 ? 5 : (slot >= 30 ? 4 : 3);
            _seatWidth = slot - _seatMargin;
            _seatHeight = _seatWidth; // Maintain square seat ratio

            int maxRowWidth = maxSeatsInRow * (_seatWidth + _seatMargin) - _seatMargin;
            int totalContentWidth = maxRowWidth + (rowLabelMargin + minSidePadding) * 2;
            int totalHeight = ScreenBarHeight + 110 + rows.Count * (_seatHeight + _seatMargin) + 50;

            // Only enable horizontal scrolling if total content genuinely exceeds client width
            int scrollWidth = totalContentWidth > clientW ? totalContentWidth : 0;
            this.AutoScrollMinSize = new Size(scrollWidth, totalHeight);

            int startY = ScreenBarHeight + 96;
            int containerWidth = Math.Max(clientW, totalContentWidth);
            int startX = Math.Max(rowLabelMargin + minSidePadding, (containerWidth - maxRowWidth) / 2);

            foreach (var row in rows)
            {
                var seatsInRow = _seatMap.Seats.Where(s => s.Row == row).OrderBy(s => s.SeatNumber).ToList();
                int rowIndex = rows.IndexOf(row);
                int y = startY + rowIndex * (_seatHeight + _seatMargin);

                for (int i = 0; i < seatsInRow.Count; i++)
                {
                    var seat = seatsInRow[i];
                    int colIndex = (seat.SeatNumber > 0 && seat.SeatNumber <= maxSeatsInRow && seatsInRow.Count == maxSeatsInRow)
                        ? (seat.SeatNumber - 1)
                        : i;
                    int x = startX + colIndex * (_seatWidth + _seatMargin);
                    _seatRects[seat.SeatId] = new Rectangle(x, y, _seatWidth, _seatHeight);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CalculateLayout();
            Invalidate();
        }

        private void SeatMapControl_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_seatMap == null) return;
            Point clickPos = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
            Guid? newHovered = null;

            foreach (var kvp in _seatRects)
            {
                if (kvp.Value.Contains(clickPos))
                {
                    newHovered = kvp.Key;
                    break;
                }
            }

            if (newHovered != _hoveredSeatId)
            {
                _hoveredSeatId = newHovered;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_seatMap == null)
            {
                using (var placeholderBrush = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                using (var placeholderFont = new Font("Segoe UI", 12f, FontStyle.Italic))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("Please select a showtime to view seat availability.", placeholderFont, placeholderBrush, this.ClientRectangle, sf);
                }
                return;
            }

            int containerWidth = Math.Max(this.ClientSize.Width, this.AutoScrollMinSize.Width);
            
            // Draw Curved Screen
            int screenWidth = Math.Min(560, containerWidth - 100);
            int screenX = (containerWidth - screenWidth) / 2;
            var screenRect = new Rectangle(screenX, 14 + AutoScrollPosition.Y, screenWidth, 38);

            using (var path = new GraphicsPath())
            {
                path.AddArc(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height * 2, 180, 180);
                path.AddLine(screenRect.Right, screenRect.Bottom, screenRect.X, screenRect.Bottom);
                path.CloseFigure();
                
                using (var screenBrush = new SolidBrush(ScreenColor))
                {
                    e.Graphics.FillPath(screenBrush, path);
                }
            }

            using (var screenFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var textRect = new Rectangle(screenRect.X, screenRect.Y + 4, screenRect.Width, screenRect.Height - 10);
                e.Graphics.DrawString("SCREEN  /  STAGE", screenFont, Brushes.White, textRect, sf);
            }

            // Draw Legend
            int legendY = screenRect.Bottom + 16;
            using (var legendFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                var legendItems = new[] {
                    new { Name = "Available", Color = AvailableColor },
                    new { Name = "Selected", Color = SelectedColor },
                    new { Name = "VIP", Color = VipColor },
                    new { Name = "Held", Color = HeldColor },
                    new { Name = "Booked", Color = BookedColor }
                };
                
                int totalLegendWidth = legendItems.Length * 96;
                int currentX = (containerWidth - totalLegendWidth) / 2;

                foreach (var item in legendItems)
                {
                    using (var b = new SolidBrush(item.Color))
                    {
                        var legRect = new Rectangle(currentX, legendY, 15, 15);
                        e.Graphics.FillRectangle(b, legRect);
                        e.Graphics.DrawRectangle(Pens.Gray, legRect);
                    }
                    e.Graphics.DrawString(item.Name, legendFont, Brushes.Black, currentX + 20, legendY - 2);
                    currentX += 96;
                }
            }

            // Draw Row Letters on Both Left and Right Sides
            if (_seatMap?.Seats != null && _seatMap.Seats.Count > 0)
            {
                var rows = _seatMap.Seats.Select(s => s.Row).Distinct().OrderBy(r => r).ToList();
                using var rowFont = new Font("Segoe UI", 11f, FontStyle.Bold);
                using var rowBrush = new SolidBrush(ColorTranslator.FromHtml("#475569"));
                var rowFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                int minLeft = int.MaxValue;
                int maxRight = int.MinValue;
                foreach (var r in _seatRects.Values)
                {
                    if (r.X < minLeft) minLeft = r.X;
                    if (r.Right > maxRight) maxRight = r.Right;
                }

                if (minLeft != int.MaxValue && maxRight != int.MinValue)
                {
                    int leftLetterX = minLeft - 30 + AutoScrollPosition.X;
                    int rightLetterX = maxRight + 6 + AutoScrollPosition.X;

                    foreach (var row in rows)
                    {
                        var firstSeatInRow = _seatMap.Seats.FirstOrDefault(s => s.Row == row);
                        if (firstSeatInRow != null && _seatRects.TryGetValue(firstSeatInRow.SeatId, out var r))
                        {
                            int rowY = r.Y + AutoScrollPosition.Y;

                            // Left margin row letter
                            var leftRect = new Rectangle(leftLetterX, rowY, 24, _seatHeight);
                            e.Graphics.DrawString(row, rowFont, rowBrush, leftRect, rowFormat);

                            // Right margin row letter
                            var rightRect = new Rectangle(rightLetterX, rowY, 24, _seatHeight);
                            e.Graphics.DrawString(row, rowFont, rowBrush, rightRect, rowFormat);
                        }
                    }
                }
            }

            // Draw Seats
            float fontSize = _seatWidth >= 34 ? 9f : (_seatWidth >= 28 ? 8f : 7f);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            foreach (var kvp in _seatRects)
            {
                var seatId = kvp.Key;
                var rect = kvp.Value;
                
                // Adjust for scroll position
                rect.X += AutoScrollPosition.X;
                rect.Y += AutoScrollPosition.Y;

                var seat = _seats[seatId];

                Color bgColor = AvailableColor;
                if (string.Equals(seat.Status, "booked", StringComparison.OrdinalIgnoreCase)) bgColor = BookedColor;
                else if (string.Equals(seat.Status, "blocked", StringComparison.OrdinalIgnoreCase)) bgColor = BlockedColor;
                else if (string.Equals(seat.Status, "held", StringComparison.OrdinalIgnoreCase)) bgColor = HeldColor;
                
                if (_selectedSeatIds.Contains(seatId)) bgColor = SelectedColor;

                if (string.Equals(seat.Status, "available", StringComparison.OrdinalIgnoreCase) && 
                    string.Equals(seat.SeatType, "VIP", StringComparison.OrdinalIgnoreCase) && 
                    !_selectedSeatIds.Contains(seatId))
                {
                    bgColor = VipColor;
                }

                // Chair background shape (rounded rectangle)
                using (var brush = new SolidBrush(bgColor))
                {
                    var path = GetRoundedRectanglePath(rect, 6);
                    e.Graphics.FillPath(brush, path);
                    
                    if (_hoveredSeatId == seatId && (string.Equals(seat.Status, "available", StringComparison.OrdinalIgnoreCase) || _selectedSeatIds.Contains(seatId)))
                    {
                        using var hoverPen = new Pen(HoverOutlineColor, 3);
                        e.Graphics.DrawPath(hoverPen, path);
                    }
                    else
                    {
                        using var borderPen = new Pen(Color.FromArgb(50, Color.Black), 1);
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }

                // Chair armrest lines with adaptive contrast
                Color armrestColor = (bgColor == BookedColor || bgColor == VipColor) ? Color.FromArgb(40, Color.Black) : Color.FromArgb(80, Color.White);
                using (var pen = new Pen(armrestColor, 1.5f))
                {
                    int armTop = rect.Y + Math.Max(5, _seatHeight / 5);
                    int armBottom = rect.Bottom - Math.Max(5, _seatHeight / 5);
                    e.Graphics.DrawLine(pen, rect.X + 3, armTop, rect.X + 3, armBottom);
                    e.Graphics.DrawLine(pen, rect.Right - 3, armTop, rect.Right - 3, armBottom);
                }

                // Render only seat number inside the box
                string seatCode = seat.SeatNumber.ToString();
                Brush textBrush = (bgColor == BookedColor || bgColor == VipColor) ? Brushes.Black : Brushes.White;
                e.Graphics.DrawString(seatCode, font, textBrush, rect, format);
            }
        }

        private static GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_seatMap == null) return;

            Point clickPos = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);

            foreach (var kvp in _seatRects)
            {
                if (kvp.Value.Contains(clickPos))
                {
                    var seat = _seats[kvp.Key];
                    if (string.Equals(seat.Status, "available", StringComparison.OrdinalIgnoreCase) || _selectedSeatIds.Contains(seat.SeatId))
                    {
                        SeatClicked?.Invoke(this, seat);
                    }
                    break;
                }
            }
        }
    }
}
