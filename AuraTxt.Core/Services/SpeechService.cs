using System.Speech.Synthesis;

namespace AuraTxt.Core.Services;

public static class SpeechService
{
    /// <summary>Fire-and-forget speech with optional voice name. Falls back to system default.</summary>
    public static void Speak(string text, string? voiceName = null)
    {
        Task.Run(() =>
        {
            try
            {
                var synth = new SpeechSynthesizer();
                if (!string.IsNullOrEmpty(voiceName))
                {
                    try { synth.SelectVoice(voiceName); }
                    catch { /* voice not installed → use default */ }
                }
                synth.Speak(text);
            }
            catch { /* TTS failed silently */ }
        });
    }

    /// <summary>List installed voice names for UI selection.</summary>
    public static List<string> GetInstalledVoices()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices()
                .Select(v => v.VoiceInfo.Name)
                .ToList();
        }
        catch { return new() { "Microsoft Ava" }; }
    }
}
