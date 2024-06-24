using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace TcpChatApp {
	class Program {

		private static TcpServer server;
		private static DirectoryInfo exeDir;
		private static byte[] secKey;
		private static readonly int port = 8888;

		static void Main(string[] args) {
			Utils.ShowTitle();

			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

			// выбираем способ и получаем пароль
			Console.WriteLine(">>> Select: 1 - Use key file,  2 - Use password");

			ConsoleKeyInfo modeKI;
			do {
				modeKI = Console.ReadKey();
				Console.Write("\b \b");
			}
			while (!char.IsDigit(modeKI.KeyChar));

			if (modeKI.KeyChar == '1') {
				// достаем ключ шифрования из файла
				exeDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				var keyFile = exeDir.GetFiles("*.txt").FirstOrDefault(x => x.Name.Contains("key.txt"));
				if (keyFile != null) {
					secKey = Encoding.UTF8.GetBytes(File.ReadAllText(keyFile.FullName, Encoding.UTF8));
					Utils.WriteLineWithReplace($"Found key file <{secKey.Length} bytes>");
				} else {
					secKey = CryptHelper.GetNewAesKey();
					var path = exeDir.FullName + "key.txt";
					File.WriteAllText(path, Encoding.UTF8.GetString(secKey), Encoding.UTF8);
					Utils.WriteLineWithReplace($"Create new key file <{path}>.");
				}
			} else
			if (modeKI.KeyChar == '2') {
				Console.SetCursorPosition(0, 0);
				Console.Write(new string(' ', Console.WindowWidth));
				Console.SetCursorPosition(0, 1);
				Console.Write(new string(' ', Console.WindowWidth));
				Console.SetCursorPosition(0, 0);
				Console.Write("Password:");

				// впитываем пароль с поддержкой Backspace
				string passStr = string.Empty;
				ConsoleKeyInfo passKI;
				do {
					passKI = Console.ReadKey(true);
					if (passKI.Key == ConsoleKey.Backspace && passStr.Length > 0) {
						Console.Write("\b \b");
						passStr = passStr[0..^1];
					} else if (!char.IsControl(passKI.KeyChar)) {
						Console.Write("*");
						passStr += passKI.KeyChar;
					}
				} while (passKI.Key != ConsoleKey.Enter);

#if DEBUG
				Utils.WriteLineWithReplace($"Entered pass: {passStr}");
				#endif

				secKey = Encoding.UTF8.GetBytes(passStr);
			} else return;

			// задаем целевой IP
			Console.WriteLine();
			Console.Write($"Target IP: ");
			var strIP = Console.ReadLine();
			//var strIP = "192.168.1.100";

			if (!IPAddress.TryParse(strIP.Trim(), out var tarIP)) {
				Console.Write($"Bad IP <{tarIP}>!");
				return;
			};

			// открываем порты
			Utils.OpenPort(port);

			// определяем IP
			Console.WriteLine("Try get my IP...");
			var myIp = Utils.DetectMyIP(tarIP);
			if (myIp == null) {
				Utils.WriteLineWithReplace($"Failed detect my IP! Exit...");
				return;
			} else {
				Utils.WriteLineWithReplace($"Use address: {myIp}");
			}

			// стартуем сервер приема сообщений
			Console.WriteLine("Start TCP Server.");
			server = new TcpServer(myIp, tarIP, port, secKey);

			// читаем ввод пользователя
			while (true) {
				var line = Console.ReadLine();

				if (server.isOnline) {
					var sended = server.SendMessage(line);
					if (sended) {
						Utils.WriteLineWithReplace($"[{DateTime.Now}] You: {line}");
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
			Utils.ClosePort(port);
			Console.WriteLine();
			Console.WriteLine("EXIT...");
			Thread.Sleep(500);
		}

	}

}
