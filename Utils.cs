using Open.Nat;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TcpChatApp {
	public static class Utils {

		#region Port Mapping

		///<summary> Открывает порт на NAT устройствах через uPnP. </summary>
		public static bool OpenPort(int port) {
			try {
				var discoverer = new NatDiscoverer();

				var cts = new CancellationTokenSource(5000);
				var deviceTask = discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
				deviceTask.Wait();
				cts.Dispose();

				var task = deviceTask.Result.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "TCP Chat"));
				task.Wait();
			} catch (Exception ex) {
				Console.WriteLine($"{nameof(OpenPort)}: {ex.Message}");
				return false;
			};
			return true;
		}

		///<summary> Закрывает порт на NAT устройствах. </summary>
		public static bool ClosePort(int port) {
			try {
				var discoverer = new NatDiscoverer();

				var cts = new CancellationTokenSource(5000);
				var deviceTask = discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
				deviceTask.Wait();
				cts.Dispose();

				var task = deviceTask.Result.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port, "TCP Chat"));
				task.Wait();


			} catch (Exception ex) {
				Console.WriteLine($"{nameof(ClosePort)}: {ex.Message}");
				return false;
			};
			return true;
		}

		#endregion


		#region Console utils

		///<summary> Вывод в консоль с учетом одновременного набора сообщения пользователем. </summary>
		public static void WriteLineWithSaveInput(string msg = "") {
			try {
				var message = Regex.Replace(msg, @"\t|\n|\r", "");  // чистим от перекатов

				int curRow = Console.CursorLeft;
				if (curRow > 0) {
					//- чтото вводилось, значит нужно это чтото сместить ниже
					//- а на его место вывести сообщение, затем вернуть курсор к концу вводимого


					if (OperatingSystem.IsWindows()) {
						// доступно только на винде
						Console.MoveBufferArea(0, Console.CursorTop, Console.WindowWidth, 1, 0, Console.CursorTop + 1);
					}

					Console.SetCursorPosition(0, Console.CursorTop);
					Console.Write(message);
					Console.SetCursorPosition(curRow, Console.CursorTop + 1);
				} else {
					Console.WriteLine(message);
				}
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
			}
		}

		///<summary> Вывод в консоль с заменой строки. </summary>
		public static void WriteLineWithReplace(string msg = "") {
			var curPos = Console.CursorTop - 1;
			if (curPos < 0) curPos = 0;
			Console.SetCursorPosition(0, curPos);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, curPos);
			Console.WriteLine(msg);
		}

		#endregion



		#region IP Utils

		///<summary> Сайты для получения публичного IP. </summary>
		private readonly static string[] ipSites = {
			"https://api.ipify.org/",
			"https://ipinfo.io/ip/",
			"https://icanhazip.com/",
			"https://checkip.amazonaws.com/",
			"https://wtfismyip.com/text" };

		private static IPAddress GetPublicIP() {
			using var httpClient = new HttpClient();

			foreach (var url in ipSites) {
				try {
					var req = new HttpRequestMessage(HttpMethod.Get, url);
					using var response = httpClient.Send(req);

					if (response.StatusCode == HttpStatusCode.OK) {
						using var stream = response.Content.ReadAsStream();
						using var reader = new StreamReader(stream);
						if (IPAddress.TryParse(reader.ReadToEnd().Trim(), out var ip)) {
							return ip;
						};
					}
				} catch (Exception) {
					Console.WriteLine($"{nameof(GetPublicIP)} failed for: {url}");
					continue;
				}
			}

			return null;
		}


		///<summary> Подбираем наш локальный IP под целевой. </summary>
		private static IPAddress GuessMyLocalIP(IPAddress targetIP) {
			var ipParts = targetIP.ToString()
				.Split('.', StringSplitOptions.RemoveEmptyEntries);

			var netAddrStartStr = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.";

			var localList = NetworkInterface.GetAllNetworkInterfaces()
				.SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
				.Where(adr => adr.Address.AddressFamily == AddressFamily.InterNetwork)
				.Select(adr => adr.Address);

			return localList.FirstOrDefault(x => x.ToString().StartsWith(netAddrStartStr));
		}

		public static IPAddress DetectMyIP(IPAddress targetIP) {
			var myIP = GuessMyLocalIP(targetIP);
			myIP ??= GetPublicIP();
			return myIP;
		}

		#endregion

		public static void ShowTitle() {
			var str = Encoding.UTF8.GetString(Convert.FromBase64String(Utils.title)).ToArray();
			foreach (var ch in str) {
				if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape) {
					Console.Clear();
					return;
				}
		
				Console.Write(ch);
				if (ch != ' ' & ch != '$') Thread.Sleep(1);
			}
			Console.WriteLine();
			Thread.Sleep(1000);
			Console.Clear();
		}

		private static readonly string title = @"
DQoNCiAgLyQkJCQkJCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIA0KIC8kJF9fICAkJCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgDQp8ICQkICBcICQkIC8kJCAgLyQkICAvJCQgIC8kJCQkJCQgICAvJCQkJCQkJCAgLyQkJCQkJCAgLyQkJCQkJC8kJCQkICAgLyQkJCQkJCAgICAgICANCnwgJCQkJCQkJCR8ICQkIHwgJCQgfCAkJCAvJCRfXyAgJCQgLyQkX19fX18vIC8kJF9fICAkJHwgJCRfICAkJF8gICQkIC8kJF9fICAkJCAgICAgIA0KfCAkJF9fICAkJHwgJCQgfCAkJCB8ICQkfCAkJCQkJCQkJHwgICQkJCQkJCB8ICQkICBcICQkfCAkJCBcICQkIFwgJCR8ICQkJCQkJCQkICAgICAgDQp8ICQkICB8ICQkfCAkJCB8ICQkIHwgJCR8ICQkX19fX18vIFxfX19fICAkJHwgJCQgIHwgJCR8ICQkIHwgJCQgfCAkJHwgJCRfX19fXy8gICAgICANCnwgJCQgIHwgJCR8ICAkJCQkJC8kJCQkL3wgICQkJCQkJCQgLyQkJCQkJCQvfCAgJCQkJCQkL3wgJCQgfCAkJCB8ICQkfCAgJCQkJCQkJCAgICAgIA0KfF9fLyAgfF9fLyBcX19fX18vXF9fXy8gIFxfX19fX19fL3xfX19fX19fLyAgXF9fX19fXy8gfF9fLyB8X18vIHxfXy8gXF9fX19fX18vICAgICAgDQogLyQkJCQkJCQkLyQkJCQkJCAgLyQkJCQkJCQgICAgICAgICAgICAgICAgICAvJCQgICAgICAgICAgICAgICAgICAgLyQkICAgICAgICAgICAgICANCnxfXyAgJCRfXy8kJF9fICAkJHwgJCRfXyAgJCQgICAgICAgICAgICAgICAgfCAkJCAgICAgICAgICAgICAgICAgIHwgJCQgICAgICAgICAgICAgIA0KICAgfCAkJCB8ICQkICBcX18vfCAkJCAgXCAkJCAgICAgICAgLyQkJCQkJCR8ICQkJCQkJCQgICAvJCQkJCQkICAvJCQkJCQkICAgICAgICAgICAgDQogICB8ICQkIHwgJCQgICAgICB8ICQkJCQkJCQvICAgICAgIC8kJF9fX19fL3wgJCRfXyAgJCQgfF9fX18gICQkfF8gICQkXy8gICAgICAgICAgICANCiAgIHwgJCQgfCAkJCAgICAgIHwgJCRfX19fLyAgICAgICB8ICQkICAgICAgfCAkJCAgXCAkJCAgLyQkJCQkJCQgIHwgJCQgICAgICAgICAgICAgIA0KICAgfCAkJCB8ICQkICAgICQkfCAkJCAgICAgICAgICAgIHwgJCQgICAgICB8ICQkICB8ICQkIC8kJF9fICAkJCAgfCAkJCAvJCQgICAgICAgICAgDQogICB8ICQkIHwgICQkJCQkJC98ICQkICAgICAgICAgICAgfCAgJCQkJCQkJHwgJCQgIHwgJCR8ICAkJCQkJCQkICB8ICAkJCQkLyAgICAgICAgICANCiAgIHxfXy8gIFxfX19fX18vIHxfXy8gICAgICAgICAgICAgXF9fX19fX18vfF9fLyAgfF9fLyBcX19fX19fXy8gICBcX19fLyAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgDQo=
";


	}
}
