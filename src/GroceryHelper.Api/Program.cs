var builder = WebApplication.CreateBuilder(args);
builder.AddObservability();
builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseRequestObservability();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapHealthChecks("/health");
}

app.Run();

public partial class Program;
