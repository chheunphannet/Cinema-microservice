using System.Text.Json.Serialization;

namespace Identity.Api.Models;

public record BranchPerformance(Guid BranchId, string BranchName, decimal Revenue, int TicketsSold);
public record TopMovie(Guid MovieId, string Title, decimal Revenue, int TicketsSold);
public record ExecutiveDashboardDto(
    decimal TotalGrossRevenue,
    decimal BoxOfficeRevenue,
    decimal FnBRevenue,
    int TotalTicketsSold,
    double OverallOccupancyRate,
    List<BranchPerformance> Branches,
    List<TopMovie> TopMovies
);

public record SalesRevenueDashboardDto(
    decimal HourlyGross,
    decimal DailyGross,
    Dictionary<string, decimal> PaymentMethodShare,
    decimal AverageOrderValue,
    decimal AverageTicketYield
);

public record AuditoriumUtilization(Guid AuditoriumId, string Name, double Utilization);
public record ShowtimeMetric(string SlotTime, double AverageOccupancy);
public record OccupancyDashboardDto(
    double SeatUtilizationPercentage,
    double RevenuePerAvailableSeat,
    List<AuditoriumUtilization> ScreenBreakdown,
    List<ShowtimeMetric> PeakVsDeadSlots
);

public record ProductStockAlert(Guid ProductId, string ProductName, int CurrentStock, int Threshold);
public record TopSellingItem(Guid ProductId, string ProductName, int QuantitySold);
public record InventoryDashboardDto(
    List<ProductStockAlert> LowStockAlerts,
    decimal WastageTotalCost,
    List<TopSellingItem> FastMovingItems,
    decimal InventoryTurnoverRatio
);

public record ProfitLossDashboardDto(
    decimal GrossBoxOffice,
    decimal DistributorFilmHireCut,
    decimal NetBoxOffice,
    decimal GrossConcessions,
    decimal CostOfGoodsSold,
    decimal NetOperatingIncome
);

public record CustomerCRMDashboardDto(
    int TotalRegisteredCustomers,
    int ActiveMembers30Days,
    Dictionary<string, int> TierDistribution,
    int TotalUnredeemedPointsLiability
);

public record MarketingDashboardDto(
    int CampaignsActive,
    decimal PromoCodeDiscountsGiven,
    decimal AttributedRevenue,
    double CampaignConversionRate
);

public record FunnelDashboardDto(
    int ShowtimePageViews,
    int SeatSelectionHolds,
    int CheckoutInitiated,
    int OrdersPaid,
    double FunnelConversionRate,
    double AbandonmentDropOffRate
);

public record AuditoriumStatus(Guid AuditoriumId, string Name, string Status);
public record OperationsDashboardDto(
    int TodayTotalShowtimes,
    int CompletedShowtimes,
    int GateScansCount,
    int OpenTillShiftsCount,
    List<AuditoriumStatus> CurrentScreenStatus
);

public record SystemHealthDashboardDto(
    string DatabaseStatus,
    double DbQueryP99Latency,
    string RedisStatus,
    long RedisMemoryUsedBytes,
    string RabbitMqStatus,
    int QueueMessageBacklog,
    string StorageStatus
);

public record SuspiciousAccount(Guid CustomerId, string Email, string Reason);
public record FraudRiskDashboardDto(
    int HighVolumeRefundCount,
    decimal TotalRefundAmount,
    int FailedPaymentSpikes,
    List<SuspiciousAccount> FlaggedAccounts
);
