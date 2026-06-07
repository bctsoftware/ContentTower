using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Options;
using Moq;

namespace ContentTower.Tests.Services;

public class ValidationServiceTests
{
    private readonly Mock<IFileSystem> mockFileSystem;
    private readonly Mock<IOptions<StorageOptions>> mockOptions;

    public ValidationServiceTests()
    {
        mockFileSystem = new Mock<IFileSystem>();
        mockOptions = new Mock<IOptions<StorageOptions>>();
    }

    #region Helper Methods

    private ValidationService CreateValidationService(StorageOptions? options = null)
    {
        var optionsToUse = options ?? CreateValidStorageOptions();
        mockOptions.Setup(o => o.Value).Returns(optionsToUse);
        return new ValidationService(mockOptions.Object, mockFileSystem.Object);
    }

    private StorageOptions CreateValidStorageOptions()
    {
        return new StorageOptions
        {
            DataPath = "/valid/data/path",
            Quota = 1048576 * 100, // 100 MB
            CleanupIntervalSeconds = 600,
            StoreDurationDefaultNominalSeconds = 86400, // 1 day
            StoreDurationDefaultPressureSeconds = 43200, // 12 hours
            StoreDurationTemporaryNominalSeconds = 7200, // 2 hours
            StoreDurationTemporaryPressureSeconds = 3600 // 1 hour (minimum allowed)
        };
    }

    #endregion

    #region Tests - Happy Path

    [Test]
    public async Task ValidateOptions_WithValidConfiguration_DoesNotThrow()
    {
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService();

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - DataPath Validation

    [Test]
    public async Task ValidateOptions_WithEmptyDataPath_ThrowsExceptionWithDataPathFault()
    {
        var options = CreateValidStorageOptions();
        options.DataPath = string.Empty;
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("DataPath not provided");
    }

    [Test]
    public async Task ValidateOptions_WithNullDataPath_ThrowsExceptionWithDataPathFault()
    {
        var options = CreateValidStorageOptions();
        options.DataPath = null!;
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("DataPath not provided");
    }

    [Test]
    public async Task ValidateOptions_WhenCheckCreateDirReturnsFalse_ThrowsExceptionWithDirCreationFault()
    {
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(false);
        var service = CreateValidationService();

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("Unable to create DataPath");
    }

    #endregion

    #region Tests - Quota Validation

    [Test]
    public async Task ValidateOptions_WithQuotaLessThanMinimum_ThrowsExceptionWithQuotaFault()
    {
        var options = CreateValidStorageOptions();
        options.Quota = 1048575; // 1 byte less than minimum
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("Quota must be at least 1 MB (1048576)");
    }

    [Test]
    public async Task ValidateOptions_WithQuotaEqualToMinimum_PassesValidation()
    {
        var options = CreateValidStorageOptions();
        options.Quota = 1048576; // Exactly 1 MB
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - StoreDurationDefaultNominal Validation

    [Test]
    public async Task ValidateOptions_WithDefaultNominalTooShort_ThrowsExceptionWithDefaultNominalFault()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationDefaultNominalSeconds = 3599; // 1 second less than minimum (1 hour = 3600)
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationDefaultNominal));
    }

    [Test]
    public async Task ValidateOptions_WithDefaultNominalEqualToMinimum_PassesValidation()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationDefaultNominalSeconds = 3600; // Exactly 1 hour
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - StoreDurationDefaultPressure Validation

    [Test]
    public async Task ValidateOptions_WithDefaultPressureTooShort_ThrowsExceptionWithDefaultPressureFault()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationDefaultPressureSeconds = 3599; // 1 second less than minimum
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationDefaultPressure));
    }

    [Test]
    public async Task ValidateOptions_WithDefaultPressureEqualToMinimum_PassesValidation()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationDefaultPressureSeconds = 3600; // Exactly 1 hour
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - StoreDurationTemporaryNominal Validation

    [Test]
    public async Task ValidateOptions_WithTemporaryNominalTooShort_ThrowsExceptionWithTemporaryNominalFault()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationTemporaryNominalSeconds = 3599; // 1 second less than minimum
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationTemporaryNominal));
    }

    [Test]
    public async Task ValidateOptions_WithTemporaryNominalEqualToMinimum_PassesValidation()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationTemporaryNominalSeconds = 3600; // Exactly 1 hour
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - StoreDurationTemporaryPressure Validation

    [Test]
    public async Task ValidateOptions_WithTemporaryPressureTooShort_ThrowsExceptionWithTemporaryPressureFault()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationTemporaryPressureSeconds = 3599; // 1 second less than minimum
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationTemporaryPressure));
    }

    [Test]
    public async Task ValidateOptions_WithTemporaryPressureEqualToMinimum_PassesValidation()
    {
        var options = CreateValidStorageOptions();
        options.StoreDurationTemporaryPressureSeconds = 3600; // Exactly 1 hour
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            await Assert.That(ex).IsNull();
        }
    }

    #endregion

    #region Tests - Multiple Faults

    [Test]
    public async Task ValidateOptions_WithMultipleFaults_ThrowsExceptionWithAllFaultMessages()
    {
        var options = CreateValidStorageOptions();
        options.DataPath = string.Empty;
        options.Quota = 1000; // Too low
        options.StoreDurationDefaultNominalSeconds = 1000; // Too low
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("DataPath not provided");
        await Assert.That(thrownException!.Message).Contains("Quota must be at least 1 MB");
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationDefaultNominal));
    }

    [Test]
    public async Task ValidateOptions_WithAllPossibleFaults_ThrowsExceptionWithAllFaultMessages()
    {
        var options = CreateValidStorageOptions();
        options.DataPath = string.Empty;
        options.Quota = 0;
        options.StoreDurationDefaultNominalSeconds = 0;
        options.StoreDurationDefaultPressureSeconds = 0;
        options.StoreDurationTemporaryNominalSeconds = 0;
        options.StoreDurationTemporaryPressureSeconds = 0;
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(false);
        var service = CreateValidationService(options);

        Exception? thrownException = null;
        try
        {
            service.ValidateOptions();
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        await Assert.That(thrownException).IsNotNull();
        await Assert.That(thrownException!.Message).Contains("DataPath not provided");
        await Assert.That(thrownException!.Message).Contains("Unable to create DataPath");
        await Assert.That(thrownException!.Message).Contains("Quota must be at least 1 MB");
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationDefaultNominal));
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationDefaultPressure));
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationTemporaryNominal));
        await Assert.That(thrownException!.Message).Contains(nameof(StorageOptions.StoreDurationTemporaryPressure));
    }

    #endregion

    #region Tests - FileSystem Verification

    [Test]
    public async Task ValidateOptions_CallsCheckCreateDirWithDataPath()
    {
        var dataPath = "/test/data/path";
        var options = CreateValidStorageOptions();
        options.DataPath = dataPath;
        mockFileSystem.Setup(fs => fs.CheckCreateDir(It.IsAny<string>())).Returns(true);
        var service = CreateValidationService(options);

        // Act
        service.ValidateOptions();

        // Assert
        mockFileSystem.Verify(fs => fs.CheckCreateDir(dataPath), Times.Once);
        await Assert.That(mockFileSystem.Invocations.Count).IsGreaterThanOrEqualTo(1);
    }

    #endregion
}
