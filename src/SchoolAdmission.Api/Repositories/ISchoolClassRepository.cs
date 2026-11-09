using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Repositories;

public interface ISchoolClassRepository
{
    Task<SchoolClass?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SchoolClass?> GetByIdWithLockAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<SchoolClass>> GetAllWithHoldsAsync(CancellationToken ct = default);
    Task AddAsync(SchoolClass schoolClass, CancellationToken ct = default);
}
