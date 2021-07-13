using Open.Nat;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TcpChatApp {
	class Program {

		public static string IP = string.Empty;
		public static int port = 8888;
		public static string MyIP = string.Empty;
		private static TcpServer server;
		private static DirectoryInfo exeDir;

		public static byte[] secKey;

		///<summary> Сайты для получения публичного IP. </summary>
		private readonly static string[] ipSites = {
			"https://api.ipify.org/",
			"https://ipinfo.io/ip/",		
			"https://icanhazip.com/",
			"https://checkip.amazonaws.com/",
			"https://wtfismyip.com/text" };


		static void Main(string[] args) {
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

			// выбираем способ и получаем пароль
			Console.WriteLine("Select: 0 - open file, 1 - enter Password");
			var key = Console.ReadKey();
			if (key.KeyChar == '0') {
				// достаем ключ шифрования из файла
				exeDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				var keyFile = exeDir.GetFiles("*.txt").FirstOrDefault(x => x.Name.Contains("key.txt"));
				if (keyFile != null) {
					secKey = Encoding.UTF8.GetBytes(File.ReadAllText(keyFile.FullName, Encoding.UTF8));
					WriteMsgToConsoleSameLine($"Found key file: {Encoding.UTF8.GetString(secKey)}");
				} else {
					secKey = CryptHelper.GetNewAesKey();
					File.WriteAllText(exeDir.FullName + "/key.txt", Encoding.UTF8.GetString(secKey), Encoding.UTF8);
					WriteMsgToConsoleSameLine($"Create key file: {Encoding.UTF8.GetString(secKey)}");
				}
			} else
			if (key.KeyChar == '1') {
				Console.SetCursorPosition(0, 0);
				Console.Write(new string(' ', Console.WindowWidth));
				Console.SetCursorPosition(0, 1);
				Console.Write(new string(' ', Console.WindowWidth));
				Console.SetCursorPosition(0, 0);
				Console.Write("Password:");

				string passStr = string.Empty;
				ConsoleKeyInfo ckiPass;
				do {
					ckiPass = Console.ReadKey();
					if (ckiPass.Key == ConsoleKey.Backspace) { Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop); continue; }
					if (ckiPass.Key == ConsoleKey.Enter) break;
					passStr += ckiPass.KeyChar;
					Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
					Console.Write("*");
				} while (ckiPass.Key != ConsoleKey.Enter);
				Console.WriteLine($"Set Password: {passStr}");
				secKey = Encoding.UTF8.GetBytes(passStr);
			} else return;

			// задаем целевой IP
			Console.Write($"Target IP: ");
			IP = Console.ReadLine();

			// открываем порты
			OpenPorts();

			// определяем IP
			Console.WriteLine("Checking IP...");
			if (!IsIpLocal(IP)) MyIP = GetPublicIP();
			if (string.IsNullOrWhiteSpace(MyIP)) MyIP = GetLocalIP();
			WriteMsgToConsoleSameLine($"My IP: {MyIP}");

			// стартуем сервер приема сообщений
			Console.WriteLine("Start TCP Server.");
			server = new TcpServer();

			// читаем ввод пользователя
			while (true) {
				var line = Console.ReadLine();

				if (server.isOnline) {
					var sended = SendMessage(line);
					if (sended) {
						WriteMsgToConsoleSameLine($"[{DateTime.Now}] You: {line}");
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
			server?.Shootdown();
			Thread.Sleep(500);
			ClosePorts();
			Console.WriteLine();
			Console.WriteLine("EXIT...");
			Thread.Sleep(500);
		}

		///<summary> Вывод в консоль с учетом одновременного набора сообщения пользователем. </summary>
		public static void WriteMsgToConsole(string msg) {
			var message = Regex.Replace(msg, @"\t|\n|\r", "");	// чистим от перекатов

			int curRow = Console.CursorLeft;
			if (curRow > 0) {
				//- чтото вводилось, значит нужно это чтото сместить ниже
				//- а на его место вывести сообщение, затем вернуть курсор к концу вводимого

				Console.MoveBufferArea(0, Console.CursorTop, Console.WindowWidth, 1, 0, Console.CursorTop+1);

				Console.SetCursorPosition(0, Console.CursorTop);
				Console.Write(message);
				Console.SetCursorPosition(curRow, Console.CursorTop + 1);
			} else {
				Console.WriteLine(message);
			}
		}

		///<summary> Вывод в консоль с заменой строки. </summary>
		public static void WriteMsgToConsoleSameLine(string msg) {
			var curPos = Console.CursorTop - 1;
			Console.SetCursorPosition(0, curPos);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, curPos);
			Console.WriteLine(msg);
		}

		///<summary> Отправка сообщения. </summary>
		public static bool SendMessage(string message, bool encr = true) {
			if (string.IsNullOrWhiteSpace(message)) return false;
			var client = new TcpClient();

			try {
				client.Connect(IP, port);
			} catch (Exception ex) {
				//WriteMsgToConsoleSameLine($"Not sent: <{message}> >>> {ex.Message}");
				client.Close();
				return false;
			}

			NetworkStream Stream = client.GetStream();

			var encrMsg = message;
			if (encr && secKey != null) encrMsg = CryptHelper.AesEncryptPass(message, Encoding.UTF8.GetString(secKey));
			byte[] data = Encoding.UTF8.GetBytes($"{MyIP} {encrMsg}");
			Stream.Write(data, 0, data.Length);
			client.Close();
			return true;
		}

		//---
		private static bool isRemaped = false;
		#region Port Mapping
		///<summary> Открывает порты на сетевых устройствах через uPnP. </summary>
		private static async void OpenPorts() {
			try {
				var discoverer = new NatDiscoverer();
				var cts = new CancellationTokenSource(10000);
				var device = discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
				await device.Result.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "TCP Chat"));
			} catch (Exception) { };
			isRemaped = true;
		}

		///<summary> Закрывает порты. </summary>
		private static async void ClosePorts() {
			if (!isRemaped) return;
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

		///<summary> Является ли заданный IP локальным. </summary>
		private static bool IsIpLocal(string ip) {
			int[] ipParts = ip.Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries)
									 .Select(s => int.Parse(s)).ToArray();
			if (ipParts.Length <= 0) return false;

			// in private ip range
			if (ipParts[0] == 10 ||
				(ipParts[0] == 192 && ipParts[1] == 168) ||
				(ipParts[0] == 172 && (ipParts[1] >= 16 && ipParts[1] <= 31))) {
				return true;
			}

			// IP Address is probably public.
			// This doesn't catch some VPN ranges like OpenVPN and Hamachi.
			return false;
		}

	}
}
