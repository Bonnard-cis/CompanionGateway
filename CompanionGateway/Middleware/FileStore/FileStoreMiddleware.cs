using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Companion.Core.BusinessObjects;
using Companion.Core.BusinessObjects.FileStores;
using Companion.Core.BusinessObjects.FileStores.DataShape;
using Companion.Core.Utilities;
using Companion.FileSystem;
using Microsoft.AspNetCore.Http;

namespace Companion.Backend.AspNetCore.Middleware.FileStore
{
    public class FileStoreMiddleware : IMiddleware
    {
        readonly IFileProvider _fileProvider;
        readonly FileStoreOptions _configuration;

        public FileStoreMiddleware(FileStoreOptions fileStoreConfiguration)
        {
            _configuration = fileStoreConfiguration ?? throw new ArgumentNullException(nameof(fileStoreConfiguration));

            _fileProvider = new PhysicalFileProvider(
                Path.Combine(
                    UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.AppData),
                    fileStoreConfiguration.Source));

        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var appContext = Companion.AppContext.Current;

            if (context.Request.Method == HttpMethods.Get)
            {
                if (long.TryParse(context.Request.Path.Value.TrimStart('/'), out long fileId))
                {
                    var file = appContext.QueryStore.Query<Companion.Core.BusinessObjects.FileStores.DataShape.FileMetadata>(
                        new Companion.Core.BusinessObjects.FileStores.Legacy.Query.LoadFileById(fileId))
                        .FirstOrMaybe();

                    if (!file.HasValue
                        || string.IsNullOrEmpty(file.Value.FileName))
                    {
                        return NotFound(context);
                    }

                    var folder = appContext.QueryStore.Query(
                        new Companion.Core.BusinessObjects.FileStores.Legacy.Query.LoadFolderById(file.Value.FileStoreId))
                        .FirstOrDefault();

                    if (folder == null)
                    {
                        return NotFound(context);
                    }

                    try
                    {
                        var data = _fileProvider
                            .GetFileInfo(
                                Path.Combine(
                                    folder.Code,
                                    file.Value.FileName))
                            .OpenRead();

                        return SendFile(
                            context,
                            file.Value.ContentType,
                            data);
                    }
                    catch (Exception)
                    {
                        if ((folder.AllowedFileCategories & AllowedFileCategories.Image) != 0)
                        {
                            return WarningImage(context);
                        }

                        return NotFound(context);
                    }
                }
                else
                {
                    return NotFound(context);
                }
            }
            else if (context.Request.Method == HttpMethods.Post)
            {
                var fileStoreId = context.Request.Query["fileStoreId"].FirstOrDefault() ?? "";
                var contentType = context.Request.Query["contentType"].FirstOrDefault() ?? "";
                var entityId = context.Request.Query["entityId"].FirstOrDefault() ?? "";

                using (var bodyReader = new StreamReader(context.Request.Body))
                {
                    var base64String = bodyReader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(base64String) == false)
                    {
                        try
                        {
                            byte[] imageBytes = Convert.FromBase64String(base64String);

                            long folderid;
                            long entityidvalue;
                            var entityid = GlobalId.Parse(entityId);
                            var valuetest = entityid.Id.ToString();
                            if (long.TryParse(fileStoreId, out folderid)
                                && long.TryParse(valuetest, out entityidvalue)
                                && string.IsNullOrWhiteSpace(contentType) == false)
                            {
                                BasicFileDao _fileDao = new BasicFileDao(
                                    appContext.SendCommand,
                                    appContext.QueryStore,
                                    new FolderId(folderid));

                                var file = _fileDao.AddLocal(entityidvalue, imageBytes, null, contentType, null);

                                var folder = appContext.QueryStore.Query<FolderMetadata>(
                                    new Companion.Core.BusinessObjects.FileStores.Legacy.Query.LoadFolderById(new FolderId(folderid))).FirstOrDefault();

                                var sPath =
                                    Path.Combine(
                                        _configuration.Source,
                                        folder.Code,
                                        file.FileName);
                                try
                                {
                                    // Delete the file if it exists.
                                    if (File.Exists(sPath))
                                    {
                                        File.Delete(sPath);
                                    }

                                    // Create the file.
                                    using (FileStream fs = File.Create(sPath))
                                    {
                                        // Add some information to the file.
                                        fs.Write(imageBytes, 0, imageBytes.Length);
                                    }
                                }
                                catch (Exception)
                                {

                                    throw;
                                }

                                string json = "{\"data\":true}";
                                context.Response.ContentType = "application/json";

                                context.Response.WriteAsync(json);
                            }

                            return NotFound(context);
                        }
                        catch (Exception)
                        {
                            return NotFound(context);
                        }
                    }
                    else
                    {
                        return NotFound(context);
                    }

                }

            }
            else
            {
                return NotFound(context);
            }

        }

        static Task NotFound(HttpContext context)
        {
            context.Response.StatusCode = 404;

            return Task.CompletedTask;
        }

        static async Task SendFile(
            HttpContext context,
            string contentType,
            Stream stream)
        {
            using (stream)
            {
                context.Response.ContentType = contentType;
                context.Response.StatusCode = 200;

                await stream.CopyToAsync(context.Response.Body);
            }
        }

        static Task WarningImage(HttpContext context)
        {
            return SendFile(
                context,
                "image/png",
                File.OpenRead(Path.Combine(
                    UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.App),
                    "Warning.png")));
        }
    }
}
