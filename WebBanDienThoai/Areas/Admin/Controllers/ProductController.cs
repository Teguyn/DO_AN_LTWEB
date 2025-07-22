using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;

        public ProductController(IProductRepository productRepository, ICategoryRepository categoryRepository, ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _context = context;
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Index(string searchTerm, string sortOrder)
        {
            var products = await _productRepository.GetAllAsync();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                               p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            switch (sortOrder)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price).ToList();
                    break;
            }

            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentSort = sortOrder;

            return View(products);
        }

        [Authorize(Roles = SD.Role_Admin)]
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, IFormFile[] additionalImages)
        {
            if (ModelState.IsValid)
            {
                // Lưu ảnh chính
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }

                // Tính giá sau giảm
                if (product.Price > 0 && product.DiscountPercent > 0)
                {
                    product.DiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100); // Giảm giá theo phần trăm
                }
                else
                {
                    product.DiscountedPrice = product.Price; // Nếu không có giảm giá, giữ nguyên giá gốc
                }

                // Thêm sản phẩm vào cơ sở dữ liệu
                await _productRepository.AddAsync(product);
                TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Nếu có lỗi, trả lại view
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(product);
        }

        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);  // Trả về view hiển thị chi tiết sản phẩm
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl, IFormFile[] additionalImages)
        {
            ModelState.Remove("ImageUrl");

            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);

                // Lưu ảnh chính mới (nếu có)
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }
                else
                {
                    product.ImageUrl = existingProduct.ImageUrl;
                }
       

                // Tính giá sau giảm
                if (product.Price > 0 && product.DiscountPercent > 0)
                {
                    product.DiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100); // Giảm giá theo phần trăm
                }
                else
                {
                    product.DiscountedPrice = product.Price; // Nếu không có giảm giá, giữ nguyên giá gốc
                }

                // Cập nhật các thuộc tính khác của sản phẩm
                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.DiscountPercent = product.DiscountPercent;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.ImageUrl = product.ImageUrl;
                existingProduct.DiscountedPrice = product.DiscountedPrice;  // Lưu giá sau giảm

                await _productRepository.UpdateAsync(existingProduct);

                TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(product);
        }


        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Lấy sản phẩm từ cơ sở dữ liệu
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Xóa ảnh liên quan đến sản phẩm khỏi cơ sở dữ liệu trước
            if (product.Images != null && product.Images.Any())
            {
                foreach (var image in product.Images)
                {
                    // Xóa ảnh khỏi cơ sở dữ liệu
                    _context.ProductImages.Remove(image);

                    // Xóa tệp ảnh khỏi thư mục
                    var imagePath = Path.Combine("wwwroot/images", Path.GetFileName(image.Url));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);  // Xóa ảnh từ hệ thống tệp
                    }
                }

                // Lưu thay đổi để đảm bảo ảnh được xóa trong cơ sở dữ liệu
                await _context.SaveChangesAsync();
            }

            // Sau khi xóa ảnh, xóa sản phẩm
            await _productRepository.DeleteAsync(id);

            TempData["SuccessMessage"] = "Sản phẩm và các ảnh liên kết đã được xóa thành công!";
            return RedirectToAction("Index", "Product", new { area = "Admin" });
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
    }
}
