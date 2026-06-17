using ContentTower.Services;

namespace ContentTower.Tests.Services;

public class HashServiceTests
{
    [Test]
    public async Task GetHashTest()
    {
        var service = new HashService();

        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        var hash = service.GetHash(data);

        await Assert.That(hash.Id).IsEqualTo("ct5qeR4VrqPbwvo6i3cMcUScrn7GLGGnH6zoPGYLYuBuA6");
    }
}
