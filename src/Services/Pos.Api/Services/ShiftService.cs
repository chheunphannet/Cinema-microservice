using Microsoft.AspNetCore.Http;
using Pos.Api.Models;
using Pos.Api.Repositories;

namespace Pos.Api.Services;

public class ShiftService : IShiftService
{
    private readonly IPosRepository _repository;

    public ShiftService(IPosRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> OpenShiftAsync(OpenShiftRequest request)
    {
        var shiftId = Guid.NewGuid();
        await _repository.CreateTillShiftAsync(shiftId, request.BranchId, request.CashierId, request.TerminalCode, request.OpeningFloat);

        return Results.Created($"/api/v1/pos/shifts/{shiftId}", new
        {
            shiftId,
            branchId = request.BranchId,
            cashierId = request.CashierId,
            terminalCode = request.TerminalCode,
            openingFloat = request.OpeningFloat,
            status = Constants.Shifts.Open,
            openedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<IResult> CloseShiftAsync(CloseShiftRequest request)
    {
        var row = await _repository.CloseTillShiftAsync(request.ShiftId, request.ClosingCash);

        if (row == null)
        {
            return Results.NotFound(new { error = $"Shift {request.ShiftId} not found." });
        }

        return Results.Ok(new
        {
            shiftId = (Guid)row.shift_id,
            openingFloat = (decimal)row.opening_float,
            closingCash = (decimal)row.closing_cash,
            discrepancy = (decimal)row.closing_cash - (decimal)row.opening_float,
            status = Constants.Shifts.Closed,
            openedAt = (DateTimeOffset)row.opened_at,
            closedAt = (DateTimeOffset)row.closed_at
        });
    }
}
