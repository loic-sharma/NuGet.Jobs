﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IDownloadDataClient
    {
        Task<ResultAndAccessCondition<DownloadData>> ReadLatestIndexedAsync();
        Task ReplaceLatestIndexedAsync(DownloadData newData, IAccessCondition accessCondition);
        Task UploadSnapshotAsync(DownloadData newData);
    }
}