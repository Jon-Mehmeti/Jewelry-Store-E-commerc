using JewelryStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JewelryStore.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var role in new[] { "Admin", "Customer" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminEmail = configuration["Admin:Email"];
            var adminPassword = configuration["Admin:Password"];
            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var admin = await userManager.FindByEmailAsync(adminEmail);
                if (admin is null)
                {
                    admin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        FirstName = "Store",
                        LastName = "Administrator"
                    };

                    var result = await userManager.CreateAsync(admin, adminPassword);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
                    }
                }

                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
            else
            {
                logger.LogWarning("Admin account was not initialized because Admin:Email and Admin:Password are not configured.");
            }

            async Task<Category> GetOrCreateCategoryAsync(string name, string description)
            {
                var category = await context.Categories.SingleOrDefaultAsync(item => item.Name == name);
                if (category is not null)
                {
                    return category;
                }

                category = new Category { Name = name, Description = description, IsActive = true };
                context.Categories.Add(category);
                await context.SaveChangesAsync();
                return category;
            }

            var rings = await GetOrCreateCategoryAsync("Unaza", "Unaza elegante për çdo rast.");
            var necklaces = await GetOrCreateCategoryAsync("Qafore", "Qafore të rafinuara me përfundim premium.");
            var bracelets = await GetOrCreateCategoryAsync("Byzylykë", "Detaje luksoze për stilin e përditshëm.");
            var earrings = await GetOrCreateCategoryAsync("Vathë", "Vathë të lehtë dhe elegantë për çdo ditë.");
            var sets = await GetOrCreateCategoryAsync("Komplete", "Komplete të kuruara për dhurata dhe raste speciale.");
            var accessories = await GetOrCreateCategoryAsync("Aksesorë", "Detaje të veçanta që plotësojnë stilin tuaj.");

            var products = new[]
            {
                new Product { Name = "Unaza Aurelia", SKU = "RING-AURELIA", Description = "Unazë elegante me dizajn të përjetshëm dhe shkëlqim delikat.", Price = 129.99m, Material = "Ari 18K", Color = "Ari", StockQuantity = 12, IsFeatured = true, Category = rings, ImageUrl = "/images/products/aurelia-ring.jpg" },
                new Product { Name = "Qafore Lumière", SKU = "NECK-LUMIERE", Description = "Qafore minimaliste që sjell një prekje të rafinuar në çdo veshje.", Price = 189.99m, DiscountPrice = 169.99m, Material = "Ari i bardhë", Color = "Argjend", StockQuantity = 8, IsFeatured = true, Category = necklaces, ImageUrl = "/images/products/lumiere-necklace.jpg" },
                new Product { Name = "Byzylyk Élan", SKU = "BRAC-ELAN", Description = "Byzylyk modern me linja të pastra dhe karakter të veçantë.", Price = 99.99m, Material = "Ari 14K", Color = "Ari", StockQuantity = 20, IsFeatured = true, Category = bracelets, ImageUrl = "/images/products/elan-bracelet.jpg" },
                new Product { Name = "Unaza Solis", SKU = "RING-SOLIS", Description = "Unazë me gur zirkoni që kap dritën në çdo lëvizje.", Price = 159.99m, DiscountPrice = 139.99m, Material = "Ari 14K", Color = "Ari", StockQuantity = 14, IsFeatured = true, Category = rings },
                new Product { Name = "Unaza Mira", SKU = "RING-MIRA", Description = "Formë e pastër dhe elegante për stil të përditshëm.", Price = 89.99m, Material = "Argjend 925", Color = "Argjend", StockQuantity = 24, Category = rings },
                new Product { Name = "Qafore Luna", SKU = "NECK-LUNA", Description = "Varëse delikate me medalion hëne, perfekte për dhuratë.", Price = 119.99m, Material = "Ari 14K", Color = "Ari", StockQuantity = 18, IsFeatured = true, Category = necklaces },
                new Product { Name = "Qafore Amara", SKU = "NECK-AMARA", Description = "Qafore statement me linja moderne dhe shkëlqim premium.", Price = 249.99m, DiscountPrice = 219.99m, Material = "Ari 18K", Color = "Ari", StockQuantity = 6, IsFeatured = true, Category = necklaces },
                new Product { Name = "Byzylyk Serena", SKU = "BRAC-SERENA", Description = "Byzylyk i hollë që kombinohet lehtë me çdo orë ose aksesor.", Price = 79.99m, Material = "Argjend 925", Color = "Argjend", StockQuantity = 30, Category = bracelets },
                new Product { Name = "Byzylyk Royal", SKU = "BRAC-ROYAL", Description = "Detaj luksoz për mbrëmje dhe raste të veçanta.", Price = 299.99m, Material = "Ari 18K", Color = "Ari", StockQuantity = 5, IsFeatured = true, Category = bracelets },
                new Product { Name = "Vathë Celeste", SKU = "EARR-CELESTE", Description = "Vathë të rafinuar me gur të ndritshëm dhe mbyllje të sigurt.", Price = 109.99m, DiscountPrice = 94.99m, Material = "Ari 14K", Color = "Ari", StockQuantity = 16, IsFeatured = true, Category = earrings },
                new Product { Name = "Vathë Perla", SKU = "EARR-PERLA", Description = "Perla klasike për një pamje elegante dhe të përjetshme.", Price = 139.99m, Material = "Argjend 925", Color = "E bardhë", StockQuantity = 11, Category = earrings },
                new Product { Name = "Kompleti Signature", SKU = "SET-SIGNATURE", Description = "Qafore dhe vathë të koordinuar për një dhuratë të paharrueshme.", Price = 349.99m, DiscountPrice = 299.99m, Material = "Ari 14K", Color = "Ari", StockQuantity = 7, IsFeatured = true, Category = sets },
                new Product { Name = "Kompleti Infinity", SKU = "SET-INFINITY", Description = "Set romantik me simbolin e pafundësisë për momente speciale.", Price = 279.99m, Material = "Ari i bardhë", Color = "Argjend", StockQuantity = 9, Category = sets },
                new Product { Name = "Kuti Dhurate PrinciGold", SKU = "ACC-GIFTBOX", Description = "Kuti premium dhurate me prezantim elegant për çdo porosi.", Price = 14.99m, Material = "Karton premium", Color = "E zezë", StockQuantity = 50, Category = accessories },
                new Product { Name = "Varëse Iniciali", SKU = "ACC-INITIAL", Description = "Aksesor personal me inicialin tuaj, ideal për dhuratë.", Price = 69.99m, Material = "Argjend 925", Color = "Ari", StockQuantity = 22, Category = accessories }
            };

            foreach (var product in products)
            {
                if (!await context.Products.AnyAsync(item => item.SKU == product.SKU))
                {
                    context.Products.Add(product);
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("JewelryStore database was initialized with roles and catalog seed data.");
        }
    }
}
