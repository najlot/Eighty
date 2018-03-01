﻿using System;
using Eighty.AspNetCore.Mvc.ViewFeatures;
using Eighty.AspNetCore.Mvc.ResultExecutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Eighty.AspNetCore.Mvc
{
    /// <summary>
    /// Extension methods for <see cref="IMvcBuilder"/> for configuring Eighty
    /// </summary>
    public static class MvcBuilderExtensions
    {
        /// <summary>
        /// Configure all of Eighty's MVC features
        /// </summary>
        /// <param name="builder">The MVC builder</param>
        /// <returns>The MVC builder</returns>
        public static IMvcBuilder AddEighty(this IMvcBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.AddSingleton<EightyViewEngine>(
                p => new EightyViewEngine(p.GetService<IOptions<EightyViewOptions>>().Value, p)
            );
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, EightyMvcViewOptionsSetup>()
            );
            builder.Services
                .AddSingleton(typeof(EightyViewResultExecutor<>))
                .AddSingleton(typeof(TwentyViewResultExecutor<>))
                .AddSingleton(typeof(HtmlResultExecutor))
                .AddSingleton(typeof(HtmlBuilderResultExecutor));
            return builder;
        }
    }
}