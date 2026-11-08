using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public class CreateClassRequest
{
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Class name must be between 2 and 200 characters.")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100000, ErrorMessage = "Total seats must be greater than zero.")]
    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }
}

public class CreateClassResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }

    public CreateClassResponse(string id, string name, int totalSeats)
    {
        Id = id;
        Name = name;
        TotalSeats = totalSeats;
    }
}

public class ClassResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }

    [JsonPropertyName("heldSeats")]
    public int HeldSeats { get; set; }

    [JsonPropertyName("confirmedSeats")]
    public int ConfirmedSeats { get; set; }

    [JsonPropertyName("availableSeats")]
    public int AvailableSeats { get; set; }

    public ClassResponse(string id, string name, int totalSeats, int heldSeats, int confirmedSeats, int availableSeats)
    {
        Id = id;
        Name = name;
        TotalSeats = totalSeats;
        HeldSeats = heldSeats;
        ConfirmedSeats = confirmedSeats;
        AvailableSeats = availableSeats;
    }
}
