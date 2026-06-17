using ContentTower.Services.CleanupWorkers;
using ContentTower.System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services.CleanupWorkers;

public class DatafileCleanupWorkerTests
{
    private readonly Mock<ILogger<DatafileCleanupWorker>> mockLogger;
    private readonly Mock<IFileSystem> mockFs;
    private readonly Mock<ITime> mockTimeService;
    private const string DataPath = "/data";

    public DatafileCleanupWorkerTests()
    {
        mockLogger = new Mock<ILogger<DatafileCleanupWorker>>();
        mockFs = new Mock<IFileSystem>();
        mockTimeService = new Mock<ITime>();
        mockTimeService.Setup(t => t.Sleep(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #region Helpers

    private DatafileCleanupWorker CreateWorker() => new DatafileCleanupWorker(
        mockLogger.Object,
        mockFs.Object,
        Options.Create(new StorageOptions { DataPath = DataPath }),
        mockTimeService.Object
    );

    private void SetupFiles(params string[] files)
    {
        mockFs.Setup(fs => fs.DirectoryGetFiles(DataPath)).Returns(files);
    }

    private static string DataFile(string name) => $"{DataPath}/{name}.data";
    private static string JsonFile(string name) => $"{DataPath}/{name}.json";

    #endregion

    #region Step

    [Test]
    public async Task Step_WithNoFiles_DoesNotDeleteAnything()
    {
        SetupFiles();

        CreateWorker().Step(CancellationToken.None);

        mockFs.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WhenCancellationRequestedBeforeProcessingFile_Throws()
    {
        SetupFiles(DataFile("ctFile1"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? thrown = null;
        try { CreateWorker().Step(cts.Token); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(OperationCanceledException));
    }

    [Test]
    public async Task Step_WithNonDataFile_DoesNotDeleteIt()
    {
        SetupFiles($"{DataPath}/ctFile1.json");

        CreateWorker().Step(CancellationToken.None);

        mockFs.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithDataFileAndMatchingMetadata_DoesNotDeleteIt()
    {
        var dataFile = DataFile("ctFile1");
        var jsonFile = JsonFile("ctFile1");
        SetupFiles(dataFile);
        mockFs.Setup(fs => fs.Exists(jsonFile)).Returns(true);

        CreateWorker().Step(CancellationToken.None);

        mockFs.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithDataFileAndNoMatchingMetadata_DeletesDataFile()
    {
        var dataFile = DataFile("ctFile1");
        var jsonFile = JsonFile("ctFile1");
        SetupFiles(dataFile);
        mockFs.Setup(fs => fs.Exists(jsonFile)).Returns(false);

        CreateWorker().Step(CancellationToken.None);

        mockFs.Verify(fs => fs.DeleteFile(dataFile), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithDataFileAndNoMatchingMetadata_ChecksCorrectJsonPath()
    {
        var dataFile = DataFile("ctFile1");
        var jsonFile = JsonFile("ctFile1");
        SetupFiles(dataFile);
        mockFs.Setup(fs => fs.Exists(jsonFile)).Returns(false);

        CreateWorker().Step(CancellationToken.None);

        mockFs.Verify(fs => fs.Exists(jsonFile), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WhenDeleteThrows_ExceptionIsCaughtAndNotRethrown()
    {
        var dataFile = DataFile("ctFile1");
        var jsonFile = JsonFile("ctFile1");
        SetupFiles(dataFile);
        mockFs.Setup(fs => fs.Exists(jsonFile)).Returns(false);
        mockFs.Setup(fs => fs.DeleteFile(dataFile)).Throws(new IOException("Access denied"));

        Exception? thrown = null;
        try { CreateWorker().Step(CancellationToken.None); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNull();
    }

    [Test]
    public async Task Step_WhenDeleteThrows_LogsError()
    {
        var dataFile = DataFile("ctFile1");
        var jsonFile = JsonFile("ctFile1");
        SetupFiles(dataFile);
        mockFs.Setup(fs => fs.Exists(jsonFile)).Returns(false);
        mockFs.Setup(fs => fs.DeleteFile(dataFile)).Throws(new IOException("Access denied"));

        CreateWorker().Step(CancellationToken.None);

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<IOException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_SleepsAfterEachFile()
    {
        SetupFiles(DataFile("ctFile1"), DataFile("ctFile2"));
        mockFs.Setup(fs => fs.Exists(It.IsAny<string>())).Returns(true);

        CreateWorker().Step(CancellationToken.None);

        mockTimeService.Verify(
            t => t.Sleep(TimeSpan.FromMilliseconds(100), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        await Task.CompletedTask;
    }

    #endregion
}

