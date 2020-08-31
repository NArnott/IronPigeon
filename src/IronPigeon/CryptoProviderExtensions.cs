﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft;
    using PCLCrypto;

    /// <summary>
    /// Extension methods to the <see cref="CryptoSettings"/> interface.
    /// </summary>
    public static class CryptoProviderExtensions
    {
        /// <summary>
        /// Creates a web safe base64 thumbprint of some buffer.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>A string representation of a hash of the <paramref name="buffer"/>.</returns>
        public static string CreateWebSafeBase64Thumbprint(this CryptoSettings cryptoProvider, byte[] buffer)
        {
            Requires.NotNull(cryptoProvider, nameof(cryptoProvider));
            Requires.NotNull(buffer, nameof(buffer));

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(cryptoProvider.HashAlgorithm);
            var hash = hasher.HashData(buffer);
            return Utilities.ToBase64WebSafe(hash);
        }

        /// <summary>
        /// Determines whether a given thumbprint matches the actual hash of the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="allegedHashWebSafeBase64Thumbprint">The web-safe base64 encoding of the thumbprint that the specified buffer's thumbprint is expected to match.</param>
        /// <returns><c>true</c> if the thumbprints match; <c>false</c> otherwise.</returns>
        /// <exception cref="System.NotSupportedException">If the length of the thumbprint is not consistent with any supported hash algorithm.</exception>
        public static bool IsThumbprintMatch(byte[] buffer, string allegedHashWebSafeBase64Thumbprint)
        {
            Requires.NotNull(buffer, nameof(buffer));
            Requires.NotNullOrEmpty(allegedHashWebSafeBase64Thumbprint, nameof(allegedHashWebSafeBase64Thumbprint));

            byte[] allegedThumbprint = Convert.FromBase64String(Utilities.FromBase64WebSafe(allegedHashWebSafeBase64Thumbprint));
            HashAlgorithm hashAlgorithm = Utilities.GuessHashAlgorithmFromLength(allegedThumbprint.Length);

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(hashAlgorithm);
            var actualThumbprint = hasher.HashData(buffer);
            return Utilities.AreEquivalent(actualThumbprint, allegedThumbprint);
        }

        /// <summary>
        /// Gets the name of the hash algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm.</param>
        /// <returns>A non-empty string.</returns>
        public static string GetHashAlgorithmName(this HashAlgorithm algorithm)
        {
            return algorithm.ToString();
        }

        /// <summary>
        /// Parses the name of the hash algorithm into an enum.
        /// </summary>
        /// <param name="algorithmName">Name of the algorithm. Null is allowed.</param>
        /// <returns>The parsed form, or null if <paramref name="algorithmName"/> is null.</returns>
        public static HashAlgorithm? ParseHashAlgorithmName(string algorithmName)
        {
            if (algorithmName == null)
            {
                return null;
            }

            return (HashAlgorithm)Enum.Parse(typeof(HashAlgorithm), algorithmName, ignoreCase: true);
        }

        /// <summary>
        /// Computes the hash of the specified buffer and checks for a match to an expected hash.
        /// </summary>
        /// <param name="cryptoProvider">The crypto provider.</param>
        /// <param name="data">The data to hash.</param>
        /// <param name="expectedHash">The expected hash.</param>
        /// <param name="hashAlgorithm">The hash algorithm.</param>
        /// <returns>
        ///   <c>true</c> if the hashes came out equal; <c>false</c> otherwise.
        /// </returns>
        internal static bool IsHashMatchWithTolerantHashAlgorithm(this CryptoSettings cryptoProvider, byte[] data, byte[] expectedHash, HashAlgorithm? hashAlgorithm)
        {
            Requires.NotNull(cryptoProvider, nameof(cryptoProvider));
            Requires.NotNull(data, nameof(data));
            Requires.NotNull(expectedHash, nameof(expectedHash));

            if (!hashAlgorithm.HasValue)
            {
                hashAlgorithm = Utilities.GuessHashAlgorithmFromLength(expectedHash.Length);
            }

            IHashAlgorithmProvider? hasher = WinRTCrypto.HashAlgorithmProvider.OpenAlgorithm(hashAlgorithm.Value);
            byte[] actualHash = hasher.HashData(data);
            return Utilities.AreEquivalent(expectedHash, actualHash);
        }

        /// <summary>
        /// Gets the signature provider.
        /// </summary>
        /// <param name="hashAlgorithm">The hash algorithm to use.</param>
        /// <returns>The asymmetric key provider.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the arguments are not supported.</exception>
        internal static AsymmetricAlgorithm GetSignatureProvider(string hashAlgorithm)
        {
            switch (hashAlgorithm)
            {
                case "SHA1":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha1;
                case "SHA256":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha256;
                case "SHA384":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha384;
                case "SHA512":
                    return AsymmetricAlgorithm.RsaSignPkcs1Sha512;
                default:
                    throw new NotSupportedException($"The hash algorithm \"{hashAlgorithm}\" is not supported.");
            }
        }
    }
}
