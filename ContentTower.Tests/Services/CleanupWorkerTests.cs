using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class CleanupWorkerTests
{
    private readonly Mock<ILogger<CleanupService>> mockLogger;
    private readonly Mock<IOptions<StorageOptions>> mockOptions;
    private readonly Mock<IQuotaService> mockQuotaService;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<IPresenceService> mockPresenceService;
    private readonly Mock<IFileSystem> mockFileSystem;

    public CleanupWorkerTests()
    {
        mockLogger = new Mock<ILogger<CleanupService>>();
        mockOptions = new Mock<IOptions<StorageOptions>>();
        mockQuotaService = new Mock<IQuotaService>();
        mockTimeService = new Mock<ITime>();
        mockPresenceService = new Mock<IPresenceService>();
        mockFileSystem = new Mock<IFileSystem>();
    }

    #region Helper Methods

    private CleanupWorker CreateCleanupWorker(StorageOptions? options = null)
    {
        var optionsToUse = options ?? CreateValidStorageOptions();
        mockOptions.Setup(o => o.Value).Returns(optionsToUse);
        return new CleanupWorker(
            mockLogger.Object,
            mockOptions.Object,
            mockQuotaService.Object,
            mockTimeService.Object,
            mockPresenceService.Object,
            mockFileSystem.Object
        );
    }

    private StorageOptions CreateValidStorageOptions()
    {
        return new StorageOptions
        {
            DataPath = "/data",
            Quota = 1000000,
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
        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.DeleteObject(It.IsAny<Cid>()))
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
        mockFileSystem.Verify(fs => fs.DeleteObject(It.IsAny<Cid>()), Times.Never);
        mockPresenceService.Verify(ps => ps.ClearPresence(It.IsAny<Cid>()), Times.Never);
        mockQuotaService.Verify(qs => qs.RemoveUsedBytes(It.IsAny<long>()), Times.Never);
    }

    [Test]
    public async Task ProcessItem_WithPermanentFile_StillSleeps()
    {
        var worker = CreateCleanupWorker();
        var file = CreateTestFile(storeType: StoreRequestType.PermanentFile);
        SetupTimeService(DateTime.UtcNow);
        SetupQuotaService(QuotaState.Nominal);
        var sleepCalled = false;

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => sleepCalled = true)
            .Returns(Task.CompletedTask);

        await worker.ProcessItem(file, CancellationToken.None);

        await Assert.That(sleepCalled).IsTrue();
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
        mockFileSystem.Verify(fs => fs.DeleteObject(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
        mockFileSystem.Verify(fs => fs.DeleteObject(file.Cid), Times.Once);
        mockPresenceService.Verify(ps => ps.ClearPresence(file.Cid), Times.Once);
        mockQuotaService.Verify(qs => qs.RemoveUsedBytes(file.Length), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
        mockFileSystem.Verify(fs => fs.DeleteObject(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
        mockFileSystem.Verify(fs => fs.DeleteObject(file.Cid), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
    }

    #endregion

    #region Tests - ProcessItem - Sleep Behavior

    [Test]
    public async Task ProcessItem_AlwaysSleepsAfterProcessing()
    {
        var worker = CreateCleanupWorker();
        var file = CreateTestFile(storeType: StoreRequestType.Default);
        SetupTimeService(DateTime.UtcNow);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();
        var sleepCalled = false;

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => sleepCalled = true)
            .Returns(Task.CompletedTask);

        await worker.ProcessItem(file, CancellationToken.None);

        await Assert.That(sleepCalled).IsTrue();
    }

    [Test]
    public async Task ProcessItem_SleepDurationIsTenSeconds()
    {
        var worker = CreateCleanupWorker();
        var file = CreateTestFile(storeType: StoreRequestType.Default);
        SetupTimeService(DateTime.UtcNow);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();
        var sleepDuration = TimeSpan.Zero;

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((duration, _) => sleepDuration = duration)
            .Returns(Task.CompletedTask);

        await worker.ProcessItem(file, CancellationToken.None);

        await Assert.That(sleepDuration.TotalSeconds).IsEqualTo(10);
    }

    [Test]
    public async Task ProcessItem_PassesCancellationTokenToSleep()
    {
        var worker = CreateCleanupWorker();
        var file = CreateTestFile(storeType: StoreRequestType.Default);
        SetupTimeService(DateTime.UtcNow);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();
        var cancellationToken = new CancellationToken();
        var receivedToken = CancellationToken.None;

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, token) => receivedToken = token)
            .Returns(Task.CompletedTask);

        await worker.ProcessItem(file, cancellationToken);

        await Assert.That(receivedToken).IsEqualTo(cancellationToken);
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
        var file = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            uploadUtc: uploadTime,
            lastActivityUtc: lastActivityTime);
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        var expiredFile = CreateTestFile(
            storeType: StoreRequestType.TemporaryFile,
            lastActivityUtc: now.AddHours(-3)); // 3 hours old, should expire in nominal state
        await worker.ProcessItem(expiredFile, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(It.IsAny<Cid>()), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
        mockFileSystem.Verify(fs => fs.DeleteObject(file.Cid), Times.Once);
        mockPresenceService.Verify(ps => ps.ClearPresence(file.Cid), Times.Once);
        mockQuotaService.Verify(qs => qs.RemoveUsedBytes(file.Length), Times.Once);
    }

    [Test]
    public async Task DeleteFile_SuccessfulDeletion_LogsTraceMessages()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file, CancellationToken.None);

        mockLogger.AssertLogged(LogLevel.Trace, "Cleaning up");
        mockLogger.AssertLogged(LogLevel.Trace, "Successfully cleaned up");
    }

    [Test]
    public async Task DeleteFile_RemovesCorrectAmountOfBytes()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var fileLength = 50000L;
        var file = CreateTestFile(
            length: fileLength,
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        SetupFileDeletion();

        await worker.ProcessItem(file, CancellationToken.None);

        mockQuotaService.Verify(qs => qs.RemoveUsedBytes(fileLength), Times.Once);
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

        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .ThrowsAsync(exception);

        await Assert.That(async () =>
            await worker.ProcessItem(file, CancellationToken.None))
            .Throws<IOException>();
    }

    [Test]
    public async Task DeleteFile_DeleteObjectFails_ThrowsException()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);

        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.DeleteObject(It.IsAny<Cid>()))
            .ThrowsAsync(new IOException("Metadata error"));

        await Assert.That(async () =>
            await worker.ProcessItem(file, CancellationToken.None))
            .Throws<IOException>();
    }

    [Test]
    public async Task DeleteFile_FailureDuringDeletion_LogsError()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);
        var exception = new IOException("Disk error");

        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .ThrowsAsync(exception);

        try
        {
            await worker.ProcessItem(file, CancellationToken.None);
        }
        catch { }

        mockLogger.AssertLogged(LogLevel.Error, "Fatal: Failed to delete file");
    }

    [Test]
    public async Task DeleteFile_FailureDuringDeletion_PartialCleanupNotPerformed()
    {
        var worker = CreateCleanupWorker();
        var now = DateTime.UtcNow;
        var file = CreateTestFile(
            storeType: StoreRequestType.Default,
            uploadUtc: now.AddDays(-2)); // Expired
        SetupTimeService(now);
        SetupQuotaService(QuotaState.Nominal);

        mockFileSystem.Setup(fs => fs.DeleteData(It.IsAny<Cid>()))
            .ThrowsAsync(new IOException("Disk error"));

        try
        {
            await worker.ProcessItem(file, CancellationToken.None);
        }
        catch { }

        mockFileSystem.Verify(fs => fs.DeleteObject(It.IsAny<Cid>()), Times.Never);
        mockPresenceService.Verify(ps => ps.ClearPresence(It.IsAny<Cid>()), Times.Never);
        mockQuotaService.Verify(qs => qs.RemoveUsedBytes(It.IsAny<long>()), Times.Never);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
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

        await worker.ProcessItem(file, CancellationToken.None);

        mockFileSystem.Verify(fs => fs.DeleteData(file.Cid), Times.Once);
    }

    #endregion
}
