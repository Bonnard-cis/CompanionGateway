using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Companion.Backend.Schema;
using Companion.BusinessObjects;
using Companion.BusinessObjects.TaskList.DataShape;
using Companion.Core.BusinessObjects;
using Companion.Core.BusinessObjects.FileStores;
using Companion.Core.Utilities;
using Companion.FileSystem;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace Companion.Backend.AspNetCore.Middleware
{
    using System;
    using Companion.Backend.Widget.Serializer.Json;
    using Companion.Backend.Workflow;
    using Companion.BusinessObjects.BP.ReadModel;

    public class WorkflowMiddleware : IMiddleware
    {
        readonly IFileProvider _fileProvider;
        readonly BPRepository _bpRepository;

        public WorkflowMiddleware(
            FileStoreOptions fileStoreConfiguration,
            BPRepository bpRepository)
        {
            _fileProvider = new PhysicalFileProvider(
                Path.Combine(
                    UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.AppData),
                    fileStoreConfiguration.Source));

            _bpRepository = bpRepository ?? throw new ArgumentNullException(nameof(bpRepository));
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Method == HttpMethods.Get)
            {
                var appContext = Companion.AppContext.Current;

                var visitId = context.Request.Query["visit"]
                        .SelectMany(TryParseGlobalId)
                        .FirstOrDefault();

                var defaultworkflow = context.Request.Query["defaultworkflow"].FirstOrDefault();


                if (visitId?.Id is VisitId actualVisitId)
                {
                    var workflowInfo = await appContext.Mediator.Send(new BusinessObjects.TaskList.LoadWorkflow(actualVisitId));

                    if (workflowInfo == null)
                    {
                        await NotFound(context);
                    }
                    else
                    {
                        var fileName = workflowInfo.File?.FileName ?? defaultworkflow;

                        var workflow = await Load(appContext, fileName, new Dictionary<string, object>()
                        {
                            { "BPId", CustomerEntityType.ToGlobalId(workflowInfo.BP.DepartmentIsTrue_ParentBPId ?? workflowInfo.BP.BPId)  },
                            { "VisitId", VisitType.ToId(workflowInfo.Visit.VisitId) },
                            { "DepartmentBPId", workflowInfo.BP.RowType == RowTypes.SubEntity ? CustomerEntityType.ToGlobalId(workflowInfo.BP.BPId) : null},
                            { "StoreVisitId", workflowInfo.Visit.BPDepartmentIsTrue_ParentVisitId.ToMaybe().Select(VisitType.ToId).WithDefault() }
                        });

                        await WriteWorkflow(context, workflow);
                    }
                }
                else
                {
                    var workflow = await Load(appContext, "Home.xml", new Dictionary<string, object>());

                    await WriteWorkflow(context, workflow);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }

        static async Task WriteWorkflow(HttpContext context, Workflow workflow)
        {
            if (workflow == null)
            {
                await NotFound(context);
                return;
            }

            var widgetSerializer = new WidgetJsonSerializer(Companion.AppContext.Current, null);


            var json = new JObject()
            {
                { "activities", await SerializeActivities(widgetSerializer, workflow) },
                { "initialActivity", workflow.Activities.TakeWhile(x => x != workflow.InitialActivity).Count() },
                { "indicators", await SerializeIndicators(widgetSerializer, workflow.Indicators) },
            };

            await WriteJson(context, json);
        }

        Task<Workflow> Load(
            Companion.AppContext context,
            string code,
            Dictionary<string, object> arguments)
        {
            var repository = new WorkflowRepository(
                context,
                _fileProvider);

            return repository.LoadWorkflow(code, arguments);
        }

        static IEnumerable<GlobalId> TryParseGlobalId(string value)
        {
            if (GlobalId.TryParse(Base64.FromUrl(value), out var result))
            {
                return Maybe.Just(result).AsEnumerable();
            }
            else
            {
                return Maybe.Empty<GlobalId>().AsEnumerable();
            }
        }

        static async Task<JToken> SerializeActivities(
            WidgetJsonSerializer widgetSerializer,
            Workflow workflow)
        {
            var jArray = new JArray();

            var isFirst = true;
            
            foreach (var activity in workflow.Activities)
            {
                var content = new JArray();

                foreach (var contentWidget in activity.Content)
                {
                    content.Add(await widgetSerializer.Serialize(contentWidget));
                }

                jArray.Add(new JObject()
                {
                    { "title", activity.Title.ToString() },
                    { "isHidden", activity.IsHidden },
                    { "canJumpTo", activity.CanJumpTo },
                    { "canMoveBack", isFirst ? true : activity.CanMoveBack },
                    { "canMoveNext", activity.CanMoveNext },
                    { "indicators", await SerializeIndicators(widgetSerializer, activity.Indicators) },
                    { "content", content },
                });

                isFirst = false;
            }

            return jArray;
        }

        static async Task<JToken> SerializeIndicators(
            WidgetJsonSerializer widgetSerializer,
            WorkflowIndicators indicators)
        {
            return new JObject()
            {
                { "title", await widgetSerializer.Serialize(indicators.Title) },
                { "context1", await widgetSerializer.Serialize(indicators.Context1) },
                { "context2", await widgetSerializer.Serialize(indicators.Context2) },
            };
        }

        static Task WriteJson(HttpContext context, JObject json)
        {
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(json.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        static Task NotFound(HttpContext context)
        {
            context.Response.StatusCode = 404;

            return Task.CompletedTask;
        }
    }
}
