namespace SimpleDiscordNet.Commands;

/// <summary>
/// A component ID pattern with named parts, such as "vote:{pollId}:{choice}".
/// Generated code creates these from handler attributes.
/// </summary>
public sealed class ComponentIdTemplate
{
    private readonly string[] _literals;
    private readonly string[] _names;

    /// <summary>How much fixed text the pattern has. More specific patterns win over looser ones.</summary>
    internal int LiteralLength { get; }

    public ComponentIdTemplate(string template)
    {
        List<string> literals = [];
        List<string> names = [];
        int position = 0;

        while (true)
        {
            int open = template.IndexOf('{', position);
            int close = open < 0 ? -1 : template.IndexOf('}', open);
            if (close < 0)
            {
                literals.Add(template[position..]);
                break;
            }

            literals.Add(template[position..open]);
            names.Add(template[(open + 1)..close]);
            position = close + 1;
        }

        _literals = literals.ToArray();
        _names = names.ToArray();
        LiteralLength = _literals.Sum(static literal => literal.Length);
    }

    /// <summary>
    /// Matches a component ID and returns its named parts, or null if it doesn't fit this pattern.
    /// </summary>
    internal Dictionary<string, string>? Match(string customId)
    {
        if (!customId.StartsWith(_literals[0], StringComparison.Ordinal))
            return null;

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        int position = _literals[0].Length;

        for (int i = 0; i < _names.Length; i++)
        {
            string next = _literals[i + 1];
            bool isLastPart = i == _names.Length - 1;

            // The last part runs to the end of the ID, so a trailing literal has to end it exactly
            int end = isLastPart
                ? customId.Length - next.Length
                : next.Length == 0 ? -1 : customId.IndexOf(next, position, StringComparison.Ordinal);

            if (end <= position || (isLastPart && !customId.EndsWith(next, StringComparison.Ordinal)))
                return null;

            values[_names[i]] = customId[position..end];
            position = end + next.Length;
        }

        return position == customId.Length ? values : null;
    }
}
