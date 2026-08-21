namespace MsfsSave.Core;

/// <summary>
/// Håller takten på återanslutningsförsöken. Ren logik utan klocka eller I/O — tiden skickas in,
/// så intervallet kan verifieras med tester.
/// </summary>
public class ReconnectPolicy
{
    private readonly TimeSpan _interval;
    private DateTime _nextAttemptUtc = DateTime.MinValue;

    public ReconnectPolicy(TimeSpan interval) => _interval = interval;

    /// <summary>
    /// Sant när ett nytt anslutningsförsök ska göras nu. Svarar sant skjuts nästa tillåtna
    /// tidpunkt fram, så anropet får aldrig göras utan att försöket faktiskt utförs.
    /// </summary>
    public bool ShouldAttempt(bool isConnected, DateTime nowUtc)
    {
        if (isConnected || nowUtc < _nextAttemptUtc) return false;
        _nextAttemptUtc = nowUtc + _interval;
        return true;
    }
}
