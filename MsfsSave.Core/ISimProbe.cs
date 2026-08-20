namespace MsfsSave.Core;

/// <summary>
/// Svarar på den enda frågan "går det att ansluta till simulatorn just nu?". Skild från
/// <see cref="ISimConnector"/> därför att svaret hämtas från en bakgrundstråd medan
/// UI-tråden kan hålla på med annat.
/// </summary>
public interface ISimProbe
{
    /// <summary>
    /// Sant när en anslutning kunde upprättas. Måste vara statslös — anropas från en annan tråd
    /// än den som äger anslutningen, och får därför inte dela något med den.
    /// </summary>
    bool IsAvailable();
}
