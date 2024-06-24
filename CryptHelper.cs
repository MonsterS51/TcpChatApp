using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TcpChatApp {
	/// <summary>
	/// Вспомогательный класс шифрования.
	/// Использовано с доработкой: https://stackoverflow.com/questions/8041451/good-aes-initialization-vector-practice (paultechguy)
	/// Цепляем IV к сообщению в начале до шифрования и вытягиваем при дешифровке. Пароль прогоняем через PBKDF2 с выцепленным IV.
	/// </summary>
	public static class CryptHelper {

		private const int KeySizes = 256;
		private const int BlockSize = 128;
		private const int AesKeySize = KeySizes / 8;
		private const int AesIvSize = BlockSize / 8;

		public static string AesEncryptPass(string data, string pass) {
			return Convert.ToBase64String(EncryptStringToBytes_Aes(Encoding.UTF8.GetBytes(data), pass));
		}

		public static string AesDecryptPass(string data, string pass) {
			return Encoding.UTF8.GetString(DecryptStringFromBytes_Aes(Convert.FromBase64String(data), pass));
		}

		///<summary> Хэшируем пароль через PBKDF2. </summary>
		private static byte[] HashPassPbkdf2(string password, byte[] salt) {
			return KeyDerivation.Pbkdf2(
				password: password,
				salt: salt,
				prf: KeyDerivationPrf.HMACSHA1,
				iterationCount: 10000,
				numBytesRequested: AesKeySize);
		}

		private static void ConfigAes(Aes aesAlg) {
			aesAlg.Mode = CipherMode.CBC;
			aesAlg.Padding = PaddingMode.PKCS7;
			aesAlg.KeySize = KeySizes;
			aesAlg.BlockSize = BlockSize;
		}

		public static byte[] GetNewAesKey() {
			using var aesAlg = Aes.Create();
			ConfigAes(aesAlg);
			aesAlg.GenerateKey();
			return aesAlg.Key;
		}

		private static byte[] EncryptStringToBytes_Aes(byte[] data, string pass) {
			if (data == null || data.Length <= 0)
				throw new ArgumentNullException(nameof(data));
			if (string.IsNullOrWhiteSpace(pass))
				throw new ArgumentNullException(nameof(pass));

			using var aesAlg = Aes.Create();
			ConfigAes(aesAlg);
			aesAlg.GenerateIV();
			aesAlg.Key = HashPassPbkdf2(pass, aesAlg.IV);

			using var cipherStream = new MemoryStream();
			// цепляем IV до шмфрованных данных
			cipherStream.Write(aesAlg.IV);

			using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
			using (var tCryptoStream = new CryptoStream(cipherStream, encryptor, CryptoStreamMode.Write))
			using (var tBinaryWriter = new BinaryWriter(tCryptoStream)) {
				tBinaryWriter.Write(data);
				tCryptoStream.FlushFinalBlock();
			}

			var cipherBytes = cipherStream.ToArray();

			return cipherBytes;
		}

		private static byte[] DecryptStringFromBytes_Aes(byte[] data, string pass) {
			if (data == null || data.Length <= 0)
				throw new ArgumentNullException(nameof(data));
			if (string.IsNullOrWhiteSpace(pass))
				throw new ArgumentNullException(nameof(pass));

			using var aesAlg = Aes.Create();
			ConfigAes(aesAlg);

			// готовим массив под отрезание и режем прицепленный IV
			var prependIV = new byte[AesIvSize];
			Array.Copy(data, 0, prependIV, 0, prependIV.Length);
			aesAlg.Key = HashPassPbkdf2(pass, prependIV);
			aesAlg.IV = prependIV;

			using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
			using var ms = new MemoryStream();
			using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
			using (var binaryWriter = new BinaryWriter(cs)) {
				// декриптим всё после прицепленного IV
				binaryWriter.Write(
					data, 
					prependIV.Length, 
					data.Length - prependIV.Length
				);
			}

			var dataBytes = ms.ToArray();
			return dataBytes;
		}

	}

}
