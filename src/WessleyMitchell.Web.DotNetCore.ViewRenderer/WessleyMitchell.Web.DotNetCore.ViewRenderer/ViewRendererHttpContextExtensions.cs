#if !NETSTANDARD2_0
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
#if NETSTANDARD2_0
using Microsoft.Extensions.Hosting;
#endif
using System;
using System.IO;
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
            ITempDataProvider? tempDataProvider = (ITempDataProvider?)requestServices.GetService(typeof(ITempDataProvider));
            if (tempDataProvider == null) { throw new InvalidOperationException(nameof(ITempDataProvider) + " had not been added to the dependency container."); }
            TempDataDictionary tempData = new TempDataDictionary(actionContext.HttpContext, tempDataProvider);
            return tempData;
        }

        private static ViewEngineResult GetViewEngineResult(IServiceProvider requestServices, ActionContext actionContext, string viewName, bool isMainPage)
        {
            ICompositeViewEngine? viewEngine = (ICompositeViewEngine?)requestServices.GetService(typeof(ICompositeViewEngine));
            if (viewEngine == null) { throw new InvalidOperationException(nameof(ICompositeViewEngine) + " had not been added to the dependency container."); }
#if NETSTANDARD2_0
            Type hostEnvironmentType = typeof(IHostEnvironment);
            var hostEnvironment = (IHostEnvironment?)requestServices.GetService(hostEnvironmentType);
#else
            Type hostEnvironmentType = typeof(IWebHostEnvironment);
            var hostEnvironment = (IWebHostEnvironment?)requestServices.GetService(typeof(IWebHostEnvironment));
#endif
            if (hostEnvironment == null) { throw new InvalidOperationException(hostEnvironmentType.FullName + " had not been added to the dependency container."); }
            //viewEngine.GetView can apparently handle
            //"application relative" (https://github.com/dotnet/aspnetcore/blob/e30d3c52ff5f5e759dd0d3c088b63393a5809d82/src/Mvc/Mvc.Razor/src/RazorViewEngine.cs#L501)
            //and
            //"relative" (https://github.com/dotnet/aspnetcore/blob/e30d3c52ff5f5e759dd0d3c088b63393a5809d82/src/Mvc/Mvc.Razor/src/RazorViewEngine.cs#L507)
            //paths.
            ViewEngineResult viewResult = viewName.StartsWith("~/") || viewName.EndsWith(".cshtml") ?
                viewEngine.GetView(hostEnvironment.ContentRootPath, viewName, isMainPage) :
                viewEngine.FindView(actionContext, viewName, isMainPage);
            if (!viewResult.Success) { throw new ArgumentException("A view with the name " + viewName + " could not be found."); }
            return viewResult;
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
