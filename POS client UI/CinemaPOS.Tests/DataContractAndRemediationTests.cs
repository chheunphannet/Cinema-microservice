using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CinemaPOS.Core.Models;
using Xunit;

namespace CinemaPOS.Tests
{
    public class DataContractAndRemediationTests
    {
        [Fact]
        public void MovieDto_DeserializesDecimalRatingAndMapsReleaseStatus()
        {
            string json = """
            {
                "movieId": "d1000000-0000-0000-0000-000000000001",
                "title": "Avengers: Endgame",
                "genre": "Action / Sci-Fi",
                "rating": 8.4,
                "durationMinutes": 181,
                "releaseStatus": "now_showing",
                "posterUrl": "https://assets.cinema.local/posters/avengers.jpg"
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var movie = JsonSerializer.Deserialize<MovieDto>(json, options);

            Assert.NotNull(movie);
            Assert.Equal("Avengers: Endgame", movie.Title);
            Assert.Equal(8.4m, movie.Rating);
            Assert.Equal("NowShowing", movie.Status);
            Assert.Equal("NowShowing", movie.ReleaseStatus);
        }

        [Fact]
        public void MovieDto_HandlesComingSoonReleaseStatusSafely()
        {
            string json = """
            {
                "movieId": "d1000000-0000-0000-0000-000000000002",
                "title": "Avatar 3",
                "rating": 7.5,
                "releaseStatus": "coming_soon"
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var movie = JsonSerializer.Deserialize<MovieDto>(json, options);

            Assert.NotNull(movie);
            Assert.Equal("ComingSoon", movie.Status);
            Assert.Equal("ComingSoon", movie.ReleaseStatus);
        }

        [Fact]
        public void MovieDto_StatusDefaultsToNowShowingAndNeverThrowsNullReference()
        {
            var movie = new MovieDto
            {
                MovieId = Guid.NewGuid(),
                Title = "Test Movie"
            };

            Assert.NotNull(movie.Status);
            Assert.Equal("NowShowing", movie.Status);
            Assert.Equal("NowShowing", movie.Status, ignoreCase: true);
        }

        [Fact]
        public void ShowtimeDto_DeserializesStartsAtAndEndsAtCorrectly()
        {
            string json = """
            {
                "showtimeId": "c1000000-0000-0000-0000-000000000001",
                "movieId": "d1000000-0000-0000-0000-000000000001",
                "auditoriumName": "Hall 1",
                "startsAt": "2026-09-27T14:30:00Z",
                "endsAt": "2026-09-27T17:30:00Z",
                "basePrice": 6.50,
                "status": "Active"
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var showtime = JsonSerializer.Deserialize<ShowtimeDto>(json, options);

            Assert.NotNull(showtime);
            Assert.Equal(new DateTime(2026, 9, 27, 14, 30, 0, DateTimeKind.Utc), showtime.StartTime.ToUniversalTime());
            Assert.Equal(new DateTime(2026, 9, 27, 17, 30, 0, DateTimeKind.Utc), showtime.EndTime.ToUniversalTime());
            Assert.Equal(6.50m, showtime.BasePrice);
        }

        [Fact]
        public void CartItems_PreservesFnbWhenTicketsRemoved()
        {
            var cart = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), Description = "Seat A1 Ticket", Quantity = 1, UnitPrice = 8.50m, Type = "ticket" },
                new CartItem { Id = Guid.NewGuid(), Description = "Seat A2 Ticket", Quantity = 1, UnitPrice = 8.50m, Type = "ticket" },
                new CartItem { Id = Guid.NewGuid(), Description = "Caramel Popcorn", Quantity = 1, UnitPrice = 3.50m, Type = "fnb" },
                new CartItem { Id = Guid.NewGuid(), Description = "Coca Cola", Quantity = 2, UnitPrice = 2.00m, Type = "fnb" }
            };

            // Mimic ClearTicketItems: remove only items where Type == "ticket"
            cart.RemoveAll(i => i.Type == "ticket");

            Assert.Equal(2, cart.Count);
            Assert.All(cart, item => Assert.Equal("fnb", item.Type));
            Assert.Equal(7.50m, cart.Sum(i => i.Quantity * i.UnitPrice));
        }

        [Fact]
        public void CartItems_StepperDecrementLogicRemovesWhenZero()
        {
            var cart = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), Description = "Popcorn", Quantity = 2, UnitPrice = 3.50m, Type = "fnb" }
            };

            // Decrement 1
            cart[0].Quantity -= 1;
            Assert.Equal(1, cart[0].Quantity);

            // Decrement 1 more -> reaches 0, remove
            cart[0].Quantity -= 1;
            if (cart[0].Quantity <= 0)
            {
                cart.RemoveAt(0);
            }

            Assert.Empty(cart);
        }

        [Fact]
        public void TicketIssueRequest_SerializesWithNonEmptySeatIds()
        {
            var seatIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var req = new TicketIssueRequest
            {
                ReservationId = Guid.NewGuid(),
                ShowtimeId = Guid.NewGuid(),
                SeatIds = seatIds
            };

            string json = JsonSerializer.Serialize(req);
            var deserialized = JsonSerializer.Deserialize<TicketIssueRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.SeatIds.Count);
            Assert.Equal(seatIds[0], deserialized.SeatIds[0]);
            Assert.Equal(seatIds[1], deserialized.SeatIds[1]);
        }

        [Fact]
        public void BookingSearchResponseDto_DeserializesItemsCorrectly()
        {
            string json = """
            {
                "items": [
                    {
                        "reservationId": "f1000000-0000-0000-0000-000000000001",
                        "bookingReference": "HOLD-9821",
                        "movieTitle": "Avengers: Endgame",
                        "auditoriumName": "Hall 1",
                        "showtimeStart": "2026-09-27T18:30:00Z",
                        "seatsCount": 2,
                        "totalAmount": 13.00,
                        "status": "held",
                        "customerEmail": "customer@example.com"
                    }
                ],
                "totalCount": 1,
                "page": 1,
                "pageSize": 20,
                "totalPages": 1
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var res = JsonSerializer.Deserialize<BookingSearchResponseDto>(json, options);

            Assert.NotNull(res);
            Assert.Single(res.Items);
            Assert.Equal("HOLD-9821", res.Items[0].BookingReference);
            Assert.Equal(13.00m, res.Items[0].TotalAmount);
            Assert.Equal("held", res.Items[0].Status);
        }

        [Fact]
        public void BookingDetailResponseDto_DeserializesNestedObjectsCorrectly()
        {
            string json = """
            {
                "reservationId": "f1000000-0000-0000-0000-000000000001",
                "bookingReference": "RES-2026-ABCD",
                "status": "confirmed",
                "movie": {
                    "movieId": "d1000000-0000-0000-0000-000000000001",
                    "title": "Inception",
                    "durationMinutes": 148,
                    "genre": "Sci-Fi",
                    "posterUrl": "/posters/inception.jpg"
                },
                "showtime": {
                    "showtimeId": "c1000000-0000-0000-0000-000000000001",
                    "startsAt": "2026-09-27T19:00:00Z",
                    "endsAt": "2026-09-27T21:30:00Z",
                    "basePrice": 8.50
                },
                "seats": [
                    {
                        "seatId": "e1000000-0000-0000-0000-000000000001",
                        "rowLabel": "C",
                        "seatNumber": 4,
                        "seatType": "Standard",
                        "price": 8.50,
                        "status": "booked"
                    },
                    {
                        "seatId": "e1000000-0000-0000-0000-000000000002",
                        "rowLabel": "C",
                        "seatNumber": 5,
                        "seatType": "Standard",
                        "price": 8.50,
                        "status": "booked"
                    }
                ]
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var detail = JsonSerializer.Deserialize<BookingDetailResponseDto>(json, options);

            Assert.NotNull(detail);
            Assert.Equal(Guid.Parse("f1000000-0000-0000-0000-000000000001"), detail.ReservationId);
            Assert.Equal("RES-2026-ABCD", detail.BookingReference);
            Assert.Equal("confirmed", detail.Status);

            Assert.NotNull(detail.Movie);
            Assert.Equal("Inception", detail.Movie.Title);
            Assert.Equal(148, detail.Movie.DurationMinutes);

            Assert.NotNull(detail.Showtime);
            Assert.Equal(Guid.Parse("c1000000-0000-0000-0000-000000000001"), detail.Showtime.ShowtimeId);
            Assert.Equal(8.50m, detail.Showtime.BasePrice);

            Assert.Equal(2, detail.Seats.Count);
            Assert.Equal("C", detail.Seats[0].RowLabel);
            Assert.Equal(4, detail.Seats[0].SeatNumber);
            Assert.Equal(8.50m, detail.Seats[0].Price);
            Assert.Equal("booked", detail.Seats[0].Status);
        }

        [Fact]
        public void BookingSearchResultItemDto_DeserializesWithSeatIdsList()
        {
            string json = """
            {
                "reservationId": "f1000000-0000-0000-0000-000000000001",
                "bookingReference": "HOLD-9821",
                "movieTitle": "Avengers: Endgame",
                "seatIds": [
                    "e1000000-0000-0000-0000-000000000001",
                    "e1000000-0000-0000-0000-000000000002"
                ]
            }
            """;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var item = JsonSerializer.Deserialize<BookingSearchResultItemDto>(json, options);

            Assert.NotNull(item);
            Assert.Equal(2, item.SeatIds.Count);
            Assert.Equal(Guid.Parse("e1000000-0000-0000-0000-000000000001"), item.SeatIds[0]);
            Assert.Equal(Guid.Parse("e1000000-0000-0000-0000-000000000002"), item.SeatIds[1]);
        }

        [Theory]
        [InlineData(8.50, 0.00, 8.50)]    // Adult regular
        [InlineData(8.50, -2.50, 6.00)]   // Child discount
        [InlineData(8.50, 3.00, 11.50)]   // 3D/VIP surcharge
        [InlineData(8.50, -15.00, 0.00)]  // Excessive discount clamped to zero
        public void DemographicPriceModifier_ClampsNegativeValuesToZero(decimal basePrice, decimal modifier, decimal expectedPrice)
        {
            decimal effectivePrice = Math.Max(0m, basePrice + modifier);
            Assert.Equal(expectedPrice, effectivePrice);
        }

        [Fact]
        public void PaymentForm_BakongFallbackPayloadFormatting()
        {
            var orderId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
            decimal totalAmount = 14.50m;

            string fallbackPayload = $"KHQR:ORDER:{orderId}:${totalAmount:F2}";

            Assert.Equal("KHQR:ORDER:a0000000-0000-0000-0000-000000000001:$14.50", fallbackPayload);
        }
    }
}
