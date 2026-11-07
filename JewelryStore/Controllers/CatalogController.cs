using JewelryStore.Data;
using JewelryStore.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CatalogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, string sort = "featured")
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Where(product => product.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(product =>
                    product.Name.Contains(search) ||
                    product.Description.Contains(search) ||
                    product.Material.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }

            query = sort.ToLowerInvariant() switch
            {
                "price-low" => query.OrderBy(product => product.Price),
                "price-high" => query.OrderByDescending(product => product.Price),
                "newest" => query.OrderByDescending(product => product.CreatedAt),
                _ => query.OrderByDescending(product => product.IsFeatured).ThenBy(product => product.Name)
            };

            return View(new CatalogViewModel
            {
                Products = await query.ToListAsync(),
                Categories = await _context.Categories.AsNoTracking().Where(category => category.IsActive).OrderBy(category => category.Name).ToListAsync(),
                Search = search,
                CategoryId = categoryId,
                Sort = sort
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(item => item.Category)
                .Include(item => item.Images)
                .Include(item => item.Reviews.Where(review => review.IsApproved))
                .FirstOrDefaultAsync(item => item.Id == id && item.IsActive);

            return product is null ? NotFound() : View(product);
        }
    }
}
