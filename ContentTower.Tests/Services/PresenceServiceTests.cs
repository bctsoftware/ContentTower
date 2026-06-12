using ContentTower.Services;
using ContentTower.System;
using Moq;
using TUnit.Assertions;
using TUnit.Core;

namespace ContentTower.Tests.Services;

public class PresenceServiceTests
{
    private readonly Mock<IFileSystem> mockFileSystem;

    public PresenceServiceTests()
    {
        mockFileSystem = new Mock<IFileSystem>();
    }

    #region Helper Methods

    private PresenceService CreatePresenceService()
    {
        return new PresenceService(mockFileSystem.Object);
    }

    private Cid CreateTestCid(string hash = "ct+test+cid")
    {
        return new Cid(hash);
    }

    private List<Cid> CreateMultipleTestCids(int count)
    {
        var cids = new List<Cid>();
        for (int i = 0; i < count; i++)
        {
            cids.Add(new Cid($"ct+test+cid+{i}"));
        }
        return cids;
    }

    #endregion

    #region Tests - IsPresent: Already in Cache

    [Test]
    public async Task IsPresent_WhenCidInExistsCache_ReturnsTrueWithoutCheckingFileSystem()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();
        
        service.SetPresence(cid);

        mockFileSystem.Reset();

        var result = service.IsPresent(cid);

        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    #endregion

    #region Tests - IsPresent: Check FileSystem

    [Test]
    public async Task IsPresent_WhenCidNotInCache_ChecksFileSystem()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        service.IsPresent(cid);

        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    [Test]
    public async Task IsPresent_WhenCidExistsOnFileSystem_ReturnsTrueAndCachesIt()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(true);
        var service = CreatePresenceService();

        var result = service.IsPresent(cid);

        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
        
        // Verify it's now cached by calling IsPresent again
        mockFileSystem.Reset();
        var secondResult = service.IsPresent(cid);
        await Assert.That(secondResult).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    [Test]
    public async Task IsPresent_WhenCidDoesNotExistOnFileSystem_ReturnsFalse()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        var result = service.IsPresent(cid);

        await Assert.That(result).IsFalse();
        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    [Test]
    public async Task IsPresent_WhenCidDoesNotExistOnFileSystem_DoesNotCacheIt()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        service.IsPresent(cid);

        mockFileSystem.Reset();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        service.IsPresent(cid);

        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    #endregion

    #region Tests - IsPresent: Multiple Calls

    [Test]
    public async Task IsPresent_WithMultipleDifferentCids_ChecksFileSystemForEach()
    {
        var cid1 = CreateTestCid("ct+hash+1");
        var cid2 = CreateTestCid("ct+hash+2");
        mockFileSystem.Setup(fs => fs.Exists(It.IsAny<Cid>())).Returns(false);
        var service = CreatePresenceService();

        service.IsPresent(cid1);
        service.IsPresent(cid2);

        mockFileSystem.Verify(fs => fs.Exists(cid1), Times.Once);
        mockFileSystem.Verify(fs => fs.Exists(cid2), Times.Once);
    }

    [Test]
    public async Task IsPresent_WithSameCidMultipleTimes_ChecksFileSystemOnlyOnce()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(true);
        var service = CreatePresenceService();

        service.IsPresent(cid);
        service.IsPresent(cid);
        service.IsPresent(cid);

        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    #endregion

    #region Tests - SetPresence

    [Test]
    public async Task SetPresence_AddsCidToExistsCache()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        service.SetPresence(cid);

        mockFileSystem.Reset();
        var result = service.IsPresent(cid);
        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    [Test]
    public async Task SetPresence_RemovesFromDoesntExistCache()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        // First clear presence to add to doesntExist cache
        service.ClearPresence(cid);
        
        // Verify it reports as not present
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        await Assert.That(service.IsPresent(cid)).IsFalse();

        service.SetPresence(cid);

