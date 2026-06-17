using ContentTower.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests.Services;

public class DeleteServiceTests
{
    private readonly Mock<ILogger<DeleteService>> mockLogger;
    private readonly Mock<IQuotaService> mockQuotaService;
    private readonly Mock<IPresenceService> mockPresenceService;
    private readonly Mock<IObjectStoreService> mockObjectStoreService;
    private readonly Mock<IDataStoreService> mockDataStoreService;
    private readonly FileMetadata file;
    private readonly DeleteService service;

    public DeleteServiceTests()
    {
        mockLogger = new Mock<ILogger<DeleteService>>();
        mockQuotaService = new Mock<IQuotaService>();
        mockPresenceService = new Mock<IPresenceService>();
        mockObjectStoreService = new Mock<IObjectStoreService>();
        mockDataStoreService = new Mock<IDataStoreService>();

        file = CreateTestFile();
        service = CreateCleanupWorker();
    }

    #region Helper Methods

    private DeleteService CreateCleanupWorker()
    {
        return new DeleteService(
            mockLogger.Object,
            mockObjectStoreService.Object,
            mockDataStoreService.Object,
            mockPresenceService.Object,
            mockQuotaService.Object
        );
    }

    private FileMetadata CreateTestFile(
        string cidHash = "test-file",
        long length = 1000,
        StoreType storeType = StoreType.Default,
        DateTime? uploadUtc = null,
        DateTime? lastActivityUtc = null)
    {
        var now = DateTime.UtcNow;
        return new FileMetadata
        {
            Cid = new Cid(cidHash),
            Name = "test.txt",
            ContentType = "text/plain",
            Length = length,
        };
    }

    #endregion

    [Test]
    public async Task DeleteFileShouldDeleteData()
    {
        service.DeleteFile(file);

        mockDataStoreService.Verify(fs => fs.DeleteData(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldDeleteObject()
    {
        service.DeleteFile(file);

        mockObjectStoreService.Verify(fs => fs.DeleteObject(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldClearPrecense()
    {
        service.DeleteFile(file);

        mockPresenceService.Verify(fs => fs.ClearPresence(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldRemoveQuotaBytes()
    {
        service.DeleteFile(file);

        mockQuotaService.Verify(fs => fs.RemoveUsedBytes(file.Length), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldLogOnSuccess()
    {
        service.DeleteFile(file);

        mockLogger.AssertLogged(LogLevel.Information, $"Successfully deleted {file.Cid}.");
    }

    [Test]
    public async Task DeleteFileShouldLogOnException()
    {
        mockObjectStoreService.Setup(fs => fs.DeleteObject(file.Cid))
            .Throws(new InvalidOperationException("test exception"));

        await Assert.That(async () =>
            service.DeleteFile(file))
            .Throws<InvalidOperationException>();

        mockLogger.AssertLogged(LogLevel.Error, $"Fatal: Failed to delete file.");
    }
}
