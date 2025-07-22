using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> RevenueByDate(DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Orders
                .Where(o => o.Status == OrderStatus.Confirmed);

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate.Date >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(o => o.OrderDate.Date <= toDate.Value.Date);

            var statistics = await query
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new RevenueStatisticViewModel
                {
                    Date = g.Key,
                    OrderCount = g.Count(),
                    TotalRevenue = g.Sum(o => o.TotalPrice)
                })
                .OrderBy(s => s.Date)
                .ToListAsync();

            return View(statistics);
        }
    }
}
