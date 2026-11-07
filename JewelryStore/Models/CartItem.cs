using System.ComponentModel.DataAnnotations;

namespace JewelryStore.Models
{
	public class CartItem
	{
		public int Id { get; set; }

		public int CartId { get; set; }

		public Cart? Cart { get; set; }

		public int ProductId { get; set; }

		public Product? Product { get; set; }

		[Range(1, 100)]
		public int Quantity { get; set; }

		public DateTime AddedAt { get; set; } = DateTime.UtcNow;
	}
}