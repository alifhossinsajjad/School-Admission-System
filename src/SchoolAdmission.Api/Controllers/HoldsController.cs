using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Exceptions;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public HoldsController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    [HttpGet("{holdId}")]
    public async Task<ActionResult<HoldResponse>> GetHoldById(
        [FromRoute] string holdId,
        CancellationToken ct)
    {
        var hold = await _admissionService.GetHoldByIdAsync(holdId, ct);
        if (hold == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' was not found.");
        }

        return Ok(hold);
    }

    [HttpPost("{holdId}/confirm")]
    public async Task<ActionResult<ConfirmHoldResponse>> ConfirmHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Header 'X-Student-Id' is required.");
        }

        var result = await _admissionService.ConfirmHoldAsync(holdId, studentId, ct);
        return Ok(result);
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> CancelHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BadRequestException("Header 'X-Student-Id' is required.");
        }

        await _admissionService.CancelHoldAsync(holdId, studentId, ct);
        return NoContent();
    }
}
