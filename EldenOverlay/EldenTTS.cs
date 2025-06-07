using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech;
using System.Text;
using System.IO;

public class EldenTTS
{
    private readonly string _key;
    private readonly string _region;
    private readonly string _voiceOverride;

    public EldenTTS(string settingsPath)
    {
        var settings = File.ReadAllLines(settingsPath);
        foreach (var line in settings)
        {
            if (line.StartsWith("azure_key=")) _key = line.Substring("azure_key=".Length);
            if (line.StartsWith("azure_region=")) _region = line.Substring("azure_region=".Length);
            if (line.StartsWith("azure_voice=")) _voiceOverride = line.Substring("azure_voice=".Length);
        }

        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_region))
            throw new Exception("Azure TTS settings are incomplete. Make sure azure_key and azure_region are set in the INI file.");
    }

    public async Task SynthesizeToFileAsync(string text, string outputPath, string sentiment, int character)
    {
        var config = SpeechConfig.FromSubscription(_key, _region);

        string selectedVoice = ResolveVoice(character);
        config.SpeechSynthesisVoiceName = selectedVoice;

        using var fileOutput = AudioConfig.FromWavFileOutput(outputPath);
        using var synthesizer = new SpeechSynthesizer(config, fileOutput);

        string style = MapStyle(sentiment, selectedVoice);

        string ssml = GenerateSsml(text, selectedVoice, style);
        var result = await synthesizer.SpeakSsmlAsync(ssml);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            return;

        Console.WriteLine("SSML synthesis failed, falling back to plain text synthesis...");
        // Fallback to plain text
        result = await synthesizer.SpeakTextAsync(text);

        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            Console.WriteLine("Error generating voice");
            Console.WriteLine($"Cancellation Reason: {cancellation.Reason}");

            if (cancellation.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"Error Code: {cancellation.ErrorCode}");
                Console.WriteLine($"Error Details: {cancellation.ErrorDetails}");
            }

            throw new Exception($"TTS failed: {cancellation.Reason}, {cancellation.ErrorDetails}");
        }
    }

    private string MapStyle(string sentiment, string voice)
    {
        // Sellen 
        if (voice == "en-US-SerenaMultilingualNeurall")
        {
            return sentiment switch
            {
                "general" => "serious",
                "impressed" => "excited",
                "worried" => "sad",
                "death" => "sad",
                _ => "serious"
            };
        }
        // Ranni (en-GB-SoniaNeural)
        if (voice == "en-GB-SoniaNeural")
        {
            return sentiment switch
            {
                "general" => "cheerful",
                "impressed" => "cheerful",
                "worried" => "sad",
                "death" => "sad",
                _ => "cheerful"
            };
        }

        // Blaidd 
        if (voice == "en-US-AdamMultilingualNeural")
        {
            return sentiment switch
            {
                "general" => "default",
                "impressed" => "default",
                "worried" => "default",
                "death" => "default",
                _ => "default"
            };
        }
        // Millicent 
        if (voice == "en-US-PhoebeMultilingualNeural")
        {
            return sentiment switch
            {
                "general" => "serious",      
                "impressed" => "default",     
                "worried" => "sad",
                "death" => "sad",
                _ => "serious"
            };
        }
        // Messmer (en-US-ChristopherNeural)
        if (voice == "en-GB-RyanNeural")
        {
            return sentiment switch
            {
                "general" => "sad",
                "impressed" => "sad",
                "worried" => "sad",
                "death" => "sad",
                _ => "sad"
            };
        }
        // Melina
        if (voice == "en-US-NancyMultilingualNeural")
        {
            return sentiment switch
            {
                "general" => "default",
                "impressed" => "excited",
                "worried" => "shy",
                "death" => "shy",
                _ => "shy"
            };
        }

        // Melina
        if (voice == "en-US-AvaNeural")
        {
            return sentiment switch
            {
                "general" => "default",
                "impressed" => "default",
                "worried" => "angry",
                "death" => "angry",
                _ => "default"
            };
        }

        // Fallback: plain neutral if somehow missed
        return "default";
    }

    private string ResolveVoice(int character)
    {
        if (!string.IsNullOrWhiteSpace(_voiceOverride))
            return _voiceOverride;

        return character switch
        {
            0 => "en-US-NancyMultilingualNeural",     // Melina
            1 => "en-GB-SoniaNeural",                // Ranni
            2 => "en-US-AdamMultilingualNeural",     // Blaidd
            3 => "en-US-PhoebeMultilingualNeural",   // Millicent 
            4 => "en-GB-RyanNeural",                // Messmer
            5 => "en-US-SerenaMultilingualNeural",     // Sellen
            6 => "en-US-AvaNeural",                     // Malenia
            _ => "en-US-NancyMultilingualNeural"
        };
    }

    public static string GenerateSsml(string text, string voiceName = "en-US-GuyNeural", string style = "chat", string rate = "medium", string pitch = "0%", int styleDegree = 2)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\"");
        sb.AppendLine("      xmlns:mstts=\"http://www.w3.org/2001/mstts\" xml:lang=\"en-US\">");
        sb.AppendLine($"  <voice name=\"{voiceName}\">");
        sb.AppendLine($"    <prosody rate=\"{rate}\" pitch=\"{pitch}\">");
        sb.AppendLine($"      <mstts:express-as style=\"{style}\" styledegree=\"{styleDegree}\">");
        sb.AppendLine(System.Security.SecurityElement.Escape(text));
        sb.AppendLine("      </mstts:express-as>");
        sb.AppendLine("    </prosody>");
        sb.AppendLine("  </voice>");
        sb.AppendLine("</speak>");
        return sb.ToString();
    }
}
