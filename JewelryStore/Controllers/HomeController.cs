using JewelryStore.Data;
using JewelryStore.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new HomeViewModel
            {
                FeaturedProducts = await _context.Products
                    .AsNoTracking()
                    .Include(product => product.Category)
                    .Where(product => product.IsActive && product.IsFeatured)
                    .OrderByDescending(product => product.CreatedAt)
                    .Take(6)
                    .ToListAsync(),
                Categories = await _context.Categories
                    .AsNoTracking()
                    .Where(category => category.IsActive)
                    .OrderBy(category => category.Name)
                    .ToListAsync()
            };

            return View(model);
        }
    }
}
