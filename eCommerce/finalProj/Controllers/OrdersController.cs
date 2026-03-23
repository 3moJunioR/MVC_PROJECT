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

        // generate uniqueKey for current user
        private string GetCartSessionKey()
        {
            // id for currentuser
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // retutn key as Cart_UserId
            return $"Cart_{userId ?? "Guest"}";
            }

        // cart for current user
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

                    // add prods & Update stock
                    foreach (var item in cart)
                    {
                        var orderItem = new OrderItem
                        {
                            OrderId = order.OrderId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price,
                            LineTotal = item.Price * item.Quantity
                        };
                        _context.OrderItems.Add(orderItem);

                        var product = _context.Products.Find(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity -= item.Quantity;
                        }
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    HttpContext.Session.Remove(GetCartSessionKey());

                    TempData["SuccessMessage"] = $"Order #{order.OrderNumber} placed successfully, هنبعتهولك دلوقتي";
                    return RedirectToAction("Index", "Products");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Something went wrong: " + ex.Message);
                    return View("Checkout", model);
                }
            }
        }
    }
}