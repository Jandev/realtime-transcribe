namespace RealtimeTranscribe.Services;

/// <summary>
/// Provides composable, testable helpers for the in-app text-scale feature.
/// All methods are pure functions with no side effects.
/// </summary>
public static class TextScaleService
{
    /// <summary>Default content font size in device-independent units.</summary>
    public const double Default = 13.0;

    /// <summary>Smallest allowed content font size.</summary>
    public const double Minimum = 10.0;

    /// <summary>Largest allowed content font size.</summary>
    public const double Maximum = 28.0;

    /// <summary>Size change applied by each zoom step.</summary>
    public const double Step = 2.0;

    /// <summary>Preference key used when persisting the selected font size.</summary>
    public const string PreferenceKey = "ContentFontSize";

    /// <summary>Clamps <paramref name="value"/> to [<see cref="Minimum"/>, <see cref="Maximum"/>].</summary>
    public static double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);

    /// <summary>Returns the next larger font size, clamped at <see cref="Maximum"/>.</summary>
    public static double Increment(double current) => Clamp(current + Step);

    /// <summary>Returns the next smaller font size, clamped at <see cref="Minimum"/>.</summary>
    public static double Decrement(double current) => Clamp(current - Step);

    /// <summary>
    /// Validates and returns a font size loaded from persistent storage.
    /// Out-of-range or NaN values are replaced with <see cref="Default"/>.
    /// </summary>
    public static double Restore(double persisted) =>
        double.IsNaN(persisted) || persisted < Minimum || persisted > Maximum
            ? Default
            : persisted;
}
