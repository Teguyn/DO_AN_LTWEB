using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using WebBanDienThoai.Extensions; // Đảm bảo bạn có dịch vụ quản lý giỏ hàng
using WebBanDienThoai.Models;
using WebBanDienThoai.Services;
using WebBanDienThoai.Services.Momo;
using WebBanDienThoai.Services.VNPay;

namespace WebBanDienThoai.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IMomoService _momoService;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<PayPalSettings> _paypalSettings;
        private readonly ApplicationDbContext _dbContext;

        public PaymentController(IMomoService momoService, IConfiguration config, UserManager<ApplicationUser> userManager, IOptions<PayPalSettings> paypalSettings, ApplicationDbContext dbContext)
        {
            _momoService = momoService;
            _config = config;
            _userManager = userManager;
            _paypalSettings = paypalSettings;
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model)
        {
            var userName = HttpContext.User.Identity.IsAuthenticated ? HttpContext.User.Identity.Name : "";
            var response = await _momoService.CreatePaymentMomo(model, userName);
            return Redirect(response.PayUrl);
        }

        [HttpGet]
        public IActionResult PaymentCallBack()
        {
            var query = HttpContext.Request.Query;
            var response = _momoService.PaymentExecuteAsync(query);

            if (response != null && response.OrderId != null)
            {
                var userName = HttpContext.User.Identity.Name ?? "Khách chưa đăng nhập";
                var user = _userManager.FindByNameAsync(userName).Result;

                var order = new Order
                {
                    UserId = user?.Id ?? "Guest",
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = decimal.Parse(response.Amount),
                    ShippingAddress = user?.Address ?? "N/A",
                    Notes = "Thanh toán qua MoMo"
                };
                _dbContext.Orders.Add(order);
                _dbContext.SaveChanges();

                var momoInfo = new MomoInfoModel
                {
                    OrderId = order.Id,
                    MomoOrderId = response.OrderId,
                    OrderInfo = response.OrderInfo,
                    FullName = user?.FullName ?? userName,
                    Amount = decimal.Parse(response.Amount),
                    DatePaid = DateTime.UtcNow
                };
                _dbContext.MomoInfos.Add(momoInfo);
                _dbContext.SaveChanges();

                var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
                if (cart != null)
                {
                    cart.Items.Clear();
                    HttpContext.Session.SetObjectAsJson("Cart", cart);
                }
            }

            return View(response);
        }

        [HttpPost]
        public IActionResult PaymentVNPay()
        {
            var vnpay = new VNPayLibrary();
            string vnp_Returnurl = _config["VNPay:ReturnUrl"];
            string vnp_Url = _config["VNPay:PaymentUrl"];
            string vnp_TmnCode = _config["VNPay:TmnCode"];
            string vnp_HashSecret = _config["VNPay:HashSecret"];

            string amount = Request.Form["Amount"];
            string orderId = DateTime.Now.Ticks.ToString();
            string createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

            vnpay.AddRequestData("vnp_Version", _config["VNPay:Version"]);
            vnpay.AddRequestData("vnp_Command", _config["VNPay:Command"]);
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", (Convert.ToDecimal(amount) * 100).ToString("0"));
            vnpay.AddRequestData("vnp_CreateDate", createDate);
            vnpay.AddRequestData("vnp_CurrCode", _config["VNPay:CurrCode"]);
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString());
            vnpay.AddRequestData("vnp_Locale", _config["VNPay:Locale"]);
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toán đơn hàng #" + orderId);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", orderId);

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            return Redirect(paymentUrl);
        }

        public IActionResult PaymentVNPayCallback()
        {
            string responseCode = Request.Query["vnp_ResponseCode"];
            if (responseCode == "00")
            {
                var vnpOrderId = Request.Query["vnp_TxnRef"];
                var orderInfo = Request.Query["vnp_OrderInfo"];
                var amount = int.Parse(Request.Query["vnp_Amount"]) / 100m;
                var userName = HttpContext.User.Identity.Name ?? "Khách chưa đăng nhập";
                var user = _userManager.FindByNameAsync(userName).Result;

                try
                {
                    var order = new Order
                    {
                        UserId = user?.Id ?? "Guest",
                        OrderDate = DateTime.UtcNow,
                        TotalPrice = amount,
                        ShippingAddress = user?.Address ?? "N/A",
                        Notes = "Thanh toán qua VNPay"
                    };
                    _dbContext.Orders.Add(order);
                    _dbContext.SaveChanges();

                    var momoInfo = new MomoInfoModel
                    {
                        OrderId = order.Id,
                        MomoOrderId = vnpOrderId,
                        OrderInfo = orderInfo,
                        FullName = user?.FullName ?? userName,
                        Amount = amount,
                        DatePaid = DateTime.UtcNow
                    };
                    _dbContext.MomoInfos.Add(momoInfo);
                    _dbContext.SaveChanges();

                    var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
                    if (cart != null)
                    {
                        foreach (var item in cart.Items)
                        {
                            var orderDetail = new OrderDetail
                            {
                                OrderId = order.Id,
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                Price = item.Price
                            };
                            _dbContext.OrderDetails.Add(orderDetail);
                        }
                        _dbContext.SaveChanges();

                        cart.Items.Clear();
                        HttpContext.Session.SetObjectAsJson("Cart", cart);
                    }

                    ViewBag.OrderId = vnpOrderId;
                    ViewBag.OrderInfo = orderInfo;
                    ViewBag.Amount = amount.ToString("N0");
                    ViewBag.Message = "Success";
                }
                catch (Exception ex)
                {
                    ViewBag.Message = "Lỗi khi lưu thông tin thanh toán VNPay: " + ex.Message;
                    return View("PaymentCallBack");
                }
            }
            else
            {
                ViewBag.Message = "Fail";
            }

            return View("PaymentCallBack");
        }

        public IActionResult Paypal()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            decimal totalVND = cart?.Items.Sum(i => i.Price * i.Quantity) ?? 0;
            decimal totalUSD = Math.Round(totalVND / 24000, 2);
            string totalAmountUSD = totalUSD.ToString("0.00", CultureInfo.InvariantCulture);

            ViewBag.TotalAmount = totalAmountUSD;
            ViewBag.TotalVND = totalVND;
            ViewBag.ClientId = _paypalSettings.Value.ClientId;
            ViewBag.Currency = _paypalSettings.Value.Currency;

            return View();
        }

        public async Task<IActionResult> PaypalSuccess()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            decimal total = cart?.Items.Sum(i => i.Price * i.Quantity) ?? 0;
            var paypalOrderId = Guid.NewGuid().ToString("N").Substring(0, 12);

            string fullName = "Khách chưa đăng nhập";
            string userId = "Guest";
            ApplicationUser? user = null;

            if (User.Identity.IsAuthenticated)
            {
                user = await _userManager.GetUserAsync(User);
                fullName = user?.FullName ?? User.Identity.Name;
                userId = user?.Id ?? "Guest";
            }

            try
            {
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = total,
                    ShippingAddress = user?.Address ?? "N/A",
                    Notes = "Thanh toán qua PayPal"
                };
                _dbContext.Orders.Add(order);
                _dbContext.SaveChanges();

                var momoInfo = new MomoInfoModel
                {
                    OrderId = order.Id,
                    MomoOrderId = paypalOrderId,
                    OrderInfo = $"Khách hàng: {fullName}. Nội dung đơn hàng: Thanh toán PayPal tại WebBanDienThoai",
                    FullName = fullName,
                    Amount = total,
                    DatePaid = DateTime.UtcNow
                };
                _dbContext.MomoInfos.Add(momoInfo);
                _dbContext.SaveChanges();

                if (cart != null && cart.Items.Any())
                {
                    foreach (var item in cart.Items)
                    {
                        var orderDetail = new OrderDetail
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            Price = item.Price
                        };
                        _dbContext.OrderDetails.Add(orderDetail);
                    }
                    _dbContext.SaveChanges();

                    cart.Items.Clear();
                    HttpContext.Session.SetObjectAsJson("Cart", cart);
                }

                ViewBag.Message = "PaypalSuccess";
                ViewBag.Amount = total;
                ViewBag.OrderId = paypalOrderId;
                ViewBag.OrderInfo = $"Khách hàng: {fullName}. Nội dung đơn hàng: Thanh toán PayPal tại WebBanDienThoai";
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin thanh toán PayPal: " + ex.Message;
                return View("PaymentCallBack");
            }

            return View("PaymentCallBack");
        }

        [HttpPost]
        public async Task<IActionResult> CashOnDelivery(OrderInfoModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("CheckoutCart");

            if (cart == null || !cart.Items.Any())
            {
                TempData["PaymentError"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var order = new Order
            {
                UserId = user?.Id ?? "Guest",
                OrderDate = DateTime.UtcNow,
                TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity),
                ShippingAddress = model.ShippingAddress, // ✅ LẤY TỪ FORM
                Notes = "Thanh toán khi nhận hàng (COD)"
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            foreach (var item in cart.Items)
            {
                var detail = new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                };
                _dbContext.OrderDetails.Add(detail);
            }

            await _dbContext.SaveChangesAsync();

            cart.Items.Clear();
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            ViewBag.Message = "CODSuccess";
            ViewBag.OrderId = order.Id;
            ViewBag.Amount = order.TotalPrice;
            ViewBag.OrderInfo = $"Khách hàng: {model.FullName}. Địa chỉ: {model.ShippingAddress}. SĐT: {model.PhoneNumber}";

            return View("PaymentCallBack");
        }
        [HttpPost]
        public async Task<IActionResult> SubmitOrder(OrderInfoModel model)
        {
            var method = Request.Form["PaymentMethod"];
            if (string.IsNullOrEmpty(method))
            {
                TempData["Error"] = "Bạn chưa chọn phương thức thanh toán!";
                return RedirectToAction("ThongTinGiaoHang");
            }

            switch (method)
            {
                case "MoMo":
                    return await CreatePaymentMomo(model);

                case "VNPay":
                    return PaymentVNPay();

                case "PayPal":
                    return RedirectToAction("Paypal");

                case "COD":
                    return await CashOnDelivery(model);

                default:
                    TempData["Error"] = "Phương thức thanh toán không hợp lệ!";
                    return RedirectToAction("ThongTinGiaoHang");
            }
        }
        public IActionResult ThongTinGiaoHang()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || !cart.Items.Any())
            {
                TempData["PaymentError"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index", "ShoppingCart");
            }

            ViewBag.Cart = cart;
            return View(new OrderInfoModel());
        }



    }
}