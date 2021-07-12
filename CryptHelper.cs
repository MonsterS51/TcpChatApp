using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TcpChatApp {
	/// <summary>
	/// Вспомогательный класс шифрования.
	/// Использовано с доработкой:
	/// https://stackoverflow.com/questions/8041451/good-aes-initialization-vector-practice (paultechguy)
	/// </summary>
	public class CryptHelper {

		private const int KeySizes = 256;
		private const int BlockSize = 128;
		private const int AesKeySize = KeySizes / 8;
		private const int AesIvSize = BlockSize / 8;

		public static void EncryptAesManaged(string raw) {
			try {
				// Create Aes that generates a new key and initialization vector (IV).    
				// Same key must be used in encryption and decryption    
				using (AesManaged aes = new AesManaged() {
					Mode = CipherMode.CBC,
					Padding = PaddingMode.PKCS7,
					KeySize = KeySizes,
					BlockSize = BlockSize
				}) {
					aes.GenerateKey();
					Console.WriteLine($"Key ({aes.Key.Length}): {Encoding.UTF8.GetString(aes.Key)}");

					var encrypted = AesEncrypt(raw, aes.Key);
					Console.WriteLine($"Encrypted data: {encrypted}");
					string decrypted = AesDecrypt(encrypted, aes.Key);
					Console.WriteLine($"Decrypted data: {decrypted}");
				}
			} catch (Exception exp) {
				Console.WriteLine(exp.Message);
			}
			Console.ReadKey();
		}


		//---

		static string AesEncrypt(string data, string key) {
			return AesEncrypt(data, Encoding.UTF8.GetBytes(key));
		}

		static string AesDecrypt(string data, string key) {
			return AesDecrypt(data, Encoding.UTF8.GetBytes(key));
		}

		static string AesEncrypt(string data, byte[] key) {
			return Convert.ToBase64String(AesEncrypt(Encoding.UTF8.GetBytes(data), key));
		}

		static string AesDecrypt(string data, byte[] key) {
			return Encoding.UTF8.GetString(AesDecrypt(Convert.FromBase64String(data), key));
		}

		static byte[] AesEncrypt(byte[] data, byte[] key) {
			if (data == null || data.Length <= 0) {
				throw new ArgumentNullException($"{nameof(data)} cannot be empty");
			}

			if (key == null || key.Length != AesKeySize) {
				throw new ArgumentException($"{nameof(key)} must be length of {AesKeySize} but {key.Length}");
			}

			using (var aes = new AesCryptoServiceProvider {
				Mode = CipherMode.CBC,
				Padding = PaddingMode.PKCS7,
				KeySize = KeySizes,
				BlockSize = BlockSize,
				Key = key
			}) {
				aes.GenerateIV();
				var iv = aes.IV;
				using (var encrypter = aes.CreateEncryptor(aes.Key, iv))
				using (var cipherStream = new MemoryStream()) {
					using (var tCryptoStream = new CryptoStream(cipherStream, encrypter, CryptoStreamMode.Write))
					using (var tBinaryWriter = new BinaryWriter(tCryptoStream)) {
						// prepend IV to data
						cipherStream.Write(iv);
						tBinaryWriter.Write(data);
						tCryptoStream.FlushFinalBlock();
					}
					var cipherBytes = cipherStream.ToArray();

					return cipherBytes;
				}
			}
		}

		static byte[] AesDecrypt(byte[] data, byte[] key) {
			if (data == null || data.Length <= 0) {
				throw new ArgumentNullException($"{nameof(data)} cannot be empty");
			}

			if (key == null || key.Length != AesKeySize) {
				throw new ArgumentException($"{nameof(key)} must be length of {AesKeySize} but {key.Length}");
			}

			using (var aes = new AesCryptoServiceProvider {
				Mode = CipherMode.CBC,
				Padding = PaddingMode.PKCS7,
				KeySize = KeySizes,
				BlockSize = BlockSize,
				Key = key
			}) {
				// get first KeySize bytes of IV and use it to decrypt
				var iv = new byte[AesIvSize];
				Array.Copy(data, 0, iv, 0, iv.Length);

				using (var ms = new MemoryStream()) {
					using (var cs = new CryptoStream(ms, aes.CreateDecryptor(aes.Key, iv), CryptoStreamMode.Write))
					using (var binaryWriter = new BinaryWriter(cs)) {
						// decrypt cipher text from data, starting just past the IV
						binaryWriter.Write(
							data,
							iv.Length,
							data.Length - iv.Length
						);
					}

					var dataBytes = ms.ToArray();

					return dataBytes;
				}
			}
		}

	}
}