        mockFileSystem.Reset();
        var result = service.IsPresent(cid);
        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    [Test]
    public async Task SetPresence_MultipleInvocations_AllSucceed()
    {
        var cid1 = CreateTestCid("ct+hash+1");
        var cid2 = CreateTestCid("ct+hash+2");
        var cid3 = CreateTestCid("ct+hash+3");
        var service = CreatePresenceService();

        service.SetPresence(cid1);
        service.SetPresence(cid2);
        service.SetPresence(cid3);

        mockFileSystem.Reset();
        await Assert.That(service.IsPresent(cid1)).IsTrue();
        await Assert.That(service.IsPresent(cid2)).IsTrue();
        await Assert.That(service.IsPresent(cid3)).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    [Test]
    public async Task SetPresence_SameCidMultipleTimes_StaysInCache()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        service.SetPresence(cid);
        service.SetPresence(cid);
        service.SetPresence(cid);

        mockFileSystem.Reset();
        var result = service.IsPresent(cid);
        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    #endregion

    #region Tests - ClearPresence

    [Test]
    public async Task ClearPresence_RemovesCidFromExistsCache()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        service.SetPresence(cid);
        
        service.ClearPresence(cid);

        mockFileSystem.Reset();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        service.IsPresent(cid);
        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    [Test]
    public async Task ClearPresence_AddsCidToDoesntExistCache()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        service.SetPresence(cid);
        service.ClearPresence(cid);

        var result = service.IsPresent(cid);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ClearPresence_MultipleInvocations_AllSucceed()
    {
        var cid1 = CreateTestCid("ct+hash+1");
        var cid2 = CreateTestCid("ct+hash+2");
        var service = CreatePresenceService();

        service.SetPresence(cid1);
        service.SetPresence(cid2);

        service.ClearPresence(cid1);
        service.ClearPresence(cid2);

        await Assert.That(service.IsPresent(cid1)).IsFalse();
        await Assert.That(service.IsPresent(cid2)).IsFalse();
    }

    [Test]
    public async Task ClearPresence_SameCidMultipleTimes_StaysCleared()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        service.SetPresence(cid);
        service.ClearPresence(cid);
        service.ClearPresence(cid);

        var result = service.IsPresent(cid);
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Tests - Cache Management: Exists Cache Overflow

    [Test]
    public async Task ExistsCache_WhenCountExceeds100000_Clears()
    {
        var service = CreatePresenceService();
        var cids = CreateMultipleTestCids(100001);
        mockFileSystem.Setup(fs => fs.Exists(It.IsAny<Cid>())).Returns(false);

        foreach (var cid in cids)
        {
            service.SetPresence(cid);
        }

        mockFileSystem.Reset();
        mockFileSystem.Setup(fs => fs.Exists(cids[0])).Returns(false);
        service.IsPresent(cids[0]);
        
        // FileSystem should be checked because cache was cleared
        mockFileSystem.Verify(fs => fs.Exists(cids[0]), Times.Once);
    }

    [Test]
    public async Task ExistsCache_WhenCountIs100000_DoesNotClear()
    {
        var service = CreatePresenceService();
        var cids = CreateMultipleTestCids(100000);
        mockFileSystem.Setup(fs => fs.Exists(It.IsAny<Cid>())).Returns(false);

        foreach (var cid in cids)
        {
            service.SetPresence(cid);
        }

        mockFileSystem.Reset();
        service.IsPresent(cids[0]);
        
        // FileSystem should NOT be checked because cache is still valid
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    #endregion

    #region Tests - Complex Scenarios

    [Test]
    public async Task SetThenClearThenSetAgain_LastSetWins()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        service.SetPresence(cid);
        service.ClearPresence(cid);
        service.SetPresence(cid);

        mockFileSystem.Reset();
        var result = service.IsPresent(cid);
        await Assert.That(result).IsTrue();
        mockFileSystem.Verify(fs => fs.Exists(It.IsAny<Cid>()), Times.Never);
    }

    [Test]
    public async Task ClearThenSetThenClearAgain_LastClearWins()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        service.ClearPresence(cid);
        service.SetPresence(cid);
        service.ClearPresence(cid);

        var result = service.IsPresent(cid);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MixedOperationsOnDifferentCids()
    {
        var cid1 = CreateTestCid("ct+hash+1");
        var cid2 = CreateTestCid("ct+hash+2");
        var cid3 = CreateTestCid("ct+hash+3");
        var service = CreatePresenceService();

        service.SetPresence(cid1);
        service.ClearPresence(cid2);
        service.SetPresence(cid3);

        await Assert.That(service.IsPresent(cid1)).IsTrue();
        await Assert.That(service.IsPresent(cid2)).IsFalse();
        await Assert.That(service.IsPresent(cid3)).IsTrue();
    }

    #endregion

    #region Tests - FileSystem Exists Parameter Verification

    [Test]
    public async Task IsPresent_PassesCorrectCidToFileSystemExists()
    {
        var cid = CreateTestCid("ct+specific+hash");
        mockFileSystem.Setup(fs => fs.Exists(It.IsAny<Cid>())).Returns(false);
        var service = CreatePresenceService();

        service.IsPresent(cid);

        mockFileSystem.Verify(fs => fs.Exists(It.Is<Cid>(c => c.Id == cid.Id)), Times.Once);
    }

    [Test]
    public async Task IsPresent_WithDifferentCids_PassesEachCorrectly()
    {
        var cid1 = CreateTestCid("ct+hash+1");
        var cid2 = CreateTestCid("ct+hash+2");
        mockFileSystem.Setup(fs => fs.Exists(It.IsAny<Cid>())).Returns(false);
        var service = CreatePresenceService();

        service.IsPresent(cid1);
        service.IsPresent(cid2);

        mockFileSystem.Verify(fs => fs.Exists(It.Is<Cid>(c => c.Id == cid1.Id)), Times.Once);
        mockFileSystem.Verify(fs => fs.Exists(It.Is<Cid>(c => c.Id == cid2.Id)), Times.Once);
    }

    #endregion

    #region Tests - Return Value Verification

    [Test]
    public async Task IsPresent_ConsistentReturnValues()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(true);
        var service = CreatePresenceService();

        var firstCall = service.IsPresent(cid);
        var secondCall = service.IsPresent(cid);
        var thirdCall = service.IsPresent(cid);

        await Assert.That(firstCall).IsTrue();
        await Assert.That(secondCall).IsTrue();
        await Assert.That(thirdCall).IsTrue();
    }

