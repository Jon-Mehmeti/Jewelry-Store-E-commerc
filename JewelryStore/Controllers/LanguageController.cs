using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace JewelryStore.Controllers
{
    public class LanguageController : Controller
    {
        public IActionResult Set(string culture, string? returnUrl = null)
        {
            if (culture is not ("sq" or "en"))
            {
                culture = "sq";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            return LocalRedirect(returnUrl ?? Url.Action("Index", "Home")!);
        }
    }
}
