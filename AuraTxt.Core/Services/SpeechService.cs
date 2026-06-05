using System.Speech.Synthesis;

namespace AuraTxt.Core.Services;

public static class SpeechService
{
    public static void Speak(string text, string? voiceName = null)
    {
        _ = Task.Run(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                if (!string.IsNullOrEmpty(voiceName))
                    try { synth.SelectVoice(voiceName); } catch { }
                synth.Speak(text);
            }
            catch { }
        });
    }

    public static List<string> GetInstalledVoices()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToList();
        }
        catch { return []; }
    }
}
