using System.Text.Json;
using Xunit;
using Primworth_API.Models.Yard;
using Primworth_API.Repository.Yard;

namespace Apps.Yard.Tests;

public class SlabStackingGraphTests
{
    private readonly YardRuleEngine _rules = new();

    [Fact]
    public void Grade_439_stacks_on_a_longer_wider_slab_of_the_same_size()
    {
        var choice = Place("439", 8500, 1200, 220,
            Pit(1, occupied: 2, topGrade: "439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal(1, choice.PositionId);
    }

    [Fact]
    public void Longer_or_wider_slab_cannot_sit_on_a_smaller_one()
    {
        var longer = Place("439", 9500, 1100, 220,
            Pit(1, occupied: 1, topGrade: "439", topL: 8500, topW: 1400, topTh: 220));
        Assert.Null(longer.PositionId);
        Assert.Equal("Longer slabs must sit below shorter ones.", longer.Reason);

        var wider = Place("439", 8000, 1500, 220,
            Pit(1, occupied: 1, topGrade: "439", topL: 9000, topW: 1200, topTh: 220));
        Assert.Null(wider.PositionId);
        Assert.Equal("Wider slabs must sit below narrower ones.", wider.Reason);
    }

    [Fact]
    public void Different_grade_or_size_cannot_share_a_stack()
    {
        var grade = Place("439", 8000, 1100, 220,
            Pit(1, occupied: 1, topGrade: "304", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal("Slabs of a different grade cannot share a stack.", grade.Reason);

        var size = Place("439", 8000, 1100, 200,
            Pit(1, occupied: 1, topGrade: "439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal("Slabs of a different size cannot share a stack.", size.Reason);
    }

    [Fact]
    public void Four_hundred_series_stops_at_10_and_normal_grades_at_15()
    {
        var full = Place("439", 8000, 1100, 220,
            Pit(1, occupied: 10, topGrade: "439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal("400-series grades may stack at most 10 high.", full.Reason);

        var room = Place("439", 8000, 1100, 220,
            Pit(1, occupied: 9, topGrade: "439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal(1, room.PositionId);

        var normalFull = Place("304", 8000, 1100, 220,
            Pit(1, occupied: 15, topGrade: "304", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal("This grade may stack at most 15 high.", normalFull.Reason);

        var normalRoom = Place("304", 8000, 1100, 220,
            Pit(1, occupied: 14, topGrade: "304", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal(1, normalRoom.PositionId);
    }

    [Fact]
    public void Martensitic_and_430_mint_stop_at_5()
    {
        foreach (var grade in new[] { "410", "431", "440A", "430 Mint", "430-Mint", "17-4 PH" })
        {
            var blocked = Place(grade, 8000, 1100, 220,
                Pit(1, occupied: 5, topGrade: grade, topL: 9000, topW: 1400, topTh: 220));
            Assert.Equal("Martensitic and 430 Mint grades may stack at most 5 high.", blocked.Reason);

            var allowed = Place(grade, 8000, 1100, 220,
                Pit(1, occupied: 4, topGrade: grade, topL: 9000, topW: 1400, topTh: 220));
            Assert.Equal(1, allowed.PositionId);
        }
    }

    [Fact]
    public void Plain_430_is_400_series_not_mint()
    {
        var choice = Place("430", 8000, 1100, 220,
            Pit(1, occupied: 6, topGrade: "430", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal(1, choice.PositionId);
    }

    [Fact]
    public void Prefers_the_fullest_compatible_pit_and_uses_an_empty_one_when_needed()
    {
        var fullest = Place("439", 8000, 1100, 220,
            Pit(1, occupied: 1, topGrade: "439", topL: 9000, topW: 1400, topTh: 220, row: 1, col: 1),
            Pit(2, occupied: 4, topGrade: "439", topL: 9000, topW: 1400, topTh: 220, row: 2, col: 1),
            Pit(3, occupied: 0, topGrade: null, topL: null, topW: null, topTh: null, row: 0, col: 0));
        Assert.Equal(2, fullest.PositionId);

        var onlyEmpty = Place("439", 8000, 1100, 220,
            Pit(1, occupied: 2, topGrade: "304", topL: 9000, topW: 1400, topTh: 220),
            Pit(3, occupied: 0, topGrade: null, topL: null, topW: null, topTh: null, row: 1, col: 2));
        Assert.Equal(3, onlyEmpty.PositionId);
    }

    [Fact]
    public void A_grade_with_a_prefix_still_uses_its_aisi_family()
    {
        var choice = Place("SS439", 8000, 1100, 220,
            Pit(1, occupied: 9, topGrade: "SS439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal(1, choice.PositionId);

        var blocked = Place("SS439", 8000, 1100, 220,
            Pit(1, occupied: 10, topGrade: "SS439", topL: 9000, topW: 1400, topTh: 220));
        Assert.Equal("400-series grades may stack at most 10 high.", blocked.Reason);
    }

    [Fact]
    public void Graph_exposes_the_classification_table()
    {
        using var doc = JsonDocument.Parse(YardStackingGraph.Slab);
        var names = doc.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { "request", "Read grade", "Grade Classification", "Stack facts", "Stack Validation", "Location Selection", "response" }, names);
    }

    private Primworth_API.IRepository.YardLocationChoice Place(string grade, decimal length, decimal width, decimal thickness, params YardCandidatePosition[] pits)
    {
        var item = new YardItem
        {
            type_key = "SLAB",
            grade_code = grade,
            length_mm = length,
            width_mm = width,
            thickness_mm = thickness,
            size_profile_id = 1,
            attributes = "{}"
        };
        return _rules.SelectLocation(item, pits, YardStackingGraph.Slab);
    }

    private static YardCandidatePosition Pit(long id, int occupied, string? topGrade, decimal? topL, decimal? topW, decimal? topTh, int row = 1, int col = 1) => new()
    {
        position_id = id,
        zone_id = 1,
        storage_style = "STACK",
        occupied = occupied,
        capacity_limit = 20,
        rule_capacity = 20,
        top_grade = topGrade,
        top_length_mm = topL,
        top_width_mm = topW,
        top_thickness_mm = topTh,
        top_size_profile_id = 1,
        row_index = row,
        col_index = col,
    };
}
