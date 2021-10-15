#if !NETSTANDARD2_0 && !NETSTANDARD2_1
using Microsoft.AspNetCore.Hosting;
#endif
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using Microsoft.Extensions.Hosting;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WessleyMitchell.Web.DotNetCore.ViewRenderer
{
    public static class ViewRendererHttpContextExtensions
    {
        private static ActionContext GetActionContext(IServiceProvider requestServices)
        {
            DefaultHttpContext httpContext = new DefaultHttpContext()
            {
                RequestServices = requestServices//This cannot be an IServiceProvider that is delivered from dependency injection.  It must be HttpContext.RequestServices.
            };
            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return actionContext;
        }

        private static ViewDataDictionary<TModel> GetViewData<TModel>(TModel model)
        {
            ViewDataDictionary<TModel> viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };
            return viewData;
        }

        private static TempDataDictionary GetTempData(ActionContext actionContext, IServiceProvider requestServices)
        {
            ITempDataProvider tempDataProvider = requestServices.GetRequiredService<ITempDataProvider>();
            TempDataDictionary tempData = new TempDataDictionary(actionContext.HttpContext, tempDataProvider);
            return tempData;
        }

        private static ViewEngineResult GetViewEngineResult(IServiceProvider requestServices, ActionContext actionContext, string viewName, bool isMainPage)
        {
            ICompositeViewEngine viewEngine = requestServices.GetRequiredService<ICompositeViewEngine>();
#if NETSTANDARD2_0 || NETSTANDARD2_1
            IHostEnvironment hostEnvironment = requestServices.GetRequiredService<IHostEnvironment>();
#else
            IWebHostEnvironment hostEnvironment = requestServices.GetRequiredService<IWebHostEnvironment>();
#endif
            /*I was previously using this algorithm, but I switched the algorith following this commented section, which is modified from Microsoft.
            //viewEngine.GetView can apparently handle
            //"application relative" (https://github.com/dotnet/aspnetcore/blob/e30d3c52ff5f5e759dd0d3c088b63393a5809d82/src/Mvc/Mvc.Razor/src/RazorViewEngine.cs#L501)
            //and
            //"relative" (https://github.com/dotnet/aspnetcore/blob/e30d3c52ff5f5e759dd0d3c088b63393a5809d82/src/Mvc/Mvc.Razor/src/RazorViewEngine.cs#L507)
            //paths.
            ViewEngineResult viewResult = viewName.StartsWith("~/") || viewName.EndsWith(".cshtml") ?
                viewEngine.GetView(hostEnvironment.ContentRootPath, viewName, isMainPage) :
                viewEngine.FindView(actionContext, viewName, isMainPage);
            if (!viewResult.Success) { throw new ArgumentException("A view with the name " + viewName + " could not be found."); }*/
            //Modified from Microsoft code:  https://github.com/aspnet/samples/blob/main/samples/aspnetcore/mvc/renderviewtostring/RazorViewToStringRenderer.cs
            ViewEngineResult? getViewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: isMainPage);
            if (getViewResult.Success) { return getViewResult; }
            ViewEngineResult? findViewResult = viewEngine.FindView(actionContext, viewName, isMainPage: isMainPage);
            if (findViewResult.Success) { return findViewResult; }
            IEnumerable<string> searchedLocations = getViewResult.SearchedLocations.Concat(findViewResult.SearchedLocations);
            IEnumerable<string> errorLines = (new string[] { "Unable to find view " + viewName + ". The following locations were searched:" }).Concat(searchedLocations);
            string errorMessage = string.Join(Environment.NewLine, errorLines);
            throw new InvalidOperationException(errorMessage);
        }

        private static IView GetView(IServiceProvider requestServices, ActionContext actionContext, string viewName, bool isMainPage)
        {
            ViewEngineResult viewResult = GetViewEngineResult(requestServices, actionContext, viewName, isMainPage);
            IView view = viewResult.View;
            return view;
        }

        /// <summary>Renders a Razor view (.cshtml) to a string</summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="httpContext">The current HttpContext</param>
        /// <param name="viewNameOrPath">A view name or a path to a view (e.g., ~/Pages/SomePage.cshtml)</param>
        /// <param name="model">The model to use with the view</param>
        /// <param name="isMainPage">From the Microsoft.AspNetCore.Mvc.ViewEngines.IViewEngine documentation:  Determines if the page being found is the main page for an action.</param>
        /// <returns>An HTML string</returns>
        public static async Task<string> RenderViewAsync<TModel>(this HttpContext httpContext, string viewNameOrPath, TModel model, bool isMainPage = true)
        {
            IServiceProvider requestServices = httpContext.RequestServices;
            ActionContext actionContext = GetActionContext(requestServices);
            IView view = GetView(requestServices, actionContext, viewNameOrPath, isMainPage);
            ViewDataDictionary<TModel> viewData = GetViewData(model);
            TempDataDictionary tempData = GetTempData(actionContext, requestServices);
            using (StringWriter writer = new StringWriter())
            {
                ViewContext viewContext = new ViewContext(actionContext, view, viewData, tempData, writer, new HtmlHelperOptions());
                await view.RenderAsync(viewContext).ConfigureAwait(false);
                return writer.ToString();
            }
        }
    }
}
