namespace GroceryHelper.UnitTests;

// Infrastructure smoke only. Business-rule unit tests arrive with feature issues.
public class RuntimeSmokeTests
{
    [Test]
    public async Task Test_process_uses_the_supported_runtime()
    {
        await Assert.That(Environment.Version.Major).IsEqualTo(10);
    }
}
