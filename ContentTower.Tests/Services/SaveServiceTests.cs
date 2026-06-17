using ContentTower.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class SaveServiceTests
{
    private readonly Mock<ILogger<SaveService>> mockLogger;
    private readonly Mock<IObjectStoreService> mockObjectStoreService;
    private readonly Mock<IDataStoreService> mockDataStoreService;
    private readonly Mock<IHashService> mockHashService;
    private readonly Mock<IPresenceService> mockPresenceService;
    private readonly Mock<IQuotaService> mockQuotaService;

    public SaveServiceTests()
    {
        mockLogger = new Mock<ILogger<SaveService>>();
        mockObjectStoreService = new Mock<IObjectStoreService>();
        mockDataStoreService = new Mock<IDataStoreService>();
        mockHashService = new Mock<IHashService>();
        mockPresenceService = new Mock<IPresenceService>();
        mockQuotaService = new Mock<IQuotaService>();
    }

    #region Helper Methods

    private SaveService CreateSaveService()
    {
        return new SaveService(
            mockLogger.Object,
            mockObjectStoreService.Object,
            mockDataStoreService.Object,
            mockHashService.Object,
            mockPresenceService.Object,
            mockQuotaService.Object
        );
    }

    private SaveRequest CreateValidSaveRequest(
        string name = "test-file.txt",
        string contentType = "text/plain",
        byte[]? data = null)
    {
        return new SaveRequest(
            name,
            contentType,
            data ?? Encoding.UTF8.GetBytes("test content")
        );
    }

    private Cid CreateTestCid(string hash = "ct+test+hash")
    {
        return new Cid(hash);
    }

    #endregion

    #region Tests - Happy Path: New Content Saved

    [Test]
    public async Task Handle_WithNewContent_SavesMetadataAndData()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        var result = service.Save(request);

        await Assert.That(result.Id).IsEqualTo(testCid.Id);
        mockHashService.Verify(hs => hs.GetHash(request.Data), Times.Once);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(testCid, It.IsAny<Action<FileMetadata>>()), Times.Once);
        mockDataStoreService.Verify(ds => ds.WriteData(testCid, request.Data), Times.Once);
        mockPresenceService.Verify(ps => ps.SetPresence(testCid), Times.Once);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(request.Data.Length), Times.Once);
    }

    [Test]
    public async Task Handle_WithNewContent_CreatesMetadataWithCorrectProperties()
    {
        var request = CreateValidSaveRequest(
            name: "my-file.pdf",
            contentType: "application/pdf"
        );
        var testCid = CreateTestCid();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()))
            .Callback<IId, Action<FileMetadata>>((id, action) =>
            {
                var metadata = new FileMetadata();
                action(metadata);
                capturedMetadata = metadata;
            });

        var service = CreateSaveService();
        service.Save(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.Cid.Id).IsEqualTo(testCid.Id);
        await Assert.That(capturedMetadata.Name).IsEqualTo("my-file.pdf");
        await Assert.That(capturedMetadata.ContentType).IsEqualTo("application/pdf");
        await Assert.That(capturedMetadata.Length).IsEqualTo(request.Data.Length);
    }

    #endregion

    #region Tests - Early Exit: Content Already Present

    [Test]
    public async Task Handle_WithExistingContent_ReturnsExistingCid()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(true);

        var service = CreateSaveService();

        var result = service.Save(request);

        await Assert.That(result.Id).IsEqualTo(testCid.Id);
        mockObjectStoreService.Verify(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()), Times.Never);
        mockDataStoreService.Verify(ds => ds.WriteData(It.IsAny<IId>(), It.IsAny<byte[]>()), Times.Never);
        mockPresenceService.Verify(ps => ps.SetPresence(It.IsAny<IId>()), Times.Never);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(It.IsAny<long>()), Times.Never);
    }

    #endregion

    #region Tests - Error Path: Data Write Fails

    [Test]
    public async Task Handle_WhenWriteDataThrows_DeletesMetadataObject()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();
        var testException = new IOException("Disk write failed");

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockDataStoreService.Setup(ds => ds.WriteData(It.IsAny<IId>(), It.IsAny<byte[]>()))
            .Throws(testException);

        var service = CreateSaveService();

        Exception? thrownException = null;
        try
        {
            service.Save(request);
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException).IsOfType(typeof(IOException));
        mockObjectStoreService.Verify(os => os.DeleteObject(testCid), Times.Once);
    }

    [Test]
    public async Task Handle_WhenWriteDataThrows_DoesNotSetPresenceOrQuota()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockDataStoreService.Setup(ds => ds.WriteData(It.IsAny<IId>(), It.IsAny<byte[]>()))
            .Throws(new IOException("Write failed"));

        var service = CreateSaveService();

        try
        {
            service.Save(request);
        }
        catch
        {
            // Expected
        }

        mockPresenceService.Verify(ps => ps.SetPresence(It.IsAny<IId>()), Times.Never);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(It.IsAny<long>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenWriteDataThrows_RethrowsException()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();
        var testException = new UnauthorizedAccessException("Permission denied");

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockDataStoreService.Setup(ds => ds.WriteData(It.IsAny<IId>(), It.IsAny<byte[]>()))
            .Throws(testException);

        var service = CreateSaveService();

        Exception? thrownException = null;
        try
        {
            service.Save(request);
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException).IsOfType(typeof(UnauthorizedAccessException));
    }

    #endregion

    #region Tests - Data Size Variations

    [Test]
    public async Task Handle_WithLargeData_AddsCorrectBytesToQuota()
    {
        var largeData = new byte[10 * 1024 * 1024]; // 10 MB
        var request = CreateValidSaveRequest(data: largeData);
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        service.Save(request);

        mockQuotaService.Verify(qs => qs.AddUsedBytes(10 * 1024 * 1024), Times.Once);
    }

    [Test]
    public async Task Handle_WithSmallData_SavesCorrectly()
    {
        var smallData = new byte[1]; // 1 byte
        var request = CreateValidSaveRequest(data: smallData);
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        var result = service.Save(request);

        await Assert.That(result.Id).IsEqualTo(testCid.Id);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(1), Times.Once);
    }

    #endregion

    #region Tests - Method Call Order

    [Test]
    public async Task Handle_CallsHashServiceFirst()
    {
        var request = CreateValidSaveRequest();
        var testCid = CreateTestCid();
        var callOrder = new List<string>();

        mockHashService.Setup(hs => hs.GetHash(request.Data))
            .Callback(() => callOrder.Add("GetHash"))
            .Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid))
            .Callback(() => callOrder.Add("IsPresent"))
            .Returns(false);
        mockObjectStoreService.Setup(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()))
            .Callback(() => callOrder.Add("CreateOrUpdateObject"));
        mockDataStoreService.Setup(ds => ds.WriteData(It.IsAny<IId>(), It.IsAny<byte[]>()))
            .Callback(() => callOrder.Add("WriteData"));

        var service = CreateSaveService();

        service.Save(request);

        await Assert.That(callOrder[0]).IsEqualTo("GetHash");
        await Assert.That(callOrder[1]).IsEqualTo("IsPresent");
    }

    #endregion

    #region Tests - Empty Request Data

    [Test]
    public async Task Handle_WithEmptyData_StillSavesCorrectly()
    {
        var request = CreateValidSaveRequest(data: Array.Empty<byte>());
        var testCid = CreateTestCid("ct+empty");

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        var result = service.Save(request);

        await Assert.That(result.Id).IsEqualTo(testCid.Id);
        mockDataStoreService.Verify(ds => ds.WriteData(testCid, Array.Empty<byte>()), Times.Once);
        mockQuotaService.Verify(qs => qs.AddUsedBytes(0), Times.Once);
    }

    #endregion

    #region Tests - Custom File Names and Content Types

    [Test]
    public async Task Handle_WithCustomFileName_SavesCustomName()
    {
        var customName = "my-document-2026-06-05.docx";
        var request = CreateValidSaveRequest(name: customName);
        var testCid = CreateTestCid();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()))
            .Callback<IId, Action<FileMetadata>>((id, action) =>
            {
                var metadata = new FileMetadata();
                action(metadata);
                capturedMetadata = metadata;
            });

        var service = CreateSaveService();

        service.Save(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.Name).IsEqualTo(customName);
    }

    [Test]
    public async Task Handle_WithCustomContentType_SavesCustomContentType()
    {
        var customContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        var request = CreateValidSaveRequest(contentType: customContentType);
        var testCid = CreateTestCid();
        FileMetadata? capturedMetadata = null;

        mockHashService.Setup(hs => hs.GetHash(request.Data)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);
        mockObjectStoreService
            .Setup(os => os.CreateOrUpdateObject<FileMetadata>(It.IsAny<IId>(), It.IsAny<Action<FileMetadata>>()))
            .Callback<IId, Action<FileMetadata>>((id, action) =>
            {
                var metadata = new FileMetadata();
                action(metadata);
                capturedMetadata = metadata;
            });

        var service = CreateSaveService();

        service.Save(request);

        await Assert.That(capturedMetadata).IsNotNull();
        await Assert.That(capturedMetadata!.ContentType).IsEqualTo(customContentType);
    }

    #endregion

    #region Tests - Parameter Verification

    [Test]
    public async Task Handle_PassesCorrectDataToHashService()
    {
        var testData = Encoding.UTF8.GetBytes("specific test data");
        var request = CreateValidSaveRequest(data: testData);
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(testData)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        service.Save(request);

        mockHashService.Verify(hs => hs.GetHash(testData), Times.Once);
    }

    [Test]
    public async Task Handle_PassesCorrectDataToWriteData()
    {
        var testData = Encoding.UTF8.GetBytes("write data test");
        var request = CreateValidSaveRequest(data: testData);
        var testCid = CreateTestCid();

        mockHashService.Setup(hs => hs.GetHash(testData)).Returns(testCid);
        mockPresenceService.Setup(ps => ps.IsPresent(testCid)).Returns(false);

        var service = CreateSaveService();

        service.Save(request);

        mockDataStoreService.Verify(ds => ds.WriteData(testCid, testData), Times.Once);
    }

    #endregion
}
