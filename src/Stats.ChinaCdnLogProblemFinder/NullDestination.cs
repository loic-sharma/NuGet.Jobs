// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.ChinaCdnLogProblemFinder
{
    public class NullDestination : ILogDestination
    {
        public Task<AsyncOperationResult> TryWriteAsync(Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token)
        {
            writeAction(inputStream, Stream.Null);
            return Task.FromResult(new AsyncOperationResult(true, null));
        }
    }
}
