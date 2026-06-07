using Microsoft.Extensions.Logging;
using Moq;

namespace ContentTower.Tests
{
    public static class MockLogger
    {
        public static void AssertLogged<T>(this Mock<ILogger<T>> mockLogger, LogLevel expectedLevel, string expectedContains)
        {
            mockLogger.Verify(l => l.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
                Times.Once);
        }
    }
}
