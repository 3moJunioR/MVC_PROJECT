using finalProj.Data;
using finalProj.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace finalProj.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString, int? categoryId)
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var key = $"Cart_{userId}";
                var sessionData = HttpContext.Session.GetString(key);

                if (!string.IsNullOrEmpty(sessionData))
                {
                    var cart = JsonConvert.DeserializeObject<List<CartItem>>(sessionData);
                    HttpContext.Session.SetInt32("CartCount", cart.Sum(x => x.Quantity));
                }
                else
                {
                    HttpContext.Session.SetInt32("CartCount", 0);
                }
            }

            var products = _context.Products.Include(p => p.Category).AsQueryable();

            // لو اللي داخل مش أدمن اعرض له IsActive بس
            if (!User.IsInRole("Admin"))
            {
                products = products.Where(p => p.IsActive);
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(s => s.Name.Contains(searchString) || s.SKU.Contains(searchString));
            }

            if (categoryId.HasValue && categoryId != 0)
            {
                products = products.Where(x => x.CategoryId == categoryId);
            }

            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", categoryId);
            ViewData["CurrentFilter"] = searchString;

            return View(await products.ToListAsync());
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null) return NotFound();

            return View(product);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string productPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");

                    if (!Directory.Exists(productPath)) Directory.CreateDirectory(productPath);

                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = @"/images/products/" + fileName;
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.ProductId) return NotFound();

            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    var productInDb = await _context.Products.FindAsync(id);
                    if (productInDb == null) return NotFound();

                    productInDb.Name = product.Name;
                    productInDb.SKU = product.SKU;
                    productInDb.Price = product.Price;
                    productInDb.StockQuantity = product.StockQuantity;
                    productInDb.CategoryId = product.CategoryId;
                    productInDb.IsActive = product.IsActive;

                    if (!string.IsNullOrEmpty(product.ImageUrl))
                    {
                        productInDb.ImageUrl = product.ImageUrl;
                    }

                    _context.Update(productInDb);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStock(int productId, int extraQuantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null && extraQuantity > 0)
            {
                product.StockQuantity += extraQuantity;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Stock Updated Successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null) return NotFound();

            return View(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}