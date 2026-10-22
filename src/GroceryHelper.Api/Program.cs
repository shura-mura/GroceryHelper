using GroceryHelper.Api.Authentication;
using GroceryHelper.Api.Lists;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability();
builder.Services.AddApiProblemDetails();
builder.Services.AddDemoAuthentication();
builder.Services.AddGroceryLists();
builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseRequestObservability();
app.UseApiProblemDetails();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapHealthChecks("/health");
}

app.MapAuthenticationEndpoints();
app.MapGroceryListEndpoints();
app.Run();

public partial class Program;
