# WessleyMitchell.Web.DotNetCore.ViewRenderer
HttpContext extensions to allow Razor view (.cshtml) rendering to a string

I wanted to contribute this to ASP.NET Core:  https://github.com/dotnet/aspnetcore/issues/37187

But Microsoft decided it would be better as a separate library.  This is that library.

# Installation

Add this NuGet package:
```powershell
    PM> Install-Package WessleyMitchell.Web.DotNetCore.ViewRenderer
```

# Usage
Invoke the renderer like this:
```c#
string html = await HttpContext.RenderViewAsync("Pages/SomePage.cshtml", new SomePageModel("X"), isMainPage: false);
```

# Known Issues

I can never get this code to work for a .cshtml with `@page` at the top.  It only seems to work with partials (no `@page`).  If `@page` is used, Model is null on the .cshtml page. Again, this does not occur when no `@page` is at the top.  If someone knows a solution, I would appreciate a pull request.  Thank you.