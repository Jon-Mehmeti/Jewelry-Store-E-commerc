using System.ComponentModel.DataAnnotations;

namespace JewelryStore.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsMain { get; set; }

        public int DisplayOrder { get; set; }

        public int ProductId { get; set; }

        public Product? Product { get; set; }
    }
}