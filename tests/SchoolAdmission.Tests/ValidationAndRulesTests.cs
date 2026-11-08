using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;
using Xunit;

namespace SchoolAdmission.Tests;

public class ValidationAndRulesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ValidationAndRulesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("2015269123456789")] // 16 digits
    [InlineData("201526912345678901")] // 18 digits
    [InlineData("abcdefghijklmnopq")] // non-numeric
    public async Task InvalidStudentId_ReturnsBadRequest4xx_Never500(string invalidStudentId)
    {
        var client = _factory.CreateClient();

        // Rule 7: ভুল input-এ 4xx দিতে হবে, কখনো 500 নয়।
        using var req = new HttpRequestMessage(HttpMethod.Post, "/classes/c_nonexistent/holds");
        req.Headers.Add("X-Student-Id", invalidStudentId);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SingleStudent_CannotHoldMultipleSeatsInSameClass_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var studentId = "20152691234567895";

        // Create class with 10 seats
        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 11 - Shift A", 10));
        var createdClass = await classRes.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);

        // First hold succeeds (201)
        using var hold1Req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        hold1Req.Headers.Add("X-Student-Id", studentId);
        var hold1Res = await client.SendAsync(hold1Req);
        Assert.Equal(HttpStatusCode.Created, hold1Res.StatusCode);

        // Second hold attempt by the SAME student in the SAME class (Rule 3)
        using var hold2Req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        hold2Req.Headers.Add("X-Student-Id", studentId);
        var hold2Res = await client.SendAsync(hold2Req);

        Assert.Equal(HttpStatusCode.Conflict, hold2Res.StatusCode);
    }

    [Fact]
    public async Task ConfirmOrCancel_BelongingToAnotherStudent_ReturnsForbidden403()
    {
        var client = _factory.CreateClient();
        var studentA = "20152691234567801";
        var studentB = "20152691234567802";

        // Create class
        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 12 - Shift B", 5));
        var createdClass = await classRes.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(createdClass);

        // Student A holds seat
        using var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentA);
        var holdRes = await client.SendAsync(holdReq);
        var createdHold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(createdHold);

        // Student B attempts to confirm Student A's hold
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{createdHold.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", studentB);
        var confirmRes = await client.SendAsync(confirmReq);

        // Rule 4: "শুধু নিজের... আবেদন confirm করা যাবে।"
        Assert.Equal(HttpStatusCode.Forbidden, confirmRes.StatusCode);

        // Student B attempts to cancel Student A's hold
        using var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{createdHold.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", studentB);
        var cancelRes = await client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.Forbidden, cancelRes.StatusCode);
    }

    [Fact]
    public async Task GetMyHolds_ReturnsAllStudentHolds()
    {
        var client = _factory.CreateClient();
        var studentId = "20152691234567803";

        // Create 2 classes
        var class1Res = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 6 - Morning", 5));
        var c1 = await class1Res.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(c1);

        var class2Res = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 6 - Day", 5));
        var c2 = await class2Res.Content.ReadFromJsonAsync<CreateClassResponse>();
        Assert.NotNull(c2);

        // Hold in class 1
        using var h1Req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{c1.Id}/holds");
        h1Req.Headers.Add("X-Student-Id", studentId);
        await client.SendAsync(h1Req);

        // Hold in class 2
        using var h2Req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{c2.Id}/holds");
        h2Req.Headers.Add("X-Student-Id", studentId);
        await client.SendAsync(h2Req);

        // GET /me/holds
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        meReq.Headers.Add("X-Student-Id", studentId);
        var meRes = await client.SendAsync(meReq);

        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var myHolds = await meRes.Content.ReadFromJsonAsync<List<StudentHoldResponse>>();

        Assert.NotNull(myHolds);
        Assert.Equal(2, myHolds.Count);
        Assert.Contains(myHolds, h => h.ClassId == c1.Id && h.ClassName == "Class 6 - Morning");
        Assert.Contains(myHolds, h => h.ClassId == c2.Id && h.ClassName == "Class 6 - Day");
    }
}
