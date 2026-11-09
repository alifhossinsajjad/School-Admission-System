using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Repositories;

public interface IHoldRepository
{
    Task<SeatHold?> GetByIdAsync(string holdId, CancellationToken ct = default);
    Task<SeatHold?> GetByIdTrackingAsync(string holdId, CancellationToken ct = default);
    Task<bool> HasActiveOrConfirmedHoldAsync(string classId, string studentId, DateTime utcNow, CancellationToken ct = default);
    Task<(int HeldSeats, int ConfirmedSeats)> GetActiveSeatCountsAsync(string classId, DateTime utcNow, CancellationToken ct = default);
    Task<IReadOnlyList<SeatHold>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default);
    Task<IReadOnlyList<SeatHold>> GetExpiredHoldsAsync(DateTime utcNow, CancellationToken ct = default);
    Task AddAsync(SeatHold hold, CancellationToken ct = default);
}

