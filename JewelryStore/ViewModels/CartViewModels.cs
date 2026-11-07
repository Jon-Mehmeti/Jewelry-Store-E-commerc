using System.ComponentModel.DataAnnotations;
using JewelryStore.Models;

namespace JewelryStore.ViewModels
{
    public class CartLineViewModel
    {
        public Product Product { get; init; } = null!;
        public int Quantity { get; init; }
        public decimal UnitPrice => Product.DiscountPrice ?? Product.Price;
        public decimal Total => UnitPrice * Quantity;
    }

    public class CartViewModel
    {
        public IReadOnlyList<CartLineViewModel> Items { get; init; } = [];
        public decimal SubTotal => Items.Sum(item => item.Total);
    }

    public class CheckoutViewModel
    {
        [Required, MaxLength(200)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string PaymentMethod { get; set; } = "CashOnDelivery";
    }
}
