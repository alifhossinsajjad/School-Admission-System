using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Api.Services;

public interface ISchoolClassService
{
    Task<CreateClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClassResponse>> GetAllClassesAsync(CancellationToken ct = default);
    Task<ClassResponse?> GetClassByIdAsync(string classId, CancellationToken ct = default);
}
