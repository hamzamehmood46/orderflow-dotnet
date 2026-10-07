using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOrderFlowInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposed so integration tests can boot the app with WebApplicationFactory<Program>.
public partial class Program;
