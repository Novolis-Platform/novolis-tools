using Azure;
using Novolis.Audio.Voice.AzureSpeech;
using Novolis.Manuscript.Export.Audio;

namespace Novolis.Manuscript.Cli;

/// <summary>CLI adapter that composes the generic audiobook pipeline with Azure Speech.</summary>
sealed class AzureSpeechSynthesizer : ISynthesizer
{
    readonly AzureSpeechClient _client;

    public AzureSpeechSynthesizer(Uri endpoint, string key)
    {
        _client = new AzureSpeechClient(endpoint, new AzureKeyCredential(key));
    }

    public Task<byte[]> SynthesizeToMp3Async(
        string text,
        VoiceSettings settings,
        CancellationToken cancellationToken = default) =>
        _client.SynthesizeToMp3Async(
            text,
            new AzureSpeechSynthesisOptions
            {
                VoiceName = settings.Voice,
                RatePercent = settings.RatePercent,
                PitchHertz = settings.PitchHertz,
                VolumePercent = settings.VolumePercent,
            },
            cancellationToken);

    public async Task SaveMp3Async(
        string text,
        string path,
        VoiceSettings settings,
        CancellationToken cancellationToken = default)
    {
        var mp3 = await SynthesizeToMp3Async(text, settings, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, mp3, cancellationToken).ConfigureAwait(false);
    }
}
