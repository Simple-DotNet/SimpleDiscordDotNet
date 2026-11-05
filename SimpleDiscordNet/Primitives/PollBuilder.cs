namespace SimpleDiscordNet.Primitives;

/// <summary>
/// Builds a poll for a message.
/// Example: new PollBuilder("Pizza or tacos?").AddAnswer("Pizza", "🍕").AddAnswer("Tacos", "🌮").WithDuration(hours: 48);
/// For a quick poll, use <see cref="MessageBuilder.WithPoll(string, string[])"/>.
/// </summary>
public sealed class PollBuilder
{
    private const int MaxQuestionLength = 300;
    private const int MaxAnswerLength = 55;
    private const int MaxAnswers = 10;
    private const int MaxDurationHours = 32 * 24;

    private readonly string _question;
    private readonly List<PollAnswerRequest> _answers = [];
    private int? _durationHours;
    private bool? _allowMultiselect;

    public PollBuilder(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"A poll question can't be longer than {MaxQuestionLength} characters.", nameof(question));

        _question = question;
    }

    /// <summary>
    /// Adds an answer, optionally with a standard emoji such as "🍕".
    /// </summary>
    public PollBuilder AddAnswer(string text, string? emoji = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > MaxAnswerLength)
            throw new ArgumentException($"A poll answer can't be longer than {MaxAnswerLength} characters.", nameof(text));
        if (_answers.Count == MaxAnswers)
            throw new InvalidOperationException($"A poll can't have more than {MaxAnswers} answers.");

        _answers.Add(new PollAnswerRequest
        {
            poll_media = new PollMediaRequest { text = text, emoji = emoji is null ? null : new EmojiReference(emoji, null) }
        });
        return this;
    }

    /// <summary>
    /// Sets how many hours the poll stays open, up to 32 days. Discord defaults to 24 hours.
    /// </summary>
    public PollBuilder WithDuration(int hours)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hours, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hours, MaxDurationHours);
        _durationHours = hours;
        return this;
    }

    /// <summary>
    /// Lets people pick more than one answer.
    /// </summary>
    public PollBuilder AllowMultipleAnswers(bool allow = true)
    {
        _allowMultiselect = allow;
        return this;
    }

    internal PollRequest Build()
    {
        if (_answers.Count == 0)
            throw new InvalidOperationException("A poll needs at least one answer.");

        return new PollRequest
        {
            question = new PollMediaRequest { text = _question },
            answers = _answers.ToArray(),
            duration = _durationHours,
            allow_multiselect = _allowMultiselect
        };
    }
}

public sealed class PollRequest
{
    public required PollMediaRequest question { get; init; }
    public required PollAnswerRequest[] answers { get; init; }
    public int? duration { get; init; }
    public bool? allow_multiselect { get; init; }
}

public sealed class PollMediaRequest
{
    public string? text { get; init; }
    public EmojiReference? emoji { get; init; }
}

public sealed class PollAnswerRequest
{
    public required PollMediaRequest poll_media { get; init; }
}
