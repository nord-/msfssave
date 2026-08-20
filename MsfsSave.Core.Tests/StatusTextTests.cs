using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class StatusTextTests
{
    private static LoadResult Load(bool mismatch, bool atcIdSet) =>
        new(new AircraftState { SlotName = "SE-ABC", AtcId = "SE-ABC", Title = "Cessna 172" },
            mismatch, mismatch ? "Piper PA-28" : "Cessna 172", atcIdSet);

    [Fact]
    public void Loaded_without_warnings_is_a_plain_confirmation()
    {
        Assert.Equal("✓ Laddade SE-ABC.", StatusText.Loaded(Load(false, true)));
    }

    [Fact]
    public void Loaded_with_title_mismatch_names_the_loaded_aircraft()
    {
        Assert.Equal("⚠ Laddade SE-ABC — \"Piper PA-28\" är laddat.",
            StatusText.Loaded(Load(true, true)));
    }

    [Fact]
    public void Loaded_with_missing_atc_id_reports_it()
    {
        Assert.Equal("⚠ Laddade SE-ABC — ATC ID ej satt.",
            StatusText.Loaded(Load(false, false)));
    }

    [Fact]
    public void Loaded_with_both_warnings_joins_them_on_one_line()
    {
        Assert.Equal("⚠ Laddade SE-ABC — \"Piper PA-28\" är laddat, ATC ID ej satt.",
            StatusText.Loaded(Load(true, false)));
    }

    [Fact]
    public void Saved_distinguishes_new_from_overwrite()
    {
        var state = new AircraftState { SlotName = "SE-ABC" };
        Assert.Equal("✓ Sparade SE-ABC.", StatusText.Saved(new SaveResult(state, false)));
        Assert.Equal("✓ Skrev över SE-ABC.", StatusText.Saved(new SaveResult(state, true)));
    }

    [Fact]
    public void Deleted_reports_both_outcomes()
    {
        Assert.Equal("✓ Tog bort SE-MAF.", StatusText.Deleted("SE-MAF", true));
        Assert.Equal("✗ SE-MAF fanns inte längre — listan uppdaterad.", StatusText.Deleted("SE-MAF", false));
    }

    [Fact]
    public void Blocked_names_verb_and_reason()
    {
        Assert.Equal("✗ Kan inte ladda: motorerna är igång.", StatusText.Blocked("ladda", "motorerna är igång"));
        Assert.Equal("✗ Kan inte spara: planet är i luften.", StatusText.Blocked("spara", "planet är i luften"));
    }

    [Fact]
    public void Error_prefixes_the_message()
    {
        Assert.Equal("✗ Fel: Inget svar från simulatorn inom tidsgränsen.",
            StatusText.Error("Inget svar från simulatorn inom tidsgränsen."));
    }

    [Fact]
    public void LockReason_forklarar_att_simulatorn_inte_ar_igang()
    {
        Assert.Equal("MSFS är inte igång — försöker ansluta",
            StatusText.LockReason(connected: false, readiness: null, error: ""));
    }

    [Fact]
    public void LockReason_namner_simulatorn_forst_aven_nar_ett_fel_finns_kvar()
    {
        Assert.Equal("MSFS är inte igång — försöker ansluta",
            StatusText.LockReason(false, null, "Inte ansluten till simulatorn."));
    }

    [Fact]
    public void LockReason_ger_planets_brist_nar_anslutningen_lever()
    {
        Assert.Equal("planet är i luften",
            StatusText.LockReason(true, new SimReadiness(false, true, true), ""));
    }

    [Fact]
    public void LockReason_visar_felet_nar_anslutningen_lever_men_avlasningen_misslyckades()
    {
        Assert.Equal("simulatorns tillstånd är okänt (timeout)",
            StatusText.LockReason(true, null, "timeout"));
    }

    [Fact]
    public void LockReason_ar_null_nar_ingenting_blockerar()
    {
        Assert.Null(StatusText.LockReason(true, new SimReadiness(true, true, true), ""));
    }
}
