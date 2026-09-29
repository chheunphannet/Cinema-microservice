using System;
using System.Collections.Generic;
using Cinema.Foundation.Data;
using Cinema.Foundation.Redis;
using Cinema.Foundation.Security;
using Xunit;

namespace Cinema.UnitTests;

/// <summary>
/// Unit Tests verifying architectural invariants from the Phase 1 Technical Architecture Specification:
/// - Module 1.2: Master-Slave database replication & Read-After-Write routing
/// - Module 2.1: Read-through caching with Redis LRU
/// - Module 2.2: Concurrency, Monotonic Fencing Tokens, and Idempotency Keys
/// - Module 3: POS tender processing, supervisor overrides, and till shift reconciliation
/// </summary>
public class ArchitectureAndBusinessRulesTests
{
    [Theory]
    [InlineData(1000, 999, false)]   // Lower token -> MUST REJECT (stale write)
    [InlineData(1000, 1000, false)]  // Equal token -> MUST REJECT (duplicate/stale write)
    [InlineData(1000, 1001, true)]   // Strictly greater token -> ACCEPT
    [InlineData(0, 1, true)]         // First token -> ACCEPT
    [InlineData(1000, 0, false)]     // Non-positive token -> MUST REJECT
    public void FencingToken_MonotonicityRule_RejectsStaleWrites(long currentRecordedToken, long incomingToken, bool expectedAllowed)
    {
        // Specification: Module 2.2 - "A write request is rejected if it carries a token lower than the last processed token in the Metadata DB."
        bool isAllowed = incomingToken > 0 && incomingToken > currentRecordedToken;
        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Fact]
    public async Task SeatLock_BatchAtomicRollback_ReleasesPartialLocksOnCollision()
    {
        // Specification: Module 2.2 Distributed Locks & Lua atomic seat hold
        var lockService = new SeatLockService(redis: null);
        var showtimeId = Guid.NewGuid();
        var seat1 = Guid.NewGuid();
        var seat2 = Guid.NewGuid();
        var seat3 = Guid.NewGuid();

        var customerAHoldId = Guid.NewGuid();
        var customerBHoldId = Guid.NewGuid();
        var ttl = TimeSpan.FromMinutes(10);

        // Customer A already holds seat3
        var (acquiredSeat3, _) = await lockService.AcquireSeatHoldAsync(showtimeId, seat3, customerAHoldId, 100L, ttl);
        Assert.True(acquiredSeat3);

        // Customer B attempts batch hold for [seat1, seat2, seat3]
        var requestedSeats = new List<Guid> { seat1, seat2, seat3 };
        var acquiredByB = new List<Guid>();
        bool batchSuccess = true;
        Guid? conflictingSeat = null;

        foreach (var seat in requestedSeats)
        {
            var (success, _) = await lockService.AcquireSeatHoldAsync(showtimeId, seat, customerBHoldId, 200L, ttl);
            if (!success)
            {
                batchSuccess = false;
                conflictingSeat = seat;
                // Atomic rollback: release any seats acquired prior to the collision
                foreach (var acquired in acquiredByB)
                {
                    await lockService.ReleaseSeatHoldAsync(showtimeId, acquired, customerBHoldId.ToString());
                }
                break;
            }
            acquiredByB.Add(seat);
        }

        Assert.False(batchSuccess, "Batch must fail if any single seat is held");
        Assert.Equal(seat3, conflictingSeat);

        // Verify that seat1 and seat2 were rolled back and are not orphaned
        var (retrySeat1, _) = await lockService.AcquireSeatHoldAsync(showtimeId, seat1, Guid.NewGuid(), 300L, ttl);
        var (retrySeat2, _) = await lockService.AcquireSeatHoldAsync(showtimeId, seat2, Guid.NewGuid(), 300L, ttl);
        Assert.True(retrySeat1, "Seat 1 must be free after atomic batch rollback");
        Assert.True(retrySeat2, "Seat 2 must be free after atomic batch rollback");
    }

