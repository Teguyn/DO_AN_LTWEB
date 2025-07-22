using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace WebBanDienThoai.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;

        public ProductController(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // Index với tìm kiếm nhanh
        public async Task<IActionResult> Index(string query)
        {
            var products = await _productRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(query))
            {
                query = query.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(query)).ToList();
            }

            // Gán danh sách danh mục (bỏ danh mục "Chưa phân loại")
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categories.Where(c => c.Id != 16).ToList();

            return View(products);
        }

        // Index với bộ lọc và phân trang
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int? categoryId = null, decimal? minPrice = null, decimal? maxPrice = null, string query = "")
        {
            int pageSize = 6;
            var products = await _productRepository.GetAllAsync();

            if (categoryId.HasValue && categoryId.Value != 0)
            {
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
            }

            if (minPrice.HasValue)
            {
                products = products.Where(p => p.DiscountedPrice >= minPrice.Value).ToList();
            }

            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.DiscountedPrice <= maxPrice.Value).ToList();
            }

            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalItems = products.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var pagedProducts = products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.PageNumber = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = (await _categoryRepository.GetAllAsync()).Where(c => c.Id != 16).ToList();
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Query = query;

            return View(pagedProducts);
        }

        [Authorize]
        public async Task<IActionResult> Buy(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (cartItem != null)
            {
                cartItem.Quantity += 1;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.DiscountedPrice,
                    Quantity = 1,
                    ImageUrl = product.ImageUrl
                });
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return RedirectToAction("Index", "ShoppingCart");
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName;
        }

        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            if (product.Images == null)
            {
                product.Images = new List<ProductImage>();
            }

            return View(product);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return RedirectToAction("Index", "Product", new { area = "Admin" });
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
