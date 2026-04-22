using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GestorDocumentoApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(); ;
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterVM());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginVM loginVm, string? returnUrl = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(loginVm);
                }

                var result = await _signInManager.PasswordSignInAsync(loginVm.Email, loginVm.Password, loginVm.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return LocalRedirect(returnUrl ?? "/");
                }

                ModelState.AddModelError(string.Empty, "Credenciales invalidas.");
                return View(loginVm);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error en el servidor.");
                return View(loginVm);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterVM registerVm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(registerVm);
            }

            var existingUser = await _userManager.FindByEmailAsync(registerVm.Email);
            if (existingUser is not null)
            {
                ModelState.AddModelError(nameof(registerVm.Email), "Ya existe una cuenta con ese correo.");
                return View(registerVm);
            }

            var user = new IdentityUser
            {
                UserName = registerVm.Email,
                Email = registerVm.Email
            };

            var result = await _userManager.CreateAsync(user, registerVm.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(registerVm);
            }

            const string defaultRole = "User";
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(defaultRole));
            }
            await _userManager.AddToRoleAsync(user, defaultRole);
            TempData["SuccessMessage"] = "Registro exitoso. Inicia sesion para continuar.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));

        }

    }
}

