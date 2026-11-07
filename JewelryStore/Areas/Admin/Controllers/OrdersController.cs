using JewelryStore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, string? search)
        {
            var orders = _context.Orders
                .Include(order => order.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                orders = orders.Where(order => order.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                orders = orders.Where(order => order.OrderNumber.Contains(search) || (order.User != null && order.User.Email!.Contains(search)));
            }

            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.PendingOrders = await _context.Orders.CountAsync(order => order.Status == "Pending");
            ViewBag.CompletedOrders = await _context.Orders.CountAsync(order => order.Status == "Completed");
            return View(await orders
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(item => item.User)
                .Include(item => item.Items)
                    .ThenInclude(item => item.Product)
                .Include(item => item.Payment)
                .FirstOrDefaultAsync(item => item.Id == id);
            return order is null ? NotFound() : View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var allowedStatuses = new[] { "Pending", "Processing", "Shipped", "Completed", "Cancelled" };
            if (!allowedStatuses.Contains(status)) return BadRequest();

            var order = await _context.Orders.FindAsync(id);
            if (order is null) return NotFound();

            order.Status = status;
            if (order.Payment is not null && status == "Completed")
            {
                order.Payment.PaymentStatus = "Paid";
                order.Payment.PaidAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
