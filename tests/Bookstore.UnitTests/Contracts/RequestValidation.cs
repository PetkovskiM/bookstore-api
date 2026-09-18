using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.UnitTests.Contracts;

internal static class RequestValidation
{
    public static ModelStateDictionary Validate(object request)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();

        using var provider = services.BuildServiceProvider();
        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor());

        // Exercise MVC's nested validation directly, without an HTTP server or database.
        provider.GetRequiredService<IObjectModelValidator>()
            .Validate(context, validationState: null, prefix: string.Empty, model: request);

        return context.ModelState;
    }
}
