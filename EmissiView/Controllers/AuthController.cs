using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using JWTRegen.Interfaces;
using System;
using System.Threading.Tasks;
using EmissiView.Repositories;
using Microsoft.AspNetCore.Http;

namespace EmissiView.Controllers
{
    public class AuthController : Controller
    {
        private readonly EmpRepository _empRepository;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IConfiguration _configuration;

        public AuthController(EmpRepository empRepository, IJwtTokenService jwtTokenService, IConfiguration configuration)
        {
            _empRepository = empRepository;
            _jwtTokenService = jwtTokenService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult vLogin()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string txt_empno, string txt_password)
        {
            try
            {
                if (string.IsNullOrEmpty(txt_empno) || string.IsNullOrEmpty(txt_password))
                {
                    return Json(new { success = false, text = "Please enter username and password", errors = new[] { "txt_empno", "txt_password" } });
                }

                var (success, empNo, message) = await _empRepository.LoginAsync(txt_empno, txt_password);

                if (!success)
                {
                    return Json(new { success = false, text = message, errors = new[] { "txt_empno" } });
                }

                // Generate JWT Token using JWTRegen library
                var token = _jwtTokenService.GenerateToken(empNo!, "USER");

                // Set JWT cookie
                var cookieName = _configuration["JwtSettings:CookieName"] ?? "EmissiView_jwt";
                Response.Cookies.Append(cookieName, token, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    Expires = DateTime.UtcNow.AddHours(24)
                });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, text = $"Login failed: {ex.Message}", errors = new string[0] });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var cookieName = _configuration["JwtSettings:CookieName"] ?? "EmissiView_jwt";
            Response.Cookies.Append(cookieName, string.Empty, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTime.UtcNow.AddDays(-1)
            });

            return RedirectToAction("vLogin", "Auth");
        }
    }
}