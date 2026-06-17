using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class ObjectStoreServiceTests : IDisposable
{
    private readonly Mock<ILogger<ObjectStoreService>> mockLogger;
    private readonly Mock<IFileSystem> mockFs;
    private readonly string dataPath;

    public ObjectStoreServiceTests()
    {
        mockLogger = new Mock<ILogger<ObjectStoreService>>();
        mockFs = new Mock<IFileSystem>();
        dataPath = Path.Combine(Path.GetTempPath(), "ObjectStoreServiceTests_" + Guid.NewGuid());
        Directory.CreateDirectory(dataPath);
    }

    public void Dispose()
    {
        Directory.Delete(dataPath, recursive: true);
    }

    #region Helpers

    private ObjectStoreService CreateService() => new ObjectStoreService(
        mockLogger.Object,
        mockFs.Object,
        Options.Create(new StorageOptions { DataPath = dataPath })
    );

    private static Cid MakeCid(string id = "ctTestId") => new Cid(id);

    private string FilePath(IId id) => Path.Combine(dataPath, id.Id + ".json");

    private void WriteJsonFile<T>(IId id, T obj)
    {
        File.WriteAllText(FilePath(id), JsonConvert.SerializeObject(obj));
    }

    private class TestStorable : IStorable
    {
        public string Value { get; set; } = string.Empty;
        public bool Valid() => !string.IsNullOrEmpty(Value);
    }

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

        var service = CreateService();

        await Assert.That(service.Exists(cid)).IsTrue();
    }

    [Test]
    public async Task Exists_WhenFileDoesNotExist_ReturnsFalse()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(false);

        var service = CreateService();

        await Assert.That(service.Exists(cid)).IsFalse();
    }

    #endregion

    #region DeleteObject

    [Test]
    public async Task DeleteObject_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.DeleteObject(new Cid("")); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task DeleteObject_DeletesCorrectFile()
    {
        var cid = MakeCid();
        var service = CreateService();

        service.DeleteObject(cid);

        mockFs.Verify(fs => fs.DeleteFile(FilePath(cid)), Times.Once);
        await Task.CompletedTask;
    }

    #endregion

    #region ReadObject

    [Test]
    public async Task ReadObject_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.ReadObject<TestStorable>(new Cid("")); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task ReadObject_WhenFileContainsValidJson_ReturnsDeserializedObject()
    {
        var cid = MakeCid();
        WriteJsonFile(cid, new TestStorable { Value = "hello" });

        var service = CreateService();
        var result = service.ReadObject<TestStorable>(cid);

        await Assert.That(result.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task ReadObject_WhenDeserializationReturnsNull_Throws()
    {
        var cid = MakeCid();
        File.WriteAllText(FilePath(cid), "null");

        var service = CreateService();

        Exception? thrown = null;
        try { service.ReadObject<TestStorable>(cid); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    #endregion

    #region CreateOrUpdateObject

    [Test]
    public async Task CreateOrUpdateObject_WithEmptyId_Throws()
    {
        var service = CreateService();

        Exception? thrown = null;
        try { service.CreateOrUpdateObject<TestStorable>(new Cid(""), _ => { }); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task CreateOrUpdateObject_WhenObjectDoesNotExist_CreatesNewAndWritesJson()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(false);
        string? writtenJson = null;
        mockFs.Setup(fs => fs.WriteAllText(FilePath(cid), It.IsAny<string>()))
            .Callback<string, string>((_, text) => writtenJson = text);

        var service = CreateService();
        service.CreateOrUpdateObject<TestStorable>(cid, obj => obj.Value = "new");

        await Assert.That(writtenJson).IsNotNull();
        var written = JsonConvert.DeserializeObject<TestStorable>(writtenJson!);
        await Assert.That(written!.Value).IsEqualTo("new");
    }

    [Test]
    public async Task CreateOrUpdateObject_WhenObjectDoesNotExist_AndValidationFails_Throws()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(false);

        var service = CreateService();

        Exception? thrown = null;
        // leave Value empty so Valid() returns false
        try { service.CreateOrUpdateObject<TestStorable>(cid, _ => { }); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task CreateOrUpdateObject_WhenValidationFails_DoesNotWriteFile()
    {
        var cid = MakeCid();
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(false);

        var service = CreateService();
        try { service.CreateOrUpdateObject<TestStorable>(cid, _ => { }); } catch { }

        mockFs.Verify(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task CreateOrUpdateObject_WhenObjectExists_LoadsExistingValueBeforeApplyingUpdate()
    {
        var cid = MakeCid();
        WriteJsonFile(cid, new TestStorable { Value = "original" });
        mockFs.Setup(fs => fs.Exists(FilePath(cid))).Returns(true);
        string? writtenJson = null;
        mockFs.Setup(fs => fs.WriteAllText(FilePath(cid), It.IsAny<string>()))
            .Callback<string, string>((_, text) => writtenJson = text);

        var service = CreateService();
        // The callback receives the existing object and can see its current value
        string? valueSeenByCallback = null;
        service.CreateOrUpdateObject<TestStorable>(cid, obj =>
        {
            valueSeenByCallback = obj.Value;
            obj.Value = "updated";
        });

        await Assert.That(valueSeenByCallback).IsEqualTo("original");
        var written = JsonConvert.DeserializeObject<TestStorable>(writtenJson!);
        await Assert.That(written!.Value).IsEqualTo("updated");
    }

    #endregion

    #region IterateObjects

    [Test]
    public async Task IterateObjects_WithNoFiles_DoesNotCallAction()
    {
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath)).Returns(Array.Empty<string>());

        var service = CreateService();
        var callCount = 0;
        service.IterateObjects<TestStorable>("p", _ => callCount++);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task IterateObjects_WithMatchingFile_CallsActionWithDeserializedObject()
    {
        var fullPath = Path.Combine(dataPath, "pAbc.json");
        File.WriteAllText(fullPath, JsonConvert.SerializeObject(new TestStorable { Value = "found" }));
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath)).Returns(new[] { fullPath });

        var service = CreateService();
        TestStorable? captured = null;
        service.IterateObjects<TestStorable>("p", obj => captured = obj);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value).IsEqualTo("found");
    }

    [Test]
    public async Task IterateObjects_WithNonMatchingPrefix_DoesNotCallAction()
    {
        var fullPath = Path.Combine(dataPath, "xAbc.json");
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath)).Returns(new[] { fullPath });

        var service = CreateService();
        var callCount = 0;
        service.IterateObjects<TestStorable>("p", _ => callCount++);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task IterateObjects_WithNonJsonExtension_DoesNotCallAction()
    {
        var fullPath = Path.Combine(dataPath, "pAbc.data");
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath)).Returns(new[] { fullPath });

        var service = CreateService();
        var callCount = 0;
        service.IterateObjects<TestStorable>("p", _ => callCount++);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task IterateObjects_WithInvalidJson_DoesNotCallAction()
    {
        var fullPath = Path.Combine(dataPath, "pAbc.json");
        File.WriteAllText(fullPath, "not valid json {{");
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath)).Returns(new[] { fullPath });

        var service = CreateService();
        var callCount = 0;
        service.IterateObjects<TestStorable>("p", _ => callCount++);

        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    public async Task IterateObjects_WithMultipleFiles_OnlyCallsActionForMatchingFiles()
    {
        var matching = Path.Combine(dataPath, "pMatch.json");
        var wrongPrefix = Path.Combine(dataPath, "xOther.json");
        var wrongExt = Path.Combine(dataPath, "pNoJson.data");
        File.WriteAllText(matching, JsonConvert.SerializeObject(new TestStorable { Value = "yes" }));
        mockFs.Setup(fs => fs.DirectoryGetFiles(dataPath))
            .Returns(new[] { matching, wrongPrefix, wrongExt });

        var service = CreateService();
        var callCount = 0;
        service.IterateObjects<TestStorable>("p", _ => callCount++);

        await Assert.That(callCount).IsEqualTo(1);
    }

    #endregion
}
