using finalProj.Data;
using finalProj.Models;
using finalProj.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace finalProj.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string GetCartSessionKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return $"Cart_{userId ?? "Guest"}";
        }

        private List<CartItem> GetCart()
        {
            var key = GetCartSessionKey();
            var sessionData = HttpContext.Session.GetString(key);
            if (string.IsNullOrEmpty(sessionData))
            {
                return new List<CartItem>();
            }
            return JsonConvert.DeserializeObject<List<CartItem>>(sessionData);
        }

        public IActionResult Checkout()
        {
            var cart = GetCart();
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            var model = new CheckoutVM
            {
                TotalAmount = cart.Sum(x => x.Price * x.Quantity)
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult ConfirmOrder(CheckoutVM model)
        {
            if (!ModelState.IsValid)
            {
                model.TotalAmount = GetCart().Sum(x => x.Price * x.Quantity);
                return View("Checkout", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = GetCart();

            if (cart == null || !cart.Any()) return RedirectToAction("Index", "Cart");

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var address = new Address
                    {
                        UserId = userId,
                        Country = model.Country,
                        City = model.City,
                        Street = model.Street,
                        Zip = model.Zip
                    };
                    _context.Addresses.Add(address);
                    _context.SaveChanges();

                    var order = new Order
                    {
                        UserId = userId,
                        ShippingAddressId = address.AddressId,
                        OrderDate = DateTime.Now,
                        Status = Order.OrderStatus.Pending,
                        OrderNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                        TotalAmount = cart.Sum(x => x.Price * x.Quantity)
                    };
                    _context.Orders.Add(order);
                    _context.SaveChanges();

                    foreach (var item in cart)
                    {
                        var product = _context.Products.Find(item.ProductId);

                        if (product == null || product.StockQuantity < item.Quantity)
                        {
                            throw new Exception($"المنتج '{item.ProductName}' كميته غير كافية");
                        }

                        var orderItem = new OrderItem
                        {
                            OrderId = order.OrderId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price,
                            LineTotal = item.Price * item.Quantity
                        };
                        _context.OrderItems.Add(orderItem);

                        product.StockQuantity -= item.Quantity;
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    HttpContext.Session.Remove(GetCartSessionKey());
                    HttpContext.Session.SetInt32("CartCount", 0);

                    TempData["SuccessMessage"] = $"Order #{order.OrderNumber} placed successfully, هنبعتههولك بكرة";
                    return RedirectToAction("MyOrders");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction("Checkout");
                }
            }
        }

        public async Task<IActionResult> MyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        // Admin Actions

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, Order.OrderStatus newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                order.Status = newStatus;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Order status updated to {newStatus} successfully.";
            }
            return RedirectToAction(nameof(AdminOrders));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
        .Include(o => o.User)
        .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
        .Include(o => o.ShippingAddress)
        .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            return View(order);
        }
    }
}