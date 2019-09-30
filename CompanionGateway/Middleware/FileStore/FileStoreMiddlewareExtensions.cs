using System;
using System.IO;
using Companion.Backend;
using Companion.Backend.AspNetCore.Middleware.FileStore;
using Companion.Core.BusinessObjects.FileStores;
using Companion.Core.Security;
using Companion.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class FileStoreMiddlewareExtensions
    {
        public static IServiceCollection AddFileStore(
            this IServiceCollection services)
        {
            var settings = SecurityCafe.SecurityItemManager.GetSettings("Companion.FileStore", true);

            if (settings == null || string.IsNullOrEmpty(settings.GetSettingAsString("Source", true)))
            {
                settings = settings ?? new SecurityItem("Companion.FileStore", "", SecurityItemSource.ConfigurationFile, null);

                settings.SetSetting("Source", "FileStore.db");

                SecurityCafe.SecurityItemManager.AddSettings(settings);
                SecurityCafe.SecurityItemManager.SaveSettings();
            }

            return AddFileStore(services, settings.GetSettingAsString("Source"));
        }

        public static IServiceCollection AddFileStore(
            this IServiceCollection services,
            string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            services.AddTransient<FileStoreMiddleware>();
            
            services.AddSingleton(new FileStoreOptions()
            {
                Source = Path.Combine(
                    UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.AppData),
                    source),
            });

            services.AddSingleton<IFileLinkBuilder>(new TemplateFileLinkBuilder("/api/file-store/{0}"));

            return services;
        }

        public static IApplicationBuilder UseFileStore(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.Map("/api/file-store", app =>
            {
                app.UseAppContext();

                app.UseMiddleware<FileStoreMiddleware>();
            });

            return applicationBuilder;
        }
    }
}
