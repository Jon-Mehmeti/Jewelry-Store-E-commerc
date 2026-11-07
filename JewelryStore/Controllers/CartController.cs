using System.Text.Json;
using JewelryStore.Data;
using JewelryStore.Models;
using JewelryStore.Services;
using JewelryStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace JewelryStore.Controllers
{
    public class CartController : Controller
    {
        private const string CartKey = "JewelryStore.Cart";
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StripeSettings _stripeSettings;
        private readonly PayPalPaymentService _payPalPaymentService;

        public CartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IOptions<StripeSettings> stripeOptions,
            PayPalPaymentService payPalPaymentService)
        {
            _context = context;
            _userManager = userManager;
            _stripeSettings = stripeOptions.Value;
            _payPalPaymentService = payPalPaymentService;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            return View(await BuildCartAsync());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == productId && item.IsActive);
            if (product is null || quantity < 1 || quantity > product.StockQuantity)
            {
                return BadRequest();
            }

            var cart = ReadCart();
            cart[productId] = Math.Min(cart.GetValueOrDefault(productId) + quantity, product.StockQuantity);
            SaveCart(cart);
            TempData["CartMessage"] = "Produkti u shtua në shportë.";
            return LocalRedirect(returnUrl ?? Url.Action(nameof(Index))!);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int productId, int quantity)
        {
            var product = await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == productId && item.IsActive);
            if (product is null)
            {
                return NotFound();
            }

            var cart = ReadCart();
            if (quantity <= 0)
            {
                cart.Remove(productId);
            }
            else
            {
                cart[productId] = Math.Min(quantity, product.StockQuantity);
            }

            SaveCart(cart);
            TempData["CartMessage"] = "Shporta u përditësua.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId)
        {
            var cart = ReadCart();
            cart.Remove(productId);
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public IActionResult Checkout()
        {
            return View(new CheckoutViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = await BuildCartAsync();
            if (cart.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Shporta është bosh.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var allowedPaymentMethods = new[] { "Stripe", "PayPal", "CashOnDelivery", "BankTransfer" };
            if (!allowedPaymentMethods.Contains(model.PaymentMethod, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Metoda e pagesës nuk është e vlefshme.");
                return View(model);
            }

            if (string.Equals(model.PaymentMethod, "PayPal", StringComparison.OrdinalIgnoreCase)
                && !_payPalPaymentService.IsConfigured)
            {
                ModelState.AddModelError(string.Empty, "Pagesa me PayPal nuk është konfiguruar ende.");
                return View(model);
            }

            if (string.Equals(model.PaymentMethod, "Stripe", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(_stripeSettings.SecretKey))
            {
                ModelState.AddModelError(string.Empty, "Pagesa me kartë nuk është konfiguruar ende.");
                return View(model);
            }

            var order = new Order
            {
                OrderNumber = $"JS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                UserId = _userManager.GetUserId(User)!,
                SubTotal = cart.SubTotal,
                ShippingCost = 0,
                TotalAmount = cart.SubTotal,
                ShippingAddress = model.ShippingAddress,
                City = model.City,
                PostalCode = model.PostalCode,
                PhoneNumber = model.PhoneNumber,
                Status = "Pending",
                Items = cart.Items.Select(item => new OrderItem
                {
                    ProductId = item.Product.Id,
                    ProductName = item.Product.Name,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TotalPrice = item.Total
                }).ToList(),
                Payment = new Payment
                {
                    PaymentMethod = model.PaymentMethod,
                    PaymentStatus = "Pending",
                    Amount = cart.SubTotal
                }
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            if (string.Equals(model.PaymentMethod, "Stripe", StringComparison.OrdinalIgnoreCase))
            {
                StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
                var sessionOptions = new SessionCreateOptions
                {
                    Mode = "payment",
                    SuccessUrl = Url.Action(nameof(PaymentSuccess), "Cart", new { session_id = "{CHECKOUT_SESSION_ID}" }, Request.Scheme)!,
                    CancelUrl = Url.Action(nameof(PaymentCancelled), "Cart", new { id = order.Id }, Request.Scheme)!,
                    CustomerEmail = User.Identity?.Name,
                    Metadata = new Dictionary<string, string>
                    {
                        ["OrderId"] = order.Id.ToString(),
                        ["OrderNumber"] = order.OrderNumber
                    },
                    LineItems = cart.Items.Select(item => new SessionLineItemOptions
                    {
                        Quantity = item.Quantity,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = _stripeSettings.Currency,
                            UnitAmount = (long)Math.Round(item.UnitPrice * 100m, MidpointRounding.AwayFromZero),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Name,
                                Description = item.Product.Description.Length > 500
                                    ? item.Product.Description[..500]
                                    : item.Product.Description
                            }
                        }
                    }).ToList()
                };

                var session = await new SessionService().CreateAsync(sessionOptions);
                order.Payment!.TransactionId = session.Id;
                await _context.SaveChangesAsync();
                return Redirect(session.Url);
            }

            if (string.Equals(model.PaymentMethod, "PayPal", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var payPalOrder = await _payPalPaymentService.CreateOrderAsync(
                        order,
                        order.TotalAmount,
                        Url.Action(nameof(PayPalSuccess), "Cart", null, Request.Scheme)!,
                        Url.Action(nameof(PaymentCancelled), "Cart", new { id = order.Id }, Request.Scheme)!);
                    order.Payment!.TransactionId = payPalOrder.Id;
                    await _context.SaveChangesAsync();
                    return Redirect(payPalOrder.ApprovalUrl);
                }
                catch (HttpRequestException)
                {
                    _context.Orders.Remove(order);
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "PayPal nuk mund të kontaktohet. Ju lutemi provoni përsëri.");
                    return View(model);
                }
            }

            SaveCart([]);
            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }

        [Authorize]
        public async Task<IActionResult> PaymentSuccess(string session_id)
        {
            if (string.IsNullOrWhiteSpace(session_id) || string.IsNullOrWhiteSpace(_stripeSettings.SecretKey))
            {
                return BadRequest();
            }

            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
            var session = await new SessionService().GetAsync(session_id);
            var order = await GetUserOrderAsync(session.Metadata.GetValueOrDefault("OrderId"));
            if (order is null || !string.Equals(order.Payment?.TransactionId, session.Id, StringComparison.Ordinal))
            {
                return NotFound();
            }

            if (session.PaymentStatus == "paid")
            {
                await MarkOrderPaidAsync(order, session.PaymentIntentId ?? session.Id);
                SaveCart([]);
            }

            return View("../Cart/Confirmation", order);
        }

        [Authorize]
        public async Task<IActionResult> PayPalSuccess(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            var order = await GetUserOrderByTransactionAsync(token);
            if (order is null || order.Payment?.PaymentMethod != "PayPal")
            {
                return NotFound();
            }

            try
            {
                var capture = await _payPalPaymentService.CaptureOrderAsync(token);
                if (capture.IsCompleted)
                {
                    await MarkOrderPaidAsync(order, capture.TransactionId);
                    SaveCart([]);
                }
            }
            catch (HttpRequestException)
            {
                TempData["CartMessage"] = "PayPal nuk e konfirmoi pagesën. Ju lutemi kontaktoni PrinciGold.";
            }

            return View("../Cart/Confirmation", order);
        }

        [Authorize]
        public async Task<IActionResult> PaymentCancelled(int id)
        {
            var order = await GetUserOrderAsync(id.ToString());
            if (order is null)
            {
                return NotFound();
            }

            TempData["CartMessage"] = "Pagesa u anulua. Shporta juaj është ruajtur.";
            return RedirectToAction(nameof(Checkout));
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> StripeWebhook()
        {
            if (string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret))
            {
                return BadRequest();
            }

            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _stripeSettings.WebhookSecret);

                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted && stripeEvent.Data.Object is Session session)
                {
                    var order = await GetOrderAsync(session.Metadata.GetValueOrDefault("OrderId"));
                    if (order is not null)
                    {
                        await MarkOrderPaidAsync(order, session.PaymentIntentId ?? session.Id);
                    }
                }

                return Ok();
            }
            catch (StripeException)
            {
                return BadRequest();
            }
        }

        [Authorize]
        public async Task<IActionResult> Confirmation(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders.AsNoTracking().Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            return order is null ? NotFound() : View(order);
        }

        [Authorize]
        public async Task<IActionResult> Orders()
        {
            var userId = _userManager.GetUserId(User);
            var orders = await _context.Orders.AsNoTracking().Where(order => order.UserId == userId)
                .OrderByDescending(order => order.CreatedAt).ToListAsync();
            return View(orders);
        }

        private async Task<CartViewModel> BuildCartAsync()
        {
            var cart = ReadCart();
            var products = await _context.Products.AsNoTracking()
                .Where(product => cart.Keys.Contains(product.Id) && product.IsActive).ToListAsync();
            return new CartViewModel
            {
                Items = products.Select(product => new CartLineViewModel
                {
                    Product = product,
                    Quantity = Math.Min(cart[product.Id], product.StockQuantity)
                }).ToList()
            };
        }

        private Task<Order?> GetUserOrderAsync(string? orderId)
        {
            if (!int.TryParse(orderId, out var id))
            {
                return Task.FromResult<Order?>(null);
            }

            var userId = _userManager.GetUserId(User);
            return _context.Orders.Include(item => item.Payment)
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        }

        private Task<Order?> GetOrderAsync(string? orderId)
        {
            if (!int.TryParse(orderId, out var id))
            {
                return Task.FromResult<Order?>(null);
            }

            return _context.Orders.Include(item => item.Payment)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        private async Task MarkOrderPaidAsync(Order order, string transactionId)
        {
            if (order.Payment is null || order.Payment.PaymentStatus == "Paid")
            {
                return;
            }

            order.Payment.PaymentStatus = "Paid";
            order.Payment.PaidAt = DateTime.UtcNow;
            order.Payment.TransactionId = transactionId;
            order.Status = "Paid";
            await _context.SaveChangesAsync();
        }

        private Task<Order?> GetUserOrderByTransactionAsync(string transactionId)
        {
            var userId = _userManager.GetUserId(User);
            return _context.Orders.Include(item => item.Payment)
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.Payment != null
                    && item.Payment.TransactionId == transactionId);
        }

        private Dictionary<int, int> ReadCart()
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(HttpContext.Session.GetString(CartKey) ?? "{}") ?? new();
        }

        private void SaveCart(Dictionary<int, int> cart)
        {
            HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));
        }
    }
}
