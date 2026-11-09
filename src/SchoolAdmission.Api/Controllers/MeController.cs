using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly ISeatHoldService _holdService;

    public MeController(ISeatHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpGet("holds")]
    public async Task<ActionResult<IReadOnlyList<StudentHoldResponse>>> GetMyHolds(
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Header 'X-Student-Id' is required.");
        }

        var holds = await _holdService.GetStudentHoldsAsync(studentId, ct);
        return Ok(holds);
    }
}
