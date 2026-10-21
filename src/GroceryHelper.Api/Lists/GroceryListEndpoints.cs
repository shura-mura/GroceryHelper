using Microsoft.AspNetCore.Antiforgery;

namespace GroceryHelper.Api.Lists;

internal static class GroceryListEndpoints
{
    public static IServiceCollection AddGroceryLists(this IServiceCollection services)
    {
        services.AddSingleton<GroceryListStore>();
        return services;
    }

    public static WebApplication MapGroceryListEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/api/lists",
                (GroceryListStore store) => Results.Ok(
                    store.GetAll().Select(GroceryListResponse.From)))
            .RequireAuthorization();

        app.MapPost(
                "/api/lists",
                async (
                    HttpContext context,
                    CreateGroceryListRequest request,
                    IAntiforgery antiforgery,
                    GroceryListStore store) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return Results.BadRequest();
                    }

                    var name = GroceryListName.Create(request.Name);
                    if (!name.IsValid)
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["/name"] = [name.Error!],
                        });
                    }

                    var list = store.Create(name.Value!);
                    var response = GroceryListResponse.From(list);
                    return Results.Created($"/api/lists/{response.Id}", response);
                })
            .RequireAuthorization();

        return app;
    }

    private sealed record CreateGroceryListRequest(string? Name);

    private sealed record GroceryListResponse(string Id, string Name)
    {
        public static GroceryListResponse From(GroceryList list) =>
            new(list.Id.ToString(), list.Name.Value);
    }
}
