using Reservation.Api.Services;
using Xunit;

namespace Cinema.UnitTests;

public class Phase2SeatAllocationTests
{
    [Fact]
    public void LeavesNewOrphanSeat_WhenCreatesSingleEmptySeat_ReturnsTrue()
    {
        // Arrange
        // Row A: 5 seats. A1, A2, A3, A4, A5. A1 is Occupied. A2-A5 are Available.
        // User wants to book A3 and A4.
        // This leaves A2 as a single empty seat (orphan).
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();
        var s4 = Guid.NewGuid();
        var s5 = Guid.NewGuid();

        var rowSeats = new List<SeatState>
        {
            new(s1, "A", 1, true),
            new(s2, "A", 2, false),
            new(s3, "A", 3, false),
            new(s4, "A", 4, false),
            new(s5, "A", 5, false)
        };

        var requested = new HashSet<Guid> { s3, s4 };

        // Act
        var result = SeatAllocationValidator.LeavesNewOrphanSeat(rowSeats, requested);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LeavesNewOrphanSeat_WhenLeavesNoGaps_ReturnsFalse()
    {
        // Arrange
        // Row B: 5 seats. B1 is Occupied. 
        // User wants to book B2 and B3.
        // This leaves B4 and B5 (gap of 2), which is fine.
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();
        var s4 = Guid.NewGuid();
        var s5 = Guid.NewGuid();

        var rowSeats = new List<SeatState>
        {
            new(s1, "B", 1, true),
            new(s2, "B", 2, false),
            new(s3, "B", 3, false),
            new(s4, "B", 4, false),
            new(s5, "B", 5, false)
        };

        var requested = new HashSet<Guid> { s2, s3 };

        // Act
        var result = SeatAllocationValidator.LeavesNewOrphanSeat(rowSeats, requested);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void LeavesNewOrphanSeat_WhenRowAlreadyHasSingleSeatGap_AndUserDoesNotCreateNewOne_ReturnsFalse()
    {
        // Arrange
        // Row C: 5 seats. C2 is Occupied, C4 is Occupied. 
        // [Available, Occupied, Available, Occupied, Available]
        // Gaps before: C1 (size 1), C3 (size 1), C5 (size 1).
        // User requests C1.
        // After: C1 is Occupied. Gaps after: C3 (size 1), C5 (size 1).
        // Gaps of size 1 went from 3 down to 2. So they didn't *create* a new one.
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();
        var s4 = Guid.NewGuid();
        var s5 = Guid.NewGuid();

        var rowSeats = new List<SeatState>
        {
            new(s1, "C", 1, false),
            new(s2, "C", 2, true),
            new(s3, "C", 3, false),
            new(s4, "C", 4, true),
            new(s5, "C", 5, false)
        };

        var requested = new HashSet<Guid> { s1 };

        // Act
        var result = SeatAllocationValidator.LeavesNewOrphanSeat(rowSeats, requested);

        // Assert
        Assert.False(result);
    }
}
