﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Internal
{
    public class CompiledPageApplicationModelProvider : IPageApplicationModelProvider
    {
        private readonly object _cacheLock = new object();
        private readonly ApplicationPartManager _applicationManager;
        private readonly RazorPagesOptions _pagesOptions;
        private List<PageApplicationModel> _cachedApplicationModels;

        public CompiledPageApplicationModelProvider(
            ApplicationPartManager applicationManager,
            IOptions<RazorPagesOptions> pagesOptionsAccessor)
        {
            _applicationManager = applicationManager;
            _pagesOptions = pagesOptionsAccessor.Value;
        }

        public int Order => -1000;

        public void OnProvidersExecuting(PageApplicationModelProviderContext context)
        {
            EnsureCache();
            for (var i = 0; i < _cachedApplicationModels.Count; i++)
            {
                var pageModel = _cachedApplicationModels[i];
                context.Results.Add(new PageApplicationModel(pageModel));
            }
        }

        public void OnProvidersExecuted(PageApplicationModelProviderContext context)
        {
        }

        private void EnsureCache()
        {
            lock (_cacheLock)
            {
                if (_cachedApplicationModels != null)
                {
                    return;
                }

                var rootDirectory = _pagesOptions.RootDirectory;
                if (!rootDirectory.EndsWith("/", StringComparison.Ordinal))
                {
                    rootDirectory = rootDirectory + "/";
                }

                var cachedApplicationModels = new List<PageApplicationModel>();
                foreach (var viewDescriptor in GetViewDescriptors(_applicationManager))
                {
                    if (!viewDescriptor.RelativePath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var viewEnginePath = GetViewEnginePath(rootDirectory, viewDescriptor.RelativePath);
                    var model = new PageApplicationModel(viewDescriptor.RelativePath, viewEnginePath);
                    var pageAttribute = (RazorPageAttribute)viewDescriptor.ViewAttribute;
                    PageSelectorModel.PopulateDefaults(model, pageAttribute.RouteTemplate);

                    cachedApplicationModels.Add(model);
                }

                _cachedApplicationModels = cachedApplicationModels;
            }
        }

        /// <summary>
        /// Gets the sequence of <see cref="CompiledViewDescriptor"/> from <paramref name="applicationManager"/>.
        /// </summary>
        /// <param name="applicationManager">The <see cref="ApplicationPartManager"/>s</param>
        /// <returns>The sequence of <see cref="CompiledViewDescriptor"/>.</returns>
        protected virtual IEnumerable<CompiledViewDescriptor> GetViewDescriptors(ApplicationPartManager applicationManager)
        {
            if (applicationManager == null)
            {
                throw new ArgumentNullException(nameof(applicationManager));
            }

            var viewsFeature = new ViewsFeature();
            applicationManager.PopulateFeature(viewsFeature);

            return viewsFeature.ViewDescriptors.Where(d => d.IsPrecompiled && d.ViewAttribute is RazorPageAttribute);
        }

        private string GetViewEnginePath(string rootDirectory, string path)
        {
            var endIndex = path.LastIndexOf('.');
            if (endIndex == -1)
            {
                endIndex = path.Length;
            }

            // rootDirectory = "/Pages/AllMyPages/"
            // path = "/Pages/AllMyPages/Home.cshtml"
            // Result = "/Home"
            var startIndex = rootDirectory.Length - 1;

            return path.Substring(startIndex, endIndex - startIndex);
        }
    }
}
