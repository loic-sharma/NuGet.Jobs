using System;
using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    public class Deprecation
    {
        [SimpleField]
        public AlternatePackage AlternatePackage { get; set; }

        [SimpleField]
        public string Message { get; set; }

        [SimpleField]
        public string[] Reasons { get; set; }
    }
}
