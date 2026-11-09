using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly IClassService _classService;
    private readonly IHoldService _holdService;

    public ClassesController(IClassService classService, IHoldService holdService)
    {
        _classService = classService;
        _holdService = holdService;
    }

    [HttpPost]
    public async Task<ActionResult<CreateClassResponse>> CreateClass(
        [FromBody] CreateClassRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            throw new BadRequestException("Request body cannot be null.");
        }

        var result = await _classService.CreateClassAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassResponse>>> GetAllClasses(CancellationToken ct)
    {
        var classes = await _classService.GetAllClassesAsync(ct);
        return Ok(classes);
    }

    [HttpGet("{classId}")]
    public async Task<ActionResult<ClassResponse>> GetClassById(
        [FromRoute] string classId,
        CancellationToken ct)
    {
        var classData = await _classService.GetClassByIdAsync(classId, ct);
        if (classData == null)
        {
            throw new NotFoundException($"Class with id '{classId}' was not found.");
        }

        return Ok(classData);
    }

    [HttpPost("{classId}/holds")]
    public async Task<ActionResult<CreateHoldResponse>> CreateHold(
        [FromRoute] string classId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Header 'X-Student-Id' is required.");
        }

        var result = await _holdService.CreateHoldAsync(classId, studentId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
