using RealtimeTranscribe.Services;
using Xunit;

namespace RealtimeTranscribe.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TextScaleService"/>.
/// Covers default value, increment/decrement, boundary clamping, and persistence-restore validation.
/// </summary>
public class TextScaleServiceTests
{
    [Fact]
    public void Default_IsWithinBounds()
    {
        Assert.InRange(TextScaleService.Default, TextScaleService.Minimum, TextScaleService.Maximum);
    }

    [Fact]
    public void Increment_FromDefault_ReturnsDefaultPlusStep()
    {
        var result = TextScaleService.Increment(TextScaleService.Default);

        Assert.Equal(TextScaleService.Default + TextScaleService.Step, result);
    }

    [Fact]
    public void Decrement_FromDefault_ReturnsDefaultMinusStep()
    {
        var result = TextScaleService.Decrement(TextScaleService.Default);

        Assert.Equal(TextScaleService.Default - TextScaleService.Step, result);
    }

    [Fact]
    public void Increment_AtMaximum_ClampsToMaximum()
    {
        var result = TextScaleService.Increment(TextScaleService.Maximum);

        Assert.Equal(TextScaleService.Maximum, result);
    }

    [Fact]
    public void Decrement_AtMinimum_ClampsToMinimum()
    {
        var result = TextScaleService.Decrement(TextScaleService.Minimum);

        Assert.Equal(TextScaleService.Minimum, result);
    }

    [Fact]
    public void Clamp_AboveMaximum_ReturnsMaximum()
    {
        var result = TextScaleService.Clamp(TextScaleService.Maximum + 100);

        Assert.Equal(TextScaleService.Maximum, result);
    }

    [Fact]
    public void Clamp_BelowMinimum_ReturnsMinimum()
    {
        var result = TextScaleService.Clamp(TextScaleService.Minimum - 100);

        Assert.Equal(TextScaleService.Minimum, result);
    }

    [Fact]
    public void Restore_ValidValue_ReturnsSameValue()
    {
        double value = TextScaleService.Default;

        Assert.Equal(value, TextScaleService.Restore(value));
    }

    [Fact]
    public void Restore_TooLarge_ReturnsDefault()
    {
        Assert.Equal(TextScaleService.Default, TextScaleService.Restore(TextScaleService.Maximum + 1));
    }

    [Fact]
    public void Restore_TooSmall_ReturnsDefault()
    {
        Assert.Equal(TextScaleService.Default, TextScaleService.Restore(TextScaleService.Minimum - 1));
    }

    [Fact]
    public void Restore_NaN_ReturnsDefault()
    {
        Assert.Equal(TextScaleService.Default, TextScaleService.Restore(double.NaN));
    }

    [Fact]
    public void Restore_NegativeValue_ReturnsDefault()
    {
        Assert.Equal(TextScaleService.Default, TextScaleService.Restore(-1.0));
    }
}
