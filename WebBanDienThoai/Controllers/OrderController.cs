using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using WebBanDienThoai.Helpers;

[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, int pageNumber = 1, int pageSize = 5)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var query = _context.Orders
            .Where(o => o.UserId == user.Id)
            .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
            .AsQueryable();

        // Tìm kiếm theo mã đơn hoặc ngày
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(o =>
                o.Id.ToString().Contains(searchTerm) ||
                o.OrderDate.ToString("dd/MM/yyyy").Contains(searchTerm));
        }

        // Lọc theo trạng thái
        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<OrderStatus>(statusFilter, out var status))
        {
            query = query.Where(o => o.Status == status);
        }

        // Sắp xếp mới nhất lên đầu
        query = query.OrderByDescending(o => o.OrderDate);

        // Gửi dữ liệu lọc vào ViewData để giữ lại giá trị
        ViewData["SearchTerm"] = searchTerm;
        ViewData["StatusFilter"] = statusFilter;

        // Phân trang
        var pagedOrders = await PaginatedList<Order>.CreateAsync(query.AsNoTracking(), pageNumber, pageSize);

        return View(pagedOrders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.Orders
            .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

        if (order == null || order.Status != OrderStatus.Pending)
            return BadRequest("Không thể hủy đơn.");

        order.Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();

        return RedirectToAction("Index");
    }
}
