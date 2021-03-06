﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Tests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class AddressBookEntryTest : TestBase, IAsyncLifetime
    {
        private static readonly byte[] SerializedEndpoint = new byte[] { 0x1, 0x2 };
        private static readonly byte[] Signature = new byte[] { 0x3, 0x4 };
        private readonly Mocks.MockEnvironment environment = new Mocks.MockEnvironment();
        private OwnEndpoint receivingEndpoint = null!; // InitializeAsync

        public AddressBookEntryTest(ITestOutputHelper logger)
            : base(logger)
        {
        }

        public async Task InitializeAsync()
        {
            this.receivingEndpoint = await this.environment.CreateOwnEndpointAsync(this.TimeoutToken);
        }

        public Task DisposeAsync()
        {
            this.environment.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public void Ctor_InvalidInputs()
        {
            Assert.Throws<ArgumentNullException>("endpoint", () => new AddressBookEntry(null!));
        }

        [Fact]
        public void Ctor_Serialized()
        {
            var abe = new AddressBookEntry(SerializedEndpoint, Signature);
            Assert.True(Utilities.AreEquivalent(SerializedEndpoint, abe.SerializedEndpoint.Span));
            Assert.True(Utilities.AreEquivalent(Signature, abe.Signature.Span));
        }

        [Fact]
        public void Ctor_OwnEndpoint()
        {
            var abe = new AddressBookEntry(this.receivingEndpoint);
            Assert.NotEqual(0, abe.SerializedEndpoint.Length);
            Assert.NotEqual(0, abe.Signature.Length);
        }

        [Fact]
        public void Serializability()
        {
            AddressBookEntry entry = new AddressBookEntry(SerializedEndpoint, Signature);
            AddressBookEntry deserializedEntry = SerializeRoundTrip(entry);
            Assert.Equal<byte>(entry.SerializedEndpoint.ToArray(), deserializedEntry.SerializedEndpoint.ToArray());
            Assert.Equal<byte>(entry.Signature.ToArray(), deserializedEntry.Signature.ToArray());
        }

        [Fact]
        public void ExtractEndpoint()
        {
            AddressBookEntry entry = new AddressBookEntry(this.receivingEndpoint);
            Endpoint endpoint = entry.ExtractEndpoint();
            Assert.Equal(this.receivingEndpoint.PublicEndpoint, endpoint);
        }

        [Fact]
        public void ExtractEndpointDetectsTampering()
        {
            AddressBookEntry entry = new AddressBookEntry(this.receivingEndpoint);

            byte[] untamperedEndpoint = entry.SerializedEndpoint.ToArray();
            byte[] fuzzedEndpoint = new byte[untamperedEndpoint.Length];
            for (int i = 0; i < 100; i++)
            {
                untamperedEndpoint.CopyTo(fuzzedEndpoint, 0);
                TestUtilities.ApplyFuzzing(fuzzedEndpoint, 1);
                var fuzzedEntry = new AddressBookEntry(fuzzedEndpoint, entry.Signature);
                Assert.Throws<BadAddressBookEntryException>(() => fuzzedEntry.ExtractEndpoint());
            }
        }
    }
}
