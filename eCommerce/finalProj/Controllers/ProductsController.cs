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
using Microsoft.AspNetCore.Hosting; // إضافة المكتبة دي ضروري

namespace finalProj.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // إضافة المتغير ده للتعامل مع ملفات السيرفر

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // حقن الخدمة في الكنترولر
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

            // لو اللي داخل مش أدمن اعرضله IsActive بس
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
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);

                        // استخدام _webHostEnvironment للوصول لمسار wwwroot الحقيقي
                        string wwwRootPath = _webHostEnvironment.WebRootPath;
                        string productPath = Path.Combine(wwwRootPath, "images", "products");

                        if (!Directory.Exists(productPath))
                            Directory.CreateDirectory(productPath);

                        string fullPath = Path.Combine(productPath, fileName);
                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        product.ImageUrl = "/images/products/" + fileName;
                    }

                    _context.Add(product);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // لو حصل أي خطأ في السيرفر هيتحط هنا والبرنامج مش هيقفل
                    ModelState.AddModelError("", "Error saving data: " + ex.Message);
                }
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
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
        {
            if (id != product.ProductId) return NotFound();

            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    var productInDb = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
                    if (productInDb == null) return NotFound();

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);

                        // تعديل المسار هنا برضه للأمان
                        string wwwRootPath = _webHostEnvironment.WebRootPath;
                        string productPath = Path.Combine(wwwRootPath, "images", "products");

                        if (!Directory.Exists(productPath))
                            Directory.CreateDirectory(productPath);

                        string fullPath = Path.Combine(productPath, fileName);
                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        product.ImageUrl = "/images/products/" + fileName;
                    }
                    else
                    {
                        product.ImageUrl = productInDb.ImageUrl;
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error updating product: " + ex.Message);
                    return View(product);
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