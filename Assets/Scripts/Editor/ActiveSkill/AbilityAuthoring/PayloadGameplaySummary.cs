using System.Collections.Generic;

// Gameplay-language description of a configured payload, built by its descriptor. Consumers
// (wizard, node ability cards) render Headline as the primary sentence and Details/Warnings as
// supporting lines. Descriptors must translate serialized fields into gameplay meaning here
// rather than reflecting field names/values.
public sealed class PayloadGameplaySummary
{
    public string Headline { get; set; } = string.Empty;
    public List<string> Details { get; } = new();
    public List<string> Warnings { get; } = new();

    public static PayloadGameplaySummary Empty => new();

    public static PayloadGameplaySummary Of(string headline) => new() { Headline = headline ?? string.Empty };

    public PayloadGameplaySummary AddDetail(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            Details.Add(line);

        return this;
    }

    public PayloadGameplaySummary AddWarning(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            Warnings.Add(line);

        return this;
    }
}
