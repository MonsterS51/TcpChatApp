using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpChatApp {
	public class TcpServer {

		public TcpServer(IPAddress MyIP, IPAddress TargetIP, int Port = 8888, byte[] SecKey = null) {
			this.myIP = MyIP;
			this.targetIP = TargetIP;
			this.port = Port;
			this.secKey = Encoding.UTF8.GetString(SecKey);
			Start();
		}

		private readonly IPAddress myIP;
		private readonly IPAddress targetIP;
		private readonly int port;
		private readonly string secKey;

		public static readonly string CONN_OK = "CONN_OK";
		public static readonly string CONN_OFF = "CONN_OFF";

		public bool isOnline = false;
		private TcpListener tcpListener;
		private Stopwatch swConnectionPing;
		private CancellationTokenSource cts;

		private static readonly string noConnMsg = "Waiting for connection";

		///<summary> Запуск сервера. </summary>
		private void Start() {
			swConnectionPing = new Stopwatch();
			swConnectionPing.Start();

			// запускаем прослушку TCP
			tcpListener = new TcpListener(IPAddress.Any, port);
			tcpListener.Start();

			cts = new CancellationTokenSource();
			var ct = cts.Token;

			// запускаем поток получения сообщений
			Task.Factory.StartNew(() => {
				Console.WriteLine(noConnMsg + " ...");

				while (!ct.IsCancellationRequested) {
					try {
						if (tcpListener.Pending()) {
							using var tcpClient = tcpListener.AcceptTcpClient();
							using var netStream = tcpClient.GetStream();
							string message = GetMessage(netStream);
							var remoteIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;

							if (!remoteIP.Equals(targetIP)) {
								Utils.WriteLineWithSaveInput($"[{DateTime.Now}] [Wrong {remoteIP} in chat!]");
								return;
							}

							ProcessIncomeMessage(message, remoteIP);
						}
					} catch (Exception ex) {
						Console.WriteLine(ex.Message);
					}
					Thread.Sleep(100);
				}

			}, ct);

			// запуск мониторинга подключения
			Task.Factory.StartNew(() => {
				while (!ct.IsCancellationRequested) {
					if (swConnectionPing.ElapsedMilliseconds > 10000) {
						if (isOnline) Console.WriteLine();  // чтобы не затирать написанное
						isOnline = false;
						Utils.WriteLineWithReplace($"{noConnMsg} ({swConnectionPing.ElapsedMilliseconds / 1000}s)");
					}
					Thread.Sleep(5000);
				}
			}, ct);

			// запускаем отправку CONN_OK на целевой IP
			Task.Factory.StartNew(() => {
				while (!ct.IsCancellationRequested) {
					var sended = SendMessage(CONN_OK, false);
					if (!sended) Utils.WriteLineWithReplace("Failed send CONN_OK msg :(");
					Thread.Sleep(2000);
				};
			}, ct);

		}


		///<summary> Получить текст сообщения из потока. </summary>
		private static string GetMessage(NetworkStream Stream) {
			if (Stream == null) return string.Empty;
			byte[] data = new byte[64];
			using var ms = new MemoryStream();
			int numBytesRead;
			while ((numBytesRead = Stream.Read(data, 0, data.Length)) > 0) {
				ms.Write(data, 0, numBytesRead);
			}
			var str = Encoding.UTF8.GetString(ms.ToArray(), 0, (int)ms.Length);
			return str;
		}

		///<summary> Обработка входящего сообщения. </summary>
		private void ProcessIncomeMessage(string msg, IPAddress remoteIP) {
			var msgText = msg.Trim();

			// проверяем служебные сообщения
			if (msgText == CONN_OK) {
				if (!isOnline) {
					isOnline = true;
					Utils.WriteLineWithSaveInput();
					Utils.WriteLineWithSaveInput($"[{DateTime.Now}] [{remoteIP}] : Connection OK");
				}
				swConnectionPing.Restart();
				return;
			}

			if (msgText == CONN_OFF) {
				if (isOnline) {
					isOnline = false;
					Utils.WriteLineWithSaveInput($"[{DateTime.Now}] [{remoteIP}] : Connection OFF");
					Utils.WriteLineWithSaveInput();
				}
				return;
			}

			// просто получили текстовое сообщение
			try {
				if (!string.IsNullOrWhiteSpace(secKey)) {
					msgText = CryptHelper.AesDecryptPass(msgText, secKey);
				}
			} catch (Exception ex) {
				Utils.WriteLineWithSaveInput($"[{DateTime.Now}] Failed decrypt: {ex.Message}");
			}

			Utils.WriteLineWithSaveInput($"[{DateTime.Now}] >>>: {msgText}");
			Console.Beep();
			isOnline = true;
		}


		///<summary> Отправка сообщения. </summary>
		public bool SendMessage(string message, bool useEncr = true) {
			if (string.IsNullOrWhiteSpace(message)) return false;

			var encrMsg = message;

			if (useEncr && !string.IsNullOrWhiteSpace(secKey)) { 
				encrMsg = CryptHelper.AesEncryptPass(message, secKey); 
			}
			var data = Encoding.UTF8.GetBytes(encrMsg);

			try {
				using var tcpClient = new TcpClient();
				tcpClient.Connect(targetIP, port);
				using var netStream = tcpClient.GetStream();
				netStream.Write(data);
			} catch (SocketException) {
				// давим сообщение об отсутствии ответа
				return false;
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
				return false;
			}

			return true;
		}

		///<summary> Остановка сервера. </summary>
		public void Shootdown() {
			SendMessage(CONN_OFF, false);
			cts.Cancel();
			cts.Dispose();
			isOnline = false;
			tcpListener = null;
			swConnectionPing.Stop();
		}

	}
}
