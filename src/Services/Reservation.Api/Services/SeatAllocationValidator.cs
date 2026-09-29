using System;
using System.Collections.Generic;
using System.Linq;

namespace Reservation.Api.Services;

public record SeatState(Guid SeatId, string RowLabel, int SeatNumber, bool IsOccupied);

public static class SeatAllocationValidator
{
    public static bool LeavesNewOrphanSeat(IEnumerable<SeatState> rowSeats, HashSet<Guid> requestedSeatIds)
    {
        // Group by row because gaps don't span across rows
        var rows = rowSeats.GroupBy(s => s.RowLabel);

        foreach (var row in rows)
        {
            var seatsInRow = row.OrderBy(s => s.SeatNumber).ToList();
            
            // Only process if the requested seats involve this row
            if (!seatsInRow.Any(s => requestedSeatIds.Contains(s.SeatId)))
                continue;

            int gapsBefore = CountSizeOneGaps(seatsInRow, requestedSeatIds, applyRequest: false);
            int gapsAfter = CountSizeOneGaps(seatsInRow, requestedSeatIds, applyRequest: true);

            // If the user's selection created MORE single empty seats than before, it's a violation
            if (gapsAfter > gapsBefore)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSizeOneGaps(List<SeatState> seatsInRow, HashSet<Guid> requestedSeatIds, bool applyRequest)
    {
        int singleGapCount = 0;
        int currentGapSize = 0;

        foreach (var seat in seatsInRow)
        {
            bool isOccupied = seat.IsOccupied;
            if (applyRequest && requestedSeatIds.Contains(seat.SeatId))
            {
                isOccupied = true;
            }

            if (!isOccupied)
            {
                currentGapSize++;
            }
            else
            {
                if (currentGapSize == 1)
                {
                    singleGapCount++;
                }
                currentGapSize = 0;
            }
        }

        // Check if the row ends with a gap of 1
        if (currentGapSize == 1)
        {
            singleGapCount++;
        }

        return singleGapCount;
    }
}
