namespace SchoolAdmission.Api.Models;

public class SeatHold
{
    public string HoldId { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Status { get; set; } = HoldStatus.Held;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    public bool IsActive(DateTime utcNow) =>
        Status == HoldStatus.Held && ExpiresAt > utcNow;

    public string GetEffectiveStatus(DateTime utcNow)
    {
        if (Status == HoldStatus.Held && ExpiresAt <= utcNow)
        {
            return HoldStatus.Expired;
        }
        return Status;
    }
}

