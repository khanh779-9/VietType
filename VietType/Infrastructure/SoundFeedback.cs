using System;
using System.IO;
using System.Media;

namespace VietType.Infrastructure;

/// <summary>
/// Âm thanh phản hồi tổng hợp tại chỗ (sine wave trong bộ nhớ, không cần file .wav).
/// </summary>
public static class SoundFeedback
{
    public static bool Enabled { get; set; } = true;

    private const int SampleRate = 44100;

    /// <summary>Bật bộ gõ: chime đi lên (D5 → A5).</summary>
    public static void PlayToggleOn() => Play(BuildWav(new[] { Tone(587, 90), Tone(880, 130) }));

    /// <summary>Tắt bộ gõ: chime đi xuống (A5 → D5).</summary>
    public static void PlayToggleOff() => Play(BuildWav(new[] { Tone(880, 90), Tone(587, 130) }));

    /// <summary>Chuyển kiểu gõ / bảng mã / tuỳ chọn: tick trung tính ngắn (F#5).</summary>
    public static void PlaySwitch() => Play(BuildWav(new[] { Tone(740, 80) }));

    private static short[] Tone(int freq, int ms)
    {
        int n = SampleRate * ms / 1000;
        var samples = new short[n];
        int fadeIn = SampleRate / 100;
        int fadeOut = SampleRate / 50;
        for (int i = 0; i < n; i++)
        {
            double env = Math.Min(1.0, Math.Min((double)i / fadeIn, (double)(n - 1 - i) / fadeOut));
            double value = Math.Sin(2 * Math.PI * freq * i / (double)SampleRate);
            samples[i] = (short)(value * 0.32 * short.MaxValue * env);
        }
        return samples;
    }

    private static byte[] BuildWav(short[][] segments)
    {
        int totalSamples = 0;
        foreach (var s in segments) totalSamples += s.Length;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + totalSamples * 2);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);              // PCM
        w.Write((short)1);              // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);        // byte rate
        w.Write((short)2);              // block align
        w.Write((short)16);             // bits per sample
        w.Write("data"u8);
        w.Write(totalSamples * 2);
        foreach (var s in segments)
        {
            foreach (var v in s) w.Write(v);
        }
        return ms.ToArray();
    }

    private static void Play(byte[] wav)
    {
        if (!Enabled) return;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                using var stream = new MemoryStream(wav);
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch
            {
            }
        })
        {
            IsBackground = true,
            Name = "VietType-Sound"
        };
        thread.Start();
    }
}
