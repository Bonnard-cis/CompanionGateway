using Companion.Backend.I18n;
using Companion.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Companion.Backend.AspNetCore.Middleware.I18n
{
    public class I18nMiddleware : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Method == HttpMethods.Get)
            {
                return Get(context);
            }
            else
            {
                context.Response.StatusCode = 404;

                return Task.CompletedTask;
            }
        }

        static Task Get(HttpContext context)
        {
            var appId = context.Request.Query["appid"].FirstOrDefault() ?? "";
            var locale = context.Request.Query["locale"].FirstOrDefault() ?? "en";
            
            var labels = UtilitiesCafe.LabelManager.GetAll();
            var culture = CultureInfo.GetCultureInfo(locale);

            var result = labels.Select(x =>
                new KeyValuePair<I18nKey, string>(
                    new I18nKey(appId, locale, "label:" + x.Key.ToString(), null),
                    x.Value[culture]));


            return WriteJson(context, ToJson(result));
        }

        static Task WriteJson(HttpContext context, string json)
        {
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(json);
        }

        static string ToJson(IEnumerable<KeyValuePair<I18nKey, string>> values)
        {
            var result = new JObject();

            foreach (var kv in values)
            {
                var localeObject = result[kv.Key.Locale] as JObject;

                if (localeObject == null)
                {
                    localeObject = new JObject();
                    result[kv.Key.Locale] = localeObject;
                }

                localeObject[kv.Key.Name] = kv.Value;
            }

            return result.ToString(Formatting.Indented);
        }
    }
}
