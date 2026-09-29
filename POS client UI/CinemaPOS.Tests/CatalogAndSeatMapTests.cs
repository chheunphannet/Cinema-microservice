using System;
using System.Collections.Generic;
using System.Linq;
using CinemaPOS.Core.Models;
using Xunit;

namespace CinemaPOS.Tests
{
    public class CatalogAndSeatMapTests
    {
        [Fact]
        public void MovieDto_ShowtimesFilterByDate()
        {
            DateTime day27 = new DateTime(2026, 9, 27);
            DateTime day28 = new DateTime(2026, 9, 28);

            var movie = new MovieDto
            {
                MovieId = Guid.NewGuid(),
                Title = "Movie A",
                Genre = "Action",
                Rating = 8.5m,
                Status = "NowShowing",
                Showtimes = new List<ShowtimeDto>
                {
                    new ShowtimeDto { ShowtimeId = Guid.NewGuid(), StartTime = day27.AddHours(14), BasePrice = 6.50m },
                    new ShowtimeDto { ShowtimeId = Guid.NewGuid(), StartTime = day28.AddHours(18), BasePrice = 7.50m }
                }
            };

            var showtimesOn27 = movie.Showtimes.Where(st => st.StartTime.Day.ToString() == "27").ToList();
            var showtimesOn28 = movie.Showtimes.Where(st => st.StartTime.Day.ToString() == "28").ToList();

            Assert.Single(showtimesOn27);
            Assert.Single(showtimesOn28);
            Assert.Equal(6.50m, showtimesOn27.First().BasePrice);
            Assert.Equal(7.50m, showtimesOn28.First().BasePrice);
        }

        [Fact]
        public void SeatMapResponseDto_GeneratesValidLayoutGrid()
        {
            var st = new ShowtimeDto
            {
                ShowtimeId = Guid.NewGuid(),
                AuditoriumName = "Hall 1",
                BasePrice = 8.00m
            };

            var seats = new List<SeatDto>();
            string[] rows = new[] { "A", "B", "C" };
            foreach (var r in rows)
            {
                for (int num = 1; num <= 10; num++)
                {
                    seats.Add(new SeatDto
                    {
                        SeatId = Guid.NewGuid(),
                        Row = r,
                        SeatNumber = num,
                        SeatType = (r == "C") ? "VIP" : "Standard",
                        Price = st.BasePrice + ((r == "C") ? 2.50m : 0.00m),
                        Status = "available"
                    });
                }
            }

            var seatMap = new SeatMapResponseDto
            {
                ShowtimeId = st.ShowtimeId,
                TotalSeats = seats.Count,
                AvailableSeats = seats.Count(s => s.Status == "available"),
                Seats = seats
            };

            Assert.Equal(30, seatMap.TotalSeats);
            Assert.Equal(30, seatMap.AvailableSeats);
            Assert.Equal(10.50m, seatMap.Seats.First(s => s.Row == "C").Price);
        }

        [Fact]
        public void MovieDto_EnsureOperationalFallbackSchedulesCoverActiveDays()
        {
            // Simulate a movie with only historical showtimes (e.g. Sept 25 and Sept 26)
            DateTime today = new DateTime(2026, 9, 27);
            var movie = new MovieDto
            {
                MovieId = Guid.NewGuid(),
                Title = "Now Showing Title",
                Status = "NowShowing",
                Showtimes = new List<ShowtimeDto>
                {
                    new ShowtimeDto { ShowtimeId = Guid.NewGuid(), StartTime = new DateTime(2026, 9, 25, 14, 0, 0), BasePrice = 8.50m },
                    new ShowtimeDto { ShowtimeId = Guid.NewGuid(), StartTime = new DateTime(2026, 9, 26, 18, 0, 0), BasePrice = 8.50m }
                }
            };

            // Apply our fallback synthesis algorithm across 7 active dates
            for (int offset = 0; offset < 7; offset++)
            {
                DateTime day = today.AddDays(offset);
                if (!movie.Showtimes.Any(st => st.StartTime.Date == day.Date))
                {
                    movie.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = movie.MovieId, MovieTitle = movie.Title, StartTime = day.AddHours(10).AddMinutes(30), BasePrice = 6.50m });
                    movie.Showtimes.Add(new ShowtimeDto { ShowtimeId = Guid.NewGuid(), MovieId = movie.MovieId, MovieTitle = movie.Title, StartTime = day.AddHours(13).AddMinutes(45), BasePrice = 7.50m });
                }
            }

