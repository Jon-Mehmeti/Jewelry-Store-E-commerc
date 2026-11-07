using JewelryStore.Data;
using JewelryStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var wishlist = await _context.Wishlists.AsNoTracking()
                .Include(item => item.Items).ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.UserId == userId);
            return View(wishlist ?? new Wishlist { UserId = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int productId, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User)!;
            var wishlist = await _context.Wishlists.Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.UserId == userId);
            if (wishlist is null)
            {
                wishlist = new Wishlist { UserId = userId };
                _context.Wishlists.Add(wishlist);
            }

            var existing = wishlist.Items.FirstOrDefault(item => item.ProductId == productId);
            if (existing is null)
            {
                wishlist.Items.Add(new WishlistItem { ProductId = productId });
            }
            else
            {
                _context.WishlistItems.Remove(existing);
            }

            await _context.SaveChangesAsync();
            return LocalRedirect(returnUrl ?? Url.Action(nameof(Index))!);
        }
    }
}
