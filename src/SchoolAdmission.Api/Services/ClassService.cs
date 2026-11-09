using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class ClassService : IClassService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ClassService> _logger;

    public ClassService(AppDbContext dbContext, ILogger<ClassService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CreateClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Class name cannot be empty.");
        }

        if (request.TotalSeats <= 0)
        {
            throw new BadRequestException("Total seats must be greater than zero.");
        }

        var newClass = new SchoolClass
        {
            Id = "c_" + Guid.NewGuid().ToString("N")[..8],
            Name = request.Name.Trim(),
            TotalSeats = request.TotalSeats,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Classes.Add(newClass);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created class {ClassId} ({ClassName}) with {TotalSeats} seats",
            newClass.Id, newClass.Name, newClass.TotalSeats);

        return new CreateClassResponse(newClass.Id, newClass.Name, newClass.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassResponse>> GetAllClassesAsync(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;

        var classes = await _dbContext.Classes
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                HeldCount = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > utcNow),
                ConfirmedCount = c.Holds.Count(h => h.Status == HoldStatus.Confirmed)
            })
            .ToListAsync(ct);

        return classes.Select(c => new ClassResponse(
            c.Id,
            c.Name,
            c.TotalSeats,
            c.HeldCount,
            c.ConfirmedCount,
            Math.Max(0, c.TotalSeats - (c.HeldCount + c.ConfirmedCount))
        )).ToList();
    }

    public async Task<ClassResponse?> GetClassByIdAsync(string classId, CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;

        var classData = await _dbContext.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                HeldCount = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > utcNow),
                ConfirmedCount = c.Holds.Count(h => h.Status == HoldStatus.Confirmed)
            })
            .FirstOrDefaultAsync(ct);

        if (classData == null)
        {
            return null;
        }

        return new ClassResponse(
            classData.Id,
            classData.Name,
            classData.TotalSeats,
            classData.HeldCount,
            classData.ConfirmedCount,
            Math.Max(0, classData.TotalSeats - (classData.HeldCount + classData.ConfirmedCount))
        );
    }
}
