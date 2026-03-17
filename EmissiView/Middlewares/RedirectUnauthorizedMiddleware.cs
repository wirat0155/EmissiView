using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EmissiView.Middlewares
{
    public class RedirectUnauthorizedMiddleware
    {
        private readonly RequestDelegate _next;

        public RedirectUnauthorizedMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode == 401)
            {
                var pathBase = context.Request.PathBase;
                context.Response.Redirect($"{pathBase}/Auth/vLogin");
            }
        }
    }
}