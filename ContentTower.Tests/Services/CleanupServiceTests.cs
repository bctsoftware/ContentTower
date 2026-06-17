using ContentTower.Services;
using ContentTower.Services.CleanupWorkers;
using ContentTower.System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class CleanupServiceTests : IDisposable
{
    private readonly Mock<ILogger<CleanupService>> mockLogger;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<IHostApplicationLifetime> mockAppLifetime;
    private readonly Mock<IPinCleanupWorker> mockPinWorker;
    private readonly Mock<IContentCleanupWorker> mockContentWorker;
    private readonly Mock<IDatafileCleanupWorker> mockDatafileWorker;
    private readonly CancellationTokenSource appStoppingCts;

    private static readonly TimeSpan LongSleep = TimeSpan.FromSeconds(999);
    private static readonly TimeSpan StepSleep = TimeSpan.FromMilliseconds(100);

    public CleanupServiceTests()
    {
        mockLogger = new Mock<ILogger<CleanupService>>();
        mockTimeService = new Mock<ITime>();
        mockAppLifetime = new Mock<IHostApplicationLifetime>();
        mockPinWorker = new Mock<IPinCleanupWorker>();
        mockContentWorker = new Mock<IContentCleanupWorker>();
        mockDatafileWorker = new Mock<IDatafileCleanupWorker>();
        appStoppingCts = new CancellationTokenSource();
        mockAppLifetime.SetupGet(al => al.ApplicationStopping).Returns(appStoppingCts.Token);
    }

    public void Dispose() => appStoppingCts.Dispose();

    #region Helpers

    private CleanupService CreateService() => new CleanupService(
        mockLogger.Object,
        Options.Create(new StorageOptions { CleanupIntervalSeconds = (int)LongSleep.TotalSeconds }),
        mockAppLifetime.Object,
        mockTimeService.Object,
        mockPinWorker.Object,
        mockContentWorker.Object,
        mockDatafileWorker.Object
    );

    // Sets up the long sleep to cancel the app token and signal the TCS, then return completed.
    private void SetupLongSleepCancelsAndSignals(TaskCompletionSource tcs)
    {
        mockTimeService
            .Setup(t => t.Sleep(LongSleep, It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => { appStoppingCts.Cancel(); tcs.TrySetResult(); })
            .Returns(Task.CompletedTask);
    }

    // Sets up the long sleep to return a cancelled task (when token is already cancelled).
    private void SetupLongSleepReturnsCanceled()
    {
        mockTimeService
            .Setup(t => t.Sleep(LongSleep, It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>((_, ct) => Task.FromCanceled(ct));
    }

    private void SetupAllStepSleepsReturnImmediately()
    {
        mockTimeService
            .Setup(t => t.Sleep(StepSleep, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Task WaitForSignal(TaskCompletionSource tcs) =>
        tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

    #endregion

    #region Start

    [Test]
    public async Task Start_LogsStartupMessage()
    {
        // The log call is synchronous before Task.Run, so no background-task waiting needed.
        appStoppingCts.Cancel();

        CreateService().Start();

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Cleanup service starting")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Start_WhenTokenPreCancelled_DoesNotCallAnyWorker()
    {
        appStoppingCts.Cancel();

        CreateService().Start();

        // Brief wait for the background task to start and exit its while-loop check.
        await Task.Delay(100);

        mockPinWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
        mockContentWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
        mockDatafileWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_RunsFullIteration_CallsAllThreeWorkersOnce()
    {
        var completionTcs = new TaskCompletionSource();
        SetupAllStepSleepsReturnImmediately();
        SetupLongSleepCancelsAndSignals(completionTcs);

        CreateService().Start();
        await WaitForSignal(completionTcs);

        mockPinWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
        mockContentWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
        mockDatafileWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Start_WhenCancelledAfterPinStep_SkipsContentAndDatafileWorkers()
    {
        // Cancel inside the first stepSleep (after pin worker runs).
        var signalTcs = new TaskCompletionSource();
        mockTimeService
            .Setup(t => t.Sleep(StepSleep, It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => { appStoppingCts.Cancel(); signalTcs.TrySetResult(); })
            .Returns(Task.CompletedTask);
        SetupLongSleepReturnsCanceled();

        CreateService().Start();
        await WaitForSignal(signalTcs);

        // Pin worker was called before the first stepSleep; content and datafile are guarded
        // by the IsCancellationRequested check that immediately follows the first sleep.
        mockPinWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
        mockContentWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
        mockDatafileWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_WhenCancelledAfterContentStep_SkipsDatafileWorker()
    {
        // Cancel inside the second stepSleep (after content worker runs).
        var signalTcs = new TaskCompletionSource();
        var stepSleepCount = 0;
        mockTimeService
            .Setup(t => t.Sleep(StepSleep, It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) =>
            {
                if (++stepSleepCount == 2) { appStoppingCts.Cancel(); signalTcs.TrySetResult(); }
            })
            .Returns(Task.CompletedTask);
        SetupLongSleepReturnsCanceled();

        CreateService().Start();
        await WaitForSignal(signalTcs);

        // Content worker was called before the second stepSleep signal.
        mockPinWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
        mockContentWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Once);
        mockDatafileWorker.Verify(w => w.Step(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_WhenTaskCanceledExceptionThrown_DoesNotLogError()
    {
        // Signal from the pin worker so we know the background task started.
        var startedTcs = new TaskCompletionSource();
        mockPinWorker
            .Setup(w => w.Step(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => startedTcs.TrySetResult());
        mockTimeService
            .Setup(t => t.Sleep(StepSleep, It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new TaskCanceledException()));

        CreateService().Start();
        await WaitForSignal(startedTcs);
        // Allow Worker() time to catch the TaskCanceledException and return.
        await Task.Delay(100);

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion
}
