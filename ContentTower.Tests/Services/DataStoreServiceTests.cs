using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Options;
using Moq;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class DataStoreServiceTests
{
    private readonly Mock<IFileSystem> mockFs;
    private const string DataPath = "/data";

    public DataStoreServiceTests()
    {
        mockFs = new Mock<IFileSystem>();
    }

    #region Helpers

    private DataStoreService CreateService() => new DataStoreService(
        mockFs.Object,
        Options.Create(new StorageOptions { DataPath = DataPath })
    );

    private static Cid MakeCid(string id = "ctTestId") => new Cid(id);
    private static string FilePath(IId id) => Path.Combine(DataPath, id.Id + ".data");

    #endregion

    #region Exists

    [Test]
    public async Task Exists_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.Exists(new Cid("")); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task Exists_WhenFileExists_ReturnsTrue()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(true);

        await Assert.That(CreateService().Exists(cid)).IsTrue();
    }

    [Test]
    public async Task Exists_WhenFileDoesNotExist_ReturnsFalse()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(false);

        await Assert.That(CreateService().Exists(cid)).IsFalse();
    }

    #endregion

    #region WriteData

    [Test]
    public async Task WriteData_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.WriteData(new Cid(""), Array.Empty<byte>()); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task WriteData_CallsWriteAllBytesWithCorrectPathAndData()
    {
        var cid = MakeCid();
        var data = new byte[] { 1, 2, 3 };

        CreateService().WriteData(cid, data);

        mockFs.Verify(fs => fs.WriteAllBytes(FilePath(cid), data), Times.Once);
        await Task.CompletedTask;
    }

    #endregion

    #region ReadData

    [Test]
    public async Task ReadData_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.ReadData(new Cid("")); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task ReadData_ReturnsStreamFromOpenRead()
    {
        var cid = MakeCid();
        var expectedStream = new MemoryStream();
        mockFs.Setup(fs => fs.OpenRead(FilePath(cid))).Returns(expectedStream);

        var result = CreateService().ReadData(cid);

        await Assert.That(result).IsEqualTo(expectedStream);
    }

    #endregion

    #region DeleteData

    [Test]
    public async Task DeleteData_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.DeleteData(new Cid("")); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task DeleteData_CallsDeleteFileWithCorrectPath()
    {
        var cid = MakeCid();

        CreateService().DeleteData(cid);

        mockFs.Verify(fs => fs.DeleteFile(FilePath(cid)), Times.Once);
        await Task.CompletedTask;
    }

    #endregion
}
