using InventoryApp.Application.Abstractions;
using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Enums;
using InventoryApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryApp.Infrastructure.Seed;

/// <summary>
/// Creates the schema if needed and populates a realistic starting dataset.
/// Idempotent: it does nothing once users already exist.
/// </summary>
public sealed class DatabaseSeeder(
    InventoryDbContext db,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly Random Rng = new(20250815);

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        // If migrations have been generated, apply them; otherwise create the schema directly.
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any() ||
            (await db.Database.GetAppliedMigrationsAsync(ct)).Any())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded; skipping.");
            return;
        }

        logger.LogInformation("Seeding sample inventory data...");

        var users = SeedUsers();
        await db.Users.AddRangeAsync(users, ct);

        var categories = SeedCategories();
        await db.Categories.AddRangeAsync(categories, ct);

        var suppliers = SeedSuppliers();
        await db.Suppliers.AddRangeAsync(suppliers, ct);

        await db.SaveChangesAsync(ct);

        var products = SeedProducts(categories, suppliers);
        await db.Products.AddRangeAsync(products, ct);
        await db.SaveChangesAsync(ct);

        var manager = users.First(u => u.Role == UserRole.InventoryManager);
        var staff = users.First(u => u.Role == UserRole.Staff);

        await SeedOpeningMovementsAsync(products, manager, ct);
        await SeedPurchasesAsync(products, suppliers, manager, ct);
        await SeedSalesAsync(products, staff, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed complete: {Products} products, {Categories} categories, {Suppliers} suppliers.",
            products.Count, categories.Count, suppliers.Count);
    }

    private List<User> SeedUsers() =>
    [
        new()
        {
            Username = "admin", Email = "admin@inventoryapp.local", FullName = "Aarati Shrestha",
            PasswordHash = passwordHasher.Hash("Admin@123"), Role = UserRole.Administrator, IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-8)
        },
        new()
        {
            Username = "manager", Email = "manager@inventoryapp.local", FullName = "Bikash Thapa",
            PasswordHash = passwordHasher.Hash("Manager@123"), Role = UserRole.InventoryManager, IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-6)
        },
        new()
        {
            Username = "staff", Email = "staff@inventoryapp.local", FullName = "Sita Gurung",
            PasswordHash = passwordHasher.Hash("Staff@123"), Role = UserRole.Staff, IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-3)
        },
        new()
        {
            Username = "rmaharjan", Email = "rmaharjan@inventoryapp.local", FullName = "Rajesh Maharjan",
            PasswordHash = passwordHasher.Hash("Staff@123"), Role = UserRole.Staff, IsActive = false,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-2)
        }
    ];

    private static List<Category> SeedCategories() =>
    [
        New("Laptops & Notebooks", "Portable computers and accessories"),
        New("Desktops & Workstations", "Tower systems, all-in-ones and workstations"),
        New("Monitors & Displays", "External monitors, projectors and screens"),
        New("Peripherals", "Keyboards, mice, webcams and docking stations"),
        New("Networking", "Routers, switches, access points and cabling"),
        New("Storage", "Internal drives, external drives and memory cards"),
        New("Printers & Consumables", "Printers, toner, ink and paper"),
        New("Office Supplies", "Everyday stationery and consumables")
    ];

    private static Category New(string name, string description) => new()
    {
        Name = name,
        Description = description,
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow.AddMonths(-8),
        UpdatedAtUtc = DateTime.UtcNow.AddMonths(-8)
    };

    private static List<Supplier> SeedSuppliers() =>
    [
        new()
        {
            CompanyName = "Everest Technologies Pvt. Ltd.", ContactPerson = "Nabin Karki",
            Email = "sales@everesttech.com.np", Phone = "+977-1-4412233",
            Address = "Putalisadak, Kathmandu, Nepal", Notes = "Primary supplier for laptops. Net 30 terms.",
            IsActive = true, CreatedAtUtc = DateTime.UtcNow.AddMonths(-8)
        },
        new()
        {
            CompanyName = "Himalayan Computer House", ContactPerson = "Sujata Rai",
            Email = "orders@himalayancomputer.com", Phone = "+977-1-4785566",
            Address = "New Road, Kathmandu, Nepal", Notes = "Good pricing on monitors and peripherals.",
            IsActive = true, CreatedAtUtc = DateTime.UtcNow.AddMonths(-7)
        },
        new()
        {
            CompanyName = "Global Networks Supply Co.", ContactPerson = "Dipesh Adhikari",
            Email = "procurement@globalnetworks.com", Phone = "+977-1-5539900",
            Address = "Lalitpur, Bagmati, Nepal", Notes = "Networking hardware specialist. Lead time 2 weeks.",
            IsActive = true, CreatedAtUtc = DateTime.UtcNow.AddMonths(-6)
        },
        new()
        {
            CompanyName = "Office Essentials Trading", ContactPerson = "Manisha Poudel",
            Email = "info@officeessentials.com.np", Phone = "+977-1-4223311",
            Address = "Baneshwor, Kathmandu, Nepal", Notes = "Stationery and printer consumables.",
            IsActive = true, CreatedAtUtc = DateTime.UtcNow.AddMonths(-5)
        },
        new()
        {
            CompanyName = "Peak Storage Distributors", ContactPerson = "Anil Shrestha",
            Email = "anil@peakstorage.com", Phone = "+977-1-4667788",
            Address = "Chabahil, Kathmandu, Nepal", Notes = "SSD and HDD distributor.",
            IsActive = true, CreatedAtUtc = DateTime.UtcNow.AddMonths(-4)
        },
        new()
        {
            CompanyName = "Legacy Parts Importers", ContactPerson = "Kiran Basnet",
            Email = "kiran@legacyparts.com", Phone = "+977-1-4990011",
            Address = "Balaju, Kathmandu, Nepal", Notes = "No longer trading - kept for historical records.",
            IsActive = false, CreatedAtUtc = DateTime.UtcNow.AddMonths(-8)
        }
    ];

    private static List<Product> SeedProducts(List<Category> categories, List<Supplier> suppliers)
    {
        var laptops = categories[0];
        var desktops = categories[1];
        var monitors = categories[2];
        var peripherals = categories[3];
        var networking = categories[4];
        var storage = categories[5];
        var printers = categories[6];
        var office = categories[7];

        var everest = suppliers[0];
        var himalayan = suppliers[1];
        var globalNet = suppliers[2];
        var officeSupply = suppliers[3];
        var peak = suppliers[4];

        var definitions = new (string Sku, string Name, string Description, Category Cat, Supplier Sup,
            decimal Cost, decimal Price, int Stock, int Min, int Max, string Uom)[]
        {
            ("LAP-1001", "ProBook 14 Core i5", "14-inch business laptop, 16GB RAM, 512GB SSD", laptops, everest, 78000, 94500, 24, 6, 60, "pcs"),
            ("LAP-1002", "ProBook 15 Core i7", "15.6-inch business laptop, 16GB RAM, 1TB SSD", laptops, everest, 112000, 134900, 11, 5, 40, "pcs"),
            ("LAP-1003", "UltraSlim 13 Core i7", "13-inch ultrabook, 16GB RAM, 1TB SSD", laptops, everest, 128000, 152000, 4, 5, 30, "pcs"),
            ("LAP-1004", "Workhorse 15 Ryzen 7", "15.6-inch productivity laptop, 16GB RAM", laptops, himalayan, 89000, 106000, 0, 4, 25, "pcs"),
            ("DSK-2001", "TowerPro T400 Desktop", "Mid-tower desktop, Core i5, 16GB RAM, 512GB SSD", desktops, everest, 68000, 82000, 15, 4, 30, "pcs"),
            ("DSK-2002", "TowerPro T700 Workstation", "Workstation, Core i9, 32GB RAM, 2TB SSD", desktops, everest, 185000, 219000, 3, 2, 12, "pcs"),
            ("DSK-2003", "CompactBox Mini PC", "Small form factor mini PC, 8GB RAM", desktops, himalayan, 42000, 51500, 18, 5, 40, "pcs"),
            ("MON-3001", "ClearView 24 IPS Monitor", "24-inch 1080p IPS display", monitors, himalayan, 18500, 23900, 42, 10, 90, "pcs"),
            ("MON-3002", "ClearView 27 QHD Monitor", "27-inch 1440p IPS display", monitors, himalayan, 31000, 38500, 19, 8, 50, "pcs"),
            ("MON-3003", "ClearView 32 4K Monitor", "32-inch 4K UHD display", monitors, himalayan, 54000, 65900, 6, 4, 25, "pcs"),
            ("PER-4001", "Mechanical Keyboard MK80", "Wired mechanical keyboard, brown switches", peripherals, himalayan, 4200, 6200, 57, 15, 120, "pcs"),
            ("PER-4002", "Wireless Mouse WM20", "2.4GHz wireless optical mouse", peripherals, himalayan, 1250, 1990, 128, 30, 250, "pcs"),
            ("PER-4003", "USB-C Docking Station D5", "11-in-1 USB-C dock with dual HDMI", peripherals, everest, 9800, 13500, 9, 10, 45, "pcs"),
            ("PER-4004", "HD Webcam W90", "1080p webcam with dual microphone", peripherals, himalayan, 3400, 4990, 33, 12, 80, "pcs"),
            ("PER-4005", "Noise Cancelling Headset H7", "USB headset with boom microphone", peripherals, himalayan, 5600, 7900, 21, 10, 60, "pcs"),
            ("NET-5001", "Gigabit Switch 24-Port", "Managed 24-port gigabit switch", networking, globalNet, 24500, 31000, 8, 3, 20, "pcs"),
            ("NET-5002", "Wi-Fi 6 Access Point AX3", "Ceiling mount dual band access point", networking, globalNet, 14200, 18900, 14, 6, 40, "pcs"),
            ("NET-5003", "Cat6 Patch Cable 3m", "Shielded Cat6 patch cable", networking, globalNet, 320, 590, 240, 50, 500, "pcs"),
            ("NET-5004", "Rack Mount Router R100", "1U enterprise edge router", networking, globalNet, 68000, 84000, 2, 2, 10, "pcs"),
            ("STO-6001", "NVMe SSD 1TB", "PCIe Gen4 NVMe internal SSD", storage, peak, 11500, 15200, 46, 15, 100, "pcs"),
            ("STO-6002", "NVMe SSD 2TB", "PCIe Gen4 NVMe internal SSD", storage, peak, 21800, 27900, 17, 8, 50, "pcs"),
            ("STO-6003", "Portable SSD 1TB", "USB-C external solid state drive", storage, peak, 14200, 18500, 12, 10, 45, "pcs"),
            ("STO-6004", "Enterprise HDD 8TB", "7200rpm SATA enterprise hard drive", storage, peak, 26500, 33000, 5, 5, 30, "pcs"),
            ("PRN-7001", "LaserJet Mono Printer L200", "A4 monochrome laser printer", printers, officeSupply, 22000, 28500, 7, 3, 20, "pcs"),
            ("PRN-7002", "Toner Cartridge TN-200", "Black toner cartridge, 2600 pages", printers, officeSupply, 4800, 6900, 26, 12, 80, "pcs"),
            ("PRN-7003", "A4 Copy Paper 80gsm", "500 sheet ream, premium white", printers, officeSupply, 480, 750, 310, 60, 600, "ream"),
            ("OFF-8001", "Whiteboard Marker Set", "Assorted colour dry-erase markers, pack of 4", office, officeSupply, 220, 420, 95, 25, 200, "pack"),
            ("OFF-8002", "Box File A4", "Lever arch box file", office, officeSupply, 260, 480, 0, 20, 150, "pcs"),
            ("OFF-8003", "Sticky Notes 76x76mm", "Pack of 5 pads, assorted colours", office, officeSupply, 180, 350, 64, 20, 180, "pack")
        };

        var products = new List<Product>();
        var index = 0;

        foreach (var d in definitions)
        {
            var created = DateTime.UtcNow.AddDays(-(150 - index * 4));
            var product = new Product
            {
                Sku = d.Sku,
                Name = d.Name,
                Description = d.Description,
                Category = d.Cat,
                CategoryId = d.Cat.Id,
                Supplier = d.Sup,
                SupplierId = d.Sup.Id,
                CostPrice = d.Cost,
                UnitPrice = d.Price,
                MinimumStock = d.Min,
                MaximumStock = d.Max,
                UnitOfMeasure = d.Uom,
                Barcode = $"88{700000000 + index * 137:D10}",
                Location = index % 5 == 0 ? "Branch Store" : "Main Warehouse",
                IsActive = d.Sku != "OFF-8002",
                CreatedAtUtc = created,
                UpdatedAtUtc = created
            };

            product.SetOpeningStock(d.Stock);
            products.Add(product);
            index++;
        }

        return products;
    }

    private async Task SeedOpeningMovementsAsync(List<Product> products, User user, CancellationToken ct)
    {
        var movements = new List<StockMovement>();

        foreach (var product in products.Where(p => p.CurrentStock > 0))
        {
            movements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = MovementType.StockIn,
                Quantity = product.CurrentStock,
                PreviousQuantity = 0,
                NewQuantity = product.CurrentStock,
                Reason = "Opening stock balance",
                Reference = $"OPEN-{product.Sku}",
                UserId = user.Id,
                UserName = user.FullName,
                FromLocation = product.Location,
                ToLocation = product.Location,
                CreatedAtUtc = product.CreatedAtUtc
            });
        }

        // A handful of adjustments so the movement history looks lived-in.
        foreach (var product in products.Where(p => p.CurrentStock > 10).Take(6))
        {
            var day = Rng.Next(5, 60);
            movements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = MovementType.Adjustment,
                Quantity = -1,
                PreviousQuantity = product.CurrentStock + 1,
                NewQuantity = product.CurrentStock,
                Reason = "Physical count correction - damaged unit written off",
                Reference = $"ADJ-{day:D3}",
                UserId = user.Id,
                UserName = user.FullName,
                FromLocation = product.Location,
                ToLocation = product.Location,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-day)
            });
        }

        await db.StockMovements.AddRangeAsync(movements, ct);
    }

    private async Task SeedPurchasesAsync(List<Product> products, List<Supplier> suppliers, User user, CancellationToken ct)
    {
        var orders = new List<PurchaseOrder>();
        var year = DateTime.UtcNow.Year;
        var sequence = 1;

        var plans = new (Supplier Supplier, PurchaseStatus Status, int DaysAgo, string[] Skus, int[] Quantities)[]
        {
            (suppliers[0], PurchaseStatus.Received, 92, ["LAP-1001", "LAP-1002"], [10, 5]),
            (suppliers[1], PurchaseStatus.Received, 61, ["MON-3001", "MON-3002", "PER-4001"], [20, 8, 25]),
            (suppliers[4], PurchaseStatus.Received, 34, ["STO-6001", "STO-6002"], [20, 8]),
            (suppliers[3], PurchaseStatus.Received, 18, ["PRN-7003", "OFF-8001"], [100, 40]),
            (suppliers[2], PurchaseStatus.Ordered, 6, ["NET-5001", "NET-5002"], [5, 6]),
            (suppliers[0], PurchaseStatus.Draft, 1, ["LAP-1003", "PER-4003"], [6, 10])
        };

        foreach (var plan in plans)
        {
            var orderDate = DateTime.UtcNow.AddDays(-plan.DaysAgo);
            var order = new PurchaseOrder
            {
                OrderNumber = $"PO-{year}-{sequence++:D4}",
                SupplierId = plan.Supplier.Id,
                OrderDateUtc = orderDate,
                ExpectedDateUtc = orderDate.AddDays(14),
                ReceivedDateUtc = plan.Status == PurchaseStatus.Received ? orderDate.AddDays(10) : null,
                Status = plan.Status,
                TaxRate = 13m,
                DiscountAmount = 0m,
                Notes = plan.Status == PurchaseStatus.Draft ? "Awaiting budget approval." : string.Empty,
                CreatedByUserId = user.Id,
                CreatedByName = user.FullName,
                CreatedAtUtc = orderDate,
                UpdatedAtUtc = orderDate
            };

            for (var i = 0; i < plan.Skus.Length; i++)
            {
                var product = products.First(p => p.Sku == plan.Skus[i]);
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductId = product.Id,
                    Quantity = plan.Quantities[i],
                    UnitCost = product.CostPrice
                });
            }

            order.Recalculate();
            orders.Add(order);
        }

        await db.PurchaseOrders.AddRangeAsync(orders, ct);
    }

    private async Task SeedSalesAsync(List<Product> products, User user, CancellationToken ct)
    {
        var sales = new List<Sale>();
        var year = DateTime.UtcNow.Year;
        var sequence = 1;

        var customers = new[]
        {
            "Kathmandu Valley College", "Sunrise Bank Branch Office", "Walk-in customer",
            "Nepal Digital Solutions", "Trisuli Trading House", "Walk-in customer",
            "Pokhara Tech Institute", "Bagmati Logistics Ltd."
        };

        var baskets = new (string[] Skus, int[] Quantities, int DaysAgo, SaleStatus Status)[]
        {
            (["LAP-1001", "PER-4002"], [2, 2], 74, SaleStatus.Completed),
            (["MON-3001", "PER-4001", "PER-4002"], [4, 4, 4], 55, SaleStatus.Completed),
            (["PER-4002", "OFF-8003"], [3, 5], 41, SaleStatus.Completed),
            (["DSK-2001", "MON-3002"], [2, 2], 33, SaleStatus.Completed),
            (["NET-5003", "PER-4004"], [30, 5], 22, SaleStatus.Cancelled),
            (["PRN-7002", "PRN-7003"], [4, 20], 15, SaleStatus.Completed),
            (["STO-6001", "STO-6003"], [3, 2], 8, SaleStatus.Completed),
            (["LAP-1002", "PER-4003"], [1, 1], 2, SaleStatus.Completed)
        };

        for (var index = 0; index < baskets.Length; index++)
        {
            var basket = baskets[index];
            var saleDate = DateTime.UtcNow.AddDays(-basket.DaysAgo);

            var sale = new Sale
            {
                SaleNumber = $"SO-{year}-{sequence++:D4}",
                CustomerName = customers[index],
                SaleDateUtc = saleDate,
                Status = basket.Status,
                TaxRate = 13m,
                DiscountAmount = index % 3 == 0 ? 500m : 0m,
                Notes = basket.Status == SaleStatus.Cancelled ? "Customer cancelled before dispatch." : string.Empty,
                CreatedByUserId = user.Id,
                CreatedByName = user.FullName,
                CreatedAtUtc = saleDate,
                UpdatedAtUtc = saleDate
            };

            for (var i = 0; i < basket.Skus.Length; i++)
            {
                var product = products.First(p => p.Sku == basket.Skus[i]);
                sale.Items.Add(new SaleItem
                {
                    ProductId = product.Id,
                    Quantity = basket.Quantities[i],
                    UnitPrice = product.UnitPrice
                });
            }

            sale.Recalculate();
            sales.Add(sale);
        }

        await db.Sales.AddRangeAsync(sales, ct);

        // Matching ledger rows for completed sales. Seeded stock levels already account for these,
        // so the history explains the current balances rather than changing them.
        var movements = new List<StockMovement>();
        foreach (var sale in sales.Where(s => s.Status == SaleStatus.Completed))
        {
            foreach (var item in sale.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                movements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = MovementType.Sale,
                    Quantity = -item.Quantity,
                    PreviousQuantity = product.CurrentStock + item.Quantity,
                    NewQuantity = product.CurrentStock,
                    Reason = $"Sold on {sale.SaleNumber}",
                    Reference = sale.SaleNumber,
                    UserId = user.Id,
                    UserName = user.FullName,
                    FromLocation = product.Location,
                    ToLocation = product.Location,
                    CreatedAtUtc = sale.SaleDateUtc
                });
            }
        }

        await db.StockMovements.AddRangeAsync(movements, ct);
    }
}
