﻿using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tests.Logger
{
    internal class Job : JobBase
    {
        private const string ScenarioArgumentName = "scenario";
        private const string LogCountArgumentName = "logcount";
        private const string HelpMessage = "Please provide 1 for successful job, 2 for failed job, 3 for crashed job and 4 for successful job with heavy logging";

        private int? JobScenario { get; set; }

        private int? LogCount { get; set; }
        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            JobScenario = JobConfigManager.TryGetIntArgument(jobArgsDictionary, ScenarioArgumentName);
            if(JobScenario == null)
            {
                throw new ArgumentException("Argument '"+ ScenarioArgumentName +"' is mandatory." + HelpMessage);
            }

            LogCount = JobConfigManager.TryGetIntArgument(jobArgsDictionary, LogCountArgumentName);

            return true;
        }

        public async override Task<bool> Run()
        {
            LogCount = LogCount ?? AzureBlobJobTraceLogger.MaxLogBatchSize * 2;
            switch(JobScenario)
            {
                case 1:
                    return true;
                    
                case 2:
                    return false;

                case 3:
                    throw new Exception("Job crashed test");

                case 4:
                    for(int i = 0; i < LogCount; i++)
                    {
                        Trace.WriteLine("Message number : " + i);
                    }
                    return true;

                default:
                    throw new ArgumentException("Unknown scenario. " + HelpMessage);
            }
        }
    }
}
