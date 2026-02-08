using System;
using System.IO;
using System.Text;

namespace SerialCommTest
{
    internal class LogManager
    {
        private static string logBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static string txtPath = "";
        private static string csvPath = "";

        private static readonly object fileLock = new object();

        public static void Initialize()
        {
            if (!Directory.Exists(logBasePath))
                { Directory.CreateDirectory(logBasePath); }

            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            txtPath = Path.Combine(logBasePath, $"Debug_{timeStamp}.txt");
            csvPath = Path.Combine(logBasePath, $"Result_{timeStamp}.csv");

            WriteCsvHeader();
        }

        public static void WriteBytes(string type, string data)
        {
            try
            {
                lock (fileLock)
                {
                    using (StreamWriter sw = new StreamWriter(txtPath, true, Encoding.UTF8))
                    {
                        string time = DateTime.Now.ToString("HH:mm:ss.fff");
                        sw.WriteLine($"[{time}] [{type}] {data}");
                    }
                }
            }
            catch { /* 실패 무시 */ }
        }

        public static void WriteResult(string command, string response, string status)
        {
            try
            {
                lock (fileLock)
                {
                    if (!File.Exists(csvPath)) WriteCsvHeader();

                    using (StreamWriter sw = new StreamWriter(csvPath, true, Encoding.Default))
                    {
                        string time = DateTime.Now.ToString("HH:mm:ss");
                        string cleanCmd = command.Replace(",", ".");
                        string cleanRes = response.Replace(",", ".");

                        sw.WriteLine($"{time},{cleanCmd},{cleanRes},{status}");
                    }
                }
            }
            catch { /* 실패 무시 */ }
        }

        private static void WriteCsvHeader()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(csvPath, false, Encoding.Default))
                {
                    sw.WriteLine("시간,보낸명령,받은응답,상태");
                }
            }
            catch { }
        }
    }
}
