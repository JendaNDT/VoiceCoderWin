using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CodePlanner.Core
{
    /// <summary>
    /// Zprostředkovává nahrávání zvuku z mikrofonu na Windows pomocí MCI (winmm.dll).
    /// </summary>
    public static class HlasovyVstup
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        private static bool _nahravam = false;

        public static void SpustNahravani()
        {
            if (_nahravam) return;

            // zavřeme předchozí instanci pro jistotu
            mciSendString("close recsound", null, 0, IntPtr.Zero);

            // otevřeme nové nahrávání
            mciSendString("open new type waveaudio alias recsound", null, 0, IntPtr.Zero);
            
            // nastavíme kvalitu: 16-bit, 16000 Hz, mono (ideální pro speech-to-text)
            mciSendString("set recsound bitspersample 16", null, 0, IntPtr.Zero);
            mciSendString("set recsound samplespersec 16000", null, 0, IntPtr.Zero);
            mciSendString("set recsound channels 1", null, 0, IntPtr.Zero);
            
            // spustíme nahrávání
            mciSendString("record recsound", null, 0, IntPtr.Zero);
            _nahravam = true;
        }

        public static string ZastavNahravani()
        {
            if (!_nahravam) return null;

            string cesta = Path.Combine(Path.GetTempPath(), "voice_input.wav");
            
            // pokud soubor existuje, smažeme ho
            try { if (File.Exists(cesta)) File.Delete(cesta); } catch { }

            // zastavíme nahrávání
            mciSendString("stop recsound", null, 0, IntPtr.Zero);
            
            // uložíme do souboru
            int err = mciSendString($"save recsound \"{cesta}\"", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);
            
            _nahravam = false;

            if (err != 0 || !File.Exists(cesta)) return null;
            return cesta;
        }
    }
}
