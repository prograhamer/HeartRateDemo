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
		private static byte [] NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 };
		private static bool _Exit = false;

		private static void handleInterrupt(object sender, ConsoleCancelEventArgs args)
		{
			args.Cancel = true;
			_Exit = true;
		}

		public static void Main (string[] args)
		{
			Console.CancelKeyPress += new ConsoleCancelEventHandler(handleInterrupt);

			var connection = AntPlusConnection.GetConnection(DEVICE_ID, NETWORK_NO, NETWORK_KEY);
			var hrMonitor = new HeartRateMonitor();

			connection.Connect();

			connection.AddDevice(hrMonitor);

			while(!_Exit)
			{
				if(hrMonitor.Config.DeviceID != null && hrMonitor.ComputedHeartRate != null) {
					var json = $"{{\"type\":\"HeartRate\",\"id\":\"{hrMonitor.Config.DeviceID}\",\"value\":{hrMonitor.ComputedHeartRate} }}";

					Console.WriteLine("POSTING: {0}", json);

					try {
						var request = WebRequest.Create(REPORTING_URL);
						request.Method = "POST";
						request.ContentType = "application/json";
						var content = Encoding.ASCII.GetBytes (json);
						request.ContentLength = content.Length;

						var dataStream = request.GetRequestStream ();
						dataStream.Write (content, 0, content.Length);
						dataStream.Close ();

						WebResponse response = request.GetResponse();  
						Console.WriteLine (((HttpWebResponse)response).StatusDescription);
					} catch (System.IO.IOException) {
						Console.WriteLine ("Failed to POST to {0}", REPORTING_URL);
					} catch (WebException) {
						Console.WriteLine ("Failed to POST to {0}", REPORTING_URL);
					}

					Thread.Sleep(1000);
				}
			}

			connection.Disconnect();
		}
	}
}