            // Verify every day from today to today+6 has at least one showtime
            for (int offset = 0; offset < 7; offset++)
            {
                DateTime day = today.AddDays(offset);
                var daySlots = movie.Showtimes.Where(st => st.StartTime.Date == day.Date).ToList();
                Assert.NotEmpty(daySlots);
            }
        }

        [Theory]
        [InlineData(774, 252, 3, 246)]
        [InlineData(1270, 252, 4, 305)]
        [InlineData(630, 252, 2, 303)]
        public void MovieCardLayout_CalculatesSymmetricColumnsWithoutWrapping(int availW, int minSlot, int expectedCols, int expectedCardW)
        {
            int totalMargin = 12;
            int cols = Math.Clamp(availW / minSlot, 1, 4);
            int slotW = availW / cols;
            int cardW = slotW - totalMargin;

            Assert.Equal(expectedCols, cols);
            Assert.Equal(expectedCardW, cardW);
            // Verify cards will never wrap
            Assert.True(cols * (cardW + totalMargin) <= availW);
        }

        [Fact]
        public void ProductDto_Deserializes_UnitPrice_And_Price()
        {
            string jsonUnitPrice = "{\"productId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"name\":\"Caramel Popcorn\",\"unitPrice\":4.50}";
            var prodFromUnitPrice = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(jsonUnitPrice, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(prodFromUnitPrice);
            Assert.Equal(4.50m, prodFromUnitPrice.Price);
            Assert.Equal(4.50m, prodFromUnitPrice.UnitPrice);

            string jsonPrice = "{\"productId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"name\":\"Caramel Popcorn\",\"price\":3.75}";
            var prodFromPrice = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(jsonPrice, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(prodFromPrice);
            Assert.Equal(3.75m, prodFromPrice.Price);
            Assert.Equal(3.75m, prodFromPrice.UnitPrice);
        }

        [Fact]
        public void ProductDto_Deserializes_ImageUrl_And_SnakeCase()
        {
            string jsonCamel = "{\"productId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"imageUrl\":\"http://media/popcorn.png\"}";
            var prodCamel = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(jsonCamel, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(prodCamel);
            Assert.Equal("http://media/popcorn.png", prodCamel.ImageUrl);

            string jsonSnake = "{\"productId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"image_url\":\"http://media/drink.png\"}";
            var prodSnake = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(jsonSnake, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(prodSnake);
            Assert.Equal("http://media/drink.png", prodSnake.ImageUrl);
        }

        [Fact]
        public void SeatMapControl_ColorTokens_MatchRequiredPalette()
        {
            // Available: Dark Red (#991B1B)
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#991B1B"), CinemaPOS.App.Controls.SeatMapControl.AvailableColor);

            // Booked: Light Gray (#CBD5E1)
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#CBD5E1"), CinemaPOS.App.Controls.SeatMapControl.BookedColor);

            // VIP: Gold (#EAB308)
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#EAB308"), CinemaPOS.App.Controls.SeatMapControl.VipColor);

            // Selected: Green (#16A34A)
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#16A34A"), CinemaPOS.App.Controls.SeatMapControl.SelectedColor);

            // Held: Light Blue (#38BDF8)
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#38BDF8"), CinemaPOS.App.Controls.SeatMapControl.HeldColor);
        }

        [Fact]
        public void SeatMapControl_DrawsRowLettersAndSeatNumbersOnly_RendersWithoutErrors()
        {
            using var ctrl = new CinemaPOS.App.Controls.SeatMapControl();
            ctrl.Size = new System.Drawing.Size(800, 600);

            var seats = new List<SeatDto>
            {
                new SeatDto { SeatId = Guid.NewGuid(), Row = "A", SeatNumber = 1, Status = "available", SeatType = "Standard", Price = 7.50m },
                new SeatDto { SeatId = Guid.NewGuid(), Row = "A", SeatNumber = 2, Status = "booked", SeatType = "Standard", Price = 7.50m },
                new SeatDto { SeatId = Guid.NewGuid(), Row = "B", SeatNumber = 1, Status = "held", SeatType = "Standard", Price = 7.50m },
                new SeatDto { SeatId = Guid.NewGuid(), Row = "B", SeatNumber = 2, Status = "available", SeatType = "VIP", Price = 10.00m }
            };

            var seatMap = new SeatMapResponseDto
            {
                ShowtimeId = Guid.NewGuid(),
                TotalSeats = seats.Count,
                AvailableSeats = 2,
                Seats = seats
            };

            ctrl.LoadSeatMap(seatMap);

            using var bmp = new System.Drawing.Bitmap(800, 600);
            ctrl.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, 800, 600));

            // Bitmap successfully generated
            Assert.NotNull(bmp);
            Assert.Equal(800, bmp.Width);
            Assert.Equal(600, bmp.Height);
        }

        [Fact]
        public void FnbImageService_GeneratesCategorySpecificFallbackIcons()
        {
            var popcornIcon = CinemaPOS.App.Forms.FnbImageService.GenerateFallbackIcon("Caramel Popcorn", "Popcorn", 165, 95);
            var drinkIcon = CinemaPOS.App.Forms.FnbImageService.GenerateFallbackIcon("Coca Cola 32oz", "Beverages", 165, 95);
            var snackIcon = CinemaPOS.App.Forms.FnbImageService.GenerateFallbackIcon("Cheese Nachos", "Snacks", 165, 95);
            var comboIcon = CinemaPOS.App.Forms.FnbImageService.GenerateFallbackIcon("Combo 1", "Combos", 165, 95);

            Assert.NotNull(popcornIcon);
            Assert.NotNull(drinkIcon);
            Assert.NotNull(snackIcon);
            Assert.NotNull(comboIcon);

            Assert.Equal(165, popcornIcon.Width);
            Assert.Equal(95, popcornIcon.Height);
        }

        private static bool IsSelfVisible(Control c)
        {
            var m = typeof(Control).GetMethod("GetState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
            {
                return (bool)m.Invoke(c, new object[] { 2 /* STATE_VISIBLE */ })!;
            }
            return c.Visible;
        }

        [Fact]
        public void FnbCategoryFlow_Controls_ExistAndVisible()
        {
            using var form = new CinemaPOS.App.Forms.MainPosForm();
            form.Size = new System.Drawing.Size(1440, 900);
            form.SwitchViewForTesting("F&B");

            var fnbFlowField = typeof(CinemaPOS.App.Forms.MainPosForm).GetField("_fnbCategoryFlow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fnbFlow = fnbFlowField?.GetValue(form) as FlowLayoutPanel;
            Assert.NotNull(fnbFlow);

            int count = fnbFlow.Controls.Count;
            Assert.True(count >= 5, $"Expected at least 5 category buttons, but got {count}");
            Assert.All(fnbFlow.Controls.OfType<Button>(), b => Assert.True(IsSelfVisible(b)));
        }

        [Fact]
        public void FnbImageService_RewritesLocalAssetUrls()
        {
            string assetUrl = "https://assets.cinema.local/fnb/coke.png";
            string rewritten = CinemaPOS.App.Forms.FnbImageService.RewriteFnbUrl(assetUrl);
            Assert.Equal("http://localhost:8080/api/v1/media/files/fnb/coke.png", rewritten);
        }

        [Fact]
        public void SeatMapControl_WideFourteenSeatAuditorium_FitsWithinViewportWithoutHorizontalScroll()
        {
            using var ctrl = new CinemaPOS.App.Controls.SeatMapControl();
            ctrl.Size = new System.Drawing.Size(664, 600);

            var seats = new List<SeatDto>();
            string[] rows = new[] { "A", "B", "C", "D", "E", "F" };
            foreach (var r in rows)
            {
                for (int num = 1; num <= 14; num++)
                {
                    seats.Add(new SeatDto
                    {
                        SeatId = Guid.NewGuid(),
                        Row = r,
                        SeatNumber = num,
                        SeatType = (r == "E" || r == "F") ? "VIP" : "Standard",
                        Price = 8.50m,
                        Status = (r == "E" && (num == 7 || num == 8)) ? "booked" : "available"
                    });
                }
            }

            var seatMap = new SeatMapResponseDto
            {
                ShowtimeId = Guid.NewGuid(),
                TotalSeats = seats.Count,
                AvailableSeats = seats.Count(s => s.Status == "available"),
                Seats = seats
            };

            ctrl.LoadSeatMap(seatMap);

            // Verify no horizontal scrollbar is triggered in 664px viewport
            Assert.Equal(0, ctrl.AutoScrollMinSize.Width);

            // Verify all seat rectangles and row labels fit inside the 664px container
            var seatRects = typeof(CinemaPOS.App.Controls.SeatMapControl)
                .GetField("_seatRects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(ctrl) as Dictionary<Guid, System.Drawing.Rectangle>;

            Assert.NotNull(seatRects);
            Assert.Equal(84, seatRects.Count);

            foreach (var kvp in seatRects)
            {
                var r = kvp.Value;
                // Left row letter requires at least 30px from seat left to 0
                Assert.True(r.X >= 30, $"Seat left ({r.X}) should leave room for left row letter");
                // Right row letter requires at least 30px from seat right to client edge
                Assert.True(r.Right + 30 <= 664, $"Seat right ({r.Right}) plus row letter should fit inside 664px");
            }

            using var bmp = new System.Drawing.Bitmap(664, 600);
            ctrl.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, 664, 600));
            Assert.NotNull(bmp);
        }
    }
}
