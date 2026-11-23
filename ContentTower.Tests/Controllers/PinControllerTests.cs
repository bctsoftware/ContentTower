using ContentTower.Controllers;
using ContentTower.Services;
using Moq;

namespace ContentTower.Tests.Controllers;

public class PinControllerTests
{
    private readonly Mock<IPinService> mockPinService = new();

    private PinController CreateController() => new PinController(mockPinService.Object, Mock.Of<IPresenceService>());

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Delete_WhenPinDoesNotExist_SucceedsWithoutDeleting(bool force)
    {
        mockPinService.Setup(ps => ps.Exists(It.IsAny<PinId>())).Returns(false);
        var controller = CreateController();

        if (force) controller.DeleteForce("pGone");
        else controller.Delete("pGone");

        mockPinService.Verify(ps => ps.Get(It.IsAny<PinId>()), Times.Never);
        mockPinService.Verify(ps => ps.Delete(It.IsAny<PinId>()), Times.Never);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Delete_WhenPinExists_DeletesIt()
    {
        var pinId = new PinId("pPin1");
        mockPinService.Setup(ps => ps.Exists(pinId)).Returns(true);
        mockPinService.Setup(ps => ps.Get(pinId)).Returns(new PinData { PinId = pinId, StoreType = StoreType.Temporary });

        CreateController().Delete(pinId.Id);

        mockPinService.Verify(ps => ps.Delete(pinId), Times.Once);
        await Task.CompletedTask;
    }
}
