using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class CleanupWorkerTests
{
    private readonly Mock<IOptions<StorageOptions>> mockOptions;
    private readonly Mock<IQuotaService> mockQuotaService;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<IDeleteService> mockDeleteService;

    public CleanupWorkerTests()
    {
        mockOptions = new Mock<IOptions<StorageOptions>>();
        mockQuotaService = new Mock<IQuotaService>();
        mockTimeService = new Mock<ITime>();
        mockDeleteService = new Mock<IDeleteService>();
    }

    #region Helper Methods

    private CleanupWorker CreateCleanupWorker(StorageOptions? options = null)
    {
        var optionsToUse = options ?? CreateValidStorageOptions();
        mockOptions.Setup(o => o.Value).Returns(optionsToUse);
        return new CleanupWorker(
            mockOptions.Object,
            mockQuotaService.Object,
            mockTimeService.Object,
            mockDeleteService.Object
        );
    }

    private StorageOptions CreateValidStorageOptions()
    {
        return new StorageOptions
        {
            DataPath = "/data",
            Quota = 1000000,
            CleanupIntervalSeconds = 600,
            StoreDurationDefaultNominalSeconds = 86400,      // 1 day
            StoreDurationDefaultPressureSeconds = 43200,     // 12 hours
            StoreDurationTemporaryNominalSeconds = 7200,     // 2 hours
            StoreDurationTemporaryPressureSeconds = 3600     // 1 hour
        };
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

    private void SetupTimeService(DateTime utcNow)
    {
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(utcNow);
    }

    private void SetupQuotaService(QuotaState state = QuotaState.Nominal)
    {
        mockQuotaService.Setup(qs => qs.GetQuotaStatus())
            .Returns(CreateQuotaResponse(state));
    }

    private void SetupFileDeletion()
    {
        mockDeleteService.Setup(fs => fs.DeleteFile(It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
    }

    #endregion

    #region Tests - ProcessItem - PermanentFile

    [Test]
    public async Task ProcessItem_WithPermanentFile_ReturnsEarlyWithoutDeletion()
    {
        var worker = CreateCleanupWorker();
        var file = CreateTestFile(storeType: StoreRequestType.PermanentFile);
        SetupTimeService(DateTime.UtcNow);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    #endregion

    #region Tests - ProcessItem - Default + Nominal

    [Test]
    public async Task ProcessItem_DefaultNominalNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddHours(-5)); // Uploaded 5 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_DefaultNominalExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Uploaded 2 days ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - TemporaryFile + Nominal

    [Test]
    public async Task ProcessItem_TemporaryNominalNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddMinutes(-30)); // Last activity 30 min ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_TemporaryNominalExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddHours(-3)); // Last activity 3 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - Default + Pressure

    [Test]
    public async Task ProcessItem_DefaultPressureNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddHours(-6)); // Uploaded 6 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Pressure);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_DefaultPressureExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddHours(-13)); // Uploaded 13 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Pressure);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - TemporaryFile + Pressure

    [Test]
    public async Task ProcessItem_TemporaryPressureNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddMinutes(-30)); // Last activity 30 min ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Pressure);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_TemporaryPressureExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddMinutes(-65)); // Last activity 65 min ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Pressure);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - Default + Full

    [Test]
    public async Task ProcessItem_DefaultFullNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddHours(-6)); // Uploaded 6 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Full);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_DefaultFullExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddHours(-13)); // Uploaded 13 hours ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Full);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - TemporaryFile + Full

    [Test]
    public async Task ProcessItem_TemporaryFullNotExpired_DoesNotDelete()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddMinutes(-30)); // Last activity 30 min ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Full);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_TemporaryFullExpired_DeletesFile()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddMinutes(-65)); // Last activity 65 min ago
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Full);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - GetFileUtc

    [Test]
    public async Task GetFileUtc_TemporaryFile_ReturnsLastActivityUtc()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var uploadTime = now.AddDays(-5);
        var lastActivityTime = now.AddHours(-1);
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        var expiredFile = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddHours(-3)); // 3 hours old, should expire in nominal state
        await worker.ProcessItem(expiredFile);

        mockDeleteService.Verify(fs => fs.DeleteFile(expiredFile), Times.Once);
    }

    [Test]
    public async Task GetFileUtc_DefaultFile_ReturnsUploadUtc()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var uploadTime = now.AddDays(-2);
        var lastActivityTime = now.AddMinutes(-5);
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: uploadTime,
            lastActivityUtc: lastActivityTime);
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - DeleteFile - Success Path

    [Test]
    public async Task DeleteFile_SuccessfulDeletion_CallsAllCleanupOperations()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion

    #region Tests - DeleteFile - Failure Path

    [Test]
    public async Task DeleteFile_DeleteDataFails_ThrowsException()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        var exception = new IOException("Disk error");

        mockDeleteService.Setup(fs => fs.DeleteFile(It.IsAny<FileMetadata>()))
            .ThrowsAsync(exception);

        await Assert.That(async () =>
            await worker.ProcessItem(file))
            .Throws<IOException>();
    }

    #endregion

    #region Tests - Integration

    [Test]
    public async Task ProcessItem_IntegrationTest_DefaultFilesUseUploadTime()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;

        // File with old upload but recent activity
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2),
            lastActivityUtc: now.AddMinutes(-5));

        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    [Test]
    public async Task ProcessItem_IntegrationTest_TemporaryFilesUseActivityTime()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;

        // File with recent upload but old activity
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            uploadUtc: now.AddMinutes(-5),
            lastActivityUtc: now.AddHours(-3));

        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file);

        mockDeleteService.Verify(fs => fs.DeleteFile(file), Times.Once);
    }

    #endregion
}
