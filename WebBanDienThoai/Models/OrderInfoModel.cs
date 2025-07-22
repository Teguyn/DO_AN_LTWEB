namespace WebBanDienThoai.Models
{
    public class OrderInfoModel
    {
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string ShippingAddress { get; set; }

        public string OrderId { get; set; }
        public string OrderInformation { get; set; }

        public decimal Amount { get; set; }
    }
}
