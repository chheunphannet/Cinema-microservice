using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CinemaPOS.Core.Models
{
    public class BranchDto
    {
        public Guid BranchId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Location { get; set; }
        public bool IsActive { get; set; }
    }

    public class StaffUserDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public Guid? BranchId { get; set; }
        public bool IsActive { get; set; }
    }

    public class ShowtimeDto
    {
        public Guid ShowtimeId { get; set; }
        public Guid MovieId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public Guid AuditoriumId { get; set; }
        public string AuditoriumName { get; set; } = string.Empty;
        public string ScreenType { get; set; } = string.Empty;

        private DateTime _startTime;
        [JsonPropertyName("startsAt")]
        public DateTime StartsAt
        {
            get => _startTime;
            set => _startTime = value;
        }

        [JsonPropertyName("startTime")]
        public DateTime StartTime
        {
            get => _startTime;
            set => _startTime = value;
        }

        private DateTime _endTime;
        [JsonPropertyName("endsAt")]
        public DateTime EndsAt
        {
            get => _endTime;
            set => _endTime = value;
        }

        [JsonPropertyName("endTime")]
        public DateTime EndTime
        {
            get => _endTime;
            set => _endTime = value;
        }

        public decimal BasePrice { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class MovieDto
    {
        public Guid MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;

        [JsonPropertyName("rating")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal Rating { get; set; }

        public int DurationMinutes { get; set; }
        public string PosterUrl { get; set; } = string.Empty;

        private string _status = "NowShowing";

        [JsonPropertyName("releaseStatus")]
        public string ReleaseStatus
        {
            get => _status;
            set => SetStatus(value);
        }

        [JsonPropertyName("status")]
        public string Status
        {
            get => _status;
            set => SetStatus(value);
        }

        private void SetStatus(string? value)
        {
            if (string.Equals(value, "now_showing", StringComparison.OrdinalIgnoreCase))
                _status = "NowShowing";
            else if (string.Equals(value, "coming_soon", StringComparison.OrdinalIgnoreCase))
                _status = "ComingSoon";
            else if (!string.IsNullOrWhiteSpace(value))
                _status = value;
        }

        public List<ShowtimeDto> Showtimes { get; set; } = new List<ShowtimeDto>();
    }

    public class ProductDto
    {
        public Guid ProductId { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        [JsonPropertyName("productName")]
        public string ProductName
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        [JsonPropertyName("product_name")]
        public string ProductNameSnake
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        public string Category { get; set; } = string.Empty;

        private decimal _price;
        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice
        {
            get => _price;
            set => _price = value;
        }

        [JsonPropertyName("price")]
        public decimal Price
        {
            get => _price;
            set => _price = value;
        }

        [JsonPropertyName("unit_price")]
        public decimal UnitPriceSnake
        {
            get => _price;
            set => _price = value;
        }

        private string _imageUrl = string.Empty;
        [JsonPropertyName("imageUrl")]
        public string ImageUrl
        {
            get => _imageUrl;
            set => _imageUrl = value ?? string.Empty;
        }

        [JsonPropertyName("image_url")]
        public string ImageUrlSnake
        {
            get => _imageUrl;
            set => _imageUrl = value ?? string.Empty;
        }

        [JsonPropertyName("posterUrl")]
        public string PosterUrl
        {
            get => _imageUrl;
            set => _imageUrl = value ?? string.Empty;
        }

        public string Sku { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;

        [JsonPropertyName("stockQuantity")]
        public int StockQuantity
        {
            get => StockLevel;
            set => StockLevel = value;
        }

        [JsonPropertyName("stock_quantity")]
        public int StockQuantitySnake
        {
            get => StockLevel;
            set => StockLevel = value;
        }

        [JsonPropertyName("stockLevel")]
        public int StockLevel { get; set; } = 100;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }

    public class TicketTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public decimal PriceModifier { get; set; }
    }

    public class MemberProfileDto
    {
        public Guid MemberId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Tier { get; set; }
        public int PointsBalance { get; set; }
        public decimal DiscountRate { get; set; }
    }

    public class VoucherValidateRequest
    {
        public string Code { get; set; }
    }

    public class VoucherValidateResponse
    {
        public bool IsValid { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MaxDiscountAmount { get; set; }
        public string Description { get; set; }
    }

    public class SeatMapResponseDto
    {
        public Guid ShowtimeId { get; set; }
        public Guid AuditoriumId { get; set; }
        public string AuditoriumName { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public List<SeatDto> Seats { get; set; } = new List<SeatDto>();
    }

    public class SeatDto
    {
        public Guid SeatId { get; set; }

        private string _row = string.Empty;
        [JsonPropertyName("row")]
        public string Row
        {
            get => _row;
            set => _row = value ?? string.Empty;
        }

        [JsonPropertyName("rowLabel")]
        public string RowLabel
        {
            get => _row;
            set => _row = value ?? string.Empty;
        }

        private int _seatNumber;
        [JsonPropertyName("seatNumber")]
        public int SeatNumber
        {
            get => _seatNumber;
            set => _seatNumber = value;
        }

        [JsonPropertyName("seat_number")]
        public int SeatNumberSnake
        {
            get => _seatNumber;
            set => _seatNumber = value;
        }

        private string _seatType = "Standard";
        [JsonPropertyName("seatType")]
        public string SeatType
        {
            get => _seatType;
            set => _seatType = value ?? "Standard";
        }

        [JsonPropertyName("seat_type")]
        public string SeatTypeSnake
        {
            get => _seatType;
            set => _seatType = value ?? "Standard";
        }

        public decimal Price { get; set; }

        private string _status = "available";
        [JsonPropertyName("status")]
        public string Status
        {
            get => _status;
            set => _status = value ?? "available";
        }
    }

    public class OrderLineDto
    {
        public Guid? ProductId { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string LineType { get; set; }
    }

    public class OrderRequest
    {
        public Guid BranchId { get; set; }
        public Guid CashierId { get; set; }
        public Guid? ReservationId { get; set; }
        public Guid? CustomerId { get; set; }
        public decimal DiscountAmount { get; set; }
        public string VoucherCode { get; set; }
        public List<OrderLineDto> Lines { get; set; } = new List<OrderLineDto>();
    }

    public class OrderResponse
    {
        public Guid OrderId { get; set; }
        public Guid BranchId { get; set; }
        public Guid CashierId { get; set; }
        public Guid? ReservationId { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TicketPrintRequest
    {
        public bool IsReprint { get; set; }
        public string SupervisorPin { get; set; }
    }
    
    public class TicketPrintResponse
    {
        public Guid TicketId { get; set; }
        public bool IsPrinted { get; set; }
        public DateTime PrintedAt { get; set; }
        public string PrintFormat { get; set; }
        public bool DrawerKick { get; set; }
    }

    public class ConcessionsBarcodeScanRequest
    {
        public string BarcodeOrToken { get; set; }
        public bool AutoFulfill { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string PinOrPassword { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public List<string> Roles { get; set; }
        public Guid BranchId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class ShiftOpenRequest
    {
        public Guid BranchId { get; set; }
        public Guid CashierId { get; set; }
        public string TerminalCode { get; set; }
        public decimal OpeningFloat { get; set; }
    }

    public class ShiftOpenResponse
    {
        public Guid ShiftId { get; set; }
        public Guid BranchId { get; set; }
        public Guid CashierId { get; set; }
        public string TerminalCode { get; set; }
        public decimal OpeningFloat { get; set; }
        public string Status { get; set; }
        public DateTime OpenedAt { get; set; }
    }

    public class HoldRequest
    {
        public Guid ShowtimeId { get; set; }
        public List<Guid> SeatIds { get; set; }
        public Guid? CustomerId { get; set; }
        public string GuestEmail { get; set; }
        public string IdempotencyKey { get; set; }
    }

    public class HoldResponse
    {
        public Guid HoldId { get; set; }
        public Guid ShowtimeId { get; set; }
        public List<Guid> SeatIds { get; set; }
        public string IdempotencyKey { get; set; }
        public int FencingToken { get; set; }
        public DateTime HoldExpiresAt { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class PaymentRequest
    {
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public string ProviderReference { get; set; }
    }

    public class PaymentResponse
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public string Method { get; set; }
        public decimal AmountCharged { get; set; }
        public decimal OrderTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal ChangeGiven { get; set; }
        public decimal RemainingBalance { get; set; }
        public bool IsOrderFullyPaid { get; set; }
        public string Status { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class TicketIssueRequest
    {
        public Guid ReservationId { get; set; }
        public Guid ShowtimeId { get; set; }
        public List<Guid> SeatIds { get; set; }
    }

    public class TicketIssueResponse
    {
        public Guid TicketId { get; set; }
        public Guid ReservationId { get; set; }
        public Guid ShowtimeId { get; set; }
        public Guid SeatId { get; set; }
        public string Status { get; set; }
        public string QrHash { get; set; }
    }

    public class ShiftCloseRequest
    {
        public Guid ShiftId { get; set; }
        public decimal ClosingCash { get; set; }
    }
    
    public class ShiftCloseResponse
    {
        public Guid ShiftId { get; set; }
        public decimal OpeningFloat { get; set; }
        public decimal ClosingCash { get; set; }
        public decimal Discrepancy { get; set; }
        public string Status { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }
    }

    public class BakongQrResponse
    {
        public string OrderId { get; set; }
        public string KhqrPayload { get; set; }
        public string DeepLinkUrl { get; set; }
        public decimal Amount { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class OrderPaymentStatusResponse
    {
        public string OrderId { get; set; }
        public string Status { get; set; }
        public DateTime? PaidAt { get; set; }
        public string TransactionRef { get; set; }
    }
    
    public class SupervisorVerifyRequest
    {
        public string SupervisorPin { get; set; }
        public Guid BranchId { get; set; }
    }
    
    public class SupervisorVerifyResponse
    {
        public bool IsValid { get; set; }
        public Guid SupervisorId { get; set; }
        public string DisplayName { get; set; }
    }

    public class CartItem
    {
        public Guid Id { get; set; } // SeatId or ProductId
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Type { get; set; } = "ticket"; // "ticket" or "fnb"
    }

    public class BookingSearchResultItemDto
    {
        public Guid ReservationId { get; set; }
        public Guid? OrderId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string MovieTitle { get; set; } = string.Empty;
        public string AuditoriumName { get; set; } = string.Empty;
        public DateTimeOffset ShowtimeStart { get; set; }
        public int SeatsCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public List<Guid> SeatIds { get; set; } = new List<Guid>();
    }

    public class BookingSearchResponseDto
    {
        public List<BookingSearchResultItemDto> Items { get; set; } = new List<BookingSearchResultItemDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class BookingDetailSeatDto
    {
        public Guid SeatId { get; set; }
        public string RowLabel { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string SeatType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class BookingDetailShowtimeDto
    {
        public Guid ShowtimeId { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class BookingDetailMovieDto
    {
        public Guid MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string? Genre { get; set; }
        public string? PosterUrl { get; set; }
    }

    public class BookingDetailResponseDto
    {
        public Guid ReservationId { get; set; }
        public Guid? OrderId { get; set; }
        public string? BookingReference { get; set; }
        public string Status { get; set; } = string.Empty;
        public BookingDetailMovieDto? Movie { get; set; }
        public BookingDetailShowtimeDto? Showtime { get; set; }
        public List<BookingDetailSeatDto> Seats { get; set; } = new List<BookingDetailSeatDto>();
    }
}
