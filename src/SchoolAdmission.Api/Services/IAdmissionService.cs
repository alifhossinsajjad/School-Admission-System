using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Api.Services;

public interface IAdmissionService
{
    Task<CreateClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClassResponse>> GetAllClassesAsync(CancellationToken ct = default);
    Task<ClassResponse?> GetClassByIdAsync(string classId, CancellationToken ct = default);
    Task<CreateHoldResponse> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default);
    Task<HoldResponse?> GetHoldByIdAsync(string holdId, CancellationToken ct = default);
    Task<ConfirmHoldResponse> ConfirmHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task<bool> CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task<IReadOnlyList<StudentHoldResponse>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default);
}

