﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Services.AzureManagement;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class SearchServiceClient : ISearchServiceClient
    {
        /// <summary>
        /// To be used for <see cref="IAzureManagementAPIWrapper"/> request
        /// </summary>
        private const string ProductionSlot = "production";
        private const string SearchQueryTemplate = "q=packageid:{0} version:{1}&ignorefilter=true&semverlevel=2.0.0";
        private const string SearchUrlFormat = "{0}?{1}";

        private readonly IAzureManagementAPIWrapper _azureManagementApiWrapper;
        private readonly IHttpClientWrapper _httpClient;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _configuration;
        private readonly ILogger<SearchServiceClient> _logger;

        public SearchServiceClient(
            IAzureManagementAPIWrapper azureManagementApiWrapper,
            IHttpClientWrapper httpClient,
            IOptionsSnapshot<SearchServiceConfiguration> configuration,
            ILogger<SearchServiceClient> logger)
        {
            _azureManagementApiWrapper = azureManagementApiWrapper;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetCommitDateTimeAsync(Instance instance, CancellationToken token)
        {
            var diagResponse = await GetSearchDiagnosticResponseAsync(instance, token);
            return DateTimeOffset.Parse(
                    diagResponse.CommitUserData.CommitTimeStamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);
        }

        public async Task<DateTimeOffset> GetIndexLastReloadTimeAsync(Instance instance, CancellationToken token)
        {
            var diagResponse = await GetSearchDiagnosticResponseAsync(instance, token);
            return diagResponse.LastIndexReloadTime;
        }

        public Task<SearchResultResponse> GetResultForPackageIdVersion(Instance instance, string packageId, string packageVersion, CancellationToken token)
        {
            var queryString = String.Format(SearchQueryTemplate, packageId, packageVersion);
            var result = GetSearchResultAsync(instance, queryString, token);

            return result;
        }

        public async Task<SearchDiagnosticResponse> GetSearchDiagnosticResponseAsync(
            Instance instance,
            CancellationToken token)
        {
            try
            {
                using (var diagResponse = await _httpClient.GetAsync(
                    instance.DiagUrl,
                    HttpCompletionOption.ResponseContentRead,
                    token))
                {
                    if (!diagResponse.IsSuccessStatusCode)
                    {
                        throw new HttpResponseException(
                            diagResponse.StatusCode,
                            diagResponse.ReasonPhrase,
                            $"The HTTP response when hitting {instance.DiagUrl} was {(int)diagResponse.StatusCode} " +
                            $"{diagResponse.ReasonPhrase}, which is not successful.");
                    }

                    var diagContent = diagResponse.Content;
                    var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                    SearchDiagnosticResponse response = null;
                    switch (instance.ServiceType)
                    {
                        case ServiceType.LuceneSearch:
                            response = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);
                            break;
                        case ServiceType.AzureSearch:
                            var tempResponse = JsonConvert.DeserializeObject<AzureSearchDiagnosticResponse>(searchDiagResultRaw);
                            response = ConvertAzureSearchResponse(tempResponse);
                            break;
                    }

                    return response;
                }
            }
            catch (JsonException je)
            {
                _logger.LogError("Error: Failed to deserialize response from diagnostic endpoint: {Error}", je.Message);
                throw je;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to get diagnostic response due to unexpected error: {Error}", e.Message);
                throw e;
            }
        }

        public async Task<IReadOnlyList<Instance>> GetSearchEndpointsAsync(
            RegionInformation regionInformation,
            CancellationToken token)
        {
            switch (regionInformation.ServiceType)
            {
                case ServiceType.LuceneSearch:
                    var result = await _azureManagementApiWrapper.GetCloudServicePropertiesAsync(
                        _configuration.Value.Subscription,
                        regionInformation.ResourceGroup,
                        regionInformation.ServiceName,
                        ProductionSlot,
                        token);

                    var cloudService = AzureHelper.ParseCloudServiceProperties(result);

                    var instances = GetInstances(endpointUri: cloudService.Uri, instanceCount: cloudService.InstanceCount, regionInformation: regionInformation, serviceType: ServiceType.LuceneSearch);

                    return instances;
                case ServiceType.AzureSearch:
                    return GetInstances(endpointUri: new Uri(regionInformation.BaseUrl), instanceCount: 1, regionInformation: regionInformation, serviceType: ServiceType.AzureSearch);
                default:
                    throw new UnknownServiceTypeException();
            }
        }

        public async Task<SearchResultResponse> GetSearchResultAsync(Instance instance, string query, CancellationToken token)
        {
            try
            {
                var fullUrl = String.Format(SearchUrlFormat, instance.BaseQueryUrl, query);
                using (var response = await _httpClient.GetAsync(
                    fullUrl,
                    HttpCompletionOption.ResponseContentRead,
                    token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpResponseException(
                            response.StatusCode,
                            response.ReasonPhrase,
                            $"The HTTP response when hitting {fullUrl} was {(int)response.StatusCode} " +
                            $"{response.ReasonPhrase}, which is not successful.");
                    }
                    var content = response.Content;
                    var searchResultRaw = await content.ReadAsStringAsync();
                    var searchResultObject = JsonConvert.DeserializeObject<SearchResultResponse>(searchResultRaw);

                    return searchResultObject;
                }
            }
            catch (JsonException je)
            {
                _logger.LogError("Error: Failed to deserialize response from search endpoint: {Error}", je.Message);
                throw je;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to get search result due to unexpected error: {Error}", e.Message);
                throw e;
            }

            throw new NotImplementedException();
        }

        private List<Instance> GetInstances(Uri endpointUri, int instanceCount, RegionInformation regionInformation, ServiceType serviceType)
        {
            var instancePortMinimum = _configuration.Value.InstancePortMinimum;
            switch (serviceType)
            {
                case ServiceType.LuceneSearch:
                    _logger.LogInformation(
                        "{ServiceType}: Testing {InstanceCount} instances, starting at port {InstancePortMinimum} for region {Region}.",
                        Enum.GetName(typeof(ServiceType), ServiceType.LuceneSearch),
                        instanceCount,
                        instancePortMinimum,
                        regionInformation.Region);
                    break;
                case ServiceType.AzureSearch:
                    _logger.LogInformation(
                        "{ServiceType}: Testing for region {Region}.",
                        Enum.GetName(typeof(ServiceType), ServiceType.AzureSearch),
                        regionInformation.Region);
                    break;
            }

            return Enumerable
                .Range(0, instanceCount)
                .Select(i =>
                {
                    var diagUriBuilder = new UriBuilder(endpointUri);

                    diagUriBuilder.Scheme = "https";
                    if (serviceType == ServiceType.LuceneSearch)
                    {
                        diagUriBuilder.Port = instancePortMinimum + i;
                    }
                    diagUriBuilder.Path = "search/diag";

                    var queryBaseUriBuilder = new UriBuilder(endpointUri);

                    queryBaseUriBuilder.Scheme = "https";
                    if (serviceType == ServiceType.LuceneSearch)
                    {
                        queryBaseUriBuilder.Port = instancePortMinimum + i;
                    }
                    queryBaseUriBuilder.Path = "search/query";

                    return new Instance(
                        ProductionSlot,
                        i,
                        diagUriBuilder.Uri.ToString(),
                        queryBaseUriBuilder.Uri.ToString(),
                        regionInformation.Region,
                        serviceType);
                })
                .ToList();
        }

        private SearchDiagnosticResponse ConvertAzureSearchResponse(AzureSearchDiagnosticResponse azureSearchDiagnosticResponse)
        {
            var result = new SearchDiagnosticResponse()
            {
                NumDocs = azureSearchDiagnosticResponse.SearchIndex.DocumentCount,
                IndexName = azureSearchDiagnosticResponse.SearchIndex.Name,
                LastIndexReloadTime = azureSearchDiagnosticResponse.SearchIndex.LastCommitTimestamp,
                LastIndexReloadDurationInMilliseconds = (long)azureSearchDiagnosticResponse.SearchIndex.LastCommitTimestampDuration.TotalMilliseconds,
                LastReopen = azureSearchDiagnosticResponse.SearchIndex.LastCommitTimestamp,
                CommitUserData = new CommitUserData()
                {
                    CommitTimeStamp = azureSearchDiagnosticResponse.SearchIndex.LastCommitTimestamp.ToString()
                }
            };

            return result;
        }
    }
}
