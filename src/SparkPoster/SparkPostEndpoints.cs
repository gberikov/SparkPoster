namespace SparkPoster;

/// <summary>
/// SparkPost base addresses. The US and EU services are independent accounts:
/// a key issued for one does not work with the other.
/// </summary>
public static class SparkPostEndpoints
{
    /// <summary>The main SparkPost service (US).</summary>
    public static Uri Us { get; } = new("https://api.sparkpost.com/api/v1/");

    /// <summary>SparkPost EU — the same service hosted in Western Europe.</summary>
    public static Uri Eu { get; } = new("https://api.eu.sparkpost.com/api/v1/");
}
