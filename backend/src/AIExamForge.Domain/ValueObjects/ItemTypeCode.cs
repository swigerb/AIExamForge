namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Represents the 19 supported item type codes per docs/item-types.md.
/// This is a strongly-typed value object to prevent invalid type codes.
/// </summary>
public sealed record ItemTypeCode
{
    private static readonly HashSet<string> ValidCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "M1", "BA", "DA", "SC", "DOMC", "CHOOSE_N",
        "AS", "BL", "MW", "HA", "DD", "MG",
        "GI", "MS", "PA", "CA", "AN", "LABS", "SA"
    };

    public string Code { get; }

    private ItemTypeCode(string code) => Code = code.ToUpperInvariant();

    public static ItemTypeCode Create(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));

        var normalized = code.ToUpperInvariant();
        if (!ValidCodes.Contains(normalized))
        {
            throw new ArgumentException(
                $"Invalid item type code: '{code}'. Valid codes are: {string.Join(", ", ValidCodes.Order())}",
                nameof(code));
        }

        return new ItemTypeCode(normalized);
    }

    public static bool TryCreate(string code, out ItemTypeCode? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.ToUpperInvariant();
        if (!ValidCodes.Contains(normalized))
        {
            return false;
        }

        result = new ItemTypeCode(normalized);
        return true;
    }

    public static IReadOnlySet<string> GetAllValidCodes() => ValidCodes;

    // Dichotomous types (single correct answer)
    public static ItemTypeCode M1 => new("M1");
    public static ItemTypeCode BA => new("BA");
    public static ItemTypeCode DA => new("DA");
    public static ItemTypeCode SC => new("SC");
    public static ItemTypeCode DOMC => new("DOMC");

    // Polytomous types (multiple/partial credit)
    public static ItemTypeCode CHOOSE_N => new("CHOOSE_N");
    public static ItemTypeCode AS => new("AS");
    public static ItemTypeCode BL => new("BL");
    public static ItemTypeCode MW => new("MW");
    public static ItemTypeCode HA => new("HA");
    public static ItemTypeCode DD => new("DD");
    public static ItemTypeCode MG => new("MG");
    public static ItemTypeCode GI => new("GI");
    public static ItemTypeCode MS => new("MS");
    public static ItemTypeCode PA => new("PA");
    public static ItemTypeCode CA => new("CA");
    public static ItemTypeCode AN => new("AN");
    public static ItemTypeCode LABS => new("LABS");
    public static ItemTypeCode SA => new("SA");

    public bool IsDichotomous => Code is "M1" or "BA" or "DA" or "SC" or "DOMC";
    public bool IsPolytomous => !IsDichotomous;

    public override string ToString() => Code;

    public static implicit operator string(ItemTypeCode code) => code.Code;
}
