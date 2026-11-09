using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Api.Services;

public interface IHoldService
{
    Task<CreateHoldResponse> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default);
    Task<HoldResponse?> GetHoldByIdAsync(string holdId, CancellationToken ct = default);
    Task<ConfirmHoldResponse> ConfirmHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task<bool> CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentHoldResponse>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default);
}

