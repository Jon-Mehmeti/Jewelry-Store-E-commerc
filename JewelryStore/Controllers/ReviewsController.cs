using JewelryStore.Data;
using JewelryStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int rating, string comment)
        {
            if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(comment) || comment.Length > 1000)
            {
                return BadRequest();
            }

            var productExists = await _context.Products.AnyAsync(product => product.Id == productId && product.IsActive);
            if (!productExists)
            {
                return NotFound();
            }

            _context.Reviews.Add(new Review
            {
                ProductId = productId,
                UserId = _userManager.GetUserId(User)!,
                Rating = rating,
                Comment = comment.Trim(),
                IsApproved = false
            });
            await _context.SaveChangesAsync();
            TempData["ReviewMessage"] = "Faleminderit. Review-i juaj do të publikohet pas moderimit.";
            return RedirectToAction("Details", "Catalog", new { id = productId });
        }
    }
}
