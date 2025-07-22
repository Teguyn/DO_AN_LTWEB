using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Helpers
{
    public static class OrderStatusHelper
    {
        public static IHtmlContent DisplayOrderStatus(this IHtmlHelper html, OrderStatus status)
        {
            var (label, badgeClass) = status switch
            {
                OrderStatus.Pending => ("Chờ xác nhận", "badge bg-warning text-dark"),
                OrderStatus.Confirmed => ("Đã xác nhận", "badge bg-info text-white"),
                OrderStatus.Shipping => ("Đang giao hàng", "badge bg-primary"),
                OrderStatus.Completed => ("Đã hoàn thành", "badge bg-success"),
                OrderStatus.Cancelled => ("Đã hủy", "badge bg-danger"),
                _ => ("Không xác định", "badge bg-secondary")
            };

            var htmlString = $"<span class=\"{badgeClass} rounded-pill\">{label}</span>";
            return new HtmlString(htmlString);
        }
    }
}
