using System;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

using Truant;
using Truant.Devices;

namespace HeartRateService
{
	class MainClass
	{
		private const string REPORTING_URL = "http://127.0.0.1:3000/api/device_reporting";
		private const byte DEVICE_ID = 0;
		private const byte NETWORK_NO = 0;
		private static bool _Exit = false;
		private const long MINIMUM_POST_INTERVAL = 2500;
		private static ConcurrentDictionary<ushort, long> LastPostTicks = new ConcurrentDictionary<ushort, long>();

		private static void handleInterrupt(object sender, ConsoleCancelEventArgs args)
		{
			args.Cancel = true;
			_Exit = true;
		}

		public static void Main(string[] args)
		{
			TextWriterTraceListener myWriter = new TextWriterTraceListener(Console.Out);
			Debug.Listeners.Add(myWriter);

			Console.CancelKeyPress += handleInterrupt;

			var connection = AntPlusConnection.GetConnection(DEVICE_ID, NETWORK_NO);

			HeartRateMonitor[] hrMonitors = { new HeartRateMonitor(), new HeartRateMonitor(), new HeartRateMonitor() };
			hrMonitors[0].Config = new DeviceConfig(12029, 1);
			hrMonitors[1].Config = new DeviceConfig(2515, 1);
			hrMonitors[2].Config = new DeviceConfig(47330, 1);

			foreach (var hrm in hrMonitors) {
				hrm.AddNewDataCallback(ProcessHeartRateData);
			}

			connection.Connect();

			for (int i = 0; i < hrMonitors.Length; i++)
				connection.AddDevice(hrMonitors[i]);

			while (!_Exit) Thread.Sleep(100);

			connection.Disconnect();
		}

		static void ProcessHeartRateData(ushort deviceId, object data)
		{
			var hrData = (HeartRateMonitor.HeartRateData)data;

			lock (LastPostTicks) {
				if (!LastPostTicks.ContainsKey(deviceId) ||
					new TimeSpan(DateTime.UtcNow.Ticks - LastPostTicks[deviceId]).TotalMilliseconds >= MINIMUM_POST_INTERVAL) {
					LastPostTicks[deviceId] = DateTime.UtcNow.Ticks;

					ThreadPool.QueueUserWorkItem((object _) => PostData(deviceId, hrData.ComputedHeartRate));
				}
			}
		}

		static void PostData(ushort id, int? value)
		{
			var json = $"{{\"type\":\"HeartRate\",\"id\":\"{id}\",\"value\":{value} }}";

			Console.WriteLine("{0}: POSTING: {1}", DateTime.Now, json);

			try {
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
			} catch (System.IO.IOException) {
				Console.WriteLine("Failed to POST to {0}", REPORTING_URL);
			} catch (WebException) {
				Console.WriteLine("Failed to POST to {0}", REPORTING_URL);
			}
		}
	}
}
