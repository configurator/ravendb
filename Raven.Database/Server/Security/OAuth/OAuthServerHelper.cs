﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Raven.Abstractions.Connection;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Extensions.Internal;

namespace Raven.Database.Server.Security.OAuth
{
	internal static class OAuthServerHelper
	{
		private const int rsaKeySize = 2560;

		private static readonly ThreadLocal<RNGCryptoServiceProvider> rng = new ThreadLocal<RNGCryptoServiceProvider>(() => new RNGCryptoServiceProvider());
		private static readonly ThreadLocal<RSACryptoServiceProvider> rsa;
		private static readonly ThreadLocal<AesCryptoServiceProvider> aes;

		private static readonly string rsaExponent;
		private static readonly string rsaModulus;

		static OAuthServerHelper()
		{
			RSAParameters privateRsaParameters;
			RSAParameters publicRsaParameters;
			using (var rsaKeyGen = new RSACryptoServiceProvider(rsaKeySize))
			{
				privateRsaParameters = rsaKeyGen.ExportParameters(true);
				publicRsaParameters = rsaKeyGen.ExportParameters(false);
			}

			Tuple<byte[], byte[]> aesKeyAndIV;
			using (var aesKeyGen = new AesCryptoServiceProvider())
			{
				aesKeyAndIV = Tuple.Create(aesKeyGen.Key, aesKeyGen.IV);
			}

			rsa = new ThreadLocal<RSACryptoServiceProvider>(() =>
			{
				var result = new RSACryptoServiceProvider();
				result.ImportParameters(privateRsaParameters);
				return result;
			});

			aes = new ThreadLocal<AesCryptoServiceProvider>(() =>
			{
				var result = new AesCryptoServiceProvider();
				result.Key = aesKeyAndIV.Item1;
				result.IV = aesKeyAndIV.Item2;
				return result;
			});

			rsaExponent = OAuthHelper.BytesToString(publicRsaParameters.Exponent);
			rsaModulus = OAuthHelper.BytesToString(publicRsaParameters.Modulus);
		}

		public static string RSAExponent
		{
			get { return rsaExponent; }
		}

		public static string RSAModulus
		{
			get { return rsaModulus; }
		}

		public static byte[] RandomBytes(int count)
		{
			var result = new byte[count];
			rng.Value.GetBytes(result);
			return result;
		}

		public static string EncryptSymmetric(string data)
		{
			var bytes = Encoding.UTF8.GetBytes(data);
			using (var encryptor = aes.Value.CreateEncryptor())
			{
				var result = encryptor.TransformEntireBlock(bytes);
				return OAuthHelper.BytesToString(result);
			}
		}

		public static string DecryptSymmetric(string data)
		{
			var bytes = OAuthHelper.ParseBytes(data);
			using (var decryptor = aes.Value.CreateDecryptor())
			{
				var result = decryptor.TransformEntireBlock(bytes);
				return Encoding.UTF8.GetString(result);
			}
		}

		public static string DecryptAsymmetric(string data)
		{
			var bytes = OAuthHelper.ParseBytes(data);
			var decrypted = rsa.Value.Decrypt(bytes, true);
			return Encoding.UTF8.GetString(decrypted);
		}

		public static DateTime? ParseDateTime(string data)
		{
			DateTime result;
			if (DateTime.TryParseExact(data, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
				return result;
			else
				return null;
		}

		public static string DateTimeToString(DateTime data)
		{
			return data.ToString("O", CultureInfo.InvariantCulture);
		}
	}
}
