using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using CinemaPOS.App.Controls;
using CinemaPOS.Core.Models;
using CinemaPOS.Core.Services;

namespace CinemaPOS.App.Forms
{
    // ==========================================
    // POSTER CACHING & FALLBACK SERVICE
    // ==========================================
    public static class MoviePosterService
    {
        private static readonly ConcurrentDictionary<string, Image> _posterCache = new();
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

        public static string RewritePosterUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var rewritten = url.Replace("https://assets.cinema.local/posters/", "http://localhost:8080/api/v1/media/files/posters/")
                               .Replace("http://assets.cinema.local/posters/", "http://localhost:8080/api/v1/media/files/posters/");
            if (rewritten.StartsWith("/"))
            {
                rewritten = "http://localhost:8080" + rewritten;
            }
            else if (!rewritten.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !rewritten.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = "http://localhost:8080/api/v1/media/files/posters/" + rewritten.TrimStart('/');
            }
            return rewritten;
        }

        public static Image GenerateFallbackPoster(string title, int width = 100, int height = 150)
        {
            string cacheKey = $"fallback_{title}_{width}_{height}";
            if (_posterCache.TryGetValue(cacheKey, out var cached)) return cached;

            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Gradient background: Dark slate / navy
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height),
                    ColorTranslator.FromHtml("#1E293B"), ColorTranslator.FromHtml("#0F172A"), 45f))
                {
                    g.FillRectangle(brush, 0, 0, width, height);
                }

                // Clapperboard graphic at top
                using (var clapperBrush = new SolidBrush(ColorTranslator.FromHtml("#334155")))
                {
                    g.FillRectangle(clapperBrush, 8, 10, width - 16, 22);
                }

                // Diagonal stripes on clapperboard
                using (var stripeBrush = new SolidBrush(ColorTranslator.FromHtml("#94A3B8")))
                {
                    for (int x = 12; x < width - 16; x += 16)
                    {
                        var pts = new[] { new Point(x, 10), new Point(x + 7, 10), new Point(x + 2, 32), new Point(x - 5, 32) };
                        g.FillPolygon(stripeBrush, pts);
                    }
                }

                // Film icon symbol in center
                using (var iconFont = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (var iconBrush = new SolidBrush(ColorTranslator.FromHtml("#38BDF8")))
                {
                    var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("🎬", iconFont, iconBrush, new RectangleF(0, 36, width, 32), sfCenter);
                }

                // Title text wrapped nicely
                using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisWord
                    };
                    g.DrawString(title, font, brush, new RectangleF(4, 70, width - 8, height - 76), sf);
                }

                // Border
                using (var borderPen = new Pen(ColorTranslator.FromHtml("#475569"), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);
                }
            }

            _posterCache[cacheKey] = bmp;
            return bmp;
        }

        public static async Task<Image> LoadPosterAsync(string? url, string title, int width = 100, int height = 150)
        {
            string rewritten = RewritePosterUrl(url);
            string cacheKey = string.IsNullOrEmpty(rewritten) ? $"title_{title}_{width}_{height}" : $"{rewritten}_{width}_{height}";

            if (_posterCache.TryGetValue(cacheKey, out var existing))
            {
                return existing;
            }

            if (!string.IsNullOrEmpty(rewritten))
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(rewritten);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using var ms = new System.IO.MemoryStream(bytes);
                        using var img = Image.FromStream(ms);
                        var resized = new Bitmap(img, new Size(width, height));
                        _posterCache[cacheKey] = resized;
                        return resized;
                    }
                }
                catch
                {
                    // Fallback on download error or offline
                }
            }

            var fallback = GenerateFallbackPoster(title, width, height);
            _posterCache[cacheKey] = fallback;
            return fallback;
        }
    }

    // ==========================================
    // F&B IMAGE CACHING & PROCEDURAL SERVICE
    // ==========================================
    public static class FnbImageService
    {
        private static readonly ConcurrentDictionary<string, byte[]> _fnbBytesCache = new();
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }

        public static string RewriteFnbUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            var rewritten = url.Replace("https://assets.cinema.local/fnb/", "http://localhost:8080/api/v1/media/files/fnb/")
                               .Replace("http://assets.cinema.local/fnb/", "http://localhost:8080/api/v1/media/files/fnb/");
            if (rewritten.StartsWith("/"))
            {
                rewritten = "http://localhost:8080" + rewritten;
            }
            else if (!rewritten.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !rewritten.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = "http://localhost:8080/api/v1/media/files/fnb/" + rewritten.TrimStart('/');
            }
            return rewritten;
        }

        public static Image GenerateFallbackIcon(string name, string category, int width = 165, int height = 95)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color topColor;
                Color bottomColor;
                string iconText;
                string badgeText;

                string catLower = (category ?? "").ToLowerInvariant();
                string nameLower = (name ?? "").ToLowerInvariant();

                if (catLower.Contains("popcorn") || nameLower.Contains("popcorn"))
                {
                    topColor = ColorTranslator.FromHtml("#D97706");
                    bottomColor = ColorTranslator.FromHtml("#78350F");
                    iconText = "🍿";
                    badgeText = "POPCORN";
                }
                else if (catLower.Contains("combo") || nameLower.Contains("combo"))
                {
                    topColor = ColorTranslator.FromHtml("#7C3AED");
                    bottomColor = ColorTranslator.FromHtml("#4C1D95");
                    iconText = "🍿🥤";
                    badgeText = "COMBOS";
                }
                else if (catLower.Contains("beverage") || catLower.Contains("drink") || nameLower.Contains("cola") || nameLower.Contains("sprite") || nameLower.Contains("tea") || nameLower.Contains("water") || nameLower.Contains("dasani"))
                {
                    topColor = ColorTranslator.FromHtml("#0284C7");
                    bottomColor = ColorTranslator.FromHtml("#0C4A6E");
                    iconText = "🥤";
                    badgeText = "BEVERAGE";
                }
                else if (catLower.Contains("snack") || nameLower.Contains("nacho") || nameLower.Contains("dog") || nameLower.Contains("hotdog"))
                {
                    topColor = ColorTranslator.FromHtml("#EA580C");
                    bottomColor = ColorTranslator.FromHtml("#7C2D12");
                    iconText = "🌭";
                    badgeText = "SNACKS";
                }
                else
                {
                    topColor = ColorTranslator.FromHtml("#475569");
                    bottomColor = ColorTranslator.FromHtml("#1E293B");
                    iconText = "🍴";
                    badgeText = "F&B";
                }

                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), topColor, bottomColor, 45f))
                {
                    g.FillRectangle(brush, 0, 0, width, height);
                }

                using (var pen = new Pen(Color.FromArgb(40, Color.White), 1))
                {
                    g.DrawRectangle(pen, 1, 1, width - 3, height - 3);
                }

                // Top-right category pill badge
                using (var badgeBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
                {
                    var badgeRect = new Rectangle(width - 72, 6, 66, 16);
                    g.FillRectangle(badgeBrush, badgeRect);
                    using var badgeFont = new Font("Segoe UI", 6.5f, FontStyle.Bold);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(badgeText, badgeFont, Brushes.White, badgeRect, sf);
                }

                // Centered icon
                using (var iconFont = new Font("Segoe UI Emoji", 24f))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(iconText, iconFont, Brushes.White, new RectangleF(0, 8, width, height - 16), sf);
                }

                // Border
                using (var borderPen = new Pen(ColorTranslator.FromHtml("#CBD5E1"), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);
                }
            }

            return bmp;
        }

        public static async Task<Image> LoadFnbImageAsync(string? url, string name, string category, int width = 165, int height = 95)
        {
            string rewritten = RewriteFnbUrl(url);
            string cacheKey = string.IsNullOrEmpty(rewritten) ? $"fnb_{category}_{name}_{width}_{height}" : $"{rewritten}_{width}_{height}";

            if (_fnbBytesCache.TryGetValue(cacheKey, out var existingBytes))
            {
                using var ms = new System.IO.MemoryStream(existingBytes);
                return new Bitmap(ms);
            }

            if (!string.IsNullOrEmpty(rewritten))
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(rewritten);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using var ms = new System.IO.MemoryStream(bytes);
                        using var img = Image.FromStream(ms);
                        using var resized = new Bitmap(img, new Size(width, height));
                        using var outMs = new System.IO.MemoryStream();
                        resized.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
                        var pngBytes = outMs.ToArray();
                        _fnbBytesCache[cacheKey] = pngBytes;
                        return new Bitmap(new System.IO.MemoryStream(pngBytes));
                    }
                }
                catch
                {
                    // Fallback on download failure
                }
            }

            return GenerateFallbackIcon(name, category, width, height);
        }
    }

    public class MainPosForm : Form
    {
        public static string FindLogoPath() => LoginForm.FindLogoPath();

        public void SwitchViewForTesting(string viewName)
        {
            SwitchNavView(viewName);
        }

        public async Task OpenFirstShowtimeSeatMapForTestingAsync()
        {
            if (_showtimes != null && _showtimes.Count > 0)
            {
                var st = _showtimes.First();
                _currentShowtime = st;
                SwitchNavView("SeatMap");
                await LoadSeatMapAsync(st);
            }
        }

        public void SelectFirstAvailableSeatForTesting()
        {
            if (_seatMapControl != null)
            {
                var seats = typeof(Controls.SeatMapControl).GetField("_seats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_seatMapControl) as Dictionary<Guid, SeatDto>;
                if (seats != null)
                {
                    var avail = seats.Values.FirstOrDefault(s => s.Status.Equals("available", StringComparison.OrdinalIgnoreCase) && !s.SeatType.Equals("VIP", StringComparison.OrdinalIgnoreCase));
                    if (avail != null)
                    {
                        _seatMapControl.SetSeatSelected(avail.SeatId, true);
                    }
                }
            }
        }

        // --- Dependencies & API ---
        private readonly ApiService _api;

        // --- Layout Panels ---
        private Panel _leftNavPanel;
        private Panel _rightPanel;
        private Panel _centerAreaPanel;
        private Panel _centerHeaderPanel;
        private Panel _centerMainContainer;

        // --- Controls inside Center Main Container ---
        private Panel _movieGridPanel;
        private FlowLayoutPanel _movieFlowLayout;
        private SeatMapControl _seatMapControl;
        private Panel _seatMapContainerPanel;
        private Panel _seatMapHeader;
        private Button _btnBackToMovies;
        private Label _seatMapHeaderLabel;
        private FlowLayoutPanel _seatDemographicsFlow;
        private bool _isUpdatingSeatMapHeaderLayout;
        private Panel _fnbContainerPanel;
        private FlowLayoutPanel _fnbCategoryFlow;
        private FlowLayoutPanel _fnbGridFlowLayout;
        private Panel _reservationPanel;
        private DataGridView _gridRes;
        private TextBox _txtSearchRes;

        // --- Controls inside Right Panel ---
        private Panel _movieDetailPanel;
        private PictureBox _pbDetailPoster;
        private Label _lblDetailMovieTitle;
        private Label _lblDetailShowtime;
        private Label _lblDetailAuditorium;
        private Label _lblDetailPrice;
        private CartControl _cartControl;
        private Button _btnBuyAction;

        // --- Header & Filter Controls ---
        private Button _btnNowShowing;
        private Button _btnComingSoon;
        private TextBox _txtQuickSearch;
        private FlowLayoutPanel _dateFlowLayout;
        private List<Button> _dateButtons = new();
        private Label _lblStaffInfo;

        // --- Navigation Buttons (Left Sidebar) ---
        private Button _btnNavMovie;
        private Button _btnNavFnb;
        private Button _btnNavSeatMap;
        private Button _btnNavReservation;
        private Button _btnNavSettings;

        // --- State ---
        private List<ShowtimeDto> _showtimes = new();
        private List<MovieDto> _movies = new();
        private List<ProductDto> _fnbProducts = new();
        private List<TicketTypeDto> _ticketTypes = new();
        private TicketTypeDto? _activeTicketType;

        private string _activeCategoryFilter = "NowShowing"; // "NowShowing" or "ComingSoon"
        private DateTime _activeDate = DateTime.Today;       // Selected active date
        private string _searchFilter = string.Empty;
        private string _activeFnbCategory = "All";
        private string _activeNavView = "Movie";            // "Movie", "F&B", "SeatMap", "Reservation"

        private ShowtimeDto? _currentShowtime;
        private MovieDto? _currentMovie;
        private Guid? _currentHoldId;
        private string? _idempotencyKey;

        // Seeded offline reservations for search & resume
        private readonly List<BookingSearchResultItemDto> _mockReservations = new();
        private readonly Dictionary<Guid, Label> _fnbQtyLabels = new();

        public MainPosForm()
        {
            _api = LoginForm.Api ?? new ApiService(new HttpClient());

            // Double buffering to eliminate screen flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            InitializeComponent();
            this.Load += MainPosForm_Load;

            // Global Keyboard Shortcuts
            this.KeyPreview = true;
            this.KeyDown += MainPosForm_KeyDown;
        }

        private void InitializeComponent()
        {
            this.Text = "Cinema POS - Main POS Terminal";
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(1280, 800);
            this.BackColor = ColorTranslator.FromHtml("#EAF0F6");
            this.Font = new Font("Segoe UI", 10f);

            this.SuspendLayout();

            BuildLeftSidebarNav();
            BuildRightSidebarPanel();
            BuildCenterArea();

            _leftNavPanel.SendToBack();
            _rightPanel.SendToBack();
            _centerAreaPanel.BringToFront();

            this.Controls.SetChildIndex(_centerAreaPanel, 0);
            this.Controls.SetChildIndex(_rightPanel, 1);
            this.Controls.SetChildIndex(_leftNavPanel, 2);

            this.Resize += MainPosForm_Resize;

            this.ResumeLayout(false);
        }

        private void BuildCenterArea()
        {
            _centerAreaPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#F8FAFC")
            };

            BuildCenterHeaderAndFilters();
            _centerAreaPanel.Controls.Add(_centerHeaderPanel);
            _centerHeaderPanel.SendToBack();

            BuildCenterMainContainer();
            _centerAreaPanel.Controls.Add(_centerMainContainer);
            _centerMainContainer.BringToFront();

            this.Controls.Add(_centerAreaPanel);
        }

        // ==========================================
        // ==========================================
        // 1. LEFT SIDEBAR NAVIGATION PANEL
        // ==========================================
        private void BuildLeftSidebarNav()
        {
            _leftNavPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = ColorTranslator.FromHtml("#0F172A"), // Deep cinema slate
                Padding = new Padding(0)
            };

            // Brand / Logo Top Container
            var brandContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = ColorTranslator.FromHtml("#0F172A"),
                Padding = new Padding(14, 14, 14, 8)
            };
            brandContainer.Paint += (s, e) =>
            {
                using var p = new Pen(ColorTranslator.FromHtml("#1E293B"), 1);
                e.Graphics.DrawLine(p, 0, brandContainer.Height - 1, brandContainer.Width, brandContainer.Height - 1);
            };

            var pbLogo = new PictureBox
            {
                Location = new Point(14, 18),
                Size = new Size(36, 36),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            string logoPath = FindLogoPath();
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    pbLogo.Image = Image.FromFile(logoPath);
                }
                catch
                {
                    try
                    {
                        using var imgSharp = SixLabors.ImageSharp.Image.Load(logoPath);
                        using var ms = new System.IO.MemoryStream();
                        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(imgSharp, ms);
                        ms.Position = 0;
                        pbLogo.Image = new Bitmap(ms);
                    }
                    catch { }
                }
            }
            pbLogo.Click += (s, e) => SwitchNavView("Movie");
            brandContainer.Controls.Add(pbLogo);

            var lblBrandTitle = new Label
            {
                Name = "lblBrandTitle",
                Text = "POS Cinema",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(54, 24),
                Size = new Size(142, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            lblBrandTitle.Click += (s, e) => SwitchNavView("Movie");
            brandContainer.Controls.Add(lblBrandTitle);

            brandContainer.Cursor = Cursors.Hand;
            brandContainer.Click += (s, e) => SwitchNavView("Movie");
            _leftNavPanel.Controls.Add(brandContainer);

            var navStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10, 14, 10, 0),
                BackColor = ColorTranslator.FromHtml("#0F172A")
            };

            Button CreateNavButton(string text, string icon, string viewName, string shortcut)
            {
                var btn = new Button
                {
                    Name = $"btnNav_{viewName}",
                    AccessibleName = text,
                    Text = text,
                    Width = 180,
                    Height = 48,
                    Margin = new Padding(0, 0, 0, 8),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = (viewName == _activeNavView) ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#1E293B"),
                    ForeColor = (viewName == _activeNavView) ? Color.White : ColorTranslator.FromHtml("#94A3B8"),
                    Cursor = Cursors.Hand,
                    Tag = viewName,
                    UseMnemonic = false
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#334155");

                bool isHovered = false;
                btn.MouseEnter += (s, e) => { isHovered = true; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { isHovered = false; btn.Invalidate(); };

                btn.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    bool isActive = (_activeNavView == viewName);
                    Color bgColor = isActive
                        ? ColorTranslator.FromHtml("#2563EB")
                        : (isHovered ? ColorTranslator.FromHtml("#334155") : ColorTranslator.FromHtml("#1E293B"));
                    Color textColor = isActive
                        ? Color.White
                        : (isHovered ? Color.White : ColorTranslator.FromHtml("#94A3B8"));

                    using (var bgBrush = new SolidBrush(bgColor))
                    {
                        e.Graphics.FillRectangle(bgBrush, btn.ClientRectangle);
                    }

                    if (isActive)
                    {
                        using var accent = new SolidBrush(ColorTranslator.FromHtml("#38BDF8"));
                        e.Graphics.FillRectangle(accent, 0, 0, 4, btn.Height);
                    }

                    // Draw Icon
                    using (var fontIcon = new Font("Segoe UI Emoji", 11.5f))
                    using (var iconBrush = new SolidBrush(isActive ? Color.White : (isHovered ? Color.White : ColorTranslator.FromHtml("#38BDF8"))))
                    {
                        e.Graphics.DrawString(icon, fontIcon, iconBrush, new PointF(12, 11));
                    }

                    // Draw Label Text
                    using (var fontLabel = new Font("Segoe UI", 10f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        e.Graphics.DrawString(text, fontLabel, textBrush, new PointF(44, 13));
                    }

                    // Draw Shortcut Key Badge
                    using (var fontKey = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var keyBrush = new SolidBrush(isActive ? ColorTranslator.FromHtml("#93C5FD") : (isHovered ? ColorTranslator.FromHtml("#CBD5E1") : ColorTranslator.FromHtml("#64748B"))))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                        e.Graphics.DrawString(shortcut, fontKey, keyBrush, new RectangleF(btn.Width - 48, 0, 40, btn.Height), sf);
                    }
                };

                btn.Click += (s, e) => SwitchNavView(viewName);
                return btn;
            }

            _btnNavMovie = CreateNavButton("Movies", "🎬", "Movie", "F10");
            _btnNavSeatMap = CreateNavButton("Seat Map", "💺", "SeatMap", "F5");
            _btnNavFnb = CreateNavButton("F&B Menu", "🍿", "F&B", "F4");
            _btnNavReservation = CreateNavButton("Bookings", "📅", "Reservation", "F2");

            navStack.Controls.Add(_btnNavMovie);
            navStack.Controls.Add(_btnNavSeatMap);
            navStack.Controls.Add(_btnNavFnb);
            navStack.Controls.Add(_btnNavReservation);
            _leftNavPanel.Controls.Add(navStack);

            // Settings Button at bottom left
            _btnNavSettings = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#0F172A"),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnNavSettings.FlatAppearance.BorderSize = 0;
            _btnNavSettings.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1E293B");

            bool isSettingsHovered = false;
            _btnNavSettings.MouseEnter += (s, e) => { isSettingsHovered = true; _btnNavSettings.Invalidate(); };
            _btnNavSettings.MouseLeave += (s, e) => { isSettingsHovered = false; _btnNavSettings.Invalidate(); };

            _btnNavSettings.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color bgColor = isSettingsHovered ? ColorTranslator.FromHtml("#1E293B") : ColorTranslator.FromHtml("#0F172A");
                using (var bgBrush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillRectangle(bgBrush, _btnNavSettings.ClientRectangle);
                }

                using (var p = new Pen(ColorTranslator.FromHtml("#1E293B"), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, _btnNavSettings.Width, 0);
                }

                // Draw Icon
                using (var fontIcon = new Font("Segoe UI Emoji", 11.5f))
                using (var iconBrush = new SolidBrush(isSettingsHovered ? Color.White : ColorTranslator.FromHtml("#38BDF8")))
                {
                    e.Graphics.DrawString("⚙️", fontIcon, iconBrush, new PointF(12, 11));
                }

                // Draw Label Text
                using (var fontLabel = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(isSettingsHovered ? Color.White : ColorTranslator.FromHtml("#94A3B8")))
                {
                    e.Graphics.DrawString("Terminal Settings", fontLabel, textBrush, new PointF(38, 14));
                }

                // Draw Shortcut Key Badge
                using (var fontKey = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var keyBrush = new SolidBrush(isSettingsHovered ? ColorTranslator.FromHtml("#93C5FD") : ColorTranslator.FromHtml("#64748B")))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("F8", fontKey, keyBrush, new RectangleF(_btnNavSettings.Width - 46, 0, 38, _btnNavSettings.Height), sf);
                }
            };
            _btnNavSettings.Click += (s, e) => OpenSettingsDialog();
            _leftNavPanel.Controls.Add(_btnNavSettings);

            // In WinForms, controls with DockStyle.Top and DockStyle.Bottom must precede DockStyle.Fill in docking calculation
            brandContainer.SendToBack();
            _btnNavSettings.SendToBack();
            navStack.BringToFront();

            this.Controls.Add(_leftNavPanel);
        }

        // ==========================================
        // 2. RIGHT SIDEBAR PANEL (Movie Detail & Cart & Buy Action)
        // ==========================================
        private void BuildRightSidebarPanel()
        {
            _rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 400,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            var rightBorder = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = ColorTranslator.FromHtml("#CBD5E1") };
            _rightPanel.Controls.Add(rightBorder);

            // 2A. Top Section: Clean Harmonized "Movie Detail" Card
            _movieDetailPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 145,
                BackColor = Color.White,
                Padding = new Padding(12)
            };
            _movieDetailPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1);
                e.Graphics.DrawLine(pen, 0, _movieDetailPanel.Height - 1, _movieDetailPanel.Width, _movieDetailPanel.Height - 1);
            };

            // Poster PictureBox in Detail Card
            _pbDetailPoster = new PictureBox
            {
                Location = new Point(12, 12),
                Size = new Size(80, 120),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                BorderStyle = BorderStyle.FixedSingle,
                Image = null
            };
            _pbDetailPoster.Paint += (s, e) =>
            {
                if (_pbDetailPoster.Image == null)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using var bgBrush = new SolidBrush(ColorTranslator.FromHtml("#F1F5F9"));
                    e.Graphics.FillRectangle(bgBrush, _pbDetailPoster.ClientRectangle);

                    using var textBrush = new SolidBrush(ColorTranslator.FromHtml("#94A3B8"));
                    using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString("NO MOVIE\nSELECTED", font, textBrush, _pbDetailPoster.ClientRectangle, sf);
                }
            };
            _movieDetailPanel.Controls.Add(_pbDetailPoster);

            var lblHeaderTitle = new Label
            {
                Text = "SELECTED SHOWTIME",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2563EB"),
                Location = new Point(102, 10),
                Size = new Size(285, 18)
            };
            _movieDetailPanel.Controls.Add(lblHeaderTitle);

            _lblDetailMovieTitle = new Label
            {
                Text = "No Movie Selected",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(102, 28),
                Size = new Size(285, 38),
                AutoEllipsis = true
            };
            _movieDetailPanel.Controls.Add(_lblDetailMovieTitle);

            _lblDetailShowtime = new Label
            {
                Text = "Showtime: --:--",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorTranslator.FromHtml("#475569"),
                Location = new Point(102, 68),
                Size = new Size(285, 20),
                AutoEllipsis = true
            };
            _movieDetailPanel.Controls.Add(_lblDetailShowtime);

            _lblDetailAuditorium = new Label
            {
                Text = "Auditorium: --",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Location = new Point(102, 88),
                Size = new Size(285, 20),
                AutoEllipsis = true
            };
            _movieDetailPanel.Controls.Add(_lblDetailAuditorium);

            _lblDetailPrice = new Label
            {
                Text = "$0.00",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#16A34A"), // Crisp Green
                Location = new Point(102, 110),
                Size = new Size(285, 26)
            };
            _movieDetailPanel.Controls.Add(_lblDetailPrice);

            void UpdateDetailPanelLayout()
            {
                int labelW = Math.Max(150, _movieDetailPanel.ClientSize.Width - 114);
                lblHeaderTitle.Width = labelW;
                _lblDetailMovieTitle.Width = labelW;
                _lblDetailShowtime.Width = labelW;
                _lblDetailAuditorium.Width = labelW;
                _lblDetailPrice.Width = labelW;
            }
            _movieDetailPanel.Resize += (s, e) => UpdateDetailPanelLayout();
            _rightPanel.Controls.Add(_movieDetailPanel);
            UpdateDetailPanelLayout();

            // 2B. Bottom Action Area: "BUY" Section & Action Buttons
            var buyActionContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 148,
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Padding = new Padding(12)
            };
            var buyBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#CBD5E1") };
            buyActionContainer.Controls.Add(buyBorder);

            // Row 1: Quick Action Toolbar (Hold [F9], Void [F11], Discount [F3], Shift)
            var actionFlow = new FlowLayoutPanel
            {
                Location = new Point(12, 10),
                Size = new Size(376, 40),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            Button CreateSubActionButton(string text, string colorHex, bool isLast = false)
            {
                var btn = new Button
                {
                    Text = text,
                    Width = 88,
                    Height = 36,
                    Margin = isLast ? new Padding(0) : new Padding(0, 0, 8, 0),
                    Padding = Padding.Empty,
                    BackColor = ColorTranslator.FromHtml(colorHex),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                btn.FlatAppearance.BorderSize = 0;
                return btn;
            }

            var btnHold = CreateSubActionButton("Hold [F9]", "#2563EB");
            btnHold.Click += async (s, e) => await SyncHoldsAsync();

            var btnVoid = CreateSubActionButton("Void [F11]", "#DC2626");
            btnVoid.Click += (s, e) => VoidCurrentCart();

            var btnDiscount = CreateSubActionButton("Disc [F3]", "#D97706");
            btnDiscount.Click += (s, e) => OpenDiscountDialog();

            var btnShift = CreateSubActionButton("Shift", "#475569", true);
            btnShift.Click += (s, e) => OpenShiftSummaryDialog();

            actionFlow.Controls.Add(btnHold);
            actionFlow.Controls.Add(btnVoid);
            actionFlow.Controls.Add(btnDiscount);
            actionFlow.Controls.Add(btnShift);
            buyActionContainer.Controls.Add(actionFlow);

            // Row 2: Prominent "BUY / PAY [F12]" Button
            _btnBuyAction = new Button
            {
                Text = "🛒  PAY / BUY  [F12]  •  $0.00",
                Location = new Point(12, 56),
                Size = new Size(376, 76),
                BackColor = ColorTranslator.FromHtml("#16A34A"), // Action Green
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnBuyAction.FlatAppearance.BorderSize = 0;
            _btnBuyAction.Click += async (s, e) => await ProceedToCheckoutAsync();
            buyActionContainer.Controls.Add(_btnBuyAction);

            void UpdateBuyContainerLayout()
            {
                int w = Math.Max(200, buyActionContainer.ClientSize.Width - 24);
                actionFlow.Location = new Point(12, 10);
                actionFlow.Width = w;
                int subBtnW = Math.Max(60, (w - 24) / 4);
                btnHold.Width = subBtnW;
                btnVoid.Width = subBtnW;
                btnDiscount.Width = subBtnW;
                btnShift.Width = w - (subBtnW * 3 + 24);
                _btnBuyAction.Location = new Point(12, 56);
                _btnBuyAction.Width = w;
            }

            buyActionContainer.Resize += (s, e) => UpdateBuyContainerLayout();
            _rightPanel.Controls.Add(buyActionContainer);
            UpdateBuyContainerLayout();

            // 2C. Middle Section: Cart Control
            _cartControl = new CartControl
            {
                Dock = DockStyle.Fill
            };
            _cartControl.CartChanged += (s, e) =>
            {
                UpdateBuyButtonText();
                UpdateFnbCardQuantities();
            };
            _cartControl.ItemRemoved += (s, item) =>
            {
                if (item.Type == "ticket")
                {
                    _seatMapControl.SetSeatSelected(item.Id, false);
                    _ = SyncHoldsAsync();
                }
                UpdateFnbCardQuantities();
            };
            _rightPanel.Controls.Add(_cartControl);

            _rightPanel.Controls.SetChildIndex(_cartControl, 0);
            _rightPanel.Controls.SetChildIndex(buyActionContainer, 1);
            _rightPanel.Controls.SetChildIndex(_movieDetailPanel, 2);
            _rightPanel.Controls.SetChildIndex(rightBorder, 3);

            this.Controls.Add(_rightPanel);
        }

        // ==========================================
        // 3. CENTER HEADER & FILTERS BAR
        // ==========================================
        private Panel _staffBadgeContainer;

        private void BuildCenterHeaderAndFilters()
        {
            _centerHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 116,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 6)
            };

            var bottomLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };
            _centerHeaderPanel.Controls.Add(bottomLine);

            // Row 1 Container: Filter Tabs, Quick Search, and Staff Badge
            var row1Panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent
            };

            // Row 1 Left: "Now Showing" and "Coming Soon" Tabs
            _btnNowShowing = new Button
            {
                Text = "Now Showing",
                Location = new Point(0, 4),
                Size = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#2563EB"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnNowShowing.FlatAppearance.BorderSize = 0;
            _btnNowShowing.Click += (s, e) => SwitchCategoryFilter("NowShowing");
            row1Panel.Controls.Add(_btnNowShowing);

            _btnComingSoon = new Button
            {
                Text = "Coming Soon",
                Location = new Point(126, 4),
                Size = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                ForeColor = ColorTranslator.FromHtml("#475569"),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnComingSoon.FlatAppearance.BorderSize = 0;
            _btnComingSoon.Click += (s, e) => SwitchCategoryFilter("ComingSoon");
            row1Panel.Controls.Add(_btnComingSoon);

            // Row 1 Right: Staff & Till Info Badge (Clean Right Dock inside row 1)
            _staffBadgeContainer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                Height = 44,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            _lblStaffInfo = new Label
            {
                Text = $"👤 {LoginForm.CurrentUser?.DisplayName ?? "Cashier"}\nTill: {LoginForm.CurrentShift?.TerminalCode ?? "POS-01"}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopRight,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#334155"),
                Cursor = Cursors.Hand
            };
            _lblStaffInfo.Click += (s, e) => OpenSettingsDialog();
            _staffBadgeContainer.Controls.Add(_lblStaffInfo);
            row1Panel.Controls.Add(_staffBadgeContainer);

            // Row 1 Center: Quick Search Bar (dynamically positioned between tabs and badge)
            _txtQuickSearch = new TextBox
            {
                Location = new Point(254, 4),
                Size = new Size(320, 36),
                Font = new Font("Segoe UI", 10.5f),
                PlaceholderText = "🔍 Search movie, customer, or booking..."
            };
            _txtQuickSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtQuickSearch.Text.Trim();
                PopulateMovieGrid();
            };
            _txtQuickSearch.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    string q = _txtQuickSearch.Text.Trim();
                    if (!string.IsNullOrEmpty(q))
                    {
                        bool movieMatch = _movies.Any(m =>
                            string.Equals(m.Status, _activeCategoryFilter, StringComparison.OrdinalIgnoreCase) &&
                            (m.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Genre.Contains(q, StringComparison.OrdinalIgnoreCase)));
                        if (!movieMatch || q.StartsWith("HOLD", StringComparison.OrdinalIgnoreCase) || q.StartsWith("RES", StringComparison.OrdinalIgnoreCase) || q.Contains("@"))
                        {
                            SwitchNavView("Reservation");
                            if (_txtSearchRes != null) _txtSearchRes.Text = q;
                            await SearchBookingsAsync(q);
                        }
                    }
                }
            };
            row1Panel.Controls.Add(_txtQuickSearch);
            _centerHeaderPanel.Controls.Add(row1Panel);

            // Row 2: Dynamic Date Selector Bar (Generated from DateTime.Today)
            _dateFlowLayout = new FlowLayoutPanel
            {
                Location = new Point(16, 52),
                Size = new Size(720, 52),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false
            };

            RebuildDateButtons();
            _centerHeaderPanel.Controls.Add(_dateFlowLayout);

            _centerHeaderPanel.Resize += (s, e) => UpdateHeaderLayout();
        }

        private void UpdateHeaderLayout()
        {
            if (_centerHeaderPanel == null || _txtQuickSearch == null) return;
            int padding = 16;
            int availW = _centerHeaderPanel.ClientSize.Width - (padding * 2);
            int leftTabsW = 254;
            int rightStaffW = 200;
            int searchW = Math.Max(160, availW - leftTabsW - rightStaffW - 12);
            _txtQuickSearch.Width = searchW;

            if (_dateFlowLayout != null)
            {
                _dateFlowLayout.Location = new Point(16, 52);
                _dateFlowLayout.Width = Math.Max(500, availW);
                int dateAvailW = _dateFlowLayout.ClientSize.Width;
                int btnW = Math.Clamp((dateAvailW - (6 * 6)) / 7, 76, 110);
                foreach (Control c in _dateFlowLayout.Controls)
                {
                    if (c is Button btn)
                    {
                        btn.Width = btnW;
                    }
                }
            }
        }

        private void RebuildDateButtons()
        {
            _dateFlowLayout.Controls.Clear();
            _dateButtons.Clear();

            int availW = _dateFlowLayout.ClientSize.Width > 50 ? _dateFlowLayout.ClientSize.Width : 720;
            int btnW = Math.Clamp((availW - (6 * 6)) / 7, 76, 110);

            DateTime today = DateTime.Today;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = today.AddDays(i);
                string dayLabel = (i == 0) ? "Today" :
                                  (i == 1) ? "Tmrw" :
                                  $"{d:ddd}";
                string dateLabel = $"{d:dd MMM}";

                bool isSelected = (d.Date == _activeDate.Date);

                var dateBtn = new Button
                {
                    Text = $"{dayLabel}\n{dateLabel}",
                    Width = btnW,
                    Height = 46,
                    Margin = new Padding(0, 0, 6, 0),
                    Padding = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = isSelected ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F8FAFC"),
                    ForeColor = isSelected ? Color.White : ColorTranslator.FromHtml("#334155"),
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Tag = d
                };
                dateBtn.FlatAppearance.BorderColor = isSelected ? ColorTranslator.FromHtml("#1D4ED8") : ColorTranslator.FromHtml("#CBD5E1");
                dateBtn.Click += (s, e) => SwitchDateFilter(d);
                _dateButtons.Add(dateBtn);
                _dateFlowLayout.Controls.Add(dateBtn);
            }
        }

        // ==========================================
        // 4. CENTER MAIN VIEW CONTAINER
        // ==========================================
        private void BuildCenterMainContainer()
        {
            _centerMainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Padding = new Padding(12)
            };

            // View 1: Movies Grid Panel
            _movieGridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#F8FAFC")
            };
            _movieFlowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(4)
            };
            _movieFlowLayout.ClientSizeChanged += (s, e) => UpdateMovieCardSizes();
            _movieGridPanel.Controls.Add(_movieFlowLayout);
            _centerMainContainer.Controls.Add(_movieGridPanel);

            // View 2: Seat Map Panel with Demographic Selector
            _seatMapContainerPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            _seatMapHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = ColorTranslator.FromHtml("#E2E8F0"),
                Padding = new Padding(8, 6, 8, 6)
            };
            _seatMapHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#CBD5E1"), 1);
                e.Graphics.DrawLine(pen, 0, _seatMapHeader.Height - 1, _seatMapHeader.Width, _seatMapHeader.Height - 1);
            };
            _seatMapHeader.Resize += (s, e) => UpdateSeatMapHeaderLayout();

            _btnBackToMovies = new Button
            {
                Text = "← Back to Movies",
                Width = 140,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#1E293B"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnBackToMovies.FlatAppearance.BorderSize = 0;
            _btnBackToMovies.Click += (s, e) => SwitchNavView("Movie");
            _seatMapHeader.Controls.Add(_btnBackToMovies);

            _seatDemographicsFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Height = 36,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _seatMapHeader.Controls.Add(_seatDemographicsFlow);

            _seatMapHeaderLabel = new Label
            {
                Text = "Auditorium Seat Map Selection",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                AutoEllipsis = true
            };
            _seatMapHeaderLabel.TextChanged += (s, e) => UpdateSeatMapHeaderLayout();
            _seatMapHeader.Controls.Add(_seatMapHeaderLabel);
            _seatMapContainerPanel.Controls.Add(_seatMapHeader);

            _seatMapControl = new SeatMapControl { Dock = DockStyle.Fill };
            _seatMapControl.SeatClicked += SeatMapControl_SeatClicked;
            _seatMapContainerPanel.Controls.Add(_seatMapControl);
            _seatMapControl.BringToFront();
            _centerMainContainer.Controls.Add(_seatMapContainerPanel);

            UpdateSeatMapHeaderLayout();

            // View 3: F&B Concessions Grid View with Categories & Steppers
            _fnbContainerPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            _fnbCategoryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.White,
                Padding = new Padding(10, 6, 10, 6),
                FlowDirection = FlowDirection.LeftToRight
            };
            _fnbCategoryFlow.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1);
                e.Graphics.DrawLine(pen, 0, _fnbCategoryFlow.Height - 1, _fnbCategoryFlow.Width, _fnbCategoryFlow.Height - 1);
            };
            _fnbContainerPanel.Controls.Add(_fnbCategoryFlow);

            _fnbGridFlowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(12)
            };
            _fnbContainerPanel.Controls.Add(_fnbGridFlowLayout);
            _fnbCategoryFlow.SendToBack();
            _fnbGridFlowLayout.BringToFront();

            // Pre-seed F&B categories and products immediately so UI is never blank
            EnsureFnbProductsSeeded();
            PopulateFnbCategories();
            PopulateFnbGrid();

            _centerMainContainer.Controls.Add(_fnbContainerPanel);

            // View 4: Reservation Search/List Panel
            _reservationPanel = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(20) };
            BuildReservationView();
            _centerMainContainer.Controls.Add(_reservationPanel);
        }

        private void BuildDemographicButtons()
        {
            _seatDemographicsFlow.Controls.Clear();
            if (_ticketTypes == null || _ticketTypes.Count == 0)
            {
                UpdateSeatMapHeaderLayout();
                return;
            }

            if (_activeTicketType == null)
            {
                _activeTicketType = _ticketTypes.FirstOrDefault();
            }

            foreach (var tt in _ticketTypes)
            {
                bool isSel = (_activeTicketType?.Id == tt.Id);
                var btn = new Button
                {
                    Text = $"{tt.Name} ({(tt.PriceModifier >= 0 ? "+" : "")}${tt.PriceModifier:F2})",
                    Height = 34,
                    AutoSize = true,
                    Padding = new Padding(6, 0, 6, 0),
                    Margin = new Padding(3, 1, 3, 1),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = isSel ? ColorTranslator.FromHtml("#2563EB") : Color.White,
                    ForeColor = isSel ? Color.White : ColorTranslator.FromHtml("#0F172A"),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Tag = tt
                };
                btn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#CBD5E1");
                btn.Click += (s, e) =>
                {
                    _activeTicketType = tt;
                    BuildDemographicButtons();
                };
                _seatDemographicsFlow.Controls.Add(btn);
            }

            UpdateSeatMapHeaderLayout();
        }

        private void UpdateSeatMapHeaderLayout()
        {
            if (_isUpdatingSeatMapHeaderLayout) return;
            if (_seatMapHeader == null || _seatMapHeaderLabel == null || _btnBackToMovies == null || _seatDemographicsFlow == null)
                return;

            int availW = _seatMapHeader.ClientSize.Width > 0
                ? _seatMapHeader.ClientSize.Width
                : (_seatMapHeader.Parent?.ClientSize.Width ?? 0);
            if (availW <= 0) return;

            try
            {
                _isUpdatingSeatMapHeaderLayout = true;

                // 1. Calculate required width for all demographic buttons
                int demoW = 0;
                int maxBtnH = 34;
                foreach (Control c in _seatDemographicsFlow.Controls)
                {
                    if (c.Visible || !this.Visible)
                    {
                        int itemW = c.GetPreferredSize(Size.Empty).Width;
                        c.Size = new Size(itemW, 34);
                        if (c.Height > maxBtnH) maxBtnH = c.Height;
                        demoW += c.Width + c.Margin.Horizontal;
                    }
                }
                demoW += _seatDemographicsFlow.Padding.Horizontal;

                // 2. Measure required width for the header label text
                int labelTextW = 0;
                if (!string.IsNullOrEmpty(_seatMapHeaderLabel.Text))
                {
                    var font = _seatMapHeaderLabel.Font ?? this.Font;
                    Size textSize = TextRenderer.MeasureText(_seatMapHeaderLabel.Text, font);
                    labelTextW = textSize.Width;
                }

                int backBtnW = _btnBackToMovies.Width > 0 ? _btnBackToMovies.Width : 140;
                int gap = 12;
                int padding = 8;

                // Check if Back button, Movie/Showtime Title, and Demographics all fit comfortably on a single line
                int neededSingleLineWidth = padding + backBtnW + gap + labelTextW + gap + demoW + padding;

                _seatMapHeader.SuspendLayout();

                if ((availW >= neededSingleLineWidth && availW >= (backBtnW + demoW + 180)) || demoW == 0)
                {
                    // Single-row mode: Back button on Left, Demographic pills on Right, Title centered in between
                    _seatMapHeader.Height = 48;

                    int btnTop = Math.Max(0, (_seatMapHeader.Height - 34) / 2);
                    _btnBackToMovies.Location = new Point(padding, btnTop);
                    _btnBackToMovies.Size = new Size(backBtnW, 34);

                    _seatDemographicsFlow.AutoScroll = false;
                    _seatDemographicsFlow.AutoScrollPosition = new Point(0, 0);
                    _seatDemographicsFlow.WrapContents = false;
                    _seatDemographicsFlow.Size = new Size(Math.Max(demoW, 0), 36);
                    int demoX = availW - _seatDemographicsFlow.Width - padding;
                    _seatDemographicsFlow.Location = new Point(demoX, Math.Max(0, (_seatMapHeader.Height - 36) / 2));

                    int lblLeft = _btnBackToMovies.Right + gap;
                    int lblRight = (demoW > 0) ? (_seatDemographicsFlow.Left - gap) : (availW - padding);
                    int lblW = Math.Max(20, lblRight - lblLeft);
                    _seatMapHeaderLabel.Location = new Point(lblLeft, btnTop);
                    _seatMapHeaderLabel.Size = new Size(lblW, 34);
                    _seatMapHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
                }
                else
                {
                    // Two-tier responsive mode: Wraps cleanly into two rows to prevent overlapping or truncating
                    int row2W = Math.Max(50, availW - (padding * 2));
                    bool needsScroll = demoW > row2W;
                    int scrollBarH = needsScroll ? SystemInformation.HorizontalScrollBarHeight : 0;
                    int flowH = maxBtnH + 2 + scrollBarH;

                    _seatMapHeader.Height = 44 + flowH + 4;

                    _btnBackToMovies.Location = new Point(padding, 5);
                    _btnBackToMovies.Size = new Size(backBtnW, 34);

                    int lblLeft = _btnBackToMovies.Right + gap;
                    int lblW = Math.Max(20, availW - lblLeft - padding);
                    _seatMapHeaderLabel.Location = new Point(lblLeft, 5);
                    _seatMapHeaderLabel.Size = new Size(lblW, 34);
                    _seatMapHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;

                    _seatDemographicsFlow.AutoScroll = needsScroll;
                    if (!needsScroll)
                    {
                        _seatDemographicsFlow.AutoScrollPosition = new Point(0, 0);
                    }
                    _seatDemographicsFlow.WrapContents = false;
                    _seatDemographicsFlow.Location = new Point(padding, 44);
                    _seatDemographicsFlow.Size = new Size(row2W, flowH);
                }

                _seatMapHeader.ResumeLayout(true);
                _seatMapHeader.Invalidate();
            }
            finally
            {
                _isUpdatingSeatMapHeaderLayout = false;
            }
        }

        private void BuildReservationView()
        {
            var lblResTitle = new Label
            {
                Text = "Active Reservations and Bookings Lookup",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(10, 10),
                AutoSize = true
            };
            _reservationPanel.Controls.Add(lblResTitle);

            _txtSearchRes = new TextBox
            {
                Font = new Font("Segoe UI", 11f),
                Location = new Point(10, 44),
                Width = 360,
                PlaceholderText = "Search by Reference, Email, or Customer..."
            };
            _txtSearchRes.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    await SearchBookingsAsync(_txtSearchRes.Text.Trim());
                }
            };
            _reservationPanel.Controls.Add(_txtSearchRes);

            var btnSearchRes = new Button
            {
                Text = "🔍 SEARCH",
                Location = new Point(380, 42),
                Size = new Size(110, 34),
                BackColor = ColorTranslator.FromHtml("#2563EB"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSearchRes.FlatAppearance.BorderSize = 0;
            btnSearchRes.Click += async (s, e) => await SearchBookingsAsync(_txtSearchRes.Text.Trim());
            _reservationPanel.Controls.Add(btnSearchRes);

            var btnResumeBooking = new Button
            {
                Text = "📥 RESUME BOOKING",
                Location = new Point(500, 42),
                Size = new Size(180, 34),
                BackColor = ColorTranslator.FromHtml("#16A34A"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResumeBooking.FlatAppearance.BorderSize = 0;
            btnResumeBooking.Click += (s, e) => ResumeSelectedBooking();
            _reservationPanel.Controls.Add(btnResumeBooking);

            _gridRes = new DataGridView
            {
                Location = new Point(10, 88),
                Size = new Size(Math.Max(400, _reservationPanel.ClientSize.Width - 20), Math.Max(200, _reservationPanel.ClientSize.Height - 100)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                RowTemplate = new DataGridViewRow { Height = 40 },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                    ForeColor = ColorTranslator.FromHtml("#475569"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Padding = new Padding(5)
                }
            };

            _gridRes.Columns.Add("Ref", "Reference / Hold ID");
            _gridRes.Columns.Add("Movie", "Movie / Showtime");
            _gridRes.Columns.Add("Auditorium", "Auditorium");
            _gridRes.Columns.Add("Seats", "Seats");
            _gridRes.Columns.Add("Amount", "Total");
            _gridRes.Columns.Add("Status", "Status");
            _gridRes.Columns.Add("Customer", "Customer Email");

            _gridRes.DoubleClick += (s, e) => ResumeSelectedBooking();
            _reservationPanel.Controls.Add(_gridRes);
            _reservationPanel.Resize += (s, e) => UpdateReservationLayout();
        }

        private void UpdateReservationLayout()
        {
            if (_reservationPanel == null || _gridRes == null) return;
            int pnlW = _reservationPanel.ClientSize.Width;
            int pnlH = _reservationPanel.ClientSize.Height;
            if (pnlW > 50 && pnlH > 100)
            {
                _gridRes.Width = Math.Max(400, pnlW - 20);
                _gridRes.Height = Math.Max(200, pnlH - 100);
            }
        }

        private async Task SearchBookingsAsync(string query)
        {
            _gridRes.Rows.Clear();
            List<BookingSearchResultItemDto> results = new();

            try
            {
                var branchId = LoginForm.CurrentUser?.BranchId;
                string url = $"/api/v1/admin/bookings/search?query={Uri.EscapeDataString(query)}";
                if (branchId.HasValue && branchId.Value != Guid.Empty)
                {
                    url += $"&branchId={branchId.Value}";
                }
                var res = await _api.GetAsync<BookingSearchResponseDto>(url);
                if (res?.Items != null && res.Items.Count > 0)
                {
                    results = res.Items;
                }
            }
            catch
            {
                // Fallback local search if backend is offline or unauthenticated
            }

            if (results.Count == 0)
            {
                results = _mockReservations.Where(r =>
                    string.IsNullOrWhiteSpace(query) ||
                    r.BookingReference.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.CustomerEmail.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.MovieTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var item in results)
            {
                int rowIdx = _gridRes.Rows.Add(
                    item.BookingReference,
                    $"{item.MovieTitle} ({item.ShowtimeStart:HH:mm})",
                    item.AuditoriumName,
                    $"{item.SeatsCount} seats",
                    $"${item.TotalAmount:F2}",
                    item.Status.ToUpper(),
                    item.CustomerEmail
                );
                _gridRes.Rows[rowIdx].Tag = item;
            }
        }

        private async void ResumeSelectedBooking()
        {
            if (_gridRes.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a booking to resume.", "Select Booking", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_gridRes.SelectedRows[0].Tag is BookingSearchResultItemDto item)
            {
                // 1. Try to fetch full booking detail with real seats from the API
                BookingDetailResponseDto? detail = null;
                try
                {
                    detail = await _api.GetAsync<BookingDetailResponseDto>($"/api/v1/admin/bookings/{item.ReservationId}");
                }
                catch { }

                // Find matching movie & showtime
                var movie = _movies.FirstOrDefault(m => m.Title.Contains(item.MovieTitle, StringComparison.OrdinalIgnoreCase)) ?? _movies.FirstOrDefault();
                var st = movie?.Showtimes.FirstOrDefault(s => s.AuditoriumName == item.AuditoriumName) ?? movie?.Showtimes.FirstOrDefault();

                if (movie != null && st != null)
                {
                    SelectMovieAndShowtime(movie, st);
                    await LoadSeatMapAsync(st);
                }

                _currentHoldId = item.ReservationId;

                // Add ticket item to cart using real seats
                _cartControl.ClearTicketItems(notifyRemoved: false);

                if (detail?.Seats != null && detail.Seats.Count > 0)
                {
                    foreach (var seat in detail.Seats)
                    {
                        string desc = $"Ticket: {item.MovieTitle} - Seat {seat.RowLabel}{seat.SeatNumber} ({seat.SeatType})";
                        decimal price = seat.Price > 0 ? seat.Price : (item.TotalAmount / detail.Seats.Count);
                        _cartControl.AddItem(seat.SeatId, desc, 1, price, "ticket");
                        _seatMapControl.SetSeatSelected(seat.SeatId, true);
                    }
                }
                else if (item.SeatIds != null && item.SeatIds.Count > 0)
                {
                    decimal unitPrice = item.TotalAmount / item.SeatIds.Count;
                    for (int i = 0; i < item.SeatIds.Count; i++)
                    {
                        var sId = item.SeatIds[i];
                        string desc = $"Ticket: {item.MovieTitle} - Reserved Seat #{i + 1} ({item.BookingReference})";
                        _cartControl.AddItem(sId, desc, 1, unitPrice, "ticket");
                        _seatMapControl.SetSeatSelected(sId, true);
                    }
                }
                else
                {
                    decimal unitPrice = (item.SeatsCount > 0) ? (item.TotalAmount / item.SeatsCount) : item.TotalAmount;
                    for (int i = 1; i <= Math.Max(1, item.SeatsCount); i++)
                    {
                        var seatId = Guid.NewGuid();
                        string desc = $"Ticket: {item.MovieTitle} - Seat #{i} ({item.BookingReference})";
                        _cartControl.AddItem(seatId, desc, 1, unitPrice, "ticket");
                    }
                }

                _cartControl.StartHoldTimer(DateTime.Now.AddMinutes(10));
                SwitchNavView("Movie");
                MessageBox.Show($"Loaded booking {item.BookingReference} into active cart.", "Booking Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ==========================================
        // DATA INITIALIZATION & CATALOG SEEDING
        // ==========================================
        private async void MainPosForm_Load(object? sender, EventArgs e)
        {
            try
            {
                var branchId = LoginForm.CurrentUser?.BranchId;

                // 1. Fetch Ticket Types
                try
                {
                    _ticketTypes = await _api.GetAsync<List<TicketTypeDto>>("/api/v1/catalog/ticket-types") ?? new List<TicketTypeDto>();
                }
                catch { }

                if (_ticketTypes.Count == 0)
                {
                    _ticketTypes = new List<TicketTypeDto>
                    {
                        new TicketTypeDto { Id = Guid.NewGuid(), Name = "Adult", Label = "Standard Adult", PriceModifier = 0.00m },
                        new TicketTypeDto { Id = Guid.NewGuid(), Name = "Student", Label = "Student Discount", PriceModifier = -1.50m },
                        new TicketTypeDto { Id = Guid.NewGuid(), Name = "Child", Label = "Child Ticket", PriceModifier = -2.00m },
                        new TicketTypeDto { Id = Guid.NewGuid(), Name = "VIP Upgrade", Label = "VIP Recliner", PriceModifier = 3.00m }
                    };
                }
                _activeTicketType = _ticketTypes.FirstOrDefault();
                BuildDemographicButtons();

                // 2. Fetch Movies Catalog
                try
                {
                    var apiMovies = await _api.GetAsync<List<MovieDto>>("/api/v1/catalog/movies");
                    if (apiMovies != null && apiMovies.Count > 0)
                    {
                        _movies = apiMovies;
                    }
                }
                catch { }

                // 3. Fetch Showtimes Schedules
                try
                {
                    string showtimesUrl = (branchId.HasValue && branchId.Value != Guid.Empty)
                        ? $"/api/v1/catalog/showtimes?branchId={branchId.Value}"
                        : "/api/v1/catalog/showtimes";
                    _showtimes = await _api.GetAsync<List<ShowtimeDto>>(showtimesUrl) ?? new List<ShowtimeDto>();
                }
                catch { }

                // 4. Group and assign showtimes into movie.Showtimes by MovieId
                if (_movies.Count > 0 && _showtimes.Count > 0)
                {
                    foreach (var movie in _movies)
                    {
                        movie.Showtimes = _showtimes.Where(st => st.MovieId == movie.MovieId).ToList();
                        foreach (var st in movie.Showtimes)
                        {
                            if (string.IsNullOrEmpty(st.MovieTitle)) st.MovieTitle = movie.Title;
                            if (string.IsNullOrEmpty(st.ScreenType))
                            {
                                st.ScreenType = st.AuditoriumName?.Contains("IMAX", StringComparison.OrdinalIgnoreCase) == true ? "IMAX" :
                                                st.AuditoriumName?.Contains("ATMOS", StringComparison.OrdinalIgnoreCase) == true ? "ATMOS" :
                                                st.AuditoriumName?.Contains("SCREENX", StringComparison.OrdinalIgnoreCase) == true ? "SCREENX" : "2D";
                            }
                        }
                    }
                }

                // 5. Fallback seed catalog mock data if offline or empty
                EnsureCatalogDataSeeded();

                // Ensure all NowShowing movies have operational schedules across active dates
                DateTime baseScheduleDate = DateTime.Today;
                foreach (var m in _movies.Where(m => string.Equals(m.Status, "NowShowing", StringComparison.OrdinalIgnoreCase)))
                {
                    m.Showtimes ??= new List<ShowtimeDto>();
                    for (int offset = 0; offset < 7; offset++)
                    {
                        DateTime day = baseScheduleDate.AddDays(offset);
                        if (!m.Showtimes.Any(st => st.StartTime.Date == day.Date))
                        {
                            m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 1 (SCREENX)", ScreenType = "SCREENX", StartTime = day.AddHours(10).AddMinutes(30), BasePrice = 6.50m });
                            m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 2 (DOLBY ATMOS)", ScreenType = "ATMOS", StartTime = day.AddHours(13).AddMinutes(45), BasePrice = 7.50m });
                            m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 1 (SCREENX)", ScreenType = "SCREENX", StartTime = day.AddHours(17).AddMinutes(00), BasePrice = 6.50m });
                            m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 3 (VIP IMAX)", ScreenType = "IMAX", StartTime = day.AddHours(20).AddMinutes(15), BasePrice = 9.50m });
                        }
                    }
                }
                _showtimes = _movies.SelectMany(m => m.Showtimes).ToList();

                // 6. Fetch F&B Products
                try
                {
                    string fnbUrl = (branchId.HasValue && branchId.Value != Guid.Empty)
                        ? $"/api/v1/pos/products?branchId={branchId.Value}"
                        : "/api/v1/pos/products";
                    var prods = await _api.GetAsync<List<ProductDto>>(fnbUrl);
                    if (prods == null || prods.Count == 0)
                    {
                        // Fallback to all active products catalog if branch stock query returned empty
                        prods = await _api.GetAsync<List<ProductDto>>("/api/v1/pos/products");
                    }

                    if (prods != null && prods.Count > 0)
                    {
                        _fnbProducts = prods
                            .GroupBy(p => !string.IsNullOrEmpty(p.Sku) ? p.Sku : p.Name)
                            .Select(g => g.First())
                            .ToList();
                    }
                }
                catch { }

                EnsureFnbProductsSeeded();

                // 7. Seed mock reservations for offline lookup
                SeedMockReservations();

                // Initial UI Rendering
                PopulateMovieGrid();
                PopulateFnbCategories();
                PopulateFnbGrid();
                SwitchNavView("Movie");
                UpdateHeaderLayout();
                UpdateMovieCardSizes();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Initialization Notice: " + ex.Message, "Catalog Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void EnsureCatalogDataSeeded()
        {
            if (_movies.Count > 0) return;

            // Seed Movies A through F with decimal Rating
            var mA = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie A (Avengers: Endgame)", Genre = "Action / Sci-Fi", Rating = 8.4m, DurationMinutes = 181, Status = "NowShowing" };
            var mB = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie B (Avatar: Fire & Ash)", Genre = "Adventure / Sci-Fi", Rating = 7.8m, DurationMinutes = 190, Status = "NowShowing" };
            var mC = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie C (Spider-Man: Beyond)", Genre = "Animation / Action", Rating = 8.7m, DurationMinutes = 140, Status = "NowShowing" };
            var mD = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie D (The Dark Knight)", Genre = "Action / Crime", Rating = 9.0m, DurationMinutes = 152, Status = "NowShowing" };
            var mE = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie E (Oppenheimer)", Genre = "Biography / Drama", Rating = 8.9m, DurationMinutes = 180, Status = "ComingSoon" };
            var mF = new MovieDto { MovieId = Guid.NewGuid(), Title = "Movie F (Interstellar)", Genre = "Sci-Fi / Drama", Rating = 8.6m, DurationMinutes = 169, Status = "ComingSoon" };

            _movies = new List<MovieDto> { mA, mB, mC, mD, mE, mF };

            // Seed showtimes starting from DateTime.Today
            DateTime baseDate = DateTime.Today;
            int[] dayOffsets = new[] { 0, 1, 2, 3, 4, 5 };

            // Only add showtimes for NowShowing movies (prevent booking unreleased ComingSoon titles)
            foreach (var m in _movies.Where(m => m.Status.Equals("NowShowing", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (int offset in dayOffsets)
                {
                    DateTime day = baseDate.AddDays(offset);
                    m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 1 (SCREENX)", ScreenType = "SCREENX", StartTime = day.AddHours(10), BasePrice = 6.50m });
                    m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 2 (DOLBY ATMOS)", ScreenType = "ATMOS", StartTime = day.AddHours(13.5), BasePrice = 7.50m });
                    m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 1 (SCREENX)", ScreenType = "SCREENX", StartTime = day.AddHours(16), BasePrice = 6.50m });
                    m.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = m.MovieId, MovieTitle = m.Title, AuditoriumName = "Hall 3 (VIP IMAX)", ScreenType = "IMAX", StartTime = day.AddHours(19.5), BasePrice = 9.50m });
                }
            }

            if (_showtimes.Count == 0)
            {
                _showtimes = _movies.SelectMany(m => m.Showtimes).ToList();
            }
        }

        private void EnsureFnbProductsSeeded()
        {
            if (_fnbProducts.Count > 0 && _fnbProducts.Any(p => p.Price > 0)) return;

            _fnbProducts = new List<ProductDto>
            {
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "POP-CAR-L", Name = "Caramel Popcorn (Large)", Category = "Popcorn", Price = 4.50m, ImageUrl = "https://images.unsplash.com/photo-1578849278619-e73505e9610f?w=400&auto=format&fit=crop&q=80", Description = "Freshly popped golden corn coated with rich butter caramel" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "POP-CHS-L", Name = "Cheese Popcorn (Large)", Category = "Popcorn", Price = 4.50m, ImageUrl = "https://images.unsplash.com/photo-1512149177596-f817c7ef5d4c?w=400&auto=format&fit=crop&q=80", Description = "Savory cheddar cheese coated warm popcorn" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "POP-SWT-M", Name = "Sweet Popcorn (Medium)", Category = "Popcorn", Price = 3.50m, ImageUrl = "https://images.unsplash.com/photo-1585647347384-2593bc35786b?w=400&auto=format&fit=crop&q=80", Description = "Classic sweet crunchy cinema popcorn" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "BEV-COK-L", Name = "Coca-Cola 32oz", Category = "Beverages", Price = 2.50m, ImageUrl = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=400&auto=format&fit=crop&q=80", Description = "Chilled refreshing fountain Coca-Cola" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "BEV-CKZ-L", Name = "Coca-Cola Zero Sugar 32oz", Category = "Beverages", Price = 2.50m, ImageUrl = "https://images.unsplash.com/photo-1554866585-cd94860890b7?w=400&auto=format&fit=crop&q=80", Description = "Zero sugar refreshing ice cold Coca-Cola" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "BEV-SPR-L", Name = "Sprite 32oz", Category = "Beverages", Price = 2.50m, ImageUrl = "https://images.unsplash.com/photo-1625772299848-391b6a87d7b3?w=400&auto=format&fit=crop&q=80", Description = "Crisp lemon-lime fountain soda" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "BEV-WTR-500", Name = "Dasani Mineral Water 500ml", Category = "Beverages", Price = 1.20m, ImageUrl = "https://images.unsplash.com/photo-1548839140-29a749e1bc4e?w=400&auto=format&fit=crop&q=80", Description = "Pure bottled mineral water" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "CMB-HDOG", Name = "Jumbo Hotdog Combo", Category = "Combos", Price = 6.50m, ImageUrl = "https://images.unsplash.com/photo-1619740455993-9e612b1af08a?w=400&auto=format&fit=crop&q=80", Description = "1 Grilled Jumbo Hotdog + 1 Medium Popcorn + 1 Drink" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "CMB-CPL", Name = "Legend Couple Combo", Category = "Combos", Price = 7.50m, ImageUrl = "https://images.unsplash.com/photo-1586190848861-99aa4a171e90?w=400&auto=format&fit=crop&q=80", Description = "1 Large Popcorn + 2 Large Fountain Drinks" },
                new ProductDto { ProductId = Guid.NewGuid(), Sku = "SNK-NCH", Name = "Warm Cheese Nachos", Category = "Snacks", Price = 4.00m, ImageUrl = "https://images.unsplash.com/photo-1513456852971-30c0b8199d4d?w=400&auto=format&fit=crop&q=80", Description = "Tortilla chips served with hot cheddar cheese dip and jalapeños" }
            };
        }

        private void SeedMockReservations()
        {
            if (_mockReservations.Count > 0) return;

            _mockReservations.Add(new BookingSearchResultItemDto
            {
                ReservationId = Guid.NewGuid(),
                BookingReference = "HOLD-9821",
                MovieTitle = "Avengers: Endgame",
                AuditoriumName = "Hall 1 (SCREENX)",
                ShowtimeStart = DateTimeOffset.Now.AddHours(2),
                SeatsCount = 2,
                TotalAmount = 13.00m,
                Status = "held",
                CustomerEmail = "customer1@gmail.com",
                CreatedAt = DateTimeOffset.Now.AddMinutes(-5),
                SeatIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
            });

            _mockReservations.Add(new BookingSearchResultItemDto
            {
                ReservationId = Guid.NewGuid(),
                BookingReference = "HOLD-4410",
                MovieTitle = "Spider-Man: Beyond",
                AuditoriumName = "Hall 2 (DOLBY ATMOS)",
                ShowtimeStart = DateTimeOffset.Now.AddHours(4),
                SeatsCount = 3,
                TotalAmount = 22.50m,
                Status = "held",
                CustomerEmail = "sokha.mean@example.com",
                CreatedAt = DateTimeOffset.Now.AddMinutes(-8),
                SeatIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }
            });
        }

        // ==========================================
        // FILTERING & VIEW CONTROLLERS
        // ==========================================
        private void SwitchNavView(string viewName)
        {
            _activeNavView = viewName;

            void StyleNav(Button btn, bool active)
            {
                btn.BackColor = active ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#1E293B");
                btn.ForeColor = active ? Color.White : ColorTranslator.FromHtml("#94A3B8");
            }

            StyleNav(_btnNavMovie, viewName == "Movie");
            StyleNav(_btnNavSeatMap, viewName == "SeatMap");
            StyleNav(_btnNavFnb, viewName == "F&B");
            StyleNav(_btnNavReservation, viewName == "Reservation");

            _movieGridPanel.Visible = (viewName == "Movie");
            _seatMapContainerPanel.Visible = (viewName == "SeatMap");
            _fnbContainerPanel.Visible = (viewName == "F&B");
            _reservationPanel.Visible = (viewName == "Reservation");

            if (viewName == "SeatMap")
            {
                if (_currentShowtime == null && _seatMapHeaderLabel != null)
                {
                    _seatMapHeaderLabel.Text = "No Showtime Selected — Please select a movie showtime from Movies [F10]";
                }
                UpdateSeatMapHeaderLayout();
            }

            _leftNavPanel?.Invalidate(true);

            if (viewName == "Movie") PopulateMovieGrid();
            if (viewName == "F&B")
            {
                if (_fnbCategoryFlow.Controls.Count == 0)
                {
                    EnsureFnbProductsSeeded();
                    PopulateFnbCategories();
                    PopulateFnbGrid();
                }
                UpdateFnbCardQuantities();
            }
            if (viewName == "Reservation") _ = SearchBookingsAsync(_txtSearchRes?.Text.Trim() ?? string.Empty);
        }

        private void SwitchCategoryFilter(string category)
        {
            _activeCategoryFilter = category;
            _btnNowShowing.BackColor = (category == "NowShowing") ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F1F5F9");
            _btnNowShowing.ForeColor = (category == "NowShowing") ? Color.White : ColorTranslator.FromHtml("#475569");

            _btnComingSoon.BackColor = (category == "ComingSoon") ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F1F5F9");
            _btnComingSoon.ForeColor = (category == "ComingSoon") ? Color.White : ColorTranslator.FromHtml("#475569");

            PopulateMovieGrid();
        }

        private void SwitchDateFilter(DateTime selectedDate)
        {
            _activeDate = selectedDate.Date;
            foreach (var btn in _dateButtons)
            {
                if (btn.Tag is DateTime d)
                {
                    bool isSel = (d.Date == _activeDate.Date);
                    btn.BackColor = isSel ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F1F5F9");
                    btn.ForeColor = isSel ? Color.White : ColorTranslator.FromHtml("#334155");
                }
            }
            PopulateMovieGrid();
        }

        // ==========================================
        // MOVIE GRID & SHOWTIMES DISPLAY
        // ==========================================
        private int CalculateCurrentCardWidth()
        {
            if (_movieFlowLayout == null) return 280;
            int availW = _movieFlowLayout.ClientSize.Width - _movieFlowLayout.Padding.Horizontal;
            if (availW <= 100) return 280;

            // Target column count: dynamically calculate so cards fill availW evenly
            // Margin is 6 on each side (12px per card)
            int marginH = 6;
            int totalMargin = marginH * 2;
            int minCardSlot = 240 + totalMargin; // 252px minimum per column

            int cols = Math.Clamp(availW / minCardSlot, 1, 4);
            if (availW >= 1500) cols = 5;
            int slotW = availW / cols;
            int cardW = slotW - totalMargin;
            return Math.Max(230, cardW);
        }

        private void UpdateMovieCardSizes()
        {
            if (_movieFlowLayout == null || _movieFlowLayout.Controls.Count == 0) return;

            int cardW = CalculateCurrentCardWidth();
            int infoW = Math.Max(100, cardW - 122 - 12);
            int stFlowW = Math.Max(180, cardW - 24);
            int btnW = Math.Max(75, (stFlowW - 8) / 2);

            _movieFlowLayout.SuspendLayout();
            foreach (Control c in _movieFlowLayout.Controls)
            {
                if (c is Panel cardPnl)
                {
                    cardPnl.Width = cardW;
                    cardPnl.Margin = new Padding(6, 8, 6, 8);

                    foreach (Control child in cardPnl.Controls)
                    {
                        if (child.Tag as string == "CardInfoLabel")
                        {
                            child.Width = infoW;
                        }
                        else if (child.Name == "stFlow" && child is FlowLayoutPanel stFlow)
                        {
                            stFlow.Width = stFlowW;
                            int btnIdx = 0;
                            foreach (Control btn in stFlow.Controls)
                            {
                                btn.Width = btnW;
                                btn.Margin = (btnIdx % 2 == 0) ? new Padding(0, 0, 8, 6) : new Padding(0, 0, 0, 6);
                                btnIdx++;
                            }
                        }
                        else if (child.Name == "lblComingSoon" || child.Name == "lblNoShowtimes")
                        {
                            child.Width = stFlowW;
                        }
                    }
                }
            }
            _movieFlowLayout.ResumeLayout(true);
        }

        private void PopulateMovieGrid()
        {
            _movieFlowLayout.SuspendLayout();
            _movieFlowLayout.Controls.Clear();

            var filteredMovies = _movies.Where(m =>
                string.Equals(m.Status, _activeCategoryFilter, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(_searchFilter) ||
                 m.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                 m.Genre.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            if (filteredMovies.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = $"No movies available for {_activeCategoryFilter} on {_activeDate:dd MMM yyyy}.",
                    Font = new Font("Segoe UI", 12f, FontStyle.Italic),
                    ForeColor = ColorTranslator.FromHtml("#64748B"),
                    AutoSize = true,
                    Margin = new Padding(24)
                };
                _movieFlowLayout.Controls.Add(lblEmpty);
            }
            else
            {
                foreach (var movie in filteredMovies)
                {
                    var card = CreateMovieCard(movie);
                    _movieFlowLayout.Controls.Add(card);
                }
            }

            _movieFlowLayout.ResumeLayout(true);
            UpdateMovieCardSizes();
        }

        private Panel CreateMovieCard(MovieDto movie)
        {
            int cardW = CalculateCurrentCardWidth();
            int infoW = Math.Max(100, cardW - 122 - 12);
            int stFlowW = Math.Max(180, cardW - 24);

            var cardPnl = new Panel
            {
                Width = cardW,
                Height = 350,
                Margin = new Padding(6, 8, 6, 8),
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            cardPnl.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, cardPnl.Width - 1, cardPnl.Height - 1);
            };

            void OnCardClick(object? s, EventArgs e)
            {
                var firstSt = movie.Showtimes.FirstOrDefault(st => st.StartTime.Date == _activeDate.Date) ?? movie.Showtimes.FirstOrDefault();
                if (firstSt != null)
                {
                    SelectMovieAndShowtime(movie, firstSt);
                }
                else
                {
                    _currentMovie = movie;
                    _lblDetailMovieTitle.Text = movie.Title;
                    _lblDetailShowtime.Text = "Showtime: --:--";
                    _lblDetailAuditorium.Text = $"Genre: {movie.Genre}";
                    _lblDetailPrice.Text = "$0.00";
                    _pbDetailPoster.Image = MoviePosterService.GenerateFallbackPoster(movie.Title, 80, 122);
                    _ = Task.Run(async () =>
                    {
                        var img = await MoviePosterService.LoadPosterAsync(movie.PosterUrl, movie.Title, 80, 122);
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            this.BeginInvoke(() => { if (_currentMovie == movie) _pbDetailPoster.Image = img; });
                        }
                    });
                }
            }

            cardPnl.Click += OnCardClick;

            // Real Movie Poster with Asynchronous Caching & GDI+ Fallback
            var pbPoster = new PictureBox
            {
                Location = new Point(12, 12),
                Size = new Size(100, 150),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            pbPoster.Image = MoviePosterService.GenerateFallbackPoster(movie.Title, 100, 150);
            pbPoster.Click += OnCardClick;
            cardPnl.Controls.Add(pbPoster);

            // Trigger Async Poster Fetch
            _ = Task.Run(async () =>
            {
                var img = await MoviePosterService.LoadPosterAsync(movie.PosterUrl, movie.Title, 100, 150);
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(() => pbPoster.Image = img);
                }
            });

            // Movie Info Details
            var lblTitle = new Label
            {
                Text = movie.Title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(122, 12),
                Size = new Size(infoW, 44),
                AutoEllipsis = true,
                Cursor = Cursors.Hand,
                Tag = "CardInfoLabel"
            };
            lblTitle.Click += OnCardClick;
            cardPnl.Controls.Add(lblTitle);

            var lblGenre = new Label
            {
                Text = movie.Genre,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Location = new Point(122, 58),
                Size = new Size(infoW, 18),
                AutoEllipsis = true,
                Cursor = Cursors.Hand,
                Tag = "CardInfoLabel"
            };
            lblGenre.Click += OnCardClick;
            cardPnl.Controls.Add(lblGenre);

            var lblRating = new Label
            {
                Text = $"⏱️ {movie.DurationMinutes}m  •  ★ {movie.Rating:F1}",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2563EB"),
                Location = new Point(122, 80),
                Size = new Size(infoW, 20),
                Cursor = Cursors.Hand,
                Tag = "CardInfoLabel"
            };
            lblRating.Click += OnCardClick;
            cardPnl.Controls.Add(lblRating);

            bool isNowShowing = string.Equals(movie.Status, "NowShowing", StringComparison.OrdinalIgnoreCase);

            var lblBadge = new Label
            {
                Text = isNowShowing ? "NOW SHOWING" : "COMING SOON",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = isNowShowing ? ColorTranslator.FromHtml("#16A34A") : ColorTranslator.FromHtml("#D97706"),
                BackColor = isNowShowing ? ColorTranslator.FromHtml("#DCFCE7") : ColorTranslator.FromHtml("#FEF3C7"),
                Location = new Point(122, 106),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            lblBadge.Click += OnCardClick;
            cardPnl.Controls.Add(lblBadge);

            // Showtime Section Header
            var lblShowtimesHeader = new Label
            {
                Text = isNowShowing ? $"SHOWTIMES ({_activeDate:dd MMM}):" : "COMING SOON:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#475569"),
                Location = new Point(12, 172),
                AutoSize = true
            };
            cardPnl.Controls.Add(lblShowtimesHeader);

            if (isNowShowing)
            {
                // Filter strictly by selected active date
                var dateShowtimes = movie.Showtimes.Where(st => st.StartTime.Date == _activeDate.Date).ToList();

                if (dateShowtimes.Count == 0)
                {
                    var lblNoShowtimes = new Label
                    {
                        Name = "lblNoShowtimes",
                        Text = $"No showtimes scheduled for {_activeDate:dd MMM}.",
                        Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                        ForeColor = ColorTranslator.FromHtml("#64748B"),
                        Location = new Point(12, 202),
                        Size = new Size(stFlowW, 40)
                    };
                    cardPnl.Controls.Add(lblNoShowtimes);
                }
                else
                {
                    var stFlow = new FlowLayoutPanel
                    {
                        Name = "stFlow",
                        Location = new Point(12, 196),
                        Size = new Size(stFlowW, 142),
                        FlowDirection = FlowDirection.LeftToRight,
                        AutoScroll = false,
                        WrapContents = true,
                        Padding = Padding.Empty,
                        Margin = Padding.Empty
                    };

                    int maxButtons = Math.Min(4, dateShowtimes.Count);
                    bool hasMore = dateShowtimes.Count > 4;
                    int btnW = Math.Max(75, (stFlowW - 8) / 2);

                    for (int i = 0; i < (hasMore ? 3 : maxButtons); i++)
                    {
                        var st = dateShowtimes[i];
                        string screenType = !string.IsNullOrWhiteSpace(st.ScreenType)
                            ? st.ScreenType
                            : (st.AuditoriumName?.Contains("IMAX", StringComparison.OrdinalIgnoreCase) == true ? "IMAX" :
                               st.AuditoriumName?.Contains("ATMOS", StringComparison.OrdinalIgnoreCase) == true ? "ATMOS" :
                               st.AuditoriumName?.Contains("SCREENX", StringComparison.OrdinalIgnoreCase) == true ? "SCREENX" : "2D");

                        var stBtn = new Button
                        {
                            Text = $"{st.StartTime:HH:mm}{Environment.NewLine}{screenType}",
                            Width = btnW,
                            Height = 48,
                            Margin = (i % 2 == 0) ? new Padding(0, 0, 8, 6) : new Padding(0, 0, 0, 6),
                            Padding = new Padding(0),
                            BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                            ForeColor = ColorTranslator.FromHtml("#0F172A"),
                            FlatStyle = FlatStyle.Flat,
                            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                            Cursor = Cursors.Hand
                        };
                        stBtn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#CBD5E1");
                        stBtn.MouseEnter += (s, e) => { stBtn.BackColor = ColorTranslator.FromHtml("#DBEAFE"); stBtn.ForeColor = ColorTranslator.FromHtml("#1D4ED8"); };
                        stBtn.MouseLeave += (s, e) => { stBtn.BackColor = ColorTranslator.FromHtml("#F1F5F9"); stBtn.ForeColor = ColorTranslator.FromHtml("#0F172A"); };
                        stBtn.Click += async (s, e) =>
                        {
                            SelectMovieAndShowtime(movie, st);
                            await LoadSeatMapAsync(st);
                            SwitchNavView("SeatMap");
                        };
                        stFlow.Controls.Add(stBtn);
                    }

                    if (hasMore)
                    {
                        var btnMore = new Button
                        {
                            Text = $"+{dateShowtimes.Count - 3}{Environment.NewLine}more",
                            Width = btnW,
                            Height = 48,
                            Margin = (3 % 2 == 0) ? new Padding(0, 0, 8, 6) : new Padding(0, 0, 0, 6),
                            Padding = new Padding(0),
                            BackColor = ColorTranslator.FromHtml("#EFF6FF"),
                            ForeColor = ColorTranslator.FromHtml("#2563EB"),
                            FlatStyle = FlatStyle.Flat,
                            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                            Cursor = Cursors.Hand
                        };
                        btnMore.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#93C5FD");
                        btnMore.MouseEnter += (s, e) => btnMore.BackColor = ColorTranslator.FromHtml("#BFDBFE");
                        btnMore.MouseLeave += (s, e) => btnMore.BackColor = ColorTranslator.FromHtml("#EFF6FF");
                        btnMore.Click += (s, e) =>
                        {
                            var menu = new ContextMenuStrip();
                            menu.Font = new Font("Segoe UI", 9.5f);
                            var headerItem = new ToolStripMenuItem($"Showtimes: {movie.Title} ({_activeDate:dd MMM})") { Enabled = false, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
                            menu.Items.Add(headerItem);
                            menu.Items.Add(new ToolStripSeparator());

                            foreach (var moreSt in dateShowtimes)
                            {
                                string stScreen = !string.IsNullOrWhiteSpace(moreSt.ScreenType)
                                    ? moreSt.ScreenType
                                    : (moreSt.AuditoriumName?.Contains("IMAX", StringComparison.OrdinalIgnoreCase) == true ? "IMAX" :
                                       moreSt.AuditoriumName?.Contains("ATMOS", StringComparison.OrdinalIgnoreCase) == true ? "ATMOS" :
                                       moreSt.AuditoriumName?.Contains("SCREENX", StringComparison.OrdinalIgnoreCase) == true ? "SCREENX" : "2D");
                                var item = new ToolStripMenuItem($"🕒 {moreSt.StartTime:HH:mm}  |  {moreSt.AuditoriumName} ({stScreen})  |  ${moreSt.BasePrice:F2}");
                                item.Click += async (senderItem, ev) =>
                                {
                                    SelectMovieAndShowtime(movie, moreSt);
                                    await LoadSeatMapAsync(moreSt);
                                    SwitchNavView("SeatMap");
                                };
                                menu.Items.Add(item);
                            }
                            menu.Show(btnMore, new Point(0, btnMore.Height));
                        };
                        stFlow.Controls.Add(btnMore);
                    }

                    cardPnl.Controls.Add(stFlow);
                }
            }
            else
            {
                // ComingSoon movies: show advance notice, no showtimes available yet
                var lblComingSoonBadge = new Label
                {
                    Name = "lblComingSoon",
                    Text = "📅 Release Date: Coming Soon\nAdvance tickets will be available once schedule opens.",
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                    ForeColor = ColorTranslator.FromHtml("#D97706"),
                    Location = new Point(12, 202),
                    Size = new Size(stFlowW, 60)
                };
                cardPnl.Controls.Add(lblComingSoonBadge);
            }

            return cardPnl;
        }

        private void SelectMovieAndShowtime(MovieDto movie, ShowtimeDto st)
        {
            _currentMovie = movie;
            _currentShowtime = st;

            _lblDetailMovieTitle.Text = movie.Title;
            _lblDetailShowtime.Text = $"Showtime: {st.StartTime:yyyy-MM-dd HH:mm}";
            _lblDetailAuditorium.Text = $"Hall: {st.AuditoriumName}";
            _lblDetailPrice.Text = $"${st.BasePrice:F2}";

            // Update poster preview in detail card
            _pbDetailPoster.Image = MoviePosterService.GenerateFallbackPoster(movie.Title, 85, 128);
            _ = Task.Run(async () =>
            {
                var img = await MoviePosterService.LoadPosterAsync(movie.PosterUrl, movie.Title, 85, 128);
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(() => { if (_currentMovie == movie) _pbDetailPoster.Image = img; });
                }
            });
        }

        private void ClearMovieSelection()
        {
            _currentMovie = null;
            _currentShowtime = null;
            _lblDetailMovieTitle.Text = "No Movie Selected";
            _lblDetailShowtime.Text = "Showtime: --:--";
            _lblDetailAuditorium.Text = "Auditorium: --";
            _lblDetailPrice.Text = "$0.00";
            _pbDetailPoster.Image = null;
            _pbDetailPoster.Invalidate();
        }

        // ==========================================
        // F&B CONCESSIONS WITH CATEGORIES & STEPPERS
        // ==========================================
        private static bool CategoryMatches(string? productCategory, string activeCategory)
        {
            if (activeCategory == "All") return true;
            if (string.Equals(productCategory, activeCategory, StringComparison.OrdinalIgnoreCase)) return true;

            if (activeCategory.StartsWith("Beverage", StringComparison.OrdinalIgnoreCase) && 
                productCategory?.StartsWith("Beverage", StringComparison.OrdinalIgnoreCase) == true) return true;

            if (activeCategory.StartsWith("Snack", StringComparison.OrdinalIgnoreCase) && 
                productCategory?.StartsWith("Snack", StringComparison.OrdinalIgnoreCase) == true) return true;

            if (activeCategory.StartsWith("Combo", StringComparison.OrdinalIgnoreCase) && 
                productCategory?.StartsWith("Combo", StringComparison.OrdinalIgnoreCase) == true) return true;

            if (activeCategory.StartsWith("Popcorn", StringComparison.OrdinalIgnoreCase) && 
                productCategory?.StartsWith("Popcorn", StringComparison.OrdinalIgnoreCase) == true) return true;

            return false;
        }

        private void PopulateFnbCategories()
        {
            if (_fnbCategoryFlow == null) return;
            _fnbCategoryFlow.SuspendLayout();
            _fnbCategoryFlow.Controls.Clear();
            var categories = new List<string> { "All", "Popcorn", "Beverage", "Snacks", "Combos" };

            foreach (var p in _fnbProducts)
            {
                if (!string.IsNullOrWhiteSpace(p.Category) && !categories.Any(c => CategoryMatches(p.Category, c)))
                {
                    categories.Add(p.Category);
                }
            }

            foreach (var cat in categories)
            {
                bool isSel = CategoryMatches(cat, _activeFnbCategory);
                var btn = new Button
                {
                    Text = cat,
                    Height = 34,
                    Width = 95,
                    Margin = new Padding(0, 0, 8, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = isSel ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F1F5F9"),
                    ForeColor = isSel ? Color.White : ColorTranslator.FromHtml("#334155"),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) =>
                {
                    _activeFnbCategory = cat;
                    PopulateFnbCategories();
                    PopulateFnbGrid();
                };
                _fnbCategoryFlow.Controls.Add(btn);
            }
            _fnbCategoryFlow.ResumeLayout(true);
        }

        private void PopulateFnbGrid()
        {
            if (_fnbGridFlowLayout == null) return;
            _fnbQtyLabels.Clear();
            _fnbGridFlowLayout.SuspendLayout();
            _fnbGridFlowLayout.Controls.Clear();

            var prods = _fnbProducts.Where(p =>
                CategoryMatches(p.Category, _activeFnbCategory)
            ).ToList();

            foreach (var prod in prods)
            {
                var card = CreateFnbProductCard(prod);
                _fnbGridFlowLayout.Controls.Add(card);
            }
            _fnbGridFlowLayout.ResumeLayout(true);
        }

        private Panel CreateFnbProductCard(ProductDto prod)
        {
            var pnl = new Panel
            {
                Width = 185,
                Height = 236,
                Margin = new Padding(8),
                BackColor = Color.White
            };
            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            // Product Poster / Image
            var pbPoster = new PictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(165, 95),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F8FAFC")
            };
            pbPoster.Image = FnbImageService.GenerateFallbackIcon(prod.Name, prod.Category, 165, 95);

            if (!string.IsNullOrWhiteSpace(prod.ImageUrl))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var img = await FnbImageService.LoadFnbImageAsync(prod.ImageUrl, prod.Name, prod.Category, 165, 95);
                        if (img == null || pbPoster.IsDisposed) { img?.Dispose(); return; }

                        if (pbPoster.IsHandleCreated)
                        {
                            pbPoster.BeginInvoke(new Action(() =>
                            {
                                if (!pbPoster.IsDisposed)
                                {
                                    var old = pbPoster.Image;
                                    pbPoster.Image = img;
                                    old?.Dispose();
                                }
                                else { img.Dispose(); }
                            }));
                        }
                        else
                        {
                            // Control not yet shown — safe to set directly (no window handle = no cross-thread issue)
                            var old = pbPoster.Image;
                            pbPoster.Image = img;
                            old?.Dispose();
                        }
                    }
                    catch { }
                });
            }
            pnl.Controls.Add(pbPoster);

            var lblCategory = new Label
            {
                Text = prod.Category.ToUpper(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2563EB"),
                Location = new Point(10, 110),
                Size = new Size(165, 14)
            };
            pnl.Controls.Add(lblCategory);

            var lblName = new Label
            {
                Text = prod.Name,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(10, 126),
                Size = new Size(165, 30),
                AutoEllipsis = true
            };
            pnl.Controls.Add(lblName);

            var lblPrice = new Label
            {
                Text = $"${prod.Price:F2}",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#16A34A"),
                Location = new Point(10, 158),
                Size = new Size(165, 26)
            };
            pnl.Controls.Add(lblPrice);

            // Stepper controls: [-] QTY [+]
            var stepperPnl = new Panel
            {
                Location = new Point(10, 188),
                Size = new Size(165, 36)
            };

            var lblQty = new Label
            {
                Text = _cartControl.GetItemQuantity(prod.ProductId, "fnb").ToString(),
                Location = new Point(46, 1),
                Size = new Size(72, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A")
            };
            _fnbQtyLabels[prod.ProductId] = lblQty;

            var btnMinus = new Button
            {
                Text = "−",
                Location = new Point(0, 0),
                Size = new Size(42, 34),
                BackColor = ColorTranslator.FromHtml("#F1F5F9"),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMinus.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#CBD5E1");
            btnMinus.Click += (s, e) =>
            {
                _cartControl.DecrementItem(prod.ProductId, "fnb", 1);
                lblQty.Text = _cartControl.GetItemQuantity(prod.ProductId, "fnb").ToString();
            };

            var btnPlus = new Button
            {
                Text = "+",
                Location = new Point(122, 0),
                Size = new Size(42, 34),
                BackColor = ColorTranslator.FromHtml("#2563EB"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPlus.FlatAppearance.BorderSize = 0;
            btnPlus.Click += (s, e) =>
            {
                _cartControl.AddItem(prod.ProductId, prod.Name, 1, prod.Price, "fnb");
                lblQty.Text = _cartControl.GetItemQuantity(prod.ProductId, "fnb").ToString();
            };

            stepperPnl.Controls.Add(btnMinus);
            stepperPnl.Controls.Add(lblQty);
            stepperPnl.Controls.Add(btnPlus);
            pnl.Controls.Add(stepperPnl);

            return pnl;
        }

        private void UpdateFnbCardQuantities()
        {
            if (!_fnbContainerPanel.Visible) return;

            foreach (var kvp in _fnbQtyLabels)
            {
                if (kvp.Value != null && !kvp.Value.IsDisposed)
                {
                    int qty = _cartControl.GetItemQuantity(kvp.Key, "fnb");
                    kvp.Value.Text = qty.ToString();
                }
            }
        }

        // ==========================================
        // SEAT MAP & RESERVATIONS LOGIC
        // ==========================================
        private async Task LoadSeatMapAsync(ShowtimeDto st)
        {
            try
            {
                _seatMapHeaderLabel.Text = $"{st.MovieTitle?.ToUpper()}  |  {st.AuditoriumName}  |  {st.StartTime:HH:mm}";
                UpdateSeatMapHeaderLayout();

                SeatMapResponseDto seatMap;
                try
                {
                    seatMap = await _api.GetAsync<SeatMapResponseDto>($"/api/v1/catalog/seat-map/{st.ShowtimeId}");
                }
                catch
                {
                    seatMap = GenerateFallbackSeatMap(st);
                }

                // Delete remote seat hold if switching showtime
                if (_currentHoldId != null)
                {
                    try { await _api.DeleteAsync($"/api/v1/reservations/holds/{_currentHoldId}"); } catch { }
                    _currentHoldId = null;
                }

                // Clear ticket items while preserving F&B items!
                _cartControl.ClearTicketItems(notifyRemoved: false);
                _seatMapControl.LoadSeatMap(seatMap);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load seat map: " + ex.Message, "Seat Map Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private SeatMapResponseDto GenerateFallbackSeatMap(ShowtimeDto st)
        {
            var res = new SeatMapResponseDto
            {
                ShowtimeId = st.ShowtimeId,
                AuditoriumId = st.AuditoriumId,
                AuditoriumName = st.AuditoriumName,
                TotalSeats = 60,
                AvailableSeats = 52,
                Seats = new List<SeatDto>()
            };

            string[] rows = new[] { "A", "B", "C", "D", "E", "F" };
            foreach (var r in rows)
            {
                for (int num = 1; num <= 10; num++)
                {
                    string status = "available";
                    if (r == "A" && (num == 3 || num == 4)) status = "booked";
                    if (r == "C" && num == 5) status = "held";

                    res.Seats.Add(new SeatDto
                    {
                        SeatId = Guid.NewGuid(),
                        Row = r,
                        SeatNumber = num,
                        SeatType = (r == "F") ? "VIP" : "Standard",
                        Price = st.BasePrice + ((r == "F") ? 2.50m : 0.00m),
                        Status = status
                    });
                }
            }
            return res;
        }

        private async void SeatMapControl_SeatClicked(object? sender, SeatDto seat)
        {
            if (_currentShowtime == null) return;

            bool isSelected = _cartControl.Items.Any(i => i.Type == "ticket" && i.Id == seat.SeatId);
            if (isSelected)
            {
                _cartControl.RemoveItem(seat.SeatId, "ticket");
                _seatMapControl.SetSeatSelected(seat.SeatId, false);
                await SyncHoldsAsync();
            }
            else
            {
                // Use active demographic selector from header (eliminates popup fatigue!)
                var demographic = _activeTicketType ?? _ticketTypes.FirstOrDefault() ?? new TicketTypeDto { Name = "Adult", PriceModifier = 0.00m };
                decimal price = Math.Max(0m, seat.Price + demographic.PriceModifier);
                string desc = $"Ticket: {_currentShowtime.MovieTitle} - Seat {seat.Row}{seat.SeatNumber} ({demographic.Name})";
                _cartControl.AddItem(seat.SeatId, desc, 1, price, "ticket");
                _seatMapControl.SetSeatSelected(seat.SeatId, true);
                await SyncHoldsAsync();
            }
        }

        private async Task SyncHoldsAsync()
        {
            var ticketItems = _cartControl.Items.Where(i => i.Type == "ticket").ToList();
            if (!ticketItems.Any())
            {
                if (_currentHoldId != null)
                {
                    try { await _api.DeleteAsync($"/api/v1/reservations/holds/{_currentHoldId}"); } catch { }
                    _currentHoldId = null;
                    _cartControl.StopHoldTimer();
                }
                return;
            }

            if (_currentHoldId != null)
            {
                try { await _api.DeleteAsync($"/api/v1/reservations/holds/{_currentHoldId}"); } catch { }
                _currentHoldId = null;
            }

            _idempotencyKey = Guid.NewGuid().ToString();

            var req = new HoldRequest
            {
                ShowtimeId = _currentShowtime?.ShowtimeId ?? Guid.Empty,
                SeatIds = ticketItems.Select(i => i.Id).ToList(),
                IdempotencyKey = _idempotencyKey
            };

            try
            {
                var res = await _api.PostAsync<HoldRequest, HoldResponse>("/api/v1/reservations/holds", req);
                _currentHoldId = res.HoldId;
                _cartControl.StartHoldTimer(res.HoldExpiresAt);
            }
            catch
            {
                // Fallback hold timer offline
                _currentHoldId = Guid.NewGuid();
                _cartControl.StartHoldTimer(DateTime.Now.AddMinutes(10));
            }
        }

        // ==========================================
        // POS ACTIONS & CHECKOUT
        // ==========================================
        private void UpdateBuyButtonText()
        {
            _btnBuyAction.Text = $"🛒  PAY / BUY  [F12]  •  ${_cartControl.TotalAmount:F2}";
        }

        private async Task ProceedToCheckoutAsync()
        {
            if (_cartControl.Items.Count == 0)
            {
                MessageBox.Show("Cart is empty. Please select seats or concessions first.", "Cart Empty", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var orderReq = new OrderRequest
                {
                    BranchId = LoginForm.CurrentUser?.BranchId ?? Guid.Empty,
                    CashierId = LoginForm.CurrentUser?.UserId ?? Guid.Empty,
                    ReservationId = _currentHoldId,
                    DiscountAmount = _cartControl.DiscountAmount,
                    Lines = _cartControl.GetOrderLines()
                };

                OrderResponse orderRes;
                try
                {
                    orderRes = await _api.PostAsync<OrderRequest, OrderResponse>("/api/v1/pos/orders", orderReq, Guid.NewGuid().ToString());
                }
                catch
                {
                    orderRes = new OrderResponse
                    {
                        OrderId = Guid.NewGuid(),
                        ReservationId = _currentHoldId,
                        Status = "pending",
                        Subtotal = _cartControl.SubTotal,
                        DiscountAmount = _cartControl.DiscountAmount,
                        TotalAmount = _cartControl.TotalAmount,
                        CreatedAt = DateTime.Now
                    };
                }

                // Pass selected seat IDs so ticket issuance succeeds on checkout!
                var ticketSeatIds = _cartControl.Items
                    .Where(i => i.Type == "ticket")
                    .Select(i => i.Id)
                    .ToList();

                using var payForm = new PaymentForm(_api, orderRes, _currentShowtime?.ShowtimeId ?? Guid.Empty, ticketSeatIds);
                if (payForm.ShowDialog() == DialogResult.OK)
                {
                    // Reset order state upon successful payment
                    _cartControl.ClearCart(notifyRemoved: false);
                    _seatMapControl.ClearSelectedSeats();
                    _cartControl.StopHoldTimer();
                    _currentHoldId = null;
                    UpdateFnbCardQuantities();
                    ClearMovieSelection();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not process order: " + ex.Message, "Order Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VoidCurrentCart()
        {
            if (_cartControl.Items.Count == 0) return;
            if (MessageBox.Show("Are you sure you want to void and clear the current cart?", "Confirm Void", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _cartControl.ClearCart(notifyRemoved: false);
                _seatMapControl.ClearSelectedSeats();
                _cartControl.StopHoldTimer();
                UpdateFnbCardQuantities();
                ClearMovieSelection();
                if (_currentHoldId != null)
                {
                    var holdId = _currentHoldId;
                    _currentHoldId = null;
                    Task.Run(async () => {
                        try { await _api.DeleteAsync($"/api/v1/reservations/holds/{holdId}"); } catch { }
                    });
                }
            }
        }

        private void OpenDiscountDialog()
        {
            if (_cartControl.Items.Count == 0)
            {
                MessageBox.Show("Add items to cart before applying discount.", "Cart Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new DiscountForm(_api, _cartControl.SubTotal);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _cartControl.DiscountAmount = form.AppliedDiscountAmount;
                _cartControl.RefreshGrid();
                UpdateBuyButtonText();
            }
        }

        private bool _isLoggingOut = false;

        public void PerformLogout()
        {
            _isLoggingOut = true;
            try
            {
                _cartControl?.StopHoldTimer();
                if (_currentHoldId != null)
                {
                    var holdId = _currentHoldId;
                    _currentHoldId = null;
                    Task.Run(async () => {
                        try { await _api.DeleteAsync($"/api/v1/reservations/holds/{holdId}"); } catch { }
                    });
                }
            }
            catch { }

            LoginForm.ClearSession();
            if (LoginForm.CurrentInstance != null && !LoginForm.CurrentInstance.IsDisposed)
            {
                LoginForm.CurrentInstance.ResetLoginState();
                if (LoginForm.CurrentInstance.WindowState == FormWindowState.Minimized)
                {
                    LoginForm.CurrentInstance.WindowState = FormWindowState.Normal;
                }
                LoginForm.CurrentInstance.Show();
                LoginForm.CurrentInstance.BringToFront();
                LoginForm.CurrentInstance.Activate();
                this.Close();
            }
            else
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
                var login = new LoginForm(config);
                login.Show();
                login.BringToFront();
                login.Activate();
                this.Close();
            }
        }

        private void OpenShiftSummaryDialog()
        {
            using var form = new ShiftSummaryForm(_api, LoginForm.CurrentShift);
            if (form.ShowDialog(this) == DialogResult.OK && form.ShiftClosed)
            {
                PerformLogout();
            }
        }

        private void OpenSettingsDialog()
        {
            using var form = new TerminalSettingsForm(_api);
            if (form.ShowDialog(this) == DialogResult.OK && (form.LoggedOut || form.ShiftClosed))
            {
                PerformLogout();
            }
        }

        private void MainPosForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                e.Handled = true;
                _txtQuickSearch.Focus();
                _txtQuickSearch.SelectAll();
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                SwitchNavView("Reservation");
            }
            else if (e.KeyCode == Keys.F3)
            {
                e.Handled = true;
                OpenDiscountDialog();
            }
            else if (e.KeyCode == Keys.F4)
            {
                e.Handled = true;
                SwitchNavView("F&B");
            }
            else if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                SwitchNavView("SeatMap");
            }
            else if (e.KeyCode == Keys.F8)
            {
                e.Handled = true;
                OpenSettingsDialog();
            }
            else if (e.KeyCode == Keys.F9)
            {
                e.Handled = true;
                _ = SyncHoldsAsync();
            }
            else if (e.KeyCode == Keys.F10)
            {
                e.Handled = true;
                SwitchNavView("Movie");
            }
            else if (e.KeyCode == Keys.F11)
            {
                e.Handled = true;
                VoidCurrentCart();
            }
            else if (e.KeyCode == Keys.F12)
            {
                e.Handled = true;
                _ = ProceedToCheckoutAsync();
            }
        }

        private void MainPosForm_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized) return;
            UpdateHeaderLayout();
            UpdateSeatMapHeaderLayout();
            UpdateMovieCardSizes();
            if (_reservationPanel != null && _gridRes != null && _reservationPanel.Visible)
            {
                UpdateReservationLayout();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (!_isLoggingOut)
            {
                Application.Exit();
            }
        }
    }
}
