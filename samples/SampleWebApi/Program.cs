var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Minimal API examples for testing

// Properly secured endpoint - no findings
app.MapGet("/api/orders", () => Results.Ok(new[] { "Order 1" }))
    .RequireAuthorization();

// Properly public endpoint - no findings
app.MapGet("/api/status", () => Results.Ok("OK"))
    .AllowAnonymous();

// No auth chain - triggers AP008
app.MapGet("/api/items", () => Results.Ok(new[] { "Item 1" }));

// No auth chain with write - triggers AP008
app.MapPost("/api/items", () => Results.Created("/api/items/1", null));

// Secured with policy
app.MapDelete("/api/items/{id}", (int id) => Results.NoContent())
    .RequireAuthorization("CanDeleteItems");

// Group example with inherited authorization
var adminGroup = app.MapGroup("/api/admin")
    .RequireAuthorization("AdminPolicy");

adminGroup.MapGet("/users", () => Results.Ok(new[] { "User 1" }));
adminGroup.MapPost("/users", () => Results.Created("/api/admin/users/1", null));

app.Run();
