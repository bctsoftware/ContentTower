using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class SaveServiceTests
{
    private readonly Mock<ILogger<SaveService>> mockLogger;
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly Mock<IHashService> mockHashService;
    private readonly Mock<IPresenceService> mockPresenceService;
    private readonly Mock<IQuotaService> mockQuotaService;
    private readonly Mock<ITime> mockTimeService;

    public SaveServiceTests()
    {
        mockLogger = new Mock<ILogger<SaveService>>();
        mockFileSystem = new Mock<IFileSystem>();
        mockHashService = new Mock<IHashService>();
        mockPresenceService = new Mock<IPresenceService>();
        mockQuotaService = new Mock<IQuotaService>();
        mockTimeService = new Mock<ITime>();
    }

    #region Helper Methods

    private SaveService CreateSaveService()
    {
        return new SaveService(
            mockLogger.Object,
            mockFileSystem.Object,
            mockHashService.Object,
            mockPresenceService.Object,
            mockQuotaService.Object,
            mockTimeService.Object
        );
    }

    private UploadRequest CreateValidUploadRequest(
        StoreRequestType storeType = StoreRequestType.Default,
        string name = "test-file.txt",
        string contentType = "text/plain",
        byte[]? data = null)
    {
        return new UploadRequest
        {
            StoreType = storeType,
            Name = name,
            ContentType = contentType,
            Data = data ?? Encoding.UTF8.GetBytes("test content")
        };
    }

    private Cid CreateTestCid(string hash = "ct+test+hash")
    {
        return new Cid(hash);
    }

    private DateTime GetFixedUtcNow()
    {
        return new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
    }

    #endregion

    #region Tests - Happy Path: New Content Saved

    [Test]
    public async Task Handle_WithNewContent_SavesMetadataAndData()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        var result = await service.Handle(request);

        await Assert.That(result.Hash).IsEqualTo(testCid.Hash);
        mockHashService.Verify(hs => hs.GetHash(request.Data), Times.Once);
        mockFileSystem.Verify(fs => fs.WriteObject(testCid, It.IsAny<FileMetadata>()), Times.Once);
        mockFileSystem.Verify(fs => fs.WriteData(testCid, request.Data), Times.Once);
        mockPresenceService.Verify(ps => ps.SetPresence(testCid), Times.Once);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(request.Data.Length), Times.Once);
    }

    [Test]
    public async Task Handle_WithNewContent_CreatesMetadataWithCorrectProperties()
    {
        var request = CreateValidUploadRequest(
            storeType: StoreRequestType.PermanentFile,
            name: "my-file.pdf",
            contentType: "application/pdf"
        );
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();
        FileMetadata? capturedMetadata = null;

        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback<Cid, FileMetadata>((cid, metadata) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);

        await service.Handle(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.Cid.Hash).IsEqualTo(testCid.Hash);
        await Assert.That(capturedMetadata.Name).IsEqualTo("my-file.pdf");
        await Assert.That(capturedMetadata.ContentType).IsEqualTo("application/pdf");
        await Assert.That(capturedMetadata.Length).IsEqualTo(request.Data.Length);
        await Assert.That(capturedMetadata.StoreType).IsEqualTo(StoreRequestType.PermanentFile);
        await Assert.That(capturedMetadata.UploadUtc).IsEqualTo(fixedNow);
        await Assert.That(capturedMetadata.LastActivityUtc).IsEqualTo(fixedNow);
    }

    #endregion

    #region Tests - Early Exit: Content Already Present

    [Test]
    public async Task Handle_WithExistingContent_ReturnsExistingCid()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(true);

        var service = CreateSaveService();

        var result = await service.Handle(request);

        await Assert.That(result.Hash).IsEqualTo(testCid.Hash);
        mockFileSystem.Verify(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()), Times.Never);
        mockFileSystem.Verify(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()), Times.Never);
        mockPresenceService.Verify(ps => ps.SetPresence(It.IsAny<Cid>()), Times.Never);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(It.IsAny<long>()), Times.Never);
    }

    #endregion

    #region Tests - Error Path: Data Write Fails

    [Test]
    public async Task Handle_WhenWriteDataThrows_DeletesMetadataObject()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        var testException = new IOException("Disk write failed");

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .ThrowsAsync(testException);
        mockFileSystem.Setup(fs => fs.DeleteObject(testCid))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        Exception? thrownException = null;
        try
        {
            await service.Handle(request);
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException).IsOfType(typeof(IOException));
        mockFileSystem.Verify(fs => fs.DeleteObject(testCid), Times.Once);
    }

    [Test]
    public async Task Handle_WhenWriteDataThrows_DoesNotSetPresenceOrQuota()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .ThrowsAsync(new IOException("Write failed"));
        mockFileSystem.Setup(fs => fs.DeleteObject(testCid))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        try
        {
            await service.Handle(request);
        }
        catch
        {
            // Expected
        }

        mockPresenceService.Verify(ps => ps.SetPresence(It.IsAny<Cid>()), Times.Never);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(It.IsAny<long>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenWriteDataThrows_RethrowsException()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        var testException = new UnauthorizedAccessException("Permission denied");

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .ThrowsAsync(testException);
        mockFileSystem.Setup(fs => fs.DeleteObject(testCid))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        Exception? thrownException = null;
        try
        {
            await service.Handle(request);
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException).IsOfType(typeof(UnauthorizedAccessException));
    }

    #endregion

    #region Tests - Store Type Variations

    [Test]
    public async Task Handle_WithTemporaryStoreType_SavesWithCorrectStoreType()
    {
        var request = CreateValidUploadRequest(storeType: StoreRequestType.TemporaryFile);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback<Cid, FileMetadata>((cid, metadata) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.StoreType).IsEqualTo(StoreRequestType.TemporaryFile);
    }

    [Test]
    public async Task Handle_WithDefaultStoreType_SavesWithCorrectStoreType()
    {
        var request = CreateValidUploadRequest(storeType: StoreRequestType.Default);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback<Cid, FileMetadata>((cid, metadata) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.StoreType).IsEqualTo(StoreRequestType.Default);
    }

    #endregion

    #region Tests - Data Size Variations

    [Test]
    public async Task Handle_WithLargeData_AddsCorrectBytesToQuota()
    {
        var largeData = new byte[10 * 1024 * 1024]; // 10 MB
        var request = CreateValidUploadRequest(data: largeData);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        mockQuotaService.Verify(qs => qs.AddUsedBytes(10 * 1024 * 1024), Times.Once);
    }

    [Test]
    public async Task Handle_WithSmallData_SavesCorrectly()
    {
        var smallData = new byte[1]; // 1 byte
        var request = CreateValidUploadRequest(data: smallData);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        var result = await service.Handle(request);

        await Assert.That(result.Hash).IsEqualTo(testCid.Hash);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(1), Times.Once);
    }

    #endregion

    #region Tests - Method Call Order

    [Test]
    public async Task Handle_CallsHashServiceFirst()
    {
        var request = CreateValidUploadRequest();
        var testCid = CreateTestCid();
        var callOrder = new List<string>();

        var fixedNow = GetFixedUtcNow();
        mockHashService.Setup(hs => hs.GetHash(request.Data))
            .Callback(() => callOrder.Add("GetHash"))
            .Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid))
            .Callback(() => callOrder.Add("IsPresent"))
            .Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow())
            .Callback(() => callOrder.Add("UtcNow"))
            .Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback(() => callOrder.Add("WriteObject"))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Callback(() => callOrder.Add("WriteData"))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        await Assert.That(callOrder[0]).IsEqualTo("GetHash");
        await Assert.That(callOrder[1]).IsEqualTo("IsPresent");
    }

    #endregion

    #region Tests - Empty Request Data

    [Test]
    public async Task Handle_WithEmptyData_StillSavesCorrectly()
    {
        var request = CreateValidUploadRequest(data: Array.Empty<byte>());
        var testCid = CreateTestCid("ct+empty");
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        var result = await service.Handle(request);

        await Assert.That(result.Hash).IsEqualTo(testCid.Hash);
        mockFileSystem.Verify(fs => fs.WriteData(testCid, Array.Empty<byte>()), Times.Once);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(0), Times.Once);
    }

    #endregion

    #region Tests - Custom File Names and Content Types

    [Test]
    public async Task Handle_WithCustomFileName_SavesCustomName()
    {
        var customName = "my-document-2026-06-05.docx";
        var request = CreateValidUploadRequest(name: customName);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback<Cid, FileMetadata>((cid, metadata) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.Name).IsEqualTo(customName);
    }

    [Test]
    public async Task Handle_WithCustomContentType_SavesCustomContentType()
    {
        var customContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        var request = CreateValidUploadRequest(contentType: customContentType);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Callback<Cid, FileMetadata>((cid, metadata) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.ContentType).IsEqualTo(customContentType);
    }

    #endregion

    #region Tests - Parameter Verification

    [Test]
    public async Task Handle_PassesCorrectDataToHashService()
    {
        var testData = Encoding.UTF8.GetBytes("specific test data");
        var request = CreateValidUploadRequest(data: testData);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(testData)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(It.IsAny<Cid>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        mockHashService.Verify(hs => hs.GetHash(testData), Times.Once);
    }

    [Test]
    public async Task Handle_PassesCorrectDataToWriteData()
    {
        var testData = Encoding.UTF8.GetBytes("write data test");
        var request = CreateValidUploadRequest(data: testData);
        var testCid = CreateTestCid();
        var fixedNow = GetFixedUtcNow();

        mockHashService.Setup(hs => hs.GetHash(testData)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockTimeService.Setup(ts => ts.UtcNow()).Returns(fixedNow);
        mockFileSystem.Setup(fs => fs.WriteObject(It.IsAny<Cid>(), It.IsAny<FileMetadata>()))
            .Returns(Task.CompletedTask);
        mockFileSystem.Setup(fs => fs.WriteData(testCid, testData))
            .Returns(Task.CompletedTask);

        var service = CreateSaveService();

        await service.Handle(request);

        mockFileSystem.Verify(fs => fs.WriteData(testCid, testData), Times.Once);
    }

    #endregion
}
