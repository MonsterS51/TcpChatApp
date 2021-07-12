﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpChatApp {
	public class TcpServer {

		public TcpServer() {
			Start();
		}

		public static string CONN_OK = "CONN_OK";
		public static string CONN_OFF = "CONN_OFF";

		public bool isOnline = false;
		private TcpListener tcpListener;
		private TcpClient tcpClient;
		private Stopwatch swConnOK;
		private bool isRun = false;

		///<summary> Запуск сервера. </summary>
		private void Start() {
			isRun = true;
			swConnOK = new Stopwatch();
			swConnOK.Start();

			// запускаем прослушку TCP
			tcpListener = new TcpListener(IPAddress.Any, Program.port);
			tcpListener.Start();

			// запускаем поток получения сообщений
			Task.Factory.StartNew(() => {
				Console.WriteLine("Waiting for a connection... ");
				while (isRun) {
					try {

						tcpClient = tcpListener.AcceptTcpClient();
						var Stream = tcpClient.GetStream();
						string message = GetMessage(Stream);
						var IP = $"{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}";
						ProcessMessage(message, IP);
						tcpClient.Close();
					} catch (Exception ex) {
						Console.WriteLine(ex);
						tcpClient?.Close();
					}
				}

			});

			// запуск мониторинга подключения
			Task.Factory.StartNew(() => {
				while (isRun) {
					if (swConnOK.ElapsedMilliseconds > 10000) {
						isOnline = false;
						Program.WriteMsgToConsole($"No connection for {swConnOK.ElapsedMilliseconds/1000}s");
					}
					Thread.Sleep(5000);
				}
			});

			// запускаем отправку CONN_OK на целевой IP
			Task.Factory.StartNew(() => {
				while (true) {
					Program.SendMessage(CONN_OK);
					Thread.Sleep(5000);
				};
			});

		}


		///<summary> Получить текст сообщения из потока. </summary>
		private string GetMessage(NetworkStream Stream) {
			byte[] data = new byte[64]; // буфер для получаемых данных
			StringBuilder builder = new StringBuilder();
			do {
				var bytes = Stream.Read(data, 0, data.Length);
				builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
			}
			while (Stream.DataAvailable);
			return builder.ToString();
		}

		///<summary> Обработка сообщения. </summary>
		private void ProcessMessage(string msg, string sender) {
			var msgText = msg;

			// отделяем IP отправившего от сообщения
			var ipEnd = msgText.IndexOf(' ');
			var clientIP = msgText.Substring(0, ipEnd);
			if (clientIP.Contains('.')) {
				//раз есть точки - похоже на IP
				msgText = msgText.Remove(0, ipEnd).Trim();
			} else {
				clientIP = sender;
			}

			// проверяем служебные сообщения
			if (msgText == CONN_OK) {
				if (!isOnline) {
					isOnline = true;
					Program.WriteMsgToConsole($"[{DateTime.Now}] [{clientIP}] : Connection OK");
				}
				swConnOK.Restart();
				return;
			}

			if (msgText == CONN_OFF) {
				if (!isOnline) {
					isOnline = false;
					Program.WriteMsgToConsole($"[{DateTime.Now}] [{clientIP}] : Connection OFF");
				}
				return;
			}

			// просто получили текстовое сообщение
			Program.WriteMsgToConsole($"[{DateTime.Now}] [{clientIP}] : {msgText}");
			Console.Beep();
			isOnline = true;
		}

		///<summary> Остановка сервера. </summary>
		public void Shootdown() {
			Program.SendMessage(CONN_OFF);
			isRun = false;
			isOnline = false;
			tcpClient?.Close();
			tcpListener = null;
			swConnOK.Stop();
		}

	}
}
