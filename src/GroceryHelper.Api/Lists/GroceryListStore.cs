namespace GroceryHelper.Api.Lists;

public interface IGroceryListStore
{
    GroceryListCreation Create(GroceryListName name);

    IReadOnlyList<GroceryList> GetAll();
}

public sealed class GroceryListStore : IGroceryListStore
{
    private readonly Lock sync = new();
    private readonly List<GroceryList> lists = [];

    public GroceryListCreation Create(GroceryListName name)
    {
        lock (sync)
        {
            var existingList = lists.Find(
                list => string.Equals(
                    list.Name.Value,
                    name.Value,
                    StringComparison.OrdinalIgnoreCase));
            if (existingList is not null)
            {
                return new GroceryListCreation(existingList, false);
            }

            var list = new GroceryList(Guid.NewGuid(), name);
            lists.Add(list);
            return new GroceryListCreation(list, true);
        }
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

public sealed record GroceryListCreation(GroceryList List, bool Created);
