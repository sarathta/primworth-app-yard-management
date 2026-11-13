using Primworth_API.Models.Yard;

namespace Primworth_API.IRepository
{
    public sealed class YardRuleDecision
    {
        public bool Allowed { get; init; }
        public string? Reason { get; init; }
    }

    /// <summary>One pass over every free pit. Handled is false for a per-pit graph such as billet.</summary>
    public sealed class YardLocationChoice
    {
        public bool Handled { get; init; }
        public long? PositionId { get; init; }
        public string? Reason { get; init; }
    }

    /// <summary>
    /// Evaluates a yard's GoRules graph (sp_rules.graph) for inbound placement.
    /// A slab graph classifies the grade, checks every pit, and picks one.
    /// When a yard has no such graph, each pit is judged on its own and the built-in policy fills any gap.
    /// </summary>
    public interface IYardRuleEngine
    {
        bool UsesLocationGraph(string? graphJson);
        YardLocationChoice SelectLocation(YardItem item, IReadOnlyList<YardCandidatePosition> candidates, string? graphJson);
        YardRuleDecision EvaluatePlacement(YardItem item, YardCandidatePosition candidate, string? graphJson);
    }
}
