using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests.Services;

public class PinServiceTests
{
    private readonly Mock<ILogger<PinService>> mockLogger;
    private readonly Mock<IObjectStoreService> mockObjectStoreService;
    private readonly Mock<ITime> mockTimeService;
    private readonly Mock<IPresenceService> mockPresenceService;

    private static readonly DateTime FixedUtcNow = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    public PinServiceTests()
    {
        mockLogger = new Mock<ILogger<PinService>>();
        mockObjectStoreService = new Mock<IObjectStoreService>();
        mockTimeService = new Mock<ITime>();
        mockPresenceService = new Mock<IPresenceService>();
        mockTimeService.Setup(t => t.UtcNow()).Returns(FixedUtcNow);
    }

    #region Helpers

    private PinService CreatePinService() => new PinService(
        mockLogger.Object,
        mockObjectStoreService.Object,
        mockTimeService.Object,
        mockPresenceService.Object
    );

    private static Cid MakeCid(string id = "ctCid1") => new Cid(id);
    private static PinId MakePinId(string id = "pPin1") => new PinId(id);

    // Sets up CreateOrUpdateObject<T> to invoke the action on a fresh T instance.
    private void SetupCreateOrUpdate<T>() where T : IStorable, new()
    {
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<T>(It.IsAny<IId>(), It.IsAny<Action<T>>()))
            .Callback<IId, Action<T>>((_, action) => action(new T()));
    }

    private void SetupReadObject<T>(IId id, T value) where T : IStorable, new()
    {
        mockObjectStoreService.Setup(os => os.ReadObject<T>(id)).Returns(value);
    }

    private void SetupExists(IId id, bool exists)
    {
        mockObjectStoreService.Setup(os => os.Exists(id)).Returns(exists);
    }

    // Sets up CreateOrUpdateObject<T> for a specific id, invokes the action on the given instance, and returns it.
    private T SetupCaptureCreateOrUpdate<T>(IId id, T? seed = null) where T : class, IStorable, new()
    {
        var instance = seed ?? new T();
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<T>(id, It.IsAny<Action<T>>()))
            .Callback<IId, Action<T>>((_, action) => action(instance));
        return instance;
    }

    #endregion

    #region Attach(PinId[] pinIds, Cid cid)

    [Test]
    public async Task Attach_Pins_WithEmptyPins_DoesNothing()
    {
        var service = CreatePinService();

        service.Attach(Array.Empty<PinId>(), MakeCid());

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Attach_Pins_WithMultiplePins_AddsPinsToFileAndCidToEachPin()
    {
        var cid = MakeCid();
        var pin1 = MakePinId("pPin1");
        var pin2 = MakePinId("pPin2");
        SetupCreateOrUpdate<FileMetadata>();
        SetupCreateOrUpdate<PinData>();

        var service = CreatePinService();
        service.Attach(new[] { pin1, pin2 }, cid);

        // AddPinsToFile: one bulk FileMetadata update
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid, It.IsAny<Action<FileMetadata>>()), Times.Once);
        // AddCidToPin: one PinData update per pin
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pin1, It.IsAny<Action<PinData>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pin2, It.IsAny<Action<PinData>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Attach_Pins_AddsAllPinsToFileMetadata()
    {
        var cid = MakeCid();
        var pin1 = MakePinId("pPin1");
        var pin2 = MakePinId("pPin2");
        var fileMetadata = SetupCaptureCreateOrUpdate<FileMetadata>(cid);
        SetupCreateOrUpdate<PinData>();

        var service = CreatePinService();
        service.Attach(new[] { pin1, pin2 }, cid);

        await Assert.That(fileMetadata.PinIds.Count).IsEqualTo(2);
        await Assert.That(fileMetadata.PinIds[0].Id).IsEqualTo(pin1.Id);
        await Assert.That(fileMetadata.PinIds[1].Id).IsEqualTo(pin2.Id);
    }

    [Test]
    public async Task Attach_Pins_AddsCidToEachPinData()
    {
        var cid = MakeCid();
        var pin1 = MakePinId("pPin1");
        var pin2 = MakePinId("pPin2");
        SetupCreateOrUpdate<FileMetadata>();
        var pinData1 = SetupCaptureCreateOrUpdate<PinData>(pin1);
        var pinData2 = SetupCaptureCreateOrUpdate<PinData>(pin2);

        var service = CreatePinService();
        service.Attach(new[] { pin1, pin2 }, cid);

        await Assert.That(pinData1.Cids.Count).IsEqualTo(1);
        await Assert.That(pinData1.Cids[0].Id).IsEqualTo(cid.Id);
        await Assert.That(pinData2.Cids.Count).IsEqualTo(1);
        await Assert.That(pinData2.Cids[0].Id).IsEqualTo(cid.Id);
    }

    #endregion

    #region Attach(PinId pinId, Cid[] cids)

    [Test]
    public async Task Attach_Cids_WithEmptyCids_DoesNothing()
    {
        var service = CreatePinService();

        service.Attach(MakePinId(), Array.Empty<Cid>());

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()), Times.Never);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Attach_Cids_WithMultipleCids_AddsCidsToOnePinAndPinToEachCid()
    {
        var pinId = MakePinId();
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        SetupCreateOrUpdate<PinData>();
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        service.Attach(pinId, new[] { cid1, cid2 });

        // AddCidsToPin: one bulk PinData update
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pinId, It.IsAny<Action<PinData>>()), Times.Once);
        // AddPinToCid: one FileMetadata update per cid
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid1, It.IsAny<Action<FileMetadata>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid2, It.IsAny<Action<FileMetadata>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Attach_Cids_AddsAllCidsToPinData()
    {
        var pinId = MakePinId();
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        var pinData = SetupCaptureCreateOrUpdate<PinData>(pinId);
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        service.Attach(pinId, new[] { cid1, cid2 });

        await Assert.That(pinData.Cids.Count).IsEqualTo(2);
        await Assert.That(pinData.Cids[0].Id).IsEqualTo(cid1.Id);
        await Assert.That(pinData.Cids[1].Id).IsEqualTo(cid2.Id);
    }

    [Test]
    public async Task Attach_Cids_AddsPinToEachFileMetadata()
    {
        var pinId = MakePinId();
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        SetupCreateOrUpdate<PinData>();
        var fileMeta1 = SetupCaptureCreateOrUpdate<FileMetadata>(cid1);
        var fileMeta2 = SetupCaptureCreateOrUpdate<FileMetadata>(cid2);

        var service = CreatePinService();
        service.Attach(pinId, new[] { cid1, cid2 });

        await Assert.That(fileMeta1.PinIds.Count).IsEqualTo(1);
        await Assert.That(fileMeta1.PinIds[0].Id).IsEqualTo(pinId.Id);
        await Assert.That(fileMeta2.PinIds.Count).IsEqualTo(1);
        await Assert.That(fileMeta2.PinIds[0].Id).IsEqualTo(pinId.Id);
    }

    #endregion

    #region Detach

    [Test]
    public async Task Detach_WithEmptyCids_DoesNothing()
    {
        var service = CreatePinService();

        service.Detach(MakePinId(), Array.Empty<Cid>());

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()), Times.Never);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Detach_WhenCidExists_RemovesCidFromPinAndPinFromCid()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupCreateOrUpdate<PinData>();
        SetupCreateOrUpdate<FileMetadata>();
        SetupExists(cid, true);

        var service = CreatePinService();
        service.Detach(pinId, new[] { cid });

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pinId, It.IsAny<Action<PinData>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid, It.IsAny<Action<FileMetadata>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Detach_WhenCidDoesNotExist_RemovesCidFromPinButSkipsFileUpdate()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupCreateOrUpdate<PinData>();
        SetupExists(cid, false);

        var service = CreatePinService();
        service.Detach(pinId, new[] { cid });

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pinId, It.IsAny<Action<PinData>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Detach_RemovesCidsFromPinData()
    {
        var pinId = MakePinId();
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        var pinData = SetupCaptureCreateOrUpdate<PinData>(pinId,
            new PinData { Cids = new List<Cid> { cid1, cid2 } });
        SetupExists(cid1, false);
        SetupExists(cid2, false);

        var service = CreatePinService();
        service.Detach(pinId, new[] { cid1, cid2 });

        await Assert.That(pinData.Cids.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Detach_WhenCidExists_RemovesPinFromFileMetadata()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupCreateOrUpdate<PinData>();
        var fileMetadata = SetupCaptureCreateOrUpdate<FileMetadata>(cid,
            new FileMetadata { PinIds = new List<PinId> { pinId } });
        SetupExists(cid, true);

        var service = CreatePinService();
        service.Detach(pinId, new[] { cid });

        await Assert.That(fileMetadata.PinIds.Count).IsEqualTo(0);
    }

    #endregion

    #region Create(StoreType type, Cid[] cids)

    [Test]
    public async Task Create_SingleType_ReturnsPinIdWithCorrectPrefix()
    {
        SetupCreateOrUpdate<PinData>();

        var service = CreatePinService();
        var result = service.Create(StoreType.Default, Array.Empty<Cid>());

        await Assert.That(result.Id.StartsWith(PinService.PinIdPrefix)).IsTrue();
    }

    [Test]
    public async Task Create_SingleType_SetsCorrectPinDataProperties()
    {
        var cid = MakeCid();
        PinData? capturedPinData = null;
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()))
            .Callback<IId, Action<PinData>>((_, action) =>
            {
                var pd = new PinData();
                action(pd);
                capturedPinData = pd;
            });
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        service.Create(StoreType.Permanent, new[] { cid });

        await Assert.That(capturedPinData).IsNotNull();
        await Assert.That(capturedPinData!.StoreType).IsEqualTo(StoreType.Permanent);
        await Assert.That(capturedPinData.Cids.Count).IsEqualTo(1);
        await Assert.That(capturedPinData.Cids[0].Id).IsEqualTo(cid.Id);
        await Assert.That(capturedPinData.CreateUtc).IsEqualTo(FixedUtcNow);
        await Assert.That(capturedPinData.LastActivityUtc).IsEqualTo(FixedUtcNow);
    }

    [Test]
    public async Task Create_SingleType_SetsPresenceOnNewPin()
    {
        SetupCreateOrUpdate<PinData>();

        var service = CreatePinService();
        var result = service.Create(StoreType.Default, Array.Empty<Cid>());

        mockPresenceService.Verify(ps => ps.SetPresence(result), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Create_SingleType_WithCids_AssociatesNewPinWithEachCid()
    {
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        SetupCreateOrUpdate<PinData>();
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        service.Create(StoreType.Default, new[] { cid1, cid2 });

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid1, It.IsAny<Action<FileMetadata>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid2, It.IsAny<Action<FileMetadata>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Create_SingleType_WithCids_AddsPinIdToEachFileMetadata()
    {
        var cid1 = MakeCid("ctCid1");
        var cid2 = MakeCid("ctCid2");
        SetupCreateOrUpdate<PinData>();
        var fileMeta1 = SetupCaptureCreateOrUpdate<FileMetadata>(cid1);
        var fileMeta2 = SetupCaptureCreateOrUpdate<FileMetadata>(cid2);

        var service = CreatePinService();
        var newPinId = service.Create(StoreType.Default, new[] { cid1, cid2 });

        await Assert.That(fileMeta1.PinIds.Count).IsEqualTo(1);
        await Assert.That(fileMeta1.PinIds[0].Id).IsEqualTo(newPinId.Id);
        await Assert.That(fileMeta2.PinIds.Count).IsEqualTo(1);
        await Assert.That(fileMeta2.PinIds[0].Id).IsEqualTo(newPinId.Id);
    }

    #endregion

    #region Create(StoreType[] types, Cid cid)

    [Test]
    public async Task Create_MultipleTypes_WithEmptyTypes_ReturnsEmptyArray()
    {
        var service = CreatePinService();
        var result = service.Create(Array.Empty<StoreType>(), MakeCid());

        await Assert.That(result.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Create_MultipleTypes_CreatesOnePinPerType()
    {
        SetupCreateOrUpdate<PinData>();
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        var result = service.Create(new[] { StoreType.Default, StoreType.Permanent, StoreType.Temporary }, MakeCid());

        await Assert.That(result.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Create_MultipleTypes_SetsPresenceForEachCreatedPin()
    {
        SetupCreateOrUpdate<PinData>();
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        var result = service.Create(new[] { StoreType.Default, StoreType.Permanent }, MakeCid());

        mockPresenceService.Verify(ps => ps.SetPresence(result[0]), Times.Once);
        mockPresenceService.Verify(ps => ps.SetPresence(result[1]), Times.Once);
        await Task.CompletedTask;
    }

    #endregion

    #region Exists

    [Test]
    public async Task Exists_WhenPinExists_ReturnsTrue()
    {
        var pinId = MakePinId();
        mockObjectStoreService.Setup(os => os.Exists(pinId)).Returns(true);

        var service = CreatePinService();

        await Assert.That(service.Exists(pinId)).IsTrue();
    }

    [Test]
    public async Task Exists_WhenPinDoesNotExist_ReturnsFalse()
    {
        var pinId = MakePinId();
        mockObjectStoreService.Setup(os => os.Exists(pinId)).Returns(false);

        var service = CreatePinService();

        await Assert.That(service.Exists(pinId)).IsFalse();
    }

    #endregion

    #region RegisterActivity

    [Test]
    public async Task RegisterActivity_WithNoPins_DoesNotUpdateAnyPinData()
    {
        var cid = MakeCid();
        SetupReadObject(cid, new FileMetadata { Cid = cid });

        var service = CreatePinService();
        service.RegisterActivity(cid);

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task RegisterActivity_WithMultiplePins_UpdatesEachPin()
    {
        var cid = MakeCid();
        var pin1 = MakePinId("pPin1");
        var pin2 = MakePinId("pPin2");
        SetupReadObject(cid, new FileMetadata { Cid = cid, PinIds = new List<PinId> { pin1, pin2 } });
        SetupCreateOrUpdate<PinData>();

        var service = CreatePinService();
        service.RegisterActivity(cid);

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pin1, It.IsAny<Action<PinData>>()), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<PinData>(pin2, It.IsAny<Action<PinData>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task RegisterActivity_UpdatesLastActivityUtcOnPin()
    {
        var cid = MakeCid();
        var pinId = MakePinId();
        SetupReadObject(cid, new FileMetadata { Cid = cid, PinIds = new List<PinId> { pinId } });
        PinData? capturedPinData = null;
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<PinData>(It.IsAny<IId>(), It.IsAny<Action<PinData>>()))
            .Callback<IId, Action<PinData>>((_, action) =>
            {
                var pd = new PinData();
                action(pd);
                capturedPinData = pd;
            });

        var service = CreatePinService();
        service.RegisterActivity(cid);

        await Assert.That(capturedPinData).IsNotNull();
        await Assert.That(capturedPinData!.LastActivityUtc).IsEqualTo(FixedUtcNow);
    }

    #endregion

    #region Get

    [Test]
    public async Task Get_ReturnsPinDataFromObjectStore()
    {
        var pinId = MakePinId();
        var expected = new PinData { PinId = pinId };
        SetupReadObject(pinId, expected);

        var service = CreatePinService();
        var result = service.Get(pinId);

        await Assert.That(result.PinId.Id).IsEqualTo(pinId.Id);
    }

    #endregion

    #region Delete

    [Test]
    public async Task Delete_WithNoCids_ClearsPresenceAndDeletesObject()
    {
        var pinId = MakePinId();
        SetupReadObject(pinId, new PinData { PinId = pinId });

        var service = CreatePinService();
        service.Delete(pinId);

        mockPresenceService.Verify(ps => ps.ClearPresence(pinId), Times.Once);
        mockObjectStoreService.Verify(os => os.DeleteObject(pinId), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Delete_WhenCidExists_RemovesPinFromCid()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupReadObject(pinId, new PinData { PinId = pinId, Cids = new List<Cid> { cid } });
        SetupExists(cid, true);
        SetupCreateOrUpdate<FileMetadata>();

        var service = CreatePinService();
        service.Delete(pinId);

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(cid, It.IsAny<Action<FileMetadata>>()), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Delete_WhenCidDoesNotExist_SkipsFileUpdate()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupReadObject(pinId, new PinData { PinId = pinId, Cids = new List<Cid> { cid } });
        SetupExists(cid, false);

        var service = CreatePinService();
        service.Delete(pinId);

        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        mockPresenceService.Verify(ps => ps.ClearPresence(pinId), Times.Once);
        mockObjectStoreService.Verify(os => os.DeleteObject(pinId), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Delete_WhenCidExists_RemovesPinFromFileMetadata()
    {
        var pinId = MakePinId();
        var cid = MakeCid();
        SetupReadObject(pinId, new PinData { PinId = pinId, Cids = new List<Cid> { cid } });
        SetupExists(cid, true);
        var fileMetadata = SetupCaptureCreateOrUpdate<FileMetadata>(cid,
            new FileMetadata { PinIds = new List<PinId> { pinId } });

        var service = CreatePinService();
        service.Delete(pinId);

        await Assert.That(fileMetadata.PinIds.Count).IsEqualTo(0);
    }

    #endregion
}
