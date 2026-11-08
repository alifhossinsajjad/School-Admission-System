using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class AdmissionService : IAdmissionService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdmissionService> _logger;

    // In-memory per-class locks for non-Postgres providers (e.g. SQLite / InMemory in test environments)
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _classLocks = new();

    public AdmissionService(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<AdmissionService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetHoldDurationSeconds()
    {
        var envValue = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
        if (!string.IsNullOrWhiteSpace(envValue) && int.TryParse(envValue, out var envSec) && envSec > 0)
        {
            return envSec;
        }

        var configValue = _configuration.GetValue<int?>("HOLD_DURATION_SECONDS");
        if (configValue.HasValue && configValue.Value > 0)
        {
            return configValue.Value;
        }

        return 600; // Default 10 minutes (600 seconds)
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

    public async Task<CreateHoldResponse> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            throw new BadRequestException("Class ID is required.");
        }

        if (string.IsNullOrWhiteSpace(studentId) || studentId.Trim().Length != 17 || !studentId.All(char.IsDigit))
        {
            throw new BadRequestException("Student ID must be exactly a 17-digit birth registration number.");
        }

        var normalizedStudentId = studentId.Trim();
        var holdDurationSeconds = GetHoldDurationSeconds();

        // Concurrency Control:
        // When running on PostgreSQL, we execute SELECT ... FOR UPDATE on the class row inside a transaction.
        // This ensures cross-process & cross-instance mutual exclusion on the database level.
        // For in-memory / SQLite provider in unit tests, we supplement with a class-level SemaphoreSlim.
        var classLock = _classLocks.GetOrAdd(classId, _ => new SemaphoreSlim(1, 1));
        await classLock.WaitAsync(ct);

        try
        {
            var isRelational = _dbContext.Database.IsRelational();
            var isPostgres = _dbContext.Database.IsNpgsql();

            IDbContextTransaction? tx = null;
            if (isRelational)
            {
                tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            }

            try
            {
                SchoolClass? targetClass;
                if (isPostgres)
                {
                    // PostgreSQL row-level lock (FOR UPDATE)
                    targetClass = await _dbContext.Classes
                        .FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {classId} FOR UPDATE")
                        .FirstOrDefaultAsync(ct);
                }
                else
                {
                    targetClass = await _dbContext.Classes
                        .FirstOrDefaultAsync(c => c.Id == classId, ct);
                }

                if (targetClass == null)
                {
                    throw new NotFoundException($"Class with id '{classId}' does not exist.");
                }

                var utcNow = DateTime.UtcNow;

                // Rule 3: একজন শিক্ষার্থী একটা class-এ একই সময়ে একটার বেশি active আবেদন বা নিশ্চিত ভর্তি রাখতে পারবে না।
                var existingActiveOrConfirmed = await _dbContext.Holds.AnyAsync(h =>
                    h.ClassId == classId &&
                    h.StudentId == normalizedStudentId &&
                    (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > utcNow)),
                    ct);

                if (existingActiveOrConfirmed)
                {
                    throw new ConflictException("Student already has an active hold or confirmed admission in this class.");
                }

                // Rule 1: কোনো অবস্থাতেই একটা class-এ held + confirmed সংখ্যা totalSeats-এর বেশি হবে না
                var currentHeldSeats = await _dbContext.Holds.CountAsync(h =>
                    h.ClassId == classId &&
                    h.Status == HoldStatus.Held &&
                    h.ExpiresAt > utcNow, ct);

                var currentConfirmedSeats = await _dbContext.Holds.CountAsync(h =>
                    h.ClassId == classId &&
                    h.Status == HoldStatus.Confirmed, ct);

                if (currentHeldSeats + currentConfirmedSeats >= targetClass.TotalSeats)
                {
                    throw new ConflictException("No available seats left in this class.");
                }

                var hold = new SeatHold
                {
                    HoldId = "h_" + Guid.NewGuid().ToString("N")[..8],
                    ClassId = classId,
                    StudentId = normalizedStudentId,
                    Status = HoldStatus.Held,
                    CreatedAt = utcNow,
                    ExpiresAt = utcNow.AddSeconds(holdDurationSeconds)
                };

                _dbContext.Holds.Add(hold);
                await _dbContext.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }

                _logger.LogInformation("Student {StudentId} placed hold {HoldId} on class {ClassId}. Expires at {ExpiresAt}",
                    normalizedStudentId, hold.HoldId, classId, hold.ExpiresAt);

                return new CreateHoldResponse(
                    hold.HoldId,
                    hold.ClassId,
                    hold.StudentId,
                    hold.Status,
                    hold.ExpiresAt
                );
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
            finally
            {
                if (tx != null)
                {
                    await tx.DisposeAsync();
                }
            }
        }
        finally
        {
            classLock.Release();
        }
    }

    public async Task<HoldResponse?> GetHoldByIdAsync(string holdId, CancellationToken ct = default)
    {
        var hold = await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HoldId == holdId, ct);

        if (hold == null)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var effectiveStatus = hold.GetEffectiveStatus(utcNow);

        return new HoldResponse(
            hold.HoldId,
            hold.ClassId,
            effectiveStatus,
            hold.ExpiresAt
        );
    }

    public async Task<ConfirmHoldResponse> ConfirmHoldAsync(string holdId, string studentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(holdId))
        {
            throw new BadRequestException("Hold ID is required.");
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Student ID is required.");
        }

        var normalizedStudentId = studentId.Trim();
        var hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.HoldId == holdId, ct);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' was not found.");
        }

        // Rule 4: শুধু নিজের, মেয়াদ না পেরোনো HELD আবেদন confirm করা যাবে।
        if (!string.Equals(hold.StudentId, normalizedStudentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Cannot confirm an application that does not belong to you.");
        }

        // Rule 5: একই আবেদনে confirm একাধিকবার এলেও confirmedSeats একবারই বাড়বে। (Idempotent)
        if (hold.Status == HoldStatus.Confirmed)
        {
            return new ConfirmHoldResponse(hold.HoldId, HoldStatus.Confirmed);
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            throw new BadRequestException("Application has been cancelled and cannot be confirmed.");
        }

        var utcNow = DateTime.UtcNow;
        if (hold.Status == HoldStatus.Expired || (hold.Status == HoldStatus.Held && hold.ExpiresAt <= utcNow))
        {
            if (hold.Status == HoldStatus.Held)
            {
                hold.Status = HoldStatus.Expired;
                await _dbContext.SaveChangesAsync(ct);
            }
            throw new BadRequestException("Application hold has expired and cannot be confirmed.");
        }

        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = utcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Hold {HoldId} for class {ClassId} confirmed by student {StudentId}",
            hold.HoldId, hold.ClassId, normalizedStudentId);

        return new ConfirmHoldResponse(hold.HoldId, HoldStatus.Confirmed);
    }

    public async Task<bool> CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(holdId))
        {
            throw new BadRequestException("Hold ID is required.");
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Student ID is required.");
        }

        var normalizedStudentId = studentId.Trim();
        var hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.HoldId == holdId, ct);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' was not found.");
        }

        if (!string.Equals(hold.StudentId, normalizedStudentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Cannot cancel an application that does not belong to you.");
        }

        if (hold.Status == HoldStatus.Confirmed)
        {
            throw new BadRequestException("Cannot cancel a confirmed admission.");
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            return true; // Already cancelled, idempotent success (204)
        }

        hold.Status = HoldStatus.Cancelled;
        hold.CancelledAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Hold {HoldId} for class {ClassId} cancelled by student {StudentId}",
            hold.HoldId, hold.ClassId, normalizedStudentId);

        return true;
    }

    public async Task<IReadOnlyList<StudentHoldResponse>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Trim().Length != 17 || !studentId.All(char.IsDigit))
        {
            throw new BadRequestException("Student ID must be exactly a 17-digit birth registration number.");
        }

        var normalizedStudentId = studentId.Trim();
        var utcNow = DateTime.UtcNow;

        var holds = await _dbContext.Holds
            .AsNoTracking()
            .Include(h => h.SchoolClass)
            .Where(h => h.StudentId == normalizedStudentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return holds.Select(h => new StudentHoldResponse(
            h.HoldId,
            h.ClassId,
            h.SchoolClass?.Name ?? string.Empty,
            h.GetEffectiveStatus(utcNow),
            h.ExpiresAt
        )).ToList();
    }
}
