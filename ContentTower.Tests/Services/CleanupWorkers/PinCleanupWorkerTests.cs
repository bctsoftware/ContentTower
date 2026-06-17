using ContentTower.Services;
using ContentTower.Services.CleanupWorkers;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests.Services.CleanupWorkers;

public class PinCleanupWorkerTests
{
    private readonly Mock<ILogger<PinCleanupWorker>> mockLogger;
    private readonly Mock<IObjectStoreService> mockObjectStoreService;
    private readonly Mock<IPinService> mockPinService;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<ITimespanSelector> mockTimespanSelector;

    private static readonly DateTime FixedNow = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Span = TimeSpan.FromHours(1);
    private static readonly DateTime ExpiredStart = FixedNow - Span - TimeSpan.FromMinutes(1);
    private static readonly DateTime FreshStart = FixedNow - Span + TimeSpan.FromMinutes(1);

    public PinCleanupWorkerTests()
    {
        mockLogger = new Mock<ILogger<PinCleanupWorker>>();
        mockObjectStoreService = new Mock<IObjectStoreService>();
        mockPinService = new Mock<IPinService>();
        mockTimeService = new Mock<ITime>();
        mockTimespanSelector = new Mock<ITimespanSelector>();
        mockTimeService.Setup(t => t.UtcNow()).Returns(FixedNow);
        mockTimespanSelector.Setup(ts => ts.Get(It.IsAny<StoreType>())).Returns(Span);
    }

    #region Helpers

    private PinCleanupWorker CreateWorker() => new PinCleanupWorker(
        mockLogger.Object,
        mockObjectStoreService.Object,
        mockPinService.Object,
        mockTimeService.Object,
        mockTimespanSelector.Object
    );

    private void SetupPins(params PinData[] pins)
    {
        mockObjectStoreService
            .Setup(os => os.IterateObjects<PinData>(PinService.PinIdPrefix, It.IsAny<Action<PinData>>()))
            .Callback<string, Action<PinData>>((_, onObject) =>
            {
                foreach (var pin in pins) onObject(pin);
            });
    }

    private static PinData MakePin(PinId pinId, StoreType storeType, DateTime createUtc, DateTime lastActivityUtc) =>
        new PinData
        {
            PinId = pinId,
            StoreType = storeType,
            CreateUtc = createUtc,
            LastActivityUtc = lastActivityUtc,
        };

    private static PinId MakePinId(string id = "pPin1") => new PinId(id);

    #endregion

    #region Step

    [Test]
    public async Task Step_WithNoPins_DoesNotDeleteAnything()
    {
        SetupPins();

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(It.IsAny<PinId>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WhenCancellationRequestedBeforeProcessingPin_ThrowsOperationCanceledException()
    {
        var pin = MakePin(MakePinId(), StoreType.Default, ExpiredStart, ExpiredStart);
        SetupPins(pin);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? thrown = null;
        try { CreateWorker().Step(cts.Token); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(OperationCanceledException));
    }

    [Test]
    public async Task Step_WithPermanentPin_DoesNotDeleteIt()
    {
        var pin = MakePin(MakePinId(), StoreType.Permanent, ExpiredStart, ExpiredStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(It.IsAny<PinId>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithExpiredDefaultPin_DeletesPin()
    {
        var pinId = MakePinId();
        var pin = MakePin(pinId, StoreType.Default, createUtc: ExpiredStart, lastActivityUtc: ExpiredStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(pinId), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithNotExpiredDefaultPin_DoesNotDeleteIt()
    {
        var pin = MakePin(MakePinId(), StoreType.Default, createUtc: FreshStart, lastActivityUtc: FreshStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(It.IsAny<PinId>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithExpiredTemporaryPin_DeletesPin()
    {
        var pinId = MakePinId();
        // CreateUtc is fresh (would not expire if Default), but LastActivityUtc is old → expired as Temporary
        var pin = MakePin(pinId, StoreType.Temporary, createUtc: FreshStart, lastActivityUtc: ExpiredStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(pinId), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithNotExpiredTemporaryPin_DoesNotDeleteIt()
    {
        // CreateUtc is old (would expire if Default), but LastActivityUtc is fresh → not expired as Temporary
        var pin = MakePin(MakePinId(), StoreType.Temporary, createUtc: ExpiredStart, lastActivityUtc: FreshStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(It.IsAny<PinId>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_DefaultPin_UsesCreateUtcNotLastActivityUtcForExpiry()
    {
        var pinId = MakePinId();
        // CreateUtc expired, LastActivityUtc fresh — only Default should delete (measures from create)
        var pin = MakePin(pinId, StoreType.Default, createUtc: ExpiredStart, lastActivityUtc: FreshStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(pinId), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_TemporaryPin_UsesLastActivityUtcNotCreateUtcForExpiry()
    {
        var pinId = MakePinId();
        // LastActivityUtc expired, CreateUtc fresh — only Temporary should delete (measures from last-activity)
        var pin = MakePin(pinId, StoreType.Temporary, createUtc: FreshStart, lastActivityUtc: ExpiredStart);
        SetupPins(pin);

        CreateWorker().Step(CancellationToken.None);

        mockPinService.Verify(ps => ps.Delete(pinId), Times.Once);
        await Task.CompletedTask;
    }

    #endregion
}