    [Test]
    public async Task IsPresent_ReturnsCorrectBooleanValues()
    {
        var presentCid = CreateTestCid("ct+exists");
        var notPresentCid = CreateTestCid("ct+doesnt+exist");
        mockFileSystem.Setup(fs => fs.Exists(presentCid)).Returns(true);
        mockFileSystem.Setup(fs => fs.Exists(notPresentCid)).Returns(false);
        var service = CreatePresenceService();

        var presentResult = service.IsPresent(presentCid);
        var notPresentResult = service.IsPresent(notPresentCid);

        await Assert.That(presentResult).IsTrue();
        await Assert.That(notPresentResult).IsFalse();
    }

    #endregion

    #region Tests - SetPresence/ClearPresence Return Void

    [Test]
    public async Task SetPresence_HasNoReturnValue()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        try
        {
            service.SetPresence(cid);
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    [Test]
    public async Task ClearPresence_HasNoReturnValue()
    {
        var cid = CreateTestCid();
        var service = CreatePresenceService();

        try
        {
            service.ClearPresence(cid);
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - Edge Cases with Empty Cid Hash

    [Test]
    public async Task IsPresent_WithEmptyCidHash_Works()
    {
        var cid = CreateTestCid("");
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service = CreatePresenceService();

        var result = service.IsPresent(cid);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SetPresence_WithEmptyCidHash_Works()
    {
        var cid = CreateTestCid("");
        var service = CreatePresenceService();

        service.SetPresence(cid);

        mockFileSystem.Reset();
        var result = service.IsPresent(cid);
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Tests - Multiple Service Instances

    [Test]
    public async Task MultipleServiceInstances_HaveSeparateCaches()
    {
        var cid = CreateTestCid();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        var service1 = CreatePresenceService();
        var service2 = CreatePresenceService();

        service1.SetPresence(cid);

        mockFileSystem.Reset();
        mockFileSystem.Setup(fs => fs.Exists(cid)).Returns(false);
        service2.IsPresent(cid);
        mockFileSystem.Verify(fs => fs.Exists(cid), Times.Once);
    }

    #endregion
}
