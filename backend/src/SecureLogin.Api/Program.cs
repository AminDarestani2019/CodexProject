using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SecureLogin.Application.DependencyInjection;
using SecureLogin.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Postgres");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions());
app.MapGet("/", () => Results.Ok(new { name = "SecureLogin API", status = "running" }));

app.Run();

public partial class Program;
