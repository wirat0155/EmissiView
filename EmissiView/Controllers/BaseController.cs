using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;

namespace EmissiView.Controllers
{
    public class BaseController : Controller
    {
        public async override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
        }

        protected JsonResult GenerateErrorResponse()
        {
            var errorMessages = ModelState.Keys
                .Where(key => ModelState[key].Errors.Any())
                .SelectMany(key => ModelState[key].Errors.Select((error, index) =>
                {
                    var errorMessage = error.ErrorMessage;

                    if (key.Contains("["))
                    {
                        // Replace the first part of the key, keeping the brackets as is
                        var formattedKey = key.Replace("[", "[") // Ensure we keep the brackets
                                                       .Replace("]", "]"); // Ensure we keep the brackets

                        // Format the key correctly with the brackets
                        return new { property = formattedKey, errorMessage };
                    }
                    else
                    {
                        return new { property = key, errorMessage };
                    }
                }))
                .ToList();

            if (errorMessages.Count == 0)
            {
                errorMessages.Add(new { property = "txt_form", errorMessage = "Invalid data." });
            }

            return Json(new { success = false, errors = errorMessages });
        }
    }
}


