using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmsService.API.Middleware;
using SmsService.Application;
using SmsService.DTO;
using SmsService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        // Malformed JSON / binding errors use the same ProblemDetails + code shape as our own validation.
        o.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
            };
            problem.Extensions["code"] = ErrorCodes.ValidationError;
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<MessageDispatcher>();
builder.Services.AddInfrastructure(builder.Configuration);

var clientOrigin = builder.Configuration["ClientOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Decision 17: apply migrations on startup, then seed (decision 18). Not suitable for multi-instance production.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db, logger);
}

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    // Built-in .NET 9 OpenAPI document, displayed with Swagger UI at /swagger.
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "SMS Service API v1");
        o.DocumentTitle = "SMS Service API";
    });
}

app.MapControllers();

app.Run();
