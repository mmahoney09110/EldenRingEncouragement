using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;

namespace EldenTTS
{
    public class EldenTTS
    {
        private readonly string _key;
        private readonly string _region;
        private readonly string _voice;

        public EldenTTS(string settingsPath)
        {
            var settings = File.ReadAllLines(settingsPath);
            foreach (var line in settings)
            {
                if (line.StartsWith("azure_key=")) _key = line.Substring("azure_key=".Length);
                if (line.StartsWith("azure_region=")) _region = line.Substring("azure_region=".Length);
                if (line.StartsWith("azure_voice=")) _voice = line.Substring("azure_voice=".Length);
            }

            if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_region))
                throw new Exception("Azure TTS settings are incomplete. Make sure azure_key and azure_region are set in the INI file.");
        }

        public async Task SynthesizeToFileAsync(string text, string outputPath)
        {
            var config = SpeechConfig.FromSubscription(_key, _region);
            config.SpeechSynthesisVoiceName = string.IsNullOrWhiteSpace(_voice) ? "en-US-JennyNeural" : _voice;

            using var fileOutput = AudioConfig.FromWavFileOutput(outputPath);
            using var synthesizer = new SpeechSynthesizer(config, fileOutput);

            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
                throw new Exception($"TTS failed: {result.Reason}");
        }
    }
}
