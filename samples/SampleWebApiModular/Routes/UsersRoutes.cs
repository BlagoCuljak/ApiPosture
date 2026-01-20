namespace SampleWebApiModular.Routes;

public static class UsersRoutes
{
    public static void MapUsersRoutes(this IEndpointRouteBuilder root)
    {
        var group = root.MapGroup("/users");

        // Protected by FallbackPolicy
        group.MapGet("", () => Results.Ok(new[] { "User 1", "User 2" }));

        // Protected by FallbackPolicy
        group.MapGet("/{id}", (int id) => Results.Ok(new { Id = id, Name = $"User {id}" }));

        // Protected by FallbackPolicy
        group.MapGet("/me", () => Results.Ok(new { Id = 1, Name = "Current User" }));

        // Admin-only operations
        var adminGroup = group.MapGroup("/admin")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapPost("", () => Results.Created("/api/v1/users/1", new { Id = 1 }));
        adminGroup.MapDelete("/{id}", (int id) => Results.NoContent());

        // Public user registration - overrides FallbackPolicy
        group.MapPost("/register", () => Results.Created("/api/v1/users/1", new { Id = 1 }))
            .AllowAnonymous();
    }
}
