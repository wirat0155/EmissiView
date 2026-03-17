using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EmissiView.Middlewares
{
    public class TokenVersionMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenVersionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
        }
    }
}