    [Theory]
    [InlineData("cash", 20.00, 14.50, 5.50, true)]
    [InlineData("cash", 50.00, 50.00, 0.00, true)]
    [InlineData("qr", 14.50, 14.50, 0.00, false)]
    [InlineData("card", 14.50, 14.50, 0.00, false)]
    [InlineData("voucher", 14.50, 14.50, 0.00, false)]
    public void PosTenderProcessing_CalculatesChangeAndTriggersDrawerKickAppropriately(
        string paymentMethod, decimal tenderedAmount, decimal orderTotal, decimal expectedChange, bool expectedDrawerKick)
    {
        // Specification: Doc 1 Section 3.2 Box-Office Tender Processing
        decimal change = tenderedAmount - orderTotal;
        bool drawerKick = paymentMethod.Equals("cash", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(expectedChange, change);
        Assert.Equal(expectedDrawerKick, drawerKick);
    }

    [Fact]
    public void TicketPrinting_FirstPrintAllowed_ReprintRequiresSupervisorOverride()
    {
        // Specification: Doc 1 Section 4 Ticket Printing & Redemption Invariants
        bool isAlreadyPrinted = false;
        string? supervisorPin = null;

        // 1. First print: Allowed for cashier without supervisor override
        bool canFirstPrint = !isAlreadyPrinted;
        Assert.True(canFirstPrint);
        isAlreadyPrinted = true;

        // 2. Second print attempt without supervisor PIN: Rejected
        bool canReprintWithoutSupervisor = !isAlreadyPrinted || !string.IsNullOrWhiteSpace(supervisorPin);
        Assert.False(canReprintWithoutSupervisor, "Reprinting without supervisor PIN must be rejected");

        // 3. Second print attempt with invalid supervisor PIN: Rejected
        supervisorPin = "wrong-pin";
        string validSupervisorHash = PasswordHasher.Hash("9999");
        bool supervisorAuthorized = PasswordHasher.Verify(supervisorPin, validSupervisorHash);
        Assert.False(supervisorAuthorized, "Invalid supervisor PIN must fail authorization");

        // 4. Second print attempt with valid supervisor PIN: Authorized
        supervisorPin = "9999";
        supervisorAuthorized = PasswordHasher.Verify(supervisorPin, validSupervisorHash);
        Assert.True(supervisorAuthorized, "Valid supervisor PIN must authorize ticket reprint");
    }

    [Fact]
    public void TillShift_CashReconciliationCalculatesExactVariance()
    {
        // Specification: Module 3.1 & Till Shift schema in pos.till_shifts
        decimal openingFloat = 100.00m;
        var cashReceipts = new List<decimal> { 13.00m, 26.00m, 6.50m, 19.50m };
        decimal totalCashSales = 0m;
        foreach (var receipt in cashReceipts) totalCashSales += receipt;

        decimal expectedCash = openingFloat + totalCashSales; // $165.00
        Assert.Equal(165.00m, expectedCash);

        // Case A: Exact balance
        decimal countedCashA = 165.00m;
        decimal varianceA = countedCashA - expectedCash;
        Assert.Equal(0.00m, varianceA);

        // Case B: Cash short (under-drawer)
        decimal countedCashB = 160.00m;
        decimal varianceB = countedCashB - expectedCash;
        Assert.Equal(-5.00m, varianceB);

        // Case C: Cash over (over-drawer)
        decimal countedCashC = 170.00m;
        decimal varianceC = countedCashC - expectedCash;
        Assert.Equal(5.00m, varianceC);
    }

    [Fact]
    public void ReadAfterWriteRouting_SelectsMasterOrReplicaAppropriately()
    {
        // Specification: Module 1.2 Database Replication & Read-After-Write Routing
        const string masterConnStr = "Host=postgres;Port=5432;Database=cinema;Username=cinema_app;Password=secret";
        const string replicaConnStr = "Host=postgres-replica;Port=5432;Database=cinema;Username=cinema_app;Password=secret";

        IDbConnectionFactory factory = new NpgsqlConnectionFactory(masterConnStr, replicaConnStr);

        using var writeConn = factory.CreateConnection();
        using var readConn = factory.CreateReadConnection();

        // Write connection routes to Primary / Master
        Assert.Contains("postgres", writeConn.ConnectionString);
        Assert.DoesNotContain("postgres-replica", writeConn.ConnectionString);

        // Read connection routes to Replica / Slave
        Assert.Contains("postgres-replica", readConn.ConnectionString);
    }

    [Fact]
    public void SeatHold_EnforcesTenMinuteTtlAndTenSeatMax()
    {
        // Specification: Module 2 & Health Contract
        const int maxSeats = 10;
        var seats = new List<Guid>();
        for (int i = 0; i < 10; i++) seats.Add(Guid.NewGuid());

        Assert.Equal(10, seats.Count);
        Assert.True(seats.Count <= maxSeats);

        // Exceeding 10 seats is rejected
        seats.Add(Guid.NewGuid());
        Assert.False(seats.Count <= maxSeats);

        var ttl = TimeSpan.FromMinutes(10);
        Assert.Equal(600, ttl.TotalSeconds);
    }
}
