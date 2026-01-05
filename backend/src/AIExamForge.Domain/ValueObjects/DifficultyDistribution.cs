namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Difficulty distribution for question generation per data-model.md.
/// Percentages must sum to 100.
/// </summary>
public sealed record DifficultyDistribution
{
    public int EasyPercent { get; private set; }
    public int MediumPercent { get; private set; }
    public int HardPercent { get; private set; }

    private DifficultyDistribution() { } // EF Core

    private DifficultyDistribution(int easyPercent, int mediumPercent, int hardPercent)
    {
        EasyPercent = easyPercent;
        MediumPercent = mediumPercent;
        HardPercent = hardPercent;
    }

    public static DifficultyDistribution Create(int easy, int medium, int hard)
    {
        if (easy < 0 || medium < 0 || hard < 0)
        {
            throw new ArgumentException("Difficulty percentages cannot be negative.");
        }

        if (easy + medium + hard != 100)
        {
            throw new ArgumentException(
                $"Difficulty percentages must sum to 100. Got: Easy={easy}, Medium={medium}, Hard={hard} (sum={easy + medium + hard})");
        }

        return new DifficultyDistribution(easy, medium, hard);
    }

    /// <summary>
    /// Default distribution: 30% Easy, 50% Medium, 20% Hard
    /// </summary>
    public static DifficultyDistribution Default => new(30, 50, 20);

    /// <summary>
    /// Balanced distribution: 33% each (34% Medium due to rounding)
    /// </summary>
    public static DifficultyDistribution Balanced => new(33, 34, 33);

    /// <summary>
    /// Calculate how many questions of each difficulty for a given total.
    /// </summary>
    public (int easy, int medium, int hard) CalculateCounts(int totalQuestions)
    {
        var easy = (int)Math.Round(totalQuestions * EasyPercent / 100.0);
        var hard = (int)Math.Round(totalQuestions * HardPercent / 100.0);
        var medium = totalQuestions - easy - hard; // Remainder goes to medium

        return (easy, medium, hard);
    }
}
