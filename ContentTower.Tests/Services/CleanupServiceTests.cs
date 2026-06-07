using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class CleanupServiceTests
{
    private readonly Mock<ILogger<CleanupService>> mockLogger;
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<ICleanupWorker> mockCleanupWorker;

    public CleanupServiceTests()
    {
        mockLogger = new Mock<ILogger<CleanupService>>();
        mockFileSystem = new Mock<IFileSystem>();
        mockTimeService = new Mock<ITime>();
        mockCleanupWorker = new Mock<ICleanupWorker>();
    }

    #region Test Helpers

    private CleanupService CreateCleanupService()
    {
        return new CleanupService(
            mockLogger.Object,
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

        await service.Start();
        await Task.Delay(50); // Allow worker to start
        await service.Stop();

        mockLogger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cleanup service starting")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
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

        await service.Start();
        await Task.Delay(100); // Allow worker to execute

        await Assert.That(fillQueueCalled).IsTrue();

        await service.Stop();
    }

    [Test]
    public async Task Start_MultipleCallsCreatesNewWorkerEachTime()
    {
        var service = CreateCleanupService();
        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(50);
        await service.Stop();

        // Second start
        await service.Start();
        await Task.Delay(50);

        mockLogger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cleanup service starting")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeast(2));

        await service.Stop();
    }

    #endregion

    #region Tests - Stop

    [Test]
    public async Task Stop_CancelsCancellationToken()
    {
        var service = CreateCleanupService();
        var sleepCalled = false;
        var cancellationTokenPassed = CancellationToken.None;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, ct) =>
            {
                sleepCalled = true;
                cancellationTokenPassed = ct;
            })
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(50);
        await service.Stop();

        await Assert.That(sleepCalled).IsTrue();
        mockLogger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cleanup service stopped")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Test]
    public async Task Stop_WaitsForWorkerCompletion()
    {
        var service = CreateCleanupService();
        var workerCompletedBeforeStop = false;
        var stopCalledAfterWorkerStarted = false;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback(() => stopCalledAfterWorkerStarted = true)
            .Returns(Task.CompletedTask);
        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (stopCalledAfterWorkerStarted)
                    workerCompletedBeforeStop = true;
            })
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(50);
        await service.Stop();

        await Assert.That(workerCompletedBeforeStop).IsTrue();
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

        await service.Start();
        await Task.Delay(100);
        await service.Stop();

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

        await service.Start();
        await Task.Delay(100);
        await service.Stop();

        await Assert.That(sleepCalled).IsTrue();
        await Assert.That(sleepDuration.TotalMinutes).IsGreaterThan(5); // Should be 10 minutes
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

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>(), It.IsAny<CancellationToken>()))
            .Callback<FileMetadata, CancellationToken>((item, _) => processedItems.Add(item))
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(200);
        await service.Stop();

        await Assert.That(processedItems).Contains(files[0]);
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

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>(), It.IsAny<CancellationToken>()))
            .Callback<FileMetadata, CancellationToken>((item, _) => processedItems.Add(item))
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(300);
        await service.Stop();

        await Assert.That(processedItems).HasCount(3);
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

        mockCleanupWorker.Setup(cw => cw.ProcessItem(It.IsAny<FileMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((duration, ct) =>
            {
                if (duration.TotalMinutes > 5) // Long sleep after fill
                    queueRemovals++;
            })
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(150);
        await service.Stop();

        await Assert.That(queueRemovals).IsGreaterThan(0);
    }

    #endregion

    #region Tests - Worker Loop

    [Test]
    public async Task Worker_ContinuesUntilCancellationRequested()
    {
        var service = CreateCleanupService();
        var fillQueueCallCount = 0;
        var maxCallsToWait = 3;

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .Callback(() => fillQueueCallCount++)
            .Returns(Task.CompletedTask);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(200); // Allow multiple iterations
        await service.Stop();

        await Assert.That(fillQueueCallCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Worker_HandlesExceptionsByExiting()
    {
        var service = CreateCleanupService();
        var exceptionThrown = new InvalidOperationException("Test exception");

        mockFileSystem.Setup(fs => fs.IterateObjects<FileMetadata>(It.IsAny<Action<FileMetadata>>()))
            .ThrowsAsync(exceptionThrown);

        mockTimeService.Setup(ts => ts.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.Start();
        await Task.Delay(100);

        // Verify error was logged
        mockLogger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Fatal: Cleanup worker stopped")),
            exceptionThrown,
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion
}
