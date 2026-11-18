using System.Diagnostics;
using System.Security.Claims;
using AIExamForge.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIExamForge.ContractTests;

public sealed class TelemetryProcessorTests
{
    [Fact]
    public void OnEnd_PreservesRequestDimensionsWithoutReplacingW3CTraceIdentity()
    {
        var generationId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "test-user")], "test"))
        };
        context.Items["CorrelationId"] = "request-correlation";
        context.Items["GenerationId"] = generationId;
        using var processor = new OperationCorrelationProcessor(
            new HttpContextAccessor { HttpContext = context });
        using var activity = new Activity("request").Start();
        var traceId = activity.TraceId;

        processor.OnEnd(activity);

        Assert.Equal("request-correlation", activity.GetTagItem("CorrelationId"));
        Assert.Equal(generationId.ToString(), activity.GetTagItem("GenerationId"));
        Assert.Equal("test-user", activity.GetTagItem("enduser.id"));
        Assert.Equal(traceId, activity.TraceId);
    }

    [Fact]
    public void OnEnd_WithoutHttpContext_DoesNotAddRequestDimensions()
    {
        using var processor = new OperationCorrelationProcessor(new HttpContextAccessor());
        using var activity = new Activity("background");

        processor.OnEnd(activity);

        Assert.Empty(activity.TagObjects);
    }
}
