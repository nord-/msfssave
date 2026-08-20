using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class ReconnectPolicyTests
{
    private static readonly DateTime Start = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static ReconnectPolicy Policy() => new(TimeSpan.FromSeconds(3));

    [Fact]
    public void Forsta_forsoket_slapps_igenom_direkt()
    {
        Assert.True(Policy().ShouldAttempt(isConnected: false, Start));
    }

    [Fact]
    public void Inget_forsok_medan_anslutningen_lever()
    {
        var policy = Policy();
        Assert.False(policy.ShouldAttempt(isConnected: true, Start));
        Assert.False(policy.ShouldAttempt(isConnected: true, Start.AddMinutes(5)));
    }

    [Fact]
    public void Nasta_forsok_vantar_ut_intervallet()
    {
        var policy = Policy();

        Assert.True(policy.ShouldAttempt(false, Start));
        Assert.False(policy.ShouldAttempt(false, Start.AddSeconds(1)));
        Assert.False(policy.ShouldAttempt(false, Start.AddSeconds(2.9)));
        Assert.True(policy.ShouldAttempt(false, Start.AddSeconds(3)));
    }

    [Fact]
    public void Intervallet_raknas_fran_senaste_forsoket_inte_fran_starten()
    {
        var policy = Policy();

        Assert.True(policy.ShouldAttempt(false, Start));
        Assert.True(policy.ShouldAttempt(false, Start.AddSeconds(10)));
        Assert.False(policy.ShouldAttempt(false, Start.AddSeconds(11)));
        Assert.True(policy.ShouldAttempt(false, Start.AddSeconds(13)));
    }

    /// <summary>Tappad anslutning ska inte ge ett omedelbart försök om intervallet inte gått ut.</summary>
    [Fact]
    public void En_lyckad_anslutning_nollar_inte_takten()
    {
        var policy = Policy();

        Assert.True(policy.ShouldAttempt(false, Start));
        Assert.False(policy.ShouldAttempt(true, Start.AddSeconds(1)));
        Assert.False(policy.ShouldAttempt(false, Start.AddSeconds(2)));
        Assert.True(policy.ShouldAttempt(false, Start.AddSeconds(3)));
    }
}
