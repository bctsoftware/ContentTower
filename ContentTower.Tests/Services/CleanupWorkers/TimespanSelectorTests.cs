using ContentTower.Controllers;
using ContentTower.Services;
using ContentTower.Services.CleanupWorkers;
using Moq;

namespace ContentTower.Tests.Services.CleanupWorkers;

public class TimespanSelectorTests
{
    private readonly Mock<IQuotaService> mockQuotaService;

    private static readonly StorageOptions Options = new StorageOptions
    {
        StoreDurationDefaultNominalSeconds = 100,
        StoreDurationDefaultPressureSeconds = 50,
        StoreDurationTemporaryNominalSeconds = 30,
        StoreDurationTemporaryPressureSeconds = 10,
    };

    public TimespanSelectorTests()
    {
        mockQuotaService = new Mock<IQuotaService>();
    }

    #region Helpers

    private TimespanSelector CreateSelector() => new TimespanSelector(
        Microsoft.Extensions.Options.Options.Create(Options),
        mockQuotaService.Object
    );

    private void SetupQuotaState(QuotaState state)
    {
        mockQuotaService.Setup(qs => qs.GetQuotaStatus()).Returns(new QuotaResponse { State = state });
    }

    #endregion

    #region Nominal state

    [Test]
    public async Task Get_NominalState_Default_ReturnsDefaultNominalDuration()
    {
        SetupQuotaState(QuotaState.Nominal);

        var result = CreateSelector().Get(StoreType.Default);

        await Assert.That(result).IsEqualTo(Options.StoreDurationDefaultNominal);
    }

    [Test]
    public async Task Get_NominalState_Temporary_ReturnsTemporaryNominalDuration()
    {
        SetupQuotaState(QuotaState.Nominal);

        var result = CreateSelector().Get(StoreType.Temporary);

        await Assert.That(result).IsEqualTo(Options.StoreDurationTemporaryNominal);
    }

    [Test]
    public async Task Get_NominalState_Permanent_Throws()
    {
        SetupQuotaState(QuotaState.Nominal);

        Exception? thrown = null;
        try { CreateSelector().Get(StoreType.Permanent); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(InvalidOperationException));
    }

    #endregion

    #region Pressure state

    [Test]
    public async Task Get_PressureState_Default_ReturnsDefaultPressureDuration()
    {
        SetupQuotaState(QuotaState.Pressure);

        var result = CreateSelector().Get(StoreType.Default);

        await Assert.That(result).IsEqualTo(Options.StoreDurationDefaultPressure);
    }

    [Test]
    public async Task Get_PressureState_Temporary_ReturnsTemporaryPressureDuration()
    {
        SetupQuotaState(QuotaState.Pressure);

        var result = CreateSelector().Get(StoreType.Temporary);

        await Assert.That(result).IsEqualTo(Options.StoreDurationTemporaryPressure);
    }

    [Test]
    public async Task Get_PressureState_Permanent_Throws()
    {
        SetupQuotaState(QuotaState.Pressure);

        Exception? thrown = null;
        try { CreateSelector().Get(StoreType.Permanent); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(InvalidOperationException));
    }

    #endregion

    #region Full state (shares pressure set)

    [Test]
    public async Task Get_FullState_Default_ReturnsDefaultPressureDuration()
    {
        SetupQuotaState(QuotaState.Full);

        var result = CreateSelector().Get(StoreType.Default);

        await Assert.That(result).IsEqualTo(Options.StoreDurationDefaultPressure);
    }

    [Test]
    public async Task Get_FullState_Temporary_ReturnsTemporaryPressureDuration()
    {
        SetupQuotaState(QuotaState.Full);

        var result = CreateSelector().Get(StoreType.Temporary);

        await Assert.That(result).IsEqualTo(Options.StoreDurationTemporaryPressure);
    }

    [Test]
    public async Task Get_FullState_Permanent_Throws()
    {
        SetupQuotaState(QuotaState.Full);

        Exception? thrown = null;
        try { CreateSelector().Get(StoreType.Permanent); } catch (Exception ex) { thrown = ex; }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown).IsOfType(typeof(InvalidOperationException));
    }

    #endregion
}

