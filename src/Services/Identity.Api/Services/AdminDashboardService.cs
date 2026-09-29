using Identity.Api.Models;
using Identity.Api.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Identity.Api.Services;

public interface IAdminDashboardService
{
    Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(Guid? branchId, DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<SalesRevenueDashboardDto> GetSalesRevenueDashboardAsync(Guid? branchId, string? granularity);
    Task<OccupancyDashboardDto> GetOccupancyDashboardAsync(Guid? branchId, Guid? movieId);
    Task<InventoryDashboardDto> GetInventoryDashboardAsync(Guid? branchId);
    Task<ProfitLossDashboardDto> GetProfitLossDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<CustomerCRMDashboardDto> GetCustomerCRMDashboardAsync();
    Task<MarketingDashboardDto> GetMarketingDashboardAsync(Guid? campaignId);
    Task<FunnelDashboardDto> GetFunnelDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<OperationsDashboardDto> GetOperationsDashboardAsync(Guid? branchId);
    Task<SystemHealthDashboardDto> GetSystemHealthDashboardAsync();
    Task<FraudRiskDashboardDto> GetFraudRiskDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate);
}

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IAdminDashboardRepository _repository;
    private readonly IDistributedCache _cache;

    public AdminDashboardService(IAdminDashboardRepository repository, IDistributedCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private async Task<T> GetCachedAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan ttl)
    {
        var cachedStr = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedStr))
        {
            return JsonSerializer.Deserialize<T>(cachedStr)!;
        }

        var data = await factory();
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(data), options);
        return data;
    }

    public async Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        => await GetCachedAsync($"exec_dash_{fromDate}_{toDate}", () => _repository.GetExecutiveDashboardAsync(fromDate, toDate), TimeSpan.FromMinutes(5));

    public async Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(Guid? branchId, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        if (!branchId.HasValue)
        {
            return await GetExecutiveDashboardAsync(fromDate, toDate);
        }
        var cacheKey = $"exec_dash_{branchId}_{fromDate}_{toDate}";
        return await GetCachedAsync(cacheKey, () => _repository.GetExecutiveDashboardAsync(branchId, fromDate, toDate), TimeSpan.FromMinutes(5));
    }

    public async Task<SalesRevenueDashboardDto> GetSalesRevenueDashboardAsync(Guid? branchId, string? granularity)
        => await GetCachedAsync($"sales_dash_{branchId}_{granularity}", () => _repository.GetSalesRevenueDashboardAsync(branchId, granularity), TimeSpan.FromSeconds(30));

    public async Task<OccupancyDashboardDto> GetOccupancyDashboardAsync(Guid? branchId, Guid? movieId)
        => await GetCachedAsync($"occupancy_dash_{branchId}_{movieId}", () => _repository.GetOccupancyDashboardAsync(branchId, movieId), TimeSpan.FromMinutes(1));

    public async Task<InventoryDashboardDto> GetInventoryDashboardAsync(Guid? branchId)
        => await GetCachedAsync($"inv_dash_{branchId}", () => _repository.GetInventoryDashboardAsync(branchId), TimeSpan.FromMinutes(2));

    public async Task<ProfitLossDashboardDto> GetProfitLossDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        => await GetCachedAsync($"pnl_dash_{fromDate}_{toDate}", () => _repository.GetProfitLossDashboardAsync(fromDate, toDate), TimeSpan.FromMinutes(5));

    public async Task<CustomerCRMDashboardDto> GetCustomerCRMDashboardAsync()
        => await GetCachedAsync($"crm_dash", () => _repository.GetCustomerCRMDashboardAsync(), TimeSpan.FromMinutes(5));

    public async Task<MarketingDashboardDto> GetMarketingDashboardAsync(Guid? campaignId)
        => await GetCachedAsync($"mkt_dash_{campaignId}", () => _repository.GetMarketingDashboardAsync(campaignId), TimeSpan.FromMinutes(5));

    public async Task<FunnelDashboardDto> GetFunnelDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        => await GetCachedAsync($"funnel_dash_{fromDate}_{toDate}", () => _repository.GetFunnelDashboardAsync(fromDate, toDate), TimeSpan.FromMinutes(2));

    public async Task<OperationsDashboardDto> GetOperationsDashboardAsync(Guid? branchId)
        => await GetCachedAsync($"ops_dash_{branchId}", () => _repository.GetOperationsDashboardAsync(branchId), TimeSpan.FromSeconds(30));

    public async Task<SystemHealthDashboardDto> GetSystemHealthDashboardAsync()
        => await GetCachedAsync($"syshealth_dash", () => _repository.GetSystemHealthDashboardAsync(), TimeSpan.FromSeconds(30));

    public async Task<FraudRiskDashboardDto> GetFraudRiskDashboardAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        => await GetCachedAsync($"fraud_dash_{fromDate}_{toDate}", () => _repository.GetFraudRiskDashboardAsync(fromDate, toDate), TimeSpan.FromMinutes(2));
}
