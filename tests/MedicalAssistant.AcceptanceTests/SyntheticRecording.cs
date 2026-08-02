using System.Text;

namespace MedicalAssistant.AcceptanceTests;

public sealed record SyntheticRecording(byte[] Content, string FileName, string ContentType)
{
    public static SyntheticRecording Wave(string canary)
    {
        if (string.IsNullOrWhiteSpace(canary))
            throw new ArgumentException("A synthetic canary is required.", nameof(canary));

        var marker = Encoding.UTF8.GetBytes(canary);
        var content = new byte[44 + marker.Length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(content, 0);
        BitConverter.GetBytes(content.Length - 8).CopyTo(content, 4);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(content, 8);
        BitConverter.GetBytes(16).CopyTo(content, 16);
        BitConverter.GetBytes((short)1).CopyTo(content, 20);
        BitConverter.GetBytes((short)1).CopyTo(content, 22);
        BitConverter.GetBytes(8_000).CopyTo(content, 24);
        BitConverter.GetBytes(16_000).CopyTo(content, 28);
        BitConverter.GetBytes((short)2).CopyTo(content, 32);
        BitConverter.GetBytes((short)16).CopyTo(content, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(content, 36);
        BitConverter.GetBytes(marker.Length).CopyTo(content, 40);
        marker.CopyTo(content, 44);
        return new SyntheticRecording(content, "synthetic-consultation.wav", "audio/wav");
    }
}
