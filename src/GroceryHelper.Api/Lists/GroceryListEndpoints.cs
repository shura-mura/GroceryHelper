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

                    var creation = store.Create(name.Value!);
                    if (!creation.Created)
                    {
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["/name"] =
                                [
                                    $"A grocery list named '{creation.List.Name.Value}' already exists.",
                                ],
                            },
                            extensions: new Dictionary<string, object?>
                            {
                                ["existingListId"] = creation.List.Id.ToString(),
                                ["existingListName"] = creation.List.Name.Value,
                            });
                    }

                    var response = GroceryListResponse.From(creation.List);
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
