using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;
using Xunit;

namespace SchoolAdmission.Tests;

public class HoldLifecycleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HoldLifecycleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HoldAndConfirm_ValidStudent_ConfirmsAndIsIdempotent()
    {
        var client = _factory.CreateClient();
        var studentId = "20152691234567890";

        // 1. Create class with 2 seats
        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 8 - Morning", 2));
        var createdClass = await classRes.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);

        // 2. Place hold
        using var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await client.SendAsync(holdReq);

        Assert.Equal(HttpStatusCode.Created, holdRes.StatusCode);
        var createdHold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(createdHold);
        Assert.Equal(HoldStatus.Held, createdHold.Status);

        // 3. Confirm hold
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{createdHold.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", studentId);
        var confirmRes = await client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmedData = await confirmRes.Content.ReadFromJsonAsync<ConfirmHoldResponse>();
        Assert.NotNull(confirmedData);
        Assert.Equal(HoldStatus.Confirmed, confirmedData.Status);

        // 4. Idempotency test (Rule 5: "একই আবেদনে confirm একাধিকবার এলেও confirmedSeats একবারই বাড়বে।")
        using var confirmAgainReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{createdHold.HoldId}/confirm");
        confirmAgainReq.Headers.Add("X-Student-Id", studentId);
        var confirmAgainRes = await client.SendAsync(confirmAgainReq);

        Assert.Equal(HttpStatusCode.OK, confirmAgainRes.StatusCode);

        // Verify class has exactly 1 confirmed seat, 0 held, 1 available
        var getClassRes = await client.GetAsync($"/classes/{createdClass.Id}");
        var classState = await getClassRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(classState);
        Assert.Equal(1, classState.ConfirmedSeats);
        Assert.Equal(0, classState.HeldSeats);
        Assert.Equal(1, classState.AvailableSeats);
    }

    [Fact]
    public async Task CancelHold_ValidStudent_ReleasesSeatAndReturnsNoContent()
    {
        var client = _factory.CreateClient();
        var studentId = "20152691234567891";

        // 1. Create class with 1 seat
        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 9 - Day", 1));
        var createdClass = await classRes.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);

        // 2. Place hold
        using var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await client.SendAsync(holdReq);
        var createdHold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(createdHold);

        // Verify seat is held (available = 0)
        var classAfterHold = await client.GetFromJsonAsync<ClassResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classAfterHold);
        Assert.Equal(0, classAfterHold.AvailableSeats);

        // 3. Cancel hold (DELETE /holds/{holdId})
        using var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{createdHold.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", studentId);
        var cancelRes = await client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // 4. Verify seat is released immediately (available = 1)
        var classAfterCancel = await client.GetFromJsonAsync<ClassResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classAfterCancel);
        Assert.Equal(1, classAfterCancel.AvailableSeats);
        Assert.Equal(0, classAfterCancel.HeldSeats);

        // 5. Verify hold status is CANCELLED via GET /holds/{holdId}
        var getHoldRes = await client.GetFromJsonAsync<HoldResponse>($"/holds/{createdHold.HoldId}");
        Assert.NotNull(getHoldRes);
        Assert.Equal(HoldStatus.Cancelled, getHoldRes.Status);
    }

    [Fact]
    public async Task ExpiredHold_CannotBeConfirmed_Returns4xxAndReleasesSeatAutomatically()
    {
        var client = _factory.CreateClient();
        var studentId = "20152691234567892";

        // 1. Create class with 1 seat
        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 10 - Expiry", 1));
        var createdClass = await classRes.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);

        // 2. Place hold
        using var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await client.SendAsync(holdReq);
        var createdHold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(createdHold);

        // 3. Manually simulate expiration in DB (ExpiresAt in the past)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbHold = await db.Holds.FindAsync(createdHold.HoldId);
            Assert.NotNull(dbHold);
            dbHold.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Rule 2: "মেয়াদ শেষ হলে seat নিজে থেকে ফেরত যাবে, কাউকে কোনো API call করতে হবে না।"
        // Verify class seat count immediately reflects available = 1 without any API call
        var classState = await client.GetFromJsonAsync<ClassResponse>($"/classes/{createdClass.Id}");
        Assert.NotNull(classState);
        Assert.Equal(1, classState.AvailableSeats);
        Assert.Equal(0, classState.HeldSeats);

        // Rule 4: "মেয়াদ না পেরোনো HELD আবেদন confirm করা যাবে।" (Expired cannot be confirmed)
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{createdHold.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", studentId);
        var confirmRes = await client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);
    }
}
