using ContentTower.Services;
using Moq;

namespace ContentTower.Tests.Services
{
    internal class LoadServiceTests
    {
        private readonly Mock<IObjectStoreService> mockObjectStoreService;
        private readonly Mock<IDataStoreService> mockDataStoreService;
        private readonly Cid cid = new Cid("a");
        private readonly LoadService loadService;

        public LoadServiceTests()
        {
            mockObjectStoreService = new Mock<IObjectStoreService>();
            mockDataStoreService = new Mock<IDataStoreService>();

            loadService = new LoadService(mockObjectStoreService.Object, mockDataStoreService.Object);
        }

        [Test]
        public async Task ReadMetadata_FSReadObject()
        {
            var expected = new FileMetadata();
            mockObjectStoreService.Setup(s => s.ReadObject<FileMetadata>(cid)).Returns(expected);

            var actual = loadService.ReadMetadata(cid);

            await Assert.That(actual).IsSameReferenceAs(expected);
        }

        [Test]
        public async Task ReadData_FSReadData()
        {
            var expected = new MemoryStream();
            mockDataStoreService.Setup(s => s.ReadData(cid)).Returns(expected);

            var actual = loadService.ReadData(cid);

            await Assert.That(actual).IsSameReferenceAs(expected);
        }
    }
}
