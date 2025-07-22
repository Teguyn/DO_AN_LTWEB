using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;

namespace WebBanDienThoai.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public ShoppingCartController(ApplicationDbContext context,
        UserManager<ApplicationUser> userManager, IProductRepository
        productRepository)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult CheckoutInfo()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("CheckoutCart") ?? new ShoppingCart();
            var model = new OrderInfoModel(); // hoặc dùng ViewModel nếu cần nhiều dữ liệu
            ViewBag.Cart = cart;
            return View(model);
        }


        [HttpPost]
        public IActionResult Checkout(string QuantitiesJson)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            if (string.IsNullOrEmpty(QuantitiesJson))
            {
                TempData["PaymentError"] = "Vui lòng chọn ít nhất một sản phẩm để thanh toán.";
                return RedirectToAction("Index");
            }

            var selected = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(QuantitiesJson);

            if (selected == null || !selected.Any())
            {
                TempData["PaymentError"] = "Không có sản phẩm nào được chọn.";
                return RedirectToAction("Index");
            }

            var checkoutCart = new ShoppingCart();

            foreach (var entry in selected)
            {
                var productId = entry.Key;
                var quantity = entry.Value;

                var existing = cart.Items.FirstOrDefault(x => x.ProductId == productId);
                if (existing != null)
                {
                    checkoutCart.Items.Add(new CartItem
                    {
                        ProductId = existing.ProductId,
                        Name = existing.Name,
                        Price = existing.Price,
                        Quantity = quantity,
                        ImageUrl = existing.ImageUrl
                    });
                }
            }

            HttpContext.Session.SetObjectAsJson("CheckoutCart", checkoutCart);
            return RedirectToAction("CheckoutInfo");
        }


        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return Json(new { count = cart.Items.Count });
        }

        [Authorize]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.DiscountedPrice, // ✅ Lấy giá đã giảm
                    Quantity = quantity,
                    ImageUrl = product.ImageUrl
                });

                TempData["SuccessMessage"] = $"Đã thêm <strong>{product.Name}</strong> vào giỏ hàng!";
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Display", "Product", new { id = productId });
        }


        public IActionResult Index()
        {
            var cart =
            HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new
            ShoppingCart();
            return View(cart);
        }
        // Các actions khác...
        private async Task<Product> GetProductFromDatabase(int productId)
        {
            // Truy vấn cơ sở dữ liệu để lấy thông tin sản phẩm
            var product = await _productRepository.GetByIdAsync(productId);
            return product;
        }
        public IActionResult RemoveFromCart(int productId)
        {
            var cart =
            HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart is not null)
            {
                cart.RemoveItem(productId);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }
        [HttpPost]
        public IActionResult UpdateCart(Dictionary<int, int> Quantities)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            foreach (var entry in Quantities)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductId == entry.Key);
                if (item != null)
                {
                    item.Quantity = entry.Value > 0 ? entry.Value : 1;
                }
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Index");
        }


    }
}