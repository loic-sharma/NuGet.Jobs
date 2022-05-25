using System;
using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    public class AlternatePackage
    {
        [SimpleField]
        public string Id { get; set; }

        [SimpleField]
        public string Range { get; set; }
    }
}
