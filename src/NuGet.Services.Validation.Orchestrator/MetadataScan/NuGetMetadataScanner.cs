// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.MetadataScan
{
    [ValidatorName(ValidatorName.MetadataScanner)]
    public class NuGetMetadataScanner : INuGetValidator
    {
        private readonly HttpClient _httpClient;
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly MetadataScannerConfiguration _configuration;
        private readonly ILogger<NuGetMetadataScanner> _logger;

        public NuGetMetadataScanner(
            HttpClient httpClient,
            IEntityRepository<Package> packageRepository,
            IOptionsSnapshot<MetadataScannerConfiguration> configurationAccessor,
            ILogger<NuGetMetadataScanner> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            _configuration = configurationAccessor?.Value ?? throw new ArgumentException($"Unable to access value of {nameof(configurationAccessor)}", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task CleanUpAsync(INuGetValidationRequest request)
        {
            return Task.CompletedTask;
        }

        public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
            => StartAsync(request);

        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            try
            {
                await ProcessRequest(request);
                return NuGetValidationResponse.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process metadata service request for package {Id} {Version}", request.PackageId, request.PackageVersion);
                return NuGetValidationResponse.Incomplete;
            }
        }

        private async Task ProcessRequest(INuGetValidationRequest request)
        {
            var package = _packageRepository
                .GetAll()
                .Where(p => p.Key == request.PackageKey)
                .Include(p => p.PackageRegistration)
                .FirstOrDefault();

            if (package == null)
            {
                return;
            }

            var serviceRequest = new Request { data = new[] { new RequestItem { Id = request.PackageId, Description = package.Description } } };
            var serviceRequestText = JsonConvert.SerializeObject(serviceRequest);
            bool needUnlist = false;
            using (var requestContent = new StringContent(serviceRequestText, Encoding.UTF8, "application/json"))
            using (var response = await _httpClient.PostAsync(_configuration.EndpointUrl, requestContent))
            {
                response.EnsureSuccessStatusCode();

                var unprocessedResponseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Got response from metadata service: {Response}", unprocessedResponseString);
                var responseString = unprocessedResponseString.Trim('"').Replace("\\\"", "\"");
                var responseData = JsonConvert.DeserializeObject<Response>(responseString);
                needUnlist = responseData.result?.FirstOrDefault() == 1;
            }

            if (needUnlist)
            {
                package.Listed = false;
                package.PackageRegistration.IsLocked = true;
                await _packageRepository.CommitChangesAsync();
                _logger.LogInformation("Updated package");
            }
        }

        private class RequestItem
        {
            public string Id { get; set; }
            public string Description { get; set; }
        }

        private class Request
        {
            public RequestItem[] data { get; set; }
        }

        private class Response
        {
            public int[] result { get; set; }
        }
    }
}
