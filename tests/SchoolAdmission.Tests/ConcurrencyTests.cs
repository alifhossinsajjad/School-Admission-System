using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;
using Xunit;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ConcurrencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateHold_ConcurrentRequestsExceedingCapacity_NeverOverallocatesSeats()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int totalSeats = 5;
        const int concurrentApplicants = 50;

        // 1. Create a class with only 5 seats
        var createClassResponse = await client.PostAsJsonAsync("/classes", new CreateClassRequest(
            "Class 7 - Concurrency Test",
            totalSeats
        ));

        Assert.Equal(HttpStatusCode.Created, createClassResponse.StatusCode);
        var createdClass = await createClassResponse.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);
        var classId = createdClass.Id;

        // 2. Prepare 50 concurrent applicants with unique valid 17-digit student IDs
        var tasks = Enumerable.Range(0, concurrentApplicants).Select(async i =>
        {
            var studentId = $"201526910000000{i:D2}";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", studentId);

            var response = await client.SendAsync(request);
            return response.StatusCode;
        }).ToList();

        // Act: Execute all 50 requests simultaneously
        var results = await Task.WhenAll(tasks);

        // Assert:
        // Rule 1: কোনো অবস্থাতেই একটা class-এ held + confirmed সংখ্যা totalSeats-এর বেশি হবে না
        var successCount = results.Count(s => s == HttpStatusCode.Created);
        var conflictCount = results.Count(s => s == HttpStatusCode.Conflict);

        Assert.Equal(totalSeats, successCount); // Exactly 5 succeed
        Assert.Equal(concurrentApplicants - totalSeats, conflictCount); // Exactly 45 receive 409 Conflict

        // Verify class state via GET /classes/{classId}
        var getClassResponse = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, getClassResponse.StatusCode);
        var classState = await getClassResponse.Content.ReadFromJsonAsync<ClassResponse>();

        Assert.NotNull(classState);
        Assert.Equal(totalSeats, classState.TotalSeats);
        Assert.Equal(totalSeats, classState.HeldSeats);
        Assert.Equal(0, classState.ConfirmedSeats);
        Assert.Equal(0, classState.AvailableSeats);
        Assert.True(classState.HeldSeats + classState.ConfirmedSeats <= classState.TotalSeats);
    }
}
