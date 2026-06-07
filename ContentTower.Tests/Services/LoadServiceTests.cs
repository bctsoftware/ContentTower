using ContentTower.Services;
using ContentTower.System;
using Moq;

namespace ContentTower.Tests.Services
{
    internal class LoadServiceTests
    {
        private readonly Mock<IFileSystem> mockFileSystem;
        private readonly Cid cid = new Cid("a");
        private readonly LoadService loadService;

        public LoadServiceTests()
        {
            mockFileSystem = new Mock<IFileSystem>();

            loadService = new LoadService(mockFileSystem.Object);
        }

        [Test]
        public async Task ReadMetadata_FSReadObject()
        {
            var expected = new FileMetadata();
            mockFileSystem.Setup(s => s.ReadObject<FileMetadata>(cid)).ReturnsAsync(expected);

            var actual = await loadService.ReadMetadata(cid);

            await Assert.That(actual).IsSameReferenceAs(expected);
        }

        [Test]
        public async Task ReadData_FSReadData()
        {
            var expected = new MemoryStream();
            mockFileSystem.Setup(s => s.ReadData(cid)).ReturnsAsync(expected);

            var actual = await loadService.ReadData(cid);

            await Assert.That(actual).IsSameReferenceAs(expected);
        }
    }
}
