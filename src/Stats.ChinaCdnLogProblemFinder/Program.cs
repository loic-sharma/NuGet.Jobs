// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Stats.CollectAzureChinaCDNLogs;

namespace Stats.ChinaCdnLogProblemFinder
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var rootLogger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            var lf = new SerilogLoggerFactory(rootLogger);
            var collectorLogger = new Logger<ChinaStatsCollector>(lf);

            if (args.Length < 1)
            {
                collectorLogger.LogError("not enough arguments");
            }

            var source = new FileSource(new FileInfo(args[0]));
            var destination = new NullDestination();

            var collector = new ChinaStatsCollector(source, destination, collectorLogger);
            await collector.TryProcessAsync(10, s => s, AzureCdnLogs.Common.Collect.ContentType.GZip, AzureCdnLogs.Common.Collect.ContentType.Text, CancellationToken.None);
        }
    }
}
