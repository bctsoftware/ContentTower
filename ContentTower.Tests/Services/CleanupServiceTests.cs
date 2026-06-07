using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class CleanupServiceTests
{
    private readonly int cleanupIntervalSeconds = 123;
    private readonly Mock<ILogger<CleanupService>> mockLogger;
    private readonly Mock<IOptions<StorageOptions>> mockOptions;
    private readonly Mock<IHostApplicationLifetime> mockAppLifetime;
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<ICleanupWorker> mockCleanupWorker;

    public CleanupServiceTests()
    {
        mockLogger = new Mock<ILogger<CleanupService>>();
        mockOptions = new Mock<IOptions<StorageOptions>>();
        mockAppLifetime = new Mock<IHostApplicationLifetime>();
        mockFileSystem = new Mock<IFileSystem>();
        mockTimeService = new Mock<ITime>();
        mockCleanupWorker = new Mock<ICleanupWorker>();
    }

    #region Test Helpers

    private CleanupService CreateCleanupService()
    {
        mockOptions.Setup(o => o.Value).Returns(new StorageOptions
        {
            DataPath = "/data",
            Quota = 1000000,
            CleanupIntervalSeconds = cleanupIntervalSeconds,
            StoreDurationDefaultNominalSeconds = 86400,      // 1 day
            StoreDurationDefaultPressureSeconds = 43200,     // 12 hours
            StoreDurationTemporaryNominalSeconds = 7200,     // 2 hours
            StoreDurationTemporaryPressureSeconds = 3600     // 1 hour
        });

        return new CleanupService(
            mockLogger.Object,
            mockOptions.Object,
            mockAppLifetime.Object,
            mockFileSystem.Object,
            mockTimeService.Object,
            mockCleanupWorker.Object
        );
    }

    private FileMetadata CreateTestFile(
        string cidHash = "test-file-1",
        long length = 1000,
        StoreRequestType storeType = StoreRequestType.Default)
    {
        return new FileMetadata
        {
            Cid = new Cid(cidHash),
            Name = "test.txt",
            ContentType = "text/plain",
            Length = length,
            StoreType = storeType,
            UploadUtc = DateTime.UtcNow,
            LastActivityUtc = DateTime.UtcNow
        };
    }

    private List<FileMetadata> CreateTestFileSet(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => CreateTestFile($"file-{i}", 1000 + (i * 100)))
            .ToList();
    }

    #endregion

    #region Tests - Start

    [Test]
    public async Task Start_CreatesNewCancellationTokenSource()
    {
        var service = CreateCleanupService();
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(50); // Allow worker to start

        mockLogger.AssertLogged(LogLevel.Information, "Cleanup service starting");
    }

    [Test]
    public async Task Start_StartsWorkerTask()
    {
        var service = CreateCleanupService();
        var fillQueueCalled = false;
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback(() => fillQueueCalled = true)
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(100); // Allow worker to execute

        await Assert.That(fillQueueCalled).IsTrue();
    }

    #endregion

    #region Tests - Queue Processing - Empty Queue

    [Test]
    public async Task Step_WithEmptyQueue_CallsFillQueue()
    {
        var service = CreateCleanupService();
        var fillQueueCalled = false;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback(() => fillQueueCalled = true)
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(100);
        
        await Assert.That(fillQueueCalled).IsTrue();
        mockFileSystem.Verify(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Step_WithEmptyQueue_SleepsAfterFilling()
    {
        var service = CreateCleanupService();
        var sleepCalled = false;
        var sleepDuration = TimeSpan.Zero;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((duration, _) =>
            {
                sleepCalled = true;
                sleepDuration = duration;
            })
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(100);
        
        await Assert.That(sleepCalled).IsTrue();
        await Assert.That(sleepDuration.TotalSeconds).IsEqualTo(cleanupIntervalSeconds); // Should be 10 minutes
        mockTimeService.Verify(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region Tests - Queue Processing - Non-Empty Queue

    [Test]
    public async Task Step_WithQueuedItems_ProcessesFirstItem()
    {
        var service = CreateCleanupService();
        var files = CreateTestFileSet(3);
        var processedItems = new List<FileMetadata>();
        var stepCount = 0;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(action =>
            {
                stepCount++;
                if (stepCount == 1)
                {
                    foreach (var file in files)
                    {
                        action(file);
                    }
                }
            })
            .Returns(Task.CompletedTask);

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(processedItems.Add)
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(200);

        await Assert.That(processedItems).Contains(files[0]);

        mockTimeService.Verify(t => t.Sleep(TimeSpan.FromMilliseconds(100), It.IsAny<CancellationToken>()), Times.Exactly(files.Count));
    }

    [Test]
    public async Task Step_WithMultipleItems_ProcessesAllInOrder()
    {
        var service = CreateCleanupService();
        var files = CreateTestFileSet(3);
        var processedItems = new List<FileMetadata>();
        var fillQueueCallCount = 0;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(action =>
            {
                fillQueueCallCount++;
                if (fillQueueCallCount == 1)
                {
                    foreach (var file in files)
                    {
                        action(file);
                    }
                }
            })
            .Returns(Task.CompletedTask);

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(processedItems.Add)
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(300);

        await Assert.That(processedItems.Count).IsEqualTo(3);
        await Assert.That(processedItems[0].Cid.Hash).IsEqualTo(files[0].Cid.Hash);
        await Assert.That(processedItems[1].Cid.Hash).IsEqualTo(files[1].Cid.Hash);
        await Assert.That(processedItems[2].Cid.Hash).IsEqualTo(files[2].Cid.Hash);
    }

    [Test]
    public async Task Step_RemovesProcessedItemFromQueue()
    {
        var service = CreateCleanupService();
        var file = CreateTestFile();
        var queueRemovals = 0;
        var fillQueueCalled = false;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback<Action<FileMetadata>>(action =>
            {
                if (!fillQueueCalled)
                {
                    fillQueueCalled = true;
                    action(file);
                }
            })
            .Returns(Task.CompletedTask);

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((duration, ct) =>
            {
                if (duration.TotalSeconds == cleanupIntervalSeconds) // Long sleep after fill
                    queueRemovals++;
            })
            .Returns(Task.CompletedTask);

        service.Start();
        await Task.Delay(150);
        
        await Assert.That(queueRemovals).IsGreaterThan(0);
    }

    #endregion
}
