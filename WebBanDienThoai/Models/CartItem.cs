﻿namespace WebBanDienThoai.Models
{
    public class CartItem
    {
        public string ImageUrl { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}