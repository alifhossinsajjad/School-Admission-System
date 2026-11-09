using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Repositories;

namespace SchoolAdmission.Api.Services;

public class ClassService : IClassService
{
    private readonly IClassRepository _classRepository;
    private readonly IHoldRepository _holdRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClassService> _logger;

    public ClassService(
        IClassRepository classRepository,
        IHoldRepository holdRepository,
        IUnitOfWork unitOfWork,
        ILogger<ClassService> logger)
    {
        _classRepository = classRepository;
        _holdRepository = holdRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        // Business Validation
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

        await _classRepository.AddAsync(newClass, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created class {ClassId} ({ClassName}) with {TotalSeats} seats",
            newClass.Id, newClass.Name, newClass.TotalSeats);

        return new CreateClassResponse(newClass.Id, newClass.Name, newClass.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassResponse>> GetAllClassesAsync(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var classes = await _classRepository.GetAllWithHoldsAsync(ct);

        return classes.Select(c =>
        {
            var held = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > utcNow);
            var confirmed = c.Holds.Count(h => h.Status == HoldStatus.Confirmed);
            var available = Math.Max(0, c.TotalSeats - (held + confirmed));

            return new ClassResponse(c.Id, c.Name, c.TotalSeats, held, confirmed, available);
        }).ToList();
    }

    public async Task<ClassResponse?> GetClassByIdAsync(string classId, CancellationToken ct = default)
    {
        var targetClass = await _classRepository.GetByIdAsync(classId, ct);
        if (targetClass == null)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var (heldSeats, confirmedSeats) = await _holdRepository.GetActiveSeatCountsAsync(classId, utcNow, ct);
        var availableSeats = Math.Max(0, targetClass.TotalSeats - (heldSeats + confirmedSeats));

        return new ClassResponse(
            targetClass.Id,
            targetClass.Name,
            targetClass.TotalSeats,
            heldSeats,
            confirmedSeats,
            availableSeats
        );
    }
}
