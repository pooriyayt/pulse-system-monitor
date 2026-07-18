using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// پخش صدای کوتاه و جذاب برای End Task — یک چایم دو‌نُتی سنتزشده در حافظه.
    /// هیچ فایل صوتی و وابستگی خارجی لازم ندارد.
    /// </summary>
    public static class SoundHelper
    {
        private static byte[]? _endTaskWav;

        /// <summary>پخش غیرمسدودکننده‌ی صدای End Task</summary>
        public static void PlayEndTask()
        {
            try
            {
                _endTaskWav ??= BuildEndTaskWav();
                PlaySound(_endTaskWav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch { }
        }

        /// <summary>
        /// ساخت WAV در حافظه: سوییپ نزولی ۸۸۰→۳۳۰ هرتز با هارمونیک و دیکِی نمایی +
        /// یک «پاپ» کوتاه در انتها. حس «بسته شدن» می‌دهد.
        /// </summary>
        private static byte[] BuildEndTaskWav()
        {
            const int rate = 44100;
            const double dur = 0.32;
            int n = (int)(rate * dur);
            var samples = new short[n];

            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double prog = t / dur;

                // فرکانس از 880 به 330 هرتز سر می‌خورد (سوییپ نمایی)
                double freq = 880.0 * Math.Pow(330.0 / 880.0, prog);
                phase += 2 * Math.PI * freq / rate;

                // نُت اصلی + هارمونیک ملایم
                double v = Math.Sin(phase) * 0.7 + Math.Sin(phase * 2) * 0.18;

                // دیکِی نمایی + اتک نرم چند میلی‌ثانیه‌ای (بدون کلیک شروع)
                double env = Math.Exp(-5.5 * prog) * Math.Min(1.0, t / 0.008);

                // «پاپ» ظریف انتهایی
                if (prog > 0.82)
                {
                    double pt = (prog - 0.82) / 0.18;
                    v += Math.Sin(2 * Math.PI * 1320 * t) * Math.Exp(-14 * pt) * 0.25;
                }

                samples[i] = (short)(Math.Clamp(v * env, -1, 1) * short.MaxValue * 0.45);
            }

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataLen = n * 2;
            w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8); w.Write(dataLen);
            foreach (var s in samples) w.Write(s);
            w.Flush();
            return ms.ToArray();
        }

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_MEMORY = 0x0004;

        [DllImport("winmm.dll")]
        private static extern bool PlaySound(byte[] data, IntPtr hmod, uint flags);
    }
}
