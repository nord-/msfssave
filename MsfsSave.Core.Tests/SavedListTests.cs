using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class SavedListTests
{
    private static AircraftState State(string reg) => new() { Registration = reg, Title = "Cessna 172" };

    private static SavedList WithThree()
    {
        var list = new SavedList();
        list.Replace(new[] { State("A"), State("B"), State("C") });
        return list;
    }

    [Fact]
    public void Empty_list_has_no_selection()
    {
        var list = new SavedList();
        Assert.Equal(-1, list.SelectedIndex);
        Assert.Null(list.Selected);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void Replace_selects_first_item()
    {
        var list = WithThree();
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("A", list.Selected!.Registration);
    }

    [Fact]
    public void MoveDown_stops_at_last_item()
    {
        var list = WithThree();
        list.MoveDown();
        list.MoveDown();
        list.MoveDown();
        Assert.Equal(2, list.SelectedIndex);
    }

    [Fact]
    public void MoveUp_stops_at_first_item()
    {
        var list = WithThree();
        list.MoveDown();
        list.MoveUp();
        list.MoveUp();
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void MoveUp_and_MoveDown_are_noops_on_empty_list()
    {
        var list = new SavedList();
        list.MoveUp();
        list.MoveDown();
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void Replace_with_same_length_keeps_selection()
    {
        var list = WithThree();
        list.MoveDown();

        list.Replace(new[] { State("A"), State("B"), State("C") });

        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void Replace_with_shorter_list_clamps_selection()
    {
        var list = WithThree();
        list.MoveDown();
        list.MoveDown();

        list.Replace(new[] { State("A") });

        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("A", list.Selected!.Registration);
    }

    [Fact]
    public void Replace_with_empty_list_clears_selection()
    {
        var list = WithThree();

        list.Replace(Array.Empty<AircraftState>());

        Assert.Equal(-1, list.SelectedIndex);
        Assert.Null(list.Selected);
    }

    [Fact]
    public void SelectByRegistration_moves_selection_to_match()
    {
        var list = WithThree();

        list.SelectByRegistration("C");

        Assert.Equal(2, list.SelectedIndex);
    }

    [Fact]
    public void SelectByRegistration_leaves_selection_when_unknown()
    {
        var list = WithThree();
        list.MoveDown();

        list.SelectByRegistration("OKÄND");

        Assert.Equal(1, list.SelectedIndex);
    }
}
