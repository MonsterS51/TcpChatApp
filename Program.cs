using Open.Nat;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpChatApp {
	class Program {

		public static string IP = "128.75.172.201";
		public static int port = 8888;
		public static string MyIP = "192.168.1.100";
		private static TcpServer server;

		///<summary> Сайты для получения публичного IP. </summary>
		private readonly static string[] ipSites = {
			"https://api.ipify.org/",
			"https://ipinfo.io/ip/",		
			"https://icanhazip.com/",
			"https://checkip.amazonaws.com/",
			"https://wtfismyip.com/text" };


		static void Main(string[] args) {
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

			OpenPorts();

			MyIP = GetPublicIP();
			if (string.IsNullOrWhiteSpace(MyIP)) MyIP = GetLocalIP();

			Console.WriteLine($"My IP: {MyIP}");

			// стартуем сервер приема сообщений
			Console.WriteLine("Start TCP Server.");
			server = new TcpServer();

			//CryptHelper.EncryptAesManaged("111 testing проверка	!!! ;;;; ::::: ????");

			// читаем ввод пользователя
			while (true) {
				var line = Console.ReadLine();

				if (server.isOnline) {
					var sended = SendMessage(line);
					if (sended) {
						Console.SetCursorPosition(0, Console.CursorTop - 1);
						Console.WriteLine($"[{DateTime.Now}] You: {line}");
					} else {
						//- чистим строку
						Console.SetCursorPosition(0, Console.CursorTop - 1);
						Console.Write(new string(' ', Console.WindowWidth));
						Console.SetCursorPosition(0, Console.CursorTop);
					}
				} else {
					//- чистим строку
					Console.SetCursorPosition(0, Console.CursorTop - 1);
					Console.Write(new string(' ', Console.WindowWidth));
					Console.SetCursorPosition(0, Console.CursorTop);
				}

			}


		}


		///<summary> Операции перед выходом. </summary>
		private static void OnProcessExit(object sender, EventArgs e) {
			server.Shootdown();
			ClosePorts();
			Console.WriteLine("I'm out of here !");
			Thread.Sleep(1000);
		}

		///<summary> Вывод в консоль с учетом одновременного набора сообщения пользователем. </summary>
		public static void WriteMsgToConsole(string message) {
			int curRow = Console.CursorLeft;
			if (curRow > 0) {
				//- чтото вводилось, значит нужно это чтото сместить ниже
				//- а на его место вывести сообщение, затем вернуть курсор к концу вводимого
				Console.WriteLine("");
				Console.SetCursorPosition(0, Console.CursorTop);

#pragma warning disable CA1416 // Validate platform compatibility
				Console.MoveBufferArea(0, Console.CursorTop - 1, Console.WindowWidth, 1, 0, Console.CursorTop);
#pragma warning restore CA1416 // Validate platform compatibility

				int curLine = Console.CursorTop;
				Console.SetCursorPosition(0, curLine);
				Console.WriteLine(message);
				Console.SetCursorPosition(curRow, curLine + 1);
			} else {
				Console.WriteLine(message);
			}
		}

		///<summary> Отправка сообщения. </summary>
		public static bool SendMessage(string message) {
			if (string.IsNullOrWhiteSpace(message)) return false;
			var client = new TcpClient();

			try {
				client.Connect(Program.IP, Program.port);
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
				client.Close();
				return false;
			}

			NetworkStream Stream = client.GetStream();
			byte[] data = Encoding.Unicode.GetBytes($"{MyIP} {message}");
			Stream.Write(data, 0, data.Length);
			client.Close();
			return true;
		}

		//---

		#region Port Mapping
		///<summary> Открывает порты на сетевых устройствах через uPnP. </summary>
		private static async void OpenPorts() {
			try {
				var discoverer = new NatDiscoverer();
				var cts = new CancellationTokenSource(10000);
				var device = discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
				await device.Result.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "TCP Chat"));
			} catch (Exception) { };
		}

		///<summary> Закрывает порты. </summary>
		private static async void ClosePorts() {
			try {
				var discoverer = new NatDiscoverer();
				var cts = new CancellationTokenSource(10000);
				var device = discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
				await device.Result.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port, "TCP Chat"));
			} catch (Exception) { };
		}

		#endregion

		//---
		#region Get IP
		private static string GetPublicIP() {
			string pubIP = string.Empty;
			foreach (var url in ipSites) {
				try {
					pubIP = new System.Net.WebClient().DownloadString(url);
				} catch (Exception) { continue; }
				if (string.IsNullOrWhiteSpace(pubIP)) continue;
			}
			return pubIP.Trim();
		}

		private static string GetLocalIP() {
			string localIP = string.Empty;
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
				socket.Connect("8.8.8.8", 65530);
				IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
				localIP = endPoint.Address.ToString();
			}
			return localIP;
		}
		#endregion


	}
}
