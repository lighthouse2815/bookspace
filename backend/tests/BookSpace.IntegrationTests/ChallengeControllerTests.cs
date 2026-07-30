using System.Reflection;
using System.Security.Claims;
using BookSpace.Api.Common;
using BookSpace.Api.Controllers;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.IntegrationTests;

public sealed class ChallengeControllerTests
{
    [Fact]
    public async Task Leave_delegates_once_and_returns_the_application_result()
    {
        var userId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var expected = new ChallengeDto(
            challengeId,
            "Thử thách rời nhóm",
            "Kiểm tra controller không đọc lại sau khi application đã commit.",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            3,
            0,
            7,
            false,
            null,
            true,
            null);
        var service = DispatchProxy.Create<IChallengeService, RecordingChallengeServiceProxy>();
        var proxy = (RecordingChallengeServiceProxy)(object)service;
        proxy.LeaveResult = expected;
        var controller = new ChallengesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                            "test"))
                }
            }
        };

        var response = await controller.Leave(challengeId, CancellationToken.None);

        Assert.Equal(1, proxy.LeaveCalls);
        Assert.Equal(0, proxy.GetPublicCalls);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var envelope = Assert.IsType<ApiResponse<ChallengeDto>>(ok.Value);
        Assert.Same(expected, envelope.Data);
    }

    public class RecordingChallengeServiceProxy : DispatchProxy
    {
        public ChallengeDto? LeaveResult { get; set; }
        public int LeaveCalls { get; private set; }
        public int GetPublicCalls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IChallengeService.LeaveAsync))
            {
                LeaveCalls++;
                return Task.FromResult(
                    LeaveResult ?? throw new InvalidOperationException("Thiếu kết quả Leave."));
            }

            if (targetMethod.Name == nameof(IChallengeService.GetPublicAsync))
            {
                GetPublicCalls++;
                throw new InvalidOperationException(
                    "Controller không được gọi GetPublicAsync sau LeaveAsync.");
            }

            throw new NotSupportedException(
                $"Không cần gọi {targetMethod.Name} trong kiểm thử controller này.");
        }
    }
}
