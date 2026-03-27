using finalProj.Data;
using finalProj.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using static finalProj.Models.Order;

namespace finalProj.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var guestKey = "Cart_Guest";
                var guestData = HttpContext.Session.GetString(guestKey);

                if (!string.IsNullOrEmpty(guestData))
                {
                    var guestCart = JsonConvert.DeserializeObject<List<CartItem>>(guestData);
                    var userCart = GetCart();

                    foreach (var guestItem in guestCart)
                    {
                        var existingItem = userCart.FirstOrDefault(c => c.ProductId == guestItem.ProductId);
                        if (existingItem != null)
                        {
                            existingItem.Quantity += guestItem.Quantity;
                        }
                        else
                        {
                            userCart.Add(guestItem);
                        }
                    }

                    SaveCart(userCart);
                    HttpContext.Session.Remove(guestKey);
                }
            }

            var cart = GetCart();
            return View(cart);
        }

        private string GetCartSessionKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Guest";
            return $"Cart_{userId}";
        }

        public IActionResult AddToCart(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null || product.StockQuantity <= 0)
            {
                TempData["Error"] = "Sorry, this item is out of stock!";
                return RedirectToAction("Index", "Products");
            }

            var cart = GetCart();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id);
            int currentQtyInCart = cartItem?.Quantity ?? 0;

            if (currentQtyInCart + 1 > product.StockQuantity)
            {
                TempData["ErrorMessage"] = $"عذراً، لا يوجد سوى {product.StockQuantity} قطع فقط من هذا المنتج.";
                return RedirectToAction("Index", "Products");
            }

            if (cartItem == null)
            {
                cart.Add(new CartItem
                {
                    ProductId = id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = 1,
                    ImageUrl = product.ImageUrl
                });
            }
            else
            {
                cartItem.Quantity++;
            }

            SaveCart(cart);
            return RedirectToAction("Index", "Products");
        }

        [HttpPost]
        [Authorize]
        public IActionResult ConfirmOrder(Address address)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = GetCart();

            if (cart == null || !cart.Any())
            {
                TempData["ErrorMessage"] = "سلتك فارغة!";
                return RedirectToAction("Index");
            }

            
            address.UserId = userId;
            _context.Addresses.Add(address);
            _context.SaveChanges();


            var order = new Order
            {
                UserId = userId,
                ShippingAddressId = address.AddressId,
                OrderDate = DateTime.Now,
                Status = OrderStatus.Pending, 
                OrderNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                TotalAmount = cart.Sum(i => i.Price * i.Quantity)
            };

            _context.Orders.Add(order);
            _context.SaveChanges();


            foreach (var item in cart)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price
                };
                _context.OrderItems.Add(orderItem);

                var product = _context.Products.Find(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                }
            }

            _context.SaveChanges();

            
            var key = GetCartSessionKey();
            HttpContext.Session.Remove(key);
            HttpContext.Session.SetInt32("CartCount", 0);

            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} placed successfully!";
            return RedirectToAction("Index", "Products");
        }

        private List<CartItem> GetCart()
        {
            var key = GetCartSessionKey();
            var sessionData = HttpContext.Session.GetString(key);
            return sessionData == null ? new List<CartItem>()
                : JsonConvert.DeserializeObject<List<CartItem>>(sessionData);
        }

        private void SaveCart(List<CartItem> cart)
        {
            var key = GetCartSessionKey();
            HttpContext.Session.SetString(key, JsonConvert.SerializeObject(cart));
            int totalItems = cart.Sum(i => i.Quantity);
            HttpContext.Session.SetInt32("CartCount", totalItems);
        }

        public IActionResult Increase(int id)
        {
            var product = _context.Products.Find(id);
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);

            if (item != null && product != null)
            {
                if (item.Quantity + 1 > product.StockQuantity)
                {
                    TempData["ErrorMessage"] = $"عفواً، المتاح في المخزن {product.StockQuantity} قطع فقط.";
                }
                else
                {
                    item.Quantity++;
                }
            }
            SaveCart(cart);
            return RedirectToAction("Index");
        }

        public IActionResult Decrease(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);
            if (item != null)
            {
                if (item.Quantity > 1)
                    item.Quantity--;
                else
                    cart.Remove(item);
            }
            SaveCart(cart);
            return RedirectToAction("Index");
        }

        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);
            if (item != null)
            {
                cart.Remove(item);
            }
            SaveCart(cart);
            return RedirectToAction("Index");
        }
    }
}