using JewelryStore.Models;

namespace JewelryStore.ViewModels
{
    public class HomeViewModel
    {
        public IReadOnlyList<Product> FeaturedProducts { get; init; } = [];
        public IReadOnlyList<Category> Categories { get; init; } = [];
    }

    public class CatalogViewModel
    {
        public IReadOnlyList<Product> Products { get; init; } = [];
        public IReadOnlyList<Category> Categories { get; init; } = [];
        public string? Search { get; init; }
        public int? CategoryId { get; init; }
        public string Sort { get; init; } = "featured";
    }
}
