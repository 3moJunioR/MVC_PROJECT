using finalProj.Data;
using finalProj.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace finalProj.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CartController(ApplicationDbContext context) { 
            _context = context;
        }
        //viewCart content
        public IActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }
        
        public IActionResult AddToCart(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null || product.StockQuantity <= 0)
            {
                TempData["Error"] = "Sorry, this item is out of stock!";
                return RedirectToAction("Index", "Products");
            }
            //product.StockQuantity -= 1
           // _context.SaveChanges();
            var cart = GetCart();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id);
                //check on quantity
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
        public IActionResult ConfirmOrder(string userAddress)
        {
            if (string.IsNullOrEmpty(userAddress))
            {
                TempData["ErrorMessage"] = "Please enter your shipping address!";
                return RedirectToAction("Index");
            }

            var cart = GetCart(); 

            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index");
            }

            foreach (var item in cart)
            {
                var product = _context.Products.Find(item.ProductId);

                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                }
            }

            _context.SaveChanges();

            HttpContext.Session.Remove("Cart");

            TempData["SuccessMessage"] = "Order placed successfully, هنبعتهولك حالا.";
            return RedirectToAction("Index", "Products");
        }
        //helperJobs for dealing with sessions
        private List<CartItem> GetCart()
        {
            var sessionData = HttpContext.Session.GetString("Cart");
            return sessionData == null ? new List<CartItem>() : JsonConvert
                .DeserializeObject < List < CartItem >> (sessionData); 
        }
        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(cart));
        }
        public IActionResult Increase(int id)
        {
            var product = _context.Products.Find(id);
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);

            if (item != null && product != null)
            {
                // validate for quantity
                if (item.Quantity + 1 > product.StockQuantity)
                {
                    TempData["ErrorMessage"] = $"عفواً، المتاح في المخزن {product.StockQuantity} قطع فقط.";
                }
                else
                {
                    item.Quantity++;
                    TempData["SuccessMessage"] = "تم تحديث الكمية بنجاح.";
                }
            }

            SaveCart(cart);
            return RedirectToAction("Index");
        }
        public IActionResult Decrease(int id) { 
            var cart=GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);
            if (item!=null)
            {
                if (item.Quantity > 1)
                    item.Quantity--;
                else cart.Remove(item);
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
