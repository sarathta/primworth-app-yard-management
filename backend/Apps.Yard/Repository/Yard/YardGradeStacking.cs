using System.Text.RegularExpressions;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// Slab stack-height limits by grade family: martensitic and 430 Mint 5, 400-series 10, other grades 15.
    /// Martensitic wins when a code is both (410, 420, 440). 430 Mint is martensitic height even though 430 is a 400-series number.
    /// </summary>
    internal static class YardGradeStacking
    {
        public const int MartensiticMax = 5;
        public const int Series400Max = 10;
        public const int NormalMax = 15;

        public const string Martensitic = "martensitic";
        public const string Series400 = "400_series";
        public const string Normal = "normal";

        // Not \\b: mill codes glue the number to a prefix/suffix (SS439, 439L, AISI439).
        private static readonly Regex MartensiticAisi = new(
            @"(?<![0-9])(403|410|414|416|420|422|431|440[ABC]?)(?![0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MartensiticEn = new(
            @"\b1\.(4006|4021|4028|4031|4034|4057|4104|4112|4125)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MartensiticPh = new(
            @"\b(17[\s\-/]?4(\s*PH)?|15[\s\-/]?5(\s*PH)?|13[\s\-/]?8(\s*PH)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Series400Aisi = new(
            @"(?<![0-9])4[0-9]{2}(?![0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex Series400En = new(
            @"\b1\.4[0-9]{3}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static GradeStackLimit Classify(string? gradeCode)
        {
            var text = gradeCode?.Trim() ?? "";
            if (text.Contains("mint", StringComparison.OrdinalIgnoreCase)
                || text.Contains("marten", StringComparison.OrdinalIgnoreCase))
                return Limit(Martensitic, MartensiticMax, true, Series400Aisi.IsMatch(text) || Series400En.IsMatch(text));

            if (MartensiticAisi.IsMatch(text) || MartensiticEn.IsMatch(text) || MartensiticPh.IsMatch(text))
                return Limit(Martensitic, MartensiticMax, true, Series400Aisi.IsMatch(text) || Series400En.IsMatch(text));

            if (Series400Aisi.IsMatch(text) || Series400En.IsMatch(text))
                return Limit(Series400, Series400Max, false, true);

            return Limit(Normal, NormalMax, false, false);
        }

        public static string DenyReason(GradeStackLimit limit) => limit.Family switch
        {
            Martensitic => $"Martensitic and 430 Mint grades may stack at most {MartensiticMax} high.",
            Series400 => $"400-series grades may stack at most {Series400Max} high.",
            _ => $"This grade may stack at most {NormalMax} high.",
        };

        private static GradeStackLimit Limit(string family, int max, bool martensitic, bool series400)
            => new(family, max, martensitic, series400);
    }

    internal readonly record struct GradeStackLimit(string Family, int Max, bool IsMartensitic, bool Is400Series);
}
