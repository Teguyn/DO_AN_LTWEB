using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public decimal TotalPrice { get; set; }

        [Required]
        public string ShippingAddress { get; set; }

        public string? Notes { get; set; }

        // ✅ Thêm trạng thái đơn hàng
        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; }

        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }

    // ✅ Enum trạng thái đơn hàng
    public enum OrderStatus
    {
        Pending,        // Chờ xác nhận
        Confirmed,      // Đã xác nhận
        Shipping,       // Đang giao
        Completed,      // Đã nhận hàng
        Cancelled       // Đã hủy
    }
}
