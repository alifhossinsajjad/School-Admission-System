using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public record CreateClassRequest(
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Class name must be between 2 and 200 characters.")]
    [JsonPropertyName("name")]
    string Name,

    [Range(1, 100000, ErrorMessage = "Total seats must be greater than zero.")]
    [JsonPropertyName("totalSeats")]
    int TotalSeats
);

public record CreateClassResponse(
    [JsonPropertyName("id")] string Id,
    [JsonPropertyName("name")] string Name,
    [JsonPropertyName("totalSeats")] int TotalSeats
);

public record ClassResponse(
    [JsonPropertyName("id")] string Id,
    [JsonPropertyName("name")] string Name,
    [JsonPropertyName("totalSeats")] int TotalSeats,
    [JsonPropertyName("heldSeats")] int HeldSeats,
    [JsonPropertyName("confirmedSeats")] int ConfirmedSeats,
    [JsonPropertyName("availableSeats")] int AvailableSeats
);
