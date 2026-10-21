namespace GroceryHelper.Api.Lists;

public sealed class GroceryListStore
{
    private readonly Lock sync = new();
    private readonly List<GroceryList> lists = [];

    public GroceryList Create(GroceryListName name)
    {
        var list = new GroceryList(Guid.NewGuid(), name);
        lock (sync)
        {
            lists.Add(list);
        }

        return list;
    }

    public IReadOnlyList<GroceryList> GetAll()
    {
        lock (sync)
        {
            return lists.ToArray();
        }
    }
}

public sealed record GroceryList(Guid Id, GroceryListName Name);
