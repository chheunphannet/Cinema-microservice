using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Api.Endpoints;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Identity.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace Cinema.UnitTests;

public class AdminDashboardTests
{
    private readonly Mock<IAdminDashboardRepository> _mockRepo = new();
    private readonly Mock<IDistributedCache> _mockCache = new();

    private static DefaultHttpContext CreateHttpContext(string role, Guid? claimBranchId = null, Guid? requestBranchId = null)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (claimBranchId.HasValue) claims.Add(new Claim("branchId", claimBranchId.Value.ToString()));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        
        if (requestBranchId.HasValue)
        {
            context.Request.Headers["X-Branch-Id"] = requestBranchId.Value.ToString();
        }
        return context;
    }

    [Fact]
    public async Task Service_UsesCache_ForDashboards()
    {
        // Arrange
        var service = new AdminDashboardService(_mockRepo.Object, _mockCache.Object);
        var execDto = new ExecutiveDashboardDto(100, 50, 50, 10, 0.5, new List<BranchPerformance>(), new List<TopMovie>());
        
        _mockCache.Setup(c => c.GetAsync("exec_dash__", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockRepo.Setup(r => r.GetExecutiveDashboardAsync(null, null))
            .ReturnsAsync(execDto);

        // Act
        var result = await service.GetExecutiveDashboardAsync(null, null);

        // Assert
        Assert.Equal(100, result.TotalGrossRevenue);
        _mockRepo.Verify(r => r.GetExecutiveDashboardAsync(null, null), Times.Once);
        _mockCache.Verify(c => c.SetAsync("exec_dash__", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsTenantAuthorized_SuperAdmin_CanAccessAnyBranch()
    {
        var context = CreateHttpContext("super_admin", requestBranchId: Guid.NewGuid());
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out _);
        Assert.True(result);
    }

    [Fact]
    public void IsTenantAuthorized_Executive_CanAccessAnyBranch()
    {
        var context = CreateHttpContext("executive", requestBranchId: Guid.NewGuid());
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out _);
        Assert.True(result);
    }

    [Fact]
    public void IsTenantAuthorized_BranchManager_CanAccessOwnBranch()
    {
        var branchId = Guid.NewGuid();
        var context = CreateHttpContext("branch_manager", claimBranchId: branchId, requestBranchId: branchId);
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out var parsedId);
        Assert.True(result);
        Assert.Equal(branchId, parsedId);
    }

    [Fact]
    public void IsTenantAuthorized_BranchManager_CannotAccessOtherBranch()
    {
        var branchId = Guid.NewGuid();
        var context = CreateHttpContext("branch_manager", claimBranchId: branchId, requestBranchId: Guid.NewGuid());
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out _);
        Assert.False(result);
    }

    [Fact]
    public void IsTenantAuthorized_StaffWithoutBranch_Fails()
    {
        var context = CreateHttpContext("staff", requestBranchId: Guid.NewGuid());
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out _);
        Assert.False(result);
    }
    
    [Fact]
    public void IsTenantAuthorized_BranchManager_CannotAccessGlobal()
    {
        var context = CreateHttpContext("branch_manager", claimBranchId: Guid.NewGuid());
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out _);
        Assert.False(result);
    }

    [Fact]
    public async Task Service_UsesCache_ForBranchExecutiveDashboard()
    {
        // Arrange
        var service = new AdminDashboardService(_mockRepo.Object, _mockCache.Object);
        var execDto = new ExecutiveDashboardDto(80, 40, 40, 8, 0.6, new List<BranchPerformance>(), new List<TopMovie>());
        var branchId = Guid.NewGuid();
        var cacheKey = $"exec_dash_{branchId}__";

        _mockCache.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockRepo.Setup(r => r.GetExecutiveDashboardAsync(branchId, null, null))
            .ReturnsAsync(execDto);

        // Act
        var result = await service.GetExecutiveDashboardAsync(branchId, null, null);

        // Assert
        Assert.Equal(80, result.TotalGrossRevenue);
        _mockRepo.Verify(r => r.GetExecutiveDashboardAsync(branchId, null, null), Times.Once);
        _mockCache.Verify(c => c.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsTenantAuthorized_SupportsQueryBranchIdCamelCase()
    {
        var context = new DefaultHttpContext();
        var branchId = Guid.NewGuid();
        context.Request.QueryString = new QueryString($"?branchId={branchId}");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "super_admin") }, "TestAuth"));
        var result = AdminDashboardEndpoints.IsTenantAuthorized(context, out var parsedId);
        Assert.True(result);
        Assert.Equal(branchId, parsedId);
    }
}
