namespace SampleWebApiModular.Routes;

public static class TasksRoutes
{
    public static void MapTasksRoutes(this IEndpointRouteBuilder root)
    {
        var group = root.MapGroup("/tasks");

        // Protected by FallbackPolicy (RequireAuthenticatedUser)
        group.MapGet("", () => Results.Ok(new[] { "Task 1", "Task 2" }));

        // Protected by FallbackPolicy
        group.MapPost("", () => Results.Created("/api/v1/tasks/1", new { Id = 1 }));

        // Protected by FallbackPolicy
        group.MapGet("/{id}", (int id) => Results.Ok(new { Id = id, Name = $"Task {id}" }));

        // Protected by FallbackPolicy
        group.MapPut("/{id}", (int id) => Results.Ok(new { Id = id, Updated = true }));

        // Protected by FallbackPolicy
        group.MapDelete("/{id}", (int id) => Results.NoContent());

        // Explicitly public - overrides FallbackPolicy
        group.MapGet("/public", () => "Public tasks info").AllowAnonymous();

        // Requires additional Admin role on top of authentication
        group.MapDelete("/admin/purge", () => Results.NoContent())
            .RequireAuthorization("AdminOnly");
    }
}
