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
        await Assert.That(market.Id).IsNotEqualTo(weekly.Id);
        await Assert.That(lists).IsEquivalentTo([market, weekly]);
    }
}
