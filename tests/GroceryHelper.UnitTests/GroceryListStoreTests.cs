using GroceryHelper.Api.Lists;

namespace GroceryHelper.UnitTests;

public class GroceryListStoreTests
{
    [Test]
    public async Task Account_can_create_and_view_multiple_grocery_lists()
    {
        // Arrange
        var store = new GroceryListStore();
        var marketName = GroceryListName.Create("Market").Value!;
        var weeklyName = GroceryListName.Create("Weekly groceries").Value!;

        // Act
        var market = store.Create(marketName);
        var weekly = store.Create(weeklyName);
        var lists = store.GetAll();

        // Assert
        await Assert.That(market.Created).IsTrue();
        await Assert.That(weekly.Created).IsTrue();
        await Assert.That(market.List.Id).IsNotEqualTo(weekly.List.Id);
        await Assert.That(lists).IsEquivalentTo([market.List, weekly.List]);
    }

    [Test]
    public async Task Duplicate_list_names_return_the_existing_list_without_creating_another()
    {
        // Arrange
        var store = new GroceryListStore();
        var originalName = GroceryListName.Create("Weekly groceries").Value!;
        var duplicateName = GroceryListName.Create("  weekly\tGROCERIES  ").Value!;
        var original = store.Create(originalName);

        // Act
        var duplicate = store.Create(duplicateName);
        var lists = store.GetAll();

        // Assert
        await Assert.That(original.Created).IsTrue();
        await Assert.That(duplicate.Created).IsFalse();
        await Assert.That(duplicate.List.Id).IsEqualTo(original.List.Id);
        await Assert.That(lists).IsEquivalentTo([original.List]);
    }

    [Test]
    public async Task Canonically_distinct_unicode_list_names_remain_distinct()
    {
        // Arrange
        var store = new GroceryListStore();
        var composedName = GroceryListName.Create("Caf\u00e9").Value!;
        var decomposedName = GroceryListName.Create("Cafe\u0301").Value!;

        // Act
        var composed = store.Create(composedName);
        var decomposed = store.Create(decomposedName);
        var lists = store.GetAll();

        // Assert
        await Assert.That(composed.Created).IsTrue();
        await Assert.That(decomposed.Created).IsTrue();
        await Assert.That(lists).Count().IsEqualTo(2);
    }
}
