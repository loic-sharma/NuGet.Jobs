﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryDataCache : IAuxiliaryDataCache
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly IAuxiliaryFileClient _client;
        private readonly ILogger<AuxiliaryDataCache> _logger;
        private AuxiliaryData _data;

        public AuxiliaryDataCache(
            IAuxiliaryFileClient client,
            ILogger<AuxiliaryDataCache> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Initialized => _data != null;

        public async Task EnsureInitializedAsync()
        {
            if (!Initialized)
            {
                await LoadAsync(Timeout.InfiniteTimeSpan, shouldReload: false, token: CancellationToken.None);
            }
        }

        public async Task TryLoadAsync(CancellationToken token)
        {
            await LoadAsync(TimeSpan.Zero, shouldReload: true, token: token);
        }

        private async Task LoadAsync(TimeSpan timeout, bool shouldReload, CancellationToken token)
        {
            var acquired = false;
            try
            {
                acquired = await _lock.WaitAsync(timeout, token);
                if (!acquired)
                {
                    _logger.LogInformation("Another thread is already reloading the auxiliary data.");
                }
                else
                {
                    if (!shouldReload && Initialized)
                    {
                        return;
                    }

                    _logger.LogInformation("Starting the reload of auxiliary data.");

                    var stopwatch = Stopwatch.StartNew();

                    // Load the auxiliary files in parallel.
                    var downloadsTask = LoadAsync(_data?.Downloads, _client.LoadDownloadsAsync);
                    var verifiedPackagesTask = LoadAsync(_data?.VerifiedPackages, _client.LoadVerifiedPackagesAsync);
                    await Task.WhenAll(downloadsTask, verifiedPackagesTask);
                    var downloads = await downloadsTask;
                    var verifiedPackages = await verifiedPackagesTask;

                    // Keep track of what was actually reloaded and what didn't change.
                    var reloadedNames = new List<string>();
                    var notModifiedNames = new List<string>();
                    (ReferenceEquals(_data?.Downloads, downloads) ? notModifiedNames : reloadedNames).Add(nameof(_data.Downloads));
                    (ReferenceEquals(_data?.VerifiedPackages, verifiedPackages) ? notModifiedNames : reloadedNames).Add(nameof(_data.VerifiedPackages));

                    // Reference assignment is atomic, so this is what makes the data available for readers.
                    _data = new AuxiliaryData(downloads, verifiedPackages);

                    stopwatch.Stop();

                    _logger.LogInformation(
                        "Done reloading auxiliary data. Took {Duration}. Reloaded: {Reloaded}. Not modified: {NotModified}",
                        stopwatch.Elapsed,
                        reloadedNames,
                        notModifiedNames);
                }
            }
            finally
            {
                if (acquired)
                {
                    _lock.Release();
                }
            }
        }

        private async Task<AuxiliaryFileResult<T>> LoadAsync<T>(
            AuxiliaryFileResult<T> previousResult,
            Func<string, Task<AuxiliaryFileResult<T>>> getResult) where T : class
        {
            await Task.Yield();
            var inputETag = previousResult?.Metadata.ETag;
            var newResult = await getResult(inputETag);
            if (newResult.NotModified)
            {
                return previousResult;
            }
            else
            {
                return newResult;
            }
        }

        public IAuxiliaryData Get()
        {
            if (_data == null)
            {
                throw new InvalidOperationException(
                    $"The auxiliary data has not been loaded yet. Call {nameof(LoadAsync)}.");
            }

            return _data;
        }
    }
}