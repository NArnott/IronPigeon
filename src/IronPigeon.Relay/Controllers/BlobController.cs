﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using IronPigeon.Providers;
    using Microsoft;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    [ApiController]
    [Route("[controller]")]
    [DenyHttp]
    public class BlobController : ControllerBase
    {
        private static readonly SortedDictionary<int, TimeSpan> MaxBlobSizesAndLifetimes = new SortedDictionary<int, TimeSpan>
        {
            { 10 * 1024, TimeSpan.MaxValue }, // this is intended for address book entries.
            { 512 * 1024, TimeSpan.FromDays(7) },
        };

        private readonly AzureStorage azureStorage;
        private readonly ILogger<BlobController> logger;

        public BlobController(AzureStorage azureStorage, ILogger<BlobController> logger)
        {
            this.azureStorage = azureStorage;
            this.logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromQuery] int lifetimeInMinutes, CancellationToken cancellationToken)
        {
            Requires.Range(lifetimeInMinutes > 0, "lifetimeInMinutes");

            ////if (!this.Request.ContentLength.HasValue)
            ////{
            ////    return new StatusCodeResult(StatusCodes.Status411LengthRequired);
            ////}

            var lifetime = TimeSpan.FromMinutes(lifetimeInMinutes);
            DateTime expirationUtc = DateTime.UtcNow + lifetime;
            ////IActionResult? errorResponse = GetDisallowedLifetimeResponse(this.Request.ContentLength.Value, lifetime);
            ////if (errorResponse != null)
            ////{
            ////    return errorResponse;
            ////}

            var azureBlobStorage = new AzureBlobStorage(this.azureStorage.PayloadBlobsContainer);
            try
            {
                Uri blobUri = await azureBlobStorage.UploadMessageAsync(this.Request.Body, expirationUtc, cancellationToken: cancellationToken);
                return this.Created(blobUri, blobUri);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
            {
                // They caught us uninitialized. Ask them to try again after we mitigate the problem.
                this.logger.LogError("Request failed because blob container did not exist. Creating it now...");
                await azureBlobStorage.CreateContainerIfNotExistAsync(cancellationToken);
                return this.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private static IActionResult? GetDisallowedLifetimeResponse(long blobSize, TimeSpan lifetime)
        {
            foreach (KeyValuePair<int, TimeSpan> rule in MaxBlobSizesAndLifetimes)
            {
                if (blobSize < rule.Key)
                {
                    if (lifetime > rule.Value)
                    {
                        return new StatusCodeResult(StatusCodes.Status402PaymentRequired);
                    }

                    return null;
                }
            }

            return new StatusCodeResult(StatusCodes.Status413RequestEntityTooLarge);
        }
    }
}
