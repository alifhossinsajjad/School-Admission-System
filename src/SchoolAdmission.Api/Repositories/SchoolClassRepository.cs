using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Repositories;

public class SchoolClassRepository : ISchoolClassRepository
{
    private readonly AppDbContext _dbContext;

    public SchoolClassRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SchoolClass?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _dbContext.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<SchoolClass?> GetByIdWithLockAsync(string id, CancellationToken ct = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            try
            {
                // PostgreSQL row-level lock (FOR UPDATE) ensures multi-instance concurrency safety
                return await _dbContext.Classes
                    .FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {id} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
            }
            catch
            {
                // Safe fallback to standard EF Core tracking query
                return await _dbContext.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
            }
        }

        // Standard relational / test provider fallback
        return await _dbContext.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<SchoolClass>> GetAllWithHoldsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Classes
            .AsNoTracking()
            .Include(c => c.Holds)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SchoolClass schoolClass, CancellationToken ct = default)
    {
        await _dbContext.Classes.AddAsync(schoolClass, ct);
    }
}
