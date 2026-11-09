using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Repositories;

public class SeatHoldRepository : ISeatHoldRepository
{
    private readonly AppDbContext _dbContext;

    public SeatHoldRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SeatHold?> GetByIdAsync(string holdId, CancellationToken ct = default)
    {
        return await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HoldId == holdId, ct);
    }

    public async Task<SeatHold?> GetByIdTrackingAsync(string holdId, CancellationToken ct = default)
    {
        return await _dbContext.Holds
            .FirstOrDefaultAsync(h => h.HoldId == holdId, ct);
    }

    public async Task<bool> HasActiveOrConfirmedHoldAsync(string classId, string studentId, DateTime utcNow, CancellationToken ct = default)
    {
        return await _dbContext.Holds.AnyAsync(h =>
            h.ClassId == classId &&
            h.StudentId == studentId &&
            (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > utcNow)),
            ct);
    }

    public async Task<(int HeldSeats, int ConfirmedSeats)> GetActiveSeatCountsAsync(string classId, DateTime utcNow, CancellationToken ct = default)
    {
        var held = await _dbContext.Holds.CountAsync(h =>
            h.ClassId == classId &&
            h.Status == HoldStatus.Held &&
            h.ExpiresAt > utcNow, ct);

        var confirmed = await _dbContext.Holds.CountAsync(h =>
            h.ClassId == classId &&
            h.Status == HoldStatus.Confirmed, ct);

        return (held, confirmed);
    }

    public async Task<IReadOnlyList<SeatHold>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default)
    {
        return await _dbContext.Holds
            .AsNoTracking()
            .Include(h => h.SchoolClass)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SeatHold>> GetExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default)
    {
        return await _dbContext.Holds
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= utcNow)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SeatHold hold, CancellationToken ct = default)
    {
        await _dbContext.Holds.AddAsync(hold, ct);
    }
}
