using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Repositories;

namespace SchoolAdmission.Api.Services;

public class SeatHoldService : ISeatHoldService
{
    private readonly ISchoolClassRepository _classRepository;
    private readonly ISeatHoldRepository _holdRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeatHoldService> _logger;

    public SeatHoldService(
        ISchoolClassRepository classRepository,
        ISeatHoldRepository holdRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeatHoldService> logger)
    {
        _classRepository = classRepository;
        _holdRepository = holdRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CreateHoldResponse> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default)
    {
        // 1. Business Validation (Valid input & 17-digit student birth registration number)
        ValidateClassId(classId);
        var normalizedStudentId = ValidateAndNormalizeStudentId(studentId);
        var holdDurationSeconds = GetHoldDurationSeconds();

        // 2. Atomic Transaction via UnitOfWork (100% DRY, zero manual try/catch/rollback boilerplate)
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var utcNow = DateTime.UtcNow;

            // Step A: Acquire class entity with lock via repository (concurrency safe across instances)
            var targetClass = await _classRepository.GetByIdWithLockAsync(classId, ct);
            if (targetClass == null)
            {
                throw new NotFoundException($"Class with id '{classId}' does not exist.");
            }

            // Step B: Business Rule 3 - Single active or confirmed hold per student per class
            var hasExisting = await _holdRepository.HasActiveOrConfirmedHoldAsync(classId, normalizedStudentId, utcNow, ct);
            if (hasExisting)
            {
                throw new ConflictException("Student already has an active hold or confirmed admission in this class.");
            }

            // Step C: Business Rule 1 - Enforce capacity constraint (heldSeats + confirmedSeats < totalSeats)
            var (heldSeats, confirmedSeats) = await _holdRepository.GetActiveSeatCountsAsync(classId, utcNow, ct);
            if (heldSeats + confirmedSeats >= targetClass.TotalSeats)
            {
                throw new ConflictException("No available seats left in this class.");
            }

            // Step D: Create and persist the hold
            var hold = new SeatHold
            {
                HoldId = "h_" + Guid.NewGuid().ToString("N")[..8],
                ClassId = classId,
                StudentId = normalizedStudentId,
                Status = HoldStatus.Held,
                CreatedAt = utcNow,
                ExpiresAt = utcNow.AddSeconds(holdDurationSeconds)
            };

            await _holdRepository.AddAsync(hold, ct);

            _logger.LogInformation("Student {StudentId} reserved hold {HoldId} in class {ClassId}. Expires: {ExpiresAt}",
                normalizedStudentId, hold.HoldId, classId, hold.ExpiresAt);

            return new CreateHoldResponse(
                hold.HoldId,
                hold.ClassId,
                hold.StudentId,
                hold.Status,
                hold.ExpiresAt
            );
        }, ct);
    }

    public async Task<HoldResponse?> GetHoldByIdAsync(string holdId, CancellationToken ct = default)
    {
        ValidateHoldId(holdId);

        var hold = await _holdRepository.GetByIdAsync(holdId, ct);
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
        ValidateHoldId(holdId);
        var normalizedStudentId = ValidateAndNormalizeStudentId(studentId);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var hold = await _holdRepository.GetByIdTrackingAsync(holdId, ct);
            if (hold == null)
            {
                throw new NotFoundException($"Hold with id '{holdId}' was not found.");
            }

            // Business Rule 4: Only the applicant can confirm their own application
            if (!string.Equals(hold.StudentId, normalizedStudentId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Cannot confirm an application that does not belong to you.");
            }

            // Business Rule 5: Idempotency - Confirming multiple times does not increase confirmed count
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
                }
                throw new BadRequestException("Application hold has expired and cannot be confirmed.");
            }

            hold.Status = HoldStatus.Confirmed;
            hold.ConfirmedAt = utcNow;

            _logger.LogInformation("Hold {HoldId} in class {ClassId} confirmed by student {StudentId}",
                hold.HoldId, hold.ClassId, normalizedStudentId);

            return new ConfirmHoldResponse(hold.HoldId, HoldStatus.Confirmed);
        }, ct);
    }

    public async Task<bool> CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default)
    {
        ValidateHoldId(holdId);
        var normalizedStudentId = ValidateAndNormalizeStudentId(studentId);

        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var hold = await _holdRepository.GetByIdTrackingAsync(holdId, ct);
            if (hold == null)
            {
                throw new NotFoundException($"Hold with id '{holdId}' was not found.");
            }

            // Business Rule 4: Only the applicant can cancel their own application
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

            _logger.LogInformation("Hold {HoldId} in class {ClassId} cancelled by student {StudentId}",
                hold.HoldId, hold.ClassId, normalizedStudentId);

            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<StudentHoldResponse>> GetStudentHoldsAsync(string studentId, CancellationToken ct = default)
    {
        var normalizedStudentId = ValidateAndNormalizeStudentId(studentId);
        var utcNow = DateTime.UtcNow;

        var holds = await _holdRepository.GetStudentHoldsAsync(normalizedStudentId, ct);

        return holds.Select(h => new StudentHoldResponse(
            h.HoldId,
            h.ClassId,
            h.SchoolClass?.Name ?? string.Empty,
            h.GetEffectiveStatus(utcNow),
            h.ExpiresAt
        )).ToList();
    }

    #region Private Validation Helpers

    private static void ValidateClassId(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            throw new BadRequestException("Class ID is required.");
        }
    }

    private static void ValidateHoldId(string holdId)
    {
        if (string.IsNullOrWhiteSpace(holdId))
        {
            throw new BadRequestException("Hold ID is required.");
        }
    }

    private static string ValidateAndNormalizeStudentId(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Trim().Length != 17 || !studentId.All(char.IsDigit))
        {
            throw new BadRequestException("Student ID must be exactly a 17-digit birth registration number.");
        }

        return studentId.Trim();
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

    #endregion
}
