var builder = WebApplication.CreateBuilder(args);
builder.AddObservability();
builder.Services.AddApiProblemDetails();
builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseRequestObservability();
app.UseApiProblemDetails();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapHealthChecks("/health");
}

app.Run();

public partial class Program;
