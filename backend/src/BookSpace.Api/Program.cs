using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookSpace.Api.Common;
using BookSpace.Api.Hubs;
using BookSpace.Api.Realtime;
using BookSpace.Application.Abstractions;
using BookSpace.Infrastructure;
using BookSpace.Infrastructure.Persistence;
using BookSpace.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("BOOKSPACE_");

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => ToCamelCaseFieldName(x.Key),
                x => x.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray());
        return new BadRequestObjectResult(
            ApiResponse<object?>.Failure(
                "Dữ liệu gửi lên không hợp lệ.",
                "VALIDATION_ERROR",
                new { errors }));
    };
});
builder.Services.AddOpenApi();
builder.Services.AddBookSpaceDatabaseHealthCheck();
builder.Services.AddBookSpaceAuthRateLimiting(builder.Configuration);
builder.Services.AddBookSpaceTrustedForwarding(builder.Configuration);
builder.Services.AddBookSpaceInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<IClubChatRealtimePublisher, SignalRClubChatRealtimePublisher>();
builder.Services.AddScoped<IDirectMessageRealtimePublisher, SignalRDirectMessageRealtimePublisher>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("BookSpaceWeb", policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders(
                RequestObservabilityMiddleware.CorrelationIdHeaderName,
                "Retry-After"));
});

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
{
    throw new InvalidOperationException(
        "Thiếu Jwt:Secret hợp lệ. Hãy cấu hình secret có ít nhất 32 byte.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        var events = JwtResponseEvents.Create();
        events.OnTokenValidated = context =>
        {
            var value = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        context.Principal?.FindFirst("sub")?.Value;
            if (!Guid.TryParse(value, out var userId))
            {
                context.Fail("Không xác định được người dùng.");
                return Task.CompletedTask;
            }

            using var scope = context.HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IBookSpaceDbContext>();
            var accountAvailable = db.Users.Any(x => x.Id == userId && !x.IsLocked);
            if (!accountAvailable)
            {
                context.Fail("Tài khoản hiện không thể sử dụng.");
            }

            return Task.CompletedTask;
        };
        events.OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken) &&
                (context.HttpContext.Request.Path.StartsWithSegments("/hubs/club-chat") ||
                 context.HttpContext.Request.Path.StartsWithSegments("/hubs/direct-messages")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        };
        options.Events = events;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.StatusCode != StatusCodes.Status404NotFound || response.HasStarted)
    {
        return;
    }

    response.ContentType = "application/json; charset=utf-8";
    await response.WriteAsJsonAsync(
        ApiResponse<object?>.Failure("Không tìm thấy endpoint hoặc tài nguyên.", "ROUTE_NOT_FOUND"));
});
app.UseCors("BookSpaceWeb");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapBookSpaceHealthChecks();
app.MapGet("/", () => ApiResponse<object>.Ok(
    new
    {
        product = "BookSpace API",
        version = "v1",
        health = "/health",
        openApi = "/openapi/v1.json"
    },
    "BookSpace đang hoạt động."));
app.MapControllers();
app.MapHub<ClubChatHub>("/hubs/club-chat");
app.MapHub<DirectMessageHub>("/hubs/direct-messages");

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.Run();

static string ToCamelCaseFieldName(string key)
{
    var segment = key[(key.LastIndexOf('.') + 1)..];
    return string.IsNullOrWhiteSpace(segment)
        ? "request"
        : JsonNamingPolicy.CamelCase.ConvertName(segment);
}

public partial class Program;
