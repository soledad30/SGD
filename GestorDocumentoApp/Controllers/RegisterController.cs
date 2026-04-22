using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestorDocumentoApp.ViewModels;

namespace GestorDocumentoApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RegisterController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RegisterController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderBy(x => x.Email)
                .ToListAsync();

            var rows = new List<UserAdminRowVM>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                rows.Add(new UserAdminRowVM
                {
                    UserId = user.Id,
                    Email = user.Email ?? user.UserName ?? string.Empty,
                    Roles = roles
                });
            }

            var availableRoles = await _roleManager.Roles
                .OrderBy(x => x.Name)
                .Select(x => x.Name!)
                .ToListAsync();

            return View(new UserAdminVM
            {
                Users = rows,
                AvailableRoles = availableRoles
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(UserCreateVM vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Datos de usuario invalidos.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Users));
            }

            if (!await _roleManager.RoleExistsAsync(vm.Role))
            {
                TempData["Message"] = $"El rol '{vm.Role}' no existe.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Users));
            }

            var existing = await _userManager.FindByEmailAsync(vm.Email);
            if (existing is not null)
            {
                TempData["Message"] = "Ya existe un usuario con ese email.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Users));
            }

            var user = new IdentityUser
            {
                UserName = vm.Email,
                Email = vm.Email
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                TempData["Message"] = string.Join(" | ", result.Errors.Select(x => x.Description));
                TempData["MessageType"] = "error";
                return RedirectToAction(nameof(Users));
            }

            await _userManager.AddToRoleAsync(user, vm.Role);
            TempData["Message"] = "Usuario creado correctamente.";
            TempData["MessageType"] = "success";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> AssignRole(UserAssignRoleVM vm)
        {
            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user is null)
            {
                return NotFound();
            }

            if (!await _roleManager.RoleExistsAsync(vm.Role))
            {
                TempData["Message"] = $"El rol '{vm.Role}' no existe.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Users));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, vm.Role);
            TempData["Message"] = "Rol actualizado correctamente.";
            TempData["MessageType"] = "success";
            return RedirectToAction(nameof(Users));
        }
    }
}
