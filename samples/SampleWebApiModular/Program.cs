using Microsoft.AspNetCore.Authorization;
using SampleWebApiModular.Routes;

var builder = WebApplication.CreateBuilder(args);

// Configure authorization with FallbackPolicy - requires authentication by default
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// Register routes via extension methods
var apiGroup = app.MapGroup("/api/v1");
apiGroup.MapTasksRoutes();
apiGroup.MapUsersRoutes();

// Health endpoint - explicitly public (AllowAnonymous overrides FallbackPolicy)
app.MapGet("/health", () => "OK").AllowAnonymous();

// Direct endpoint without auth chain - should be protected by FallbackPolicy
app.MapGet("/api/direct", () => Results.Ok("Direct endpoint"));

// Direct endpoint with explicit anonymous - should be public
app.MapGet("/api/public-direct", () => Results.Ok("Public direct")).AllowAnonymous();

app.Run();
