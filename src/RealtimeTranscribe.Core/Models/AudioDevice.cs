namespace RealtimeTranscribe.Models;

/// <summary>
/// Represents an audio device (microphone or speaker) available on the system.
/// </summary>
/// <param name="Id">Platform-specific unique identifier for the device.</param>
/// <param name="Name">Human-readable display name shown in the UI.</param>
public sealed record AudioDevice(string Id, string Name);
