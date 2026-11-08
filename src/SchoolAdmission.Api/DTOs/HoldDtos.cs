using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public class CreateHoldResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    public CreateHoldResponse(string holdId, string classId, string studentId, string status, DateTime expiresAt)
    {
        HoldId = holdId;
        ClassId = classId;
        StudentId = studentId;
        Status = status;
        ExpiresAt = expiresAt;
    }
}

public class HoldResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    public HoldResponse(string holdId, string classId, string status, DateTime expiresAt)
    {
        HoldId = holdId;
        ClassId = classId;
        Status = status;
        ExpiresAt = expiresAt;
    }
}

public class ConfirmHoldResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    public ConfirmHoldResponse(string holdId, string status)
    {
        HoldId = holdId;
        Status = status;
    }
}

public class StudentHoldResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    public StudentHoldResponse(string holdId, string classId, string className, string status, DateTime expiresAt)
    {
        HoldId = holdId;
        ClassId = classId;
        ClassName = className;
        Status = status;
        ExpiresAt = expiresAt;
    }
}
