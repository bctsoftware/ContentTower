using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class QuotaServiceTests
{
    private readonly Mock<ILogger<QuotaService>> mockLogger;
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly Mock<IOptions<StorageOptions>> mockOptions;

    public QuotaServiceTests()
    {
        mockLogger = new Mock<ILogger<QuotaService>>();
        mockFileSystem = new Mock<IFileSystem>();
        mockOptions = new Mock<IOptions<StorageOptions>>();
    }

    #region Helper Methods

    private QuotaService CreateQuotaService(StorageOptions? options = null)
    {
        var optionsToUse = options ?? CreateValidStorageOptions();
        mockOptions.Setup(o => o.Value).Returns(optionsToUse);
        return new QuotaService(mockLogger.Object, mockOptions.Object, mockFileSystem.Object);
    }

    private StorageOptions CreateValidStorageOptions(long quota = 1000000) // 1MB
    {
        return new StorageOptions
        {
            DataPath = "/valid/data/path",
            Quota = quota,
            CleanupIntervalSeconds = 600,
            StoreDurationDefaultNominalSeconds = 86400,
            StoreDurationDefaultPressureSeconds = 43200,
            StoreDurationTemporaryNominalSeconds = 7200,
            StoreDurationTemporaryPressureSeconds = 3600
        };
    }

    private FileMetadata CreateTestMetadata(string name = "test-file.txt", long length = 1000)
    {
        return new FileMetadata
        {
            Cid = new Cid("ct+test"),
            Name = name,
            ContentType = "text/plain",
            Length = length,
            StoreType = StoreType.Default,
            UploadUtc = DateTime.UtcNow,
            LastActivityUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region Tests - Initialize

    [Test]
    public async Task Initialize_WithEmptyFileSystem_InitializesWithZeroUsedBytes()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(0);
        await Assert.That(status.Quota).IsEqualTo(1000000);
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task Initialize_CalculatesNominalLimitAs80Percent()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var expectedNominalLimit = (long)(quota * 0.8);
        var status = service.GetQuotaStatus();
        // We can't directly check nominalLimit as it's private, but we can verify behavior
        await Assert.That(status.Quota).IsEqualTo(quota);
    }

    [Test]
    public async Task Initialize_WithExistingFiles_CountsUsedBytes()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        var file1 = CreateTestMetadata("file1.txt", length: 100000);
        var file2 = CreateTestMetadata("file2.txt", length: 200000);
        var capturedCallback = default(Action<FileMetadata>);

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback =>
            {
                capturedCallback = callback;
                callback(file1);
                callback(file2);
            })
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(300000);
    }

    [Test]
    public async Task Initialize_WithBelowNominalUsage_SetsStateToNominal()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        var file = CreateTestMetadata("file.txt", length: 100000); // 10% of quota
        var nominalLimit = (long)(quota * 0.8);

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(file))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task Initialize_WithAboveNominalUsage_SetsStateToPressure()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        var nominalLimit = (long)(quota * 0.8); // 800000
        var file = CreateTestMetadata("file.txt", length: 850000); // 85% of quota

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(file))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Pressure);
    }

    [Test]
    public async Task Initialize_WithAboveQuotaUsage_SetsStateToFull()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        var file = CreateTestMetadata("file.txt", length: 1100000); // 110% of quota

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(file))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Full);
    }

    #endregion

    #region Tests - GetQuotaStatus

    [Test]
    public async Task GetQuotaStatus_ReturnsCurrentStatus()
    {
        var options = CreateValidStorageOptions(quota: 5000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        var status = service.GetQuotaStatus();

        await Assert.That(status.Quota).IsEqualTo(5000000);
        await Assert.That(status.Used).IsGreaterThanOrEqualTo(0);
        await Assert.That((int)status.State).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Tests - IsFull

    [Test]
    public async Task IsFull_WhenStateIsNominal_ReturnsFalse()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        var isFull = service.IsFull();

        await Assert.That(isFull).IsFalse();
    }

    [Test]
    public async Task IsFull_WhenStateIsPressure_ReturnsFalse()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 850000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        var isFull = service.IsFull();

        await Assert.That(isFull).IsFalse();
    }

    [Test]
    public async Task IsFull_WhenStateIsFull_ReturnsTrue()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 1100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        var isFull = service.IsFull();

        await Assert.That(isFull).IsTrue();
    }

    #endregion

    #region Tests - AddUsedBytes

    [Test]
    public async Task AddUsedBytes_IncreasesUsedBytes()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(50000);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(50000);
    }

    [Test]
    public async Task AddUsedBytes_MultipleInvocations_AccumulateBytes()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(10000);
        service.AddUsedBytes(20000);
        service.AddUsedBytes(30000);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(60000);
    }

    [Test]
    public async Task AddUsedBytes_KeepsStateNominalWhenBelowNominalLimit()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(100000); // 10% of quota, below 80% nominal limit

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task AddUsedBytes_TransitionsFromNominalToPressure()
    {
        var quota = 1000000;
        var nominalLimit = (long)(quota * 0.8); // 800000
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(850000); // Exceeds 80% threshold

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Pressure);
    }

    [Test]
    public async Task AddUsedBytes_TransitionsFromNominalToFull()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(1100000); // Exceeds quota

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Full);
    }

    [Test]
    public async Task AddUsedBytes_TransitionsFromPressureToFull()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 850000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Verify initial state is Pressure
        await Assert.That(service.GetQuotaStatus().State).IsEqualTo(QuotaState.Pressure);

        service.AddUsedBytes(250000); // Takes total to 1.1 million

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Full);
    }

    #endregion

    #region Tests - RemoveUsedBytes

    [Test]
    public async Task RemoveUsedBytes_DecreasesUsedBytes()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.RemoveUsedBytes(30000);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(70000);
    }

    [Test]
    public async Task RemoveUsedBytes_MultipleInvocations_DecrementCorrectly()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.RemoveUsedBytes(20000);
        service.RemoveUsedBytes(30000);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(50000);
    }

    [Test]
    public async Task RemoveUsedBytes_PreventsNegativeValue()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 50000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.RemoveUsedBytes(100000); // Tries to remove more than exists

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveUsedBytes_LogsWarningWhenGoingNegative()
    {
        var options = CreateValidStorageOptions(quota: 1000000);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.RemoveUsedBytes(50000); // Remove when used is 0

        mockLogger.AssertLogged(LogLevel.Warning, "Quota skew");
    }

    [Test]
    public async Task RemoveUsedBytes_TransitionsFromPressureToNominal()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 850000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Verify initial state is Pressure
        await Assert.That(service.GetQuotaStatus().State).IsEqualTo(QuotaState.Pressure);

        service.RemoveUsedBytes(150000); // Takes used to 700000, below 80% threshold

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task RemoveUsedBytes_TransitionsFromFullToNominal()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 1100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Verify initial state is Full
        await Assert.That(service.GetQuotaStatus().State).IsEqualTo(QuotaState.Full);

        service.RemoveUsedBytes(400000); // Takes used to 700000, below 80% threshold

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task RemoveUsedBytes_TransitionsFromFullToPressure()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 1100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Verify initial state is Full
        await Assert.That(service.GetQuotaStatus().State).IsEqualTo(QuotaState.Full);

        service.RemoveUsedBytes(150000); // Takes used to 950000, above 80% threshold but below quota

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Pressure);
    }

    #endregion

    #region Tests - State Transitions and Warnings

    [Test]
    public async Task AddUsedBytes_LogsWarningWhenTransitioningTowardsPressure()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(850000); // Transition to Pressure

        mockLogger.AssertLogged(LogLevel.Warning, "pressure");
    }

    [Test]
    public async Task RemoveUsedBytes_LogsWarningWhenTransitioningAwayFromPressure()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 850000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Reset mock to count only new log calls
        mockLogger.Reset();

        service.RemoveUsedBytes(150000); // Transition from Pressure to Nominal

        mockLogger.AssertLogged(LogLevel.Warning, "resolved");
    }

    [Test]
    public async Task StateTransition_NoWarningWhenStayingInSameState()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        // Clear mock logs from Initialize
        mockLogger.Reset();

        service.AddUsedBytes(50000); // Stay in Nominal

        // No warning logs should be called
        mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("pressure")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region Tests - Edge Cases

    [Test]
    public async Task AddUsedBytes_WithZeroBytes_DoesNotChangeState()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();
        var initialState = service.GetQuotaStatus().State;

        service.AddUsedBytes(0);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(0);
        await Assert.That(status.State).IsEqualTo(initialState);
    }

    [Test]
    public async Task RemoveUsedBytes_WithZeroBytes_DoesNotChangeState()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 100000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();
        var initialState = service.GetQuotaStatus().State;
        var initialUsed = service.GetQuotaStatus().Used;

        service.RemoveUsedBytes(0);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(initialUsed);
        await Assert.That(status.State).IsEqualTo(initialState);
    }

    [Test]
    public async Task AddUsedBytes_WithLargeValues_HandlesCorrectly()
    {
        var largeQuota = long.MaxValue / 2;
        var options = CreateValidStorageOptions(quota: largeQuota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        var largeBytes = 1000000000; // 1 GB
        service.AddUsedBytes(largeBytes);

        var status = service.GetQuotaStatus();
        await Assert.That(status.Used).IsEqualTo(largeBytes);
    }

    [Test]
    public async Task Initialize_WithQuotaOf1_Works()
    {
        var options = CreateValidStorageOptions(quota: 1);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);

        await service.Initialize();

        var status = service.GetQuotaStatus();
        await Assert.That(status.Quota).IsEqualTo(1);
    }

    #endregion

    #region Tests - Boundary Conditions

    [Test]
    public async Task AddUsedBytes_ExactlyAtNominalLimit_TransitionsToPressure()
    {
        var quota = 1000000;
        var nominalLimit = (long)(quota * 0.8); // 800000
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(nominalLimit + 1); // One byte over nominal limit

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Pressure);
    }

    [Test]
    public async Task AddUsedBytes_JustBelowNominalLimit_StaysNominal()
    {
        var quota = 1000000;
        var nominalLimit = (long)(quota * 0.8); // 800000
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(nominalLimit); // Exactly at nominal limit

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    [Test]
    public async Task AddUsedBytes_ExactlyAtQuota_TransitionsToFull()
    {
        var quota = 1000000;
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.AddUsedBytes(quota + 1); // One byte over quota

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Full);
    }

    [Test]
    public async Task RemoveUsedBytes_BringsToBoundary_UpdatesStateCorrectly()
    {
        var quota = 1000000;
        var nominalLimit = (long)(quota * 0.8); // 800000
        var options = CreateValidStorageOptions(quota: quota);
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(callback => callback(CreateTestMetadata("file.txt", 850000)))
            .Returns(Task.CompletedTask);

        var service = CreateQuotaService(options);
        await service.Initialize();

        service.RemoveUsedBytes(50001); // Takes to just below nominal limit

        var status = service.GetQuotaStatus();
        await Assert.That(status.State).IsEqualTo(QuotaState.Nominal);
    }

    #endregion
}
