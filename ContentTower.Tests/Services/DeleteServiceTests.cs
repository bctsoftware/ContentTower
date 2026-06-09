using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests.Services;

public class DeleteServiceTests
{
    private readonly Mock<ILogger<DeleteService>> mockLogger;
    private readonly Mock<IQuotaService> mockQuotaService;
    private readonly Mock<IPresenceService> mockPresenceService;
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly FileMetadata file;
    private readonly DeleteService service;

    public DeleteServiceTests()
    {
        mockLogger = new Mock<ILogger<DeleteService>>();
        mockQuotaService = new Mock<IQuotaService>();
        mockPresenceService = new Mock<IPresenceService>();
        mockFileSystem = new Mock<IFileSystem>();

        file = CreateTestFile();
        service = CreateCleanupWorker();
    }

    #region Helper Methods

    private DeleteService CreateCleanupWorker()
    {
        return new DeleteService(
            mockLogger.Object,
            mockFileSystem.Object,
            mockPresenceService.Object,
            mockQuotaService.Object
        );
    }

    private FileMetadata CreateTestFile(
        string cidHash = "test-file",
        long length = 1000,
        StoreRequestType storeType = StoreRequestType.Default,
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
            StoreType = storeType,
            UploadUtc = uploadUtc ?? now,
            LastActivityUtc = lastActivityUtc ?? now
        };
    }

    private QuotaResponse CreateQuotaResponse(QuotaState state = QuotaState.Nominal)
    {
        return new QuotaResponse
        {
            Quota = 1000000,
            Used = 100000,
            State = state
        };
    }

    private void SetupQuotaService(QuotaState state = QuotaState.Nominal)
    {
        mockQuotaService.Setup(qs => qs.GetQuotaStatus())
            .Returns(CreateQuotaResponse(state));
    }

    private void SetupFileDeletion()
    {
        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.DeleteObject(It.IsAny<Cid>()))
            .Returns(Task.CompletedTask);
    }

    #endregion

    [Test]
    public async Task DeleteFileShouldDeleteData()
    {
        await service.DeleteFile(file);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldDeleteObject()
    {
        await service.DeleteFile(file);

        mockFileSystem.Verify(fs => fs.DeleteObject(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldClearPrecense()
    {
        await service.DeleteFile(file);

        mockPresenceService.Verify(fs => fs.ClearPresence(file.Cid), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldRemoveQuotaBytes()
    {
        await service.DeleteFile(file);

        mockQuotaService.Verify(fs => fs.RemoveUsedBytes(file.Length), Times.Once());
    }

    [Test]
    public async Task DeleteFileShouldLogOnSuccess()
    {
        await service.DeleteFile(file);

        mockLogger.AssertLogged(LogLevel.Information, $"Successfully deleted {file.Cid}.");
    }

    [Test]
    public async Task DeleteFileShouldLogOnException()
    {
        mockFileSystem.Setup(fs => fs.DeleteObject(file.Cid))
            .ThrowsAsync(new InvalidOperationException("test exception"));

        await Assert.That(async () =>
            await service.DeleteFile(file))
            .Throws<InvalidOperationException>();

        mockLogger.AssertLogged(LogLevel.Error, $"Fatal: Failed to delete file.");
    }
}
