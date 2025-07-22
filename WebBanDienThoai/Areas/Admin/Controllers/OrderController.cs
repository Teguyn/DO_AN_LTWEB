using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 📦 Hiển thị danh sách đơn hàng
        public async Task<IActionResult> Index(string? statusFilter, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .AsQueryable();

            // 🎯 Lọc theo trạng thái nếu có
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse(statusFilter, out OrderStatus status))
            {
                query = query.Where(o => o.Status == status);
            }

            query = query.OrderByDescending(o => o.OrderDate);

            var orders = await PaginatedList<Order>.CreateAsync(query.AsNoTracking(), pageNumber, pageSize);
            return View(orders);
        }

        // ✅ Chi tiết đơn hàng
        //public async Task<IActionResult> Details(int id)
        //{
        //    var order = await _context.Orders
        //        .Include(o => o.ApplicationUser)
        //        .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
        //        .FirstOrDefaultAsync(o => o.Id == id);

        //    if (order == null) return NotFound();
        //    return View(order);
        //}

        // 🔁 Xác nhận đơn
        [HttpPost]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Confirmed;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ❌ Hủy đơn
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // 📊 Quản lý doanh thu (gộp theo tháng, ngày, trạng thái...)
        public IActionResult Revenue()
        {
            var revenue = _context.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .GroupBy(o => o.OrderDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(o => o.TotalPrice)
                }).ToList();

            return Json(revenue);
        }

        public async Task<IActionResult> AdminOrders(
            string? statusFilter,
            string? userFilter,
            int pageNumber = 1,
            int pageSize = 10)
            {
            var query = _context.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<OrderStatus>(statusFilter, out var status))
            {
                query = query.Where(o => o.Status == status);
            }

            // Lọc theo tên người dùng
            if (!string.IsNullOrEmpty(userFilter))
            {
                query = query.Where(o => o.ApplicationUser.FullName.Contains(userFilter));
            }

            query = query.OrderByDescending(o => o.OrderDate);

            var orders = await PaginatedList<Order>.CreateAsync(query.AsNoTracking(), pageNumber, pageSize);
            ViewData["StatusFilter"] = statusFilter;
            ViewData["UserFilter"] = userFilter;

            return View(orders);
        }
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }


    }
}
