using ContentTower.Services;
using ContentTower.Services.CleanupWorkers;
using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests.Services.CleanupWorkers;

public class ContentCleanupWorkerTests
{
    private readonly Mock<ILogger<ContentCleanupWorker>> mockLogger;
    private readonly Mock<IObjectStoreService> mockObjectStoreService;
    private readonly Mock<IDeleteService> mockDeleteService;

    public ContentCleanupWorkerTests()
    {
        mockLogger = new Mock<ILogger<ContentCleanupWorker>>();
        mockObjectStoreService = new Mock<IObjectStoreService>();
        mockDeleteService = new Mock<IDeleteService>();
    }

    #region Helpers

    private ContentCleanupWorker CreateWorker() => new ContentCleanupWorker(
        mockLogger.Object,
        mockObjectStoreService.Object,
        mockDeleteService.Object
    );

    private void SetupFiles(params FileMetadata[] files)
    {
        mockObjectStoreService
            .Setup(os => os.IterateObjects<FileMetadata>(HashService.CidPrefix, It.IsAny<Action<FileMetadata>>()))
            .Callback<string, Action<FileMetadata>>((_, onObject) =>
            {
                foreach (var f in files) onObject(f);
            });
    }

    private static Cid MakeCid(string id = "ctCid1") => new Cid(id);

    private static FileMetadata MakeFile(Cid cid, params PinId[] pinIds) =>
        new FileMetadata { Cid = cid, PinIds = pinIds.ToList() };

    #endregion

    #region Step — no files

    [Test]
    public async Task Step_WithNoFiles_DoesNotDeleteAnything()
    {
        SetupFiles();

        CreateWorker().Step(CancellationToken.None);

        mockDeleteService.Verify(ds => ds.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
        await Task.CompletedTask;
    }

    #endregion

    #region Step — cancellation

    [Test]
    public async Task Step_WhenCancelledBeforeFirstFile_Throws()
    {
        SetupFiles(MakeFile(MakeCid()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? thrown = null;
        try { CreateWorker().Step(cts.Token); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(OperationCanceledException));
    }

    #endregion

    #region Step — MarkIfUnpinned

    [Test]
    public async Task Step_WithUnpinnedFile_FirstStep_MarksButDoesNotDelete()
    {
        SetupFiles(MakeFile(MakeCid()));

        CreateWorker().Step(CancellationToken.None);

        mockDeleteService.Verify(ds => ds.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WithPinnedFile_FirstStep_DoesNotMarkOrDelete()
    {
        SetupFiles(MakeFile(MakeCid(), new PinId("pPin1")));
        var worker = CreateWorker();

        // First step
        worker.Step(CancellationToken.None);
        // Second step — if it had been marked, it would be deleted on second pass
        worker.Step(CancellationToken.None);

        mockDeleteService.Verify(ds => ds.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
        await Task.CompletedTask;
    }

    #endregion

    #region Step — DeleteIfMarkedAndUnpinned

    [Test]
    public async Task Step_WithUnpinnedFile_SecondStep_DeletesFile()
    {
        var file = MakeFile(MakeCid());
        SetupFiles(file);
        var worker = CreateWorker();

        worker.Step(CancellationToken.None); // marks
        worker.Step(CancellationToken.None); // deletes

        mockDeleteService.Verify(ds => ds.DeleteFile(file), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WhenFileIsPinnedBySecondStep_DoesNotDelete()
    {
        var cid = MakeCid();
        // First step: unpinned → marked
        SetupFiles(MakeFile(cid));
        var worker = CreateWorker();
        worker.Step(CancellationToken.None);

        // Second step: now pinned → should not delete
        SetupFiles(MakeFile(cid, new PinId("pPin1")));
        worker.Step(CancellationToken.None);

        mockDeleteService.Verify(ds => ds.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_WhenFileIsPinnedBySecondStep_RemovesFromMarkedList()
    {
        // If the file remains in the marked list it would be deleted on the third step
        // even without being seen as unpinned again. Verify it is removed.
        var cid = MakeCid();
        SetupFiles(MakeFile(cid));
        var worker = CreateWorker();
        worker.Step(CancellationToken.None); // marks

        SetupFiles(MakeFile(cid, new PinId("pPin1")));
        worker.Step(CancellationToken.None); // cancels delete, removes from list

        SetupFiles(MakeFile(cid, new PinId("pPin1")));
        worker.Step(CancellationToken.None); // would delete if still marked

        mockDeleteService.Verify(ds => ds.DeleteFile(It.IsAny<FileMetadata>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Step_DeletedFileIsSkippedForMarkOnSamePass()
    {
        // DeleteIfMarkedAndUnpinned returns true → MarkIfUnpinned is skipped via continue.
        // Verify the file is not re-added to the marked list after deletion.
        var cid = MakeCid();
        var file = MakeFile(cid);
        SetupFiles(file);
        var worker = CreateWorker();

        worker.Step(CancellationToken.None); // marks
        worker.Step(CancellationToken.None); // deletes (continue skips re-mark)
        worker.Step(CancellationToken.None); // would delete a second time if re-marked

        mockDeleteService.Verify(ds => ds.DeleteFile(file), Times.Once);
        await Task.CompletedTask;
    }

    #endregion
}

