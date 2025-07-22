using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class Product
    {     
            public int Id { get; set; }
            [Required, StringLength(100)] public string Name { get; set; }
            [Range(0.01, 100000000.00)]
            public decimal Price { get; set; }
            public string Description { get; set; }
            public string? ImageUrl { get; set; }
            public List<ProductImage>? Images { get; set; }
            public int CategoryId { get; set; }      
            public Category? Category { get; set; }
            public int Rating { get; set; }
            public int DiscountPercent { get; set; }
        public decimal DiscountedPrice { get; set; } // Để lưu giá sau giảm
    }
}
