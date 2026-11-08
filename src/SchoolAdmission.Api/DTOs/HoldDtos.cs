using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public record CreateHoldResponse(
    [JsonPropertyName("holdId")] string HoldId,
    [JsonPropertyName("classId")] string ClassId,
    [JsonPropertyName("studentId")] string StudentId,
    [JsonPropertyName("status")] string Status,
    [JsonPropertyName("expiresAt")] DateTime ExpiresAt
);

public record HoldResponse(
    [JsonPropertyName("holdId")] string HoldId,
    [JsonPropertyName("classId")] string ClassId,
    [JsonPropertyName("status")] string Status,
    [JsonPropertyName("expiresAt")] DateTime ExpiresAt
);

public record ConfirmHoldResponse(
    [JsonPropertyName("holdId")] string HoldId,
    [JsonPropertyName("status")] string Status
);

public record StudentHoldResponse(
    [JsonPropertyName("holdId")] string HoldId,
    [JsonPropertyName("classId")] string ClassId,
    [JsonPropertyName("className")] string ClassName,
    [JsonPropertyName("status")] string Status,
    [JsonPropertyName("expiresAt")] DateTime ExpiresAt
);
