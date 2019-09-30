using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Companion.Backend.AspNetCore.Middleware.AppContext
{
    using AppContext = Companion.AppContext;

    public class AppContextMiddleware : IMiddleware
    {
        readonly SessionStore _sessions;
        readonly IAppContextFactory _appContextFactor;

        public AppContextMiddleware(
            SessionStore session,
            IAppContextFactory appContextFactory)
        {
            _sessions = session ?? throw new ArgumentNullException(nameof(session));
            _appContextFactor = appContextFactory ?? throw new ArgumentNullException(nameof(appContextFactory));
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var session = GetSession(context);

            CultureInfo.CurrentCulture = session.Culture;
            CultureInfo.CurrentUICulture = session.Culture;

            var appContext = _appContextFactor.Create(
                session,
                context.RequestServices,
                context.RequestAborted);

            AppContext.Current = appContext;

            context.Response.RegisterForDispose(appContext);

            context.RequestServices = appContext.Services;

            return next(context);
        }

        Session GetSession(HttpContext context)
        {
            var authorization = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(' ');

            if (authorization != null && authorization.Length == 2)
            {
                if (authorization[0] == "Token")
                {
                    var sessionId = new Guid(
                            Convert.FromBase64String(
                                authorization[1]));

                    return _sessions.Get(sessionId) ?? Session.Anonymous();
                }
            }

            return Session.Anonymous();
        }
    }

    class CompositeServiceProvider : IServiceProvider, IDisposable
    {
        readonly IServiceProvider[] _serviceProviders;

        public CompositeServiceProvider(params IServiceProvider[] serviceProviders)
        {
            _serviceProviders = serviceProviders;
        }

        public CompositeServiceProvider(IEnumerable<IServiceProvider> serviceProviders)
        {
            _serviceProviders = serviceProviders.ToArray();
        }

        public object GetService(Type serviceType)
        {
            foreach (var serviceProvider in _serviceProviders)
            {
                var result = serviceProvider.GetService(serviceType);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var serviceProvider in _serviceProviders)
            {
                if (serviceProvider is IDisposable diposableServices)
                {
                    diposableServices.Dispose();
                }
            }
        }
    }
}
