using System;
using System.Threading;
using System.Net;
using System.Text;

using Truant;
using Truant.Devices;

namespace HeartRateService
{
    class MainClass
    {
        private const string REPORTING_URL = "http://127.0.0.1:3000/api/device_reporting";
        private const byte DEVICE_ID = 0;
        private const byte NETWORK_NO = 0;
        private static byte[] NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 };
        private static bool _Exit = false;

        private static void handleInterrupt(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            _Exit = true;
        }

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(handleInterrupt);

            var connection = AntConnection.GetConnection(DEVICE_ID, NETWORK_NO, NETWORK_KEY);

            HeartRateMonitor[] hrMonitors = { new HeartRateMonitor(), new HeartRateMonitor() };
            hrMonitors[0].Config = new DeviceConfig(12029, 1);
            hrMonitors[1].Config = new DeviceConfig(47330, 1);

            connection.Connect();

            for (int i = 0; i < hrMonitors.Length; i++)
                connection.AddDevice(hrMonitors[i]);

            while (!_Exit)
            {
                for (int i = 0; i < hrMonitors.Length; i++)
                {
                    if (hrMonitors[i].Config.DeviceID != 0 &&
                        hrMonitors[i].ComputedHeartRate.HasValue &&
                        hrMonitors[i].DataReceiptTimeSpan.HasValue &&
                        hrMonitors[i].DataReceiptTimeSpan.Value.TotalMilliseconds < 1000)
                    {
                        var json = $"{{\"type\":\"HeartRate\",\"id\":\"{hrMonitors[i].Config.DeviceID}\",\"value\":{hrMonitors[i].ComputedHeartRate} }}";

                        Console.WriteLine("POSTING: {0}", json);

                        try
                        {
                            var request = WebRequest.Create(REPORTING_URL);
                            request.Method = "POST";
                            request.ContentType = "application/json";
                            var content = Encoding.ASCII.GetBytes(json);
                            request.ContentLength = content.Length;

                            var dataStream = request.GetRequestStream();
                            dataStream.Write(content, 0, content.Length);
                            dataStream.Close();

                            WebResponse response = request.GetResponse();
                            Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                        }
                        catch (System.IO.IOException)
                        {
                            Console.WriteLine("Failed to POST to {0}", REPORTING_URL);
                        }
                        catch (WebException)
                        {
                            Console.WriteLine("Failed to POST to {0}", REPORTING_URL);
                        }
                    }
                }

                Thread.Sleep(1000);
            }

            connection.Disconnect();
        }
    }
}
