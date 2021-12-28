// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.ChinaCdnLogProblemFinder
{
    internal class FileSource : ILogSource
    {
        private readonly FileInfo _file;

        public FileSource(FileInfo file)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
        }

        public Uri Uri => new Uri(_file.FullName);

        public Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token, string prefix = null)
        {
            return Task.FromResult((IEnumerable<Uri>)new[] { Uri });
        }

        public Task<Stream> OpenReadAsync(Uri blobUri, ContentType contentType, CancellationToken token)
        {
            if (Uri == blobUri)
            {
                var rawStream = File.OpenRead(_file.FullName);
                switch (contentType)
                {
                    case ContentType.GZip:
                        return Task.FromResult((Stream)new GZipInputStream(rawStream));
                    case ContentType.Text:
                    case ContentType.None:
                        return Task.FromResult((Stream)rawStream);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(contentType));
                }

            }
            throw new ArgumentException($"Unexpected URL: {blobUri}");
        }

        public Task<AzureBlobLockResult> TakeLockAsync(Uri blobUri, CancellationToken token)
        {
            return Task.FromResult(new AzureBlobLockResult(new CloudBlob(Uri), true, "foobar", token));
        }

        public Task<AsyncOperationResult> TryCleanAsync(AzureBlobLockResult blobLock, bool onError, CancellationToken token)
        {
            return Task.FromResult(new AsyncOperationResult(true, null));
        }

        public Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult blobLock, CancellationToken token)
        {
            return Task.FromResult(new AsyncOperationResult(true, null));
        }
    }
}
