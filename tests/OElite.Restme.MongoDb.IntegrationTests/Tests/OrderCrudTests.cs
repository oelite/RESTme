using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// CRUD integration tests for TestOrder entity
/// Tests complex embedded documents, arrays, and order-specific operations
/// </summary>
public class OrderCrudTests : TestBase
{
    [Fact]
    public async Task InsertOneAsync_ShouldInsertOrder_WhenValidOrderWithComplexStructure()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var order = CreateTestOrder();

        // Act
        await collection.InsertOneAsync(order);

        // Assert
        var retrievedOrder = await collection.FindOneAsync(o => o.Id == order.Id);
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.OrderNumber.Should().Be(order.OrderNumber);
        retrievedOrder.OrderStatus.Should().Be(order.OrderStatus);
        retrievedOrder.Items.Should().HaveCount(order.Items.Count);
        retrievedOrder.TotalAmount.Should().Be(order.TotalAmount);
        retrievedOrder.ShippingAddress.Should().NotBeNull();
        retrievedOrder.PaymentInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task FindAsync_ShouldReturnOrdersByStatus_WhenFilteringByOrderStatus()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var orders = new[]
        {
            CreateTestOrder(status: OrderStatus.Pending),
            CreateTestOrder(status: OrderStatus.Confirmed),
            CreateTestOrder(status: OrderStatus.Shipped),
            CreateTestOrder(status: OrderStatus.Pending)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act
        var pendingOrders = await collection.FindAsync(o => o.OrderStatus == OrderStatus.Pending);
        var shippedOrders = await collection.FindAsync(o => o.OrderStatus == OrderStatus.Shipped);

        // Assert
        pendingOrders.Should().HaveCount(2);
        pendingOrders.All(o => o.OrderStatus == OrderStatus.Pending).Should().BeTrue();
        shippedOrders.Should().HaveCount(1);
        shippedOrders[0].OrderStatus.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task FindAsync_ShouldFilterByDateRange_WhenOrderDateInRange()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var baseDate = DateTime.UtcNow.Date;
        var orders = new[]
        {
            CreateTestOrder(orderDate: baseDate.AddDays(-10)),
            CreateTestOrder(orderDate: baseDate.AddDays(-5)),
            CreateTestOrder(orderDate: baseDate.AddDays(-1)),
            CreateTestOrder(orderDate: baseDate.AddDays(1))
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act - Find orders from last 7 days
        var cutoffDate = baseDate.AddDays(-7);
        var recentOrders = await collection.FindAsync(o => o.OrderDate >= cutoffDate);

        // Assert
        recentOrders.Should().HaveCount(3);
        recentOrders.All(o => o.OrderDate >= cutoffDate).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_WithDictionaryFilter_ShouldFilterByTotalAmountRange()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var orders = new[]
        {
            CreateTestOrder(totalAmount: 50.00m),
            CreateTestOrder(totalAmount: 150.00m),
            CreateTestOrder(totalAmount: 250.00m),
            CreateTestOrder(totalAmount: 350.00m)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act - Find orders between $100 and $300
        var filter = new Dictionary<string, object>
        {
            { "total_amount", new Dictionary<string, object>
                {
                    { "$gte", 100.00m },
                    { "$lte", 300.00m }
                }
            }
        };
        var results = await collection.FindAsync(filter);

        // Assert
        results.Should().HaveCount(2);
        results.All(o => o.TotalAmount >= 100.00m && o.TotalAmount <= 300.00m).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_ShouldFilterByPaymentMethod_WhenPaymentInfoExists()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var orders = new[]
        {
            CreateTestOrder(paymentMethod: PaymentMethod.CreditCard),
            CreateTestOrder(paymentMethod: PaymentMethod.PayPal),
            CreateTestOrder(paymentMethod: PaymentMethod.CreditCard),
            CreateTestOrder(paymentMethod: PaymentMethod.BankTransfer)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act
        var filter = new Dictionary<string, object>
        {
            { "payment_info.payment_method", (int)PaymentMethod.CreditCard }
        };
        var creditCardOrders = await collection.FindAsync(filter);

        // Assert
        creditCardOrders.Should().HaveCount(2);
        creditCardOrders.All(o => o.PaymentInfo!.PaymentMethod == PaymentMethod.CreditCard).Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceOneAsync_ShouldUpdateOrderStatus_WhenOrderProcessed()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var order = CreateTestOrder(status: OrderStatus.Pending);
        await collection.InsertOneAsync(order);

        // Modify the order
        order.OrderStatus = OrderStatus.Confirmed;
        order.PaymentInfo!.IsPaid = true;
        order.PaymentInfo.PaymentDate = DateTime.UtcNow;
        order.PaymentInfo.TransactionId = "TXN-123456789";
        order.UpdatedOnUtc = DateTime.UtcNow;

        // Act
        var result = await collection.ReplaceOneAsync(o => o.Id == order.Id, order);

        // Assert
        result.Should().BeTrue();

        var updatedOrder = await collection.FindOneAsync(o => o.Id == order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.OrderStatus.Should().Be(OrderStatus.Confirmed);
        updatedOrder.PaymentInfo!.IsPaid.Should().BeTrue();
        updatedOrder.PaymentInfo.TransactionId.Should().Be("TXN-123456789");
    }

    [Fact]
    public async Task DeleteManyAsync_ShouldDeleteCancelledOrders_WhenFilterMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var orders = new[]
        {
            CreateTestOrder(status: OrderStatus.Pending),
            CreateTestOrder(status: OrderStatus.Cancelled),
            CreateTestOrder(status: OrderStatus.Shipped),
            CreateTestOrder(status: OrderStatus.Cancelled)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act
        var deletedCount = await collection.DeleteManyAsync(o => o.OrderStatus == OrderStatus.Cancelled);

        // Assert
        deletedCount.Should().Be(2);

        var remainingOrders = await collection.FindAsync(o => true);
        remainingOrders.Should().HaveCount(2);
        remainingOrders.All(o => o.OrderStatus != OrderStatus.Cancelled).Should().BeTrue();
    }

    [Fact]
    public async Task CountDocumentsAsync_ShouldReturnCorrectCounts_ForOrderStatusGroups()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        // Clear collection to ensure clean test state
        await collection.DeleteManyAsync(o => true);
        var orders = new[]
        {
            CreateTestOrder(status: OrderStatus.Pending),
            CreateTestOrder(status: OrderStatus.Pending),
            CreateTestOrder(status: OrderStatus.Confirmed),
            CreateTestOrder(status: OrderStatus.Shipped),
            CreateTestOrder(status: OrderStatus.Delivered),
            CreateTestOrder(status: OrderStatus.Cancelled)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act
        var totalCount = await collection.CountDocumentsAsync(o => true);
        var pendingCount = await collection.CountDocumentsAsync(o => o.OrderStatus == OrderStatus.Pending);
        var activeCount = await collection.CountDocumentsAsync(o =>
            o.OrderStatus != OrderStatus.Cancelled && o.OrderStatus != OrderStatus.Delivered);
        var completedCount = await collection.CountDocumentsAsync(o => o.OrderStatus == OrderStatus.Delivered);

        // Assert
        totalCount.Should().Be(6);
        pendingCount.Should().Be(2);
        activeCount.Should().Be(3); // Pending (2) + Confirmed (1)
        completedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExistsAsync_ShouldCheckOrderExistence_ByOrderNumber()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var order = CreateTestOrder();
        order.OrderNumber = "ORD-UNIQUE-12345";
        await collection.InsertOneAsync(order);

        // Act
        var existsByOrderNumber = await collection.ExistsAsync(o => o.OrderNumber == "ORD-UNIQUE-12345");
        var notExists = await collection.ExistsAsync(o => o.OrderNumber == "ORD-NONEXISTENT");

        // Assert
        existsByOrderNumber.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task FindAsync_ShouldQueryOrderItems_UsingArrayFilters()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        // Clear collection to ensure clean test state
        await collection.DeleteManyAsync(o => true);
        var product1Id = DbObjectId.NewId();
        var product2Id = DbObjectId.NewId();

        var orders = new[]
        {
            CreateTestOrderWithSpecificItems(product1Id),
            CreateTestOrderWithSpecificItems(product2Id),
            CreateTestOrderWithSpecificItems(product1Id, product2Id)
        };

        foreach (var order in orders)
        {
            await collection.InsertOneAsync(order);
        }

        // Act - Find orders containing specific product
        var filter = new Dictionary<string, object>
        {
            { "items.product_id", product1Id.ToString() }
        };
        var ordersWithProduct1 = await collection.FindAsync(filter);

        // Assert
        ordersWithProduct1.Should().HaveCount(2);
        ordersWithProduct1.All(o => o.Items.Any(item => item.ProductId == product1Id)).Should().BeTrue();
    }

    [Fact]
    public async Task InsertOneAsync_ShouldHandleComplexOrderWithAllEmbeddedDocuments()
    {
        // Arrange
        var collection = GetDbCollection<TestOrder>();
        var complexOrder = CreateComplexTestOrder();

        // Act
        await collection.InsertOneAsync(complexOrder);

        // Assert
        var retrievedOrder = await collection.FindOneAsync(o => o.Id == complexOrder.Id);
        retrievedOrder.Should().NotBeNull();

        // Verify all embedded documents
        retrievedOrder!.Items.Should().HaveCount(3);
        retrievedOrder.ShippingAddress.Should().NotBeNull();
        retrievedOrder.ShippingAddress!.Country.Should().Be("USA");
        retrievedOrder.BillingAddress.Should().NotBeNull();
        retrievedOrder.PaymentInfo.Should().NotBeNull();
        retrievedOrder.PaymentInfo!.CardLastFour.Should().Be("4321");

        // Verify item details
        var firstItem = retrievedOrder.Items[0];
        firstItem.ProductSnapshot.Should().ContainKey("brand");
        firstItem.ProductSnapshot["brand"].Should().Be("Premium Brand");
    }

    private static TestOrder CreateTestOrder(
        OrderStatus status = OrderStatus.Pending,
        DateTime? orderDate = null,
        decimal totalAmount = 199.99m,
        PaymentMethod paymentMethod = PaymentMethod.CreditCard)
    {
        var customerId = DbObjectId.NewId();
        var order = new TestOrder
        {
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}",
            CustomerId = customerId,
            OrderDate = orderDate ?? DateTime.UtcNow,
            OrderStatus = status,
            Subtotal = totalAmount * 0.85m,
            TaxAmount = totalAmount * 0.10m,
            ShippingAmount = totalAmount * 0.05m,
            TotalAmount = totalAmount,
            Notes = "Test order for integration testing"
        };

        // Add sample items
        order.Items.Add(new OrderItem
        {
            ProductId = DbObjectId.NewId(),
            ProductName = "Test Product 1",
            Quantity = 2,
            UnitPrice = 50.00m,
            LineTotal = 100.00m,
            ProductSnapshot = new Dictionary<string, object>
            {
                { "sku", "TEST-001" },
                { "category", "Electronics" }
            }
        });

        order.Items.Add(new OrderItem
        {
            ProductId = DbObjectId.NewId(),
            ProductName = "Test Product 2",
            Quantity = 1,
            UnitPrice = 69.99m,
            LineTotal = 69.99m,
            ProductSnapshot = new Dictionary<string, object>
            {
                { "sku", "TEST-002" },
                { "category", "Accessories" }
            }
        });

        // Add addresses
        order.ShippingAddress = new Address
        {
            Street = "123 Test Street",
            City = "Test City",
            State = "TS",
            PostalCode = "12345",
            Country = "USA"
        };

        order.BillingAddress = new Address
        {
            Street = "456 Billing Ave",
            City = "Billing City",
            State = "BC",
            PostalCode = "67890",
            Country = "USA"
        };

        // Add payment info
        order.PaymentInfo = new PaymentInfo
        {
            PaymentMethod = paymentMethod,
            IsPaid = false,
            CardLastFour = "1234"
        };

        return order;
    }

    private static TestOrder CreateTestOrderWithSpecificItems(params DbObjectId[] productIds)
    {
        var order = CreateTestOrder();
        order.Items.Clear();

        foreach (var productId in productIds)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                ProductName = $"Product {productId}",
                Quantity = 1,
                UnitPrice = 50.00m,
                LineTotal = 50.00m,
                ProductSnapshot = new Dictionary<string, object>
                {
                    { "sku", $"SKU-{productId}" }
                }
            });
        }

        return order;
    }

    private static TestOrder CreateComplexTestOrder()
    {
        var order = new TestOrder
        {
            OrderNumber = "ORD-COMPLEX-789",
            CustomerId = DbObjectId.NewId(),
            OrderDate = DateTime.UtcNow.AddDays(-2),
            OrderStatus = OrderStatus.Shipped,
            Subtotal = 485.97m,
            TaxAmount = 48.60m,
            ShippingAmount = 15.43m,
            TotalAmount = 550.00m,
            Notes = "Complex order with multiple items and detailed tracking"
        };

        // Add multiple complex items
        order.Items.AddRange(new[]
        {
            new OrderItem
            {
                ProductId = DbObjectId.NewId(),
                ProductName = "Premium Laptop",
                Quantity = 1,
                UnitPrice = 299.99m,
                LineTotal = 299.99m,
                ProductSnapshot = new Dictionary<string, object>
                {
                    { "sku", "LAPTOP-PRE-001" },
                    { "brand", "Premium Brand" },
                    { "category", "Electronics" },
                    { "warranty_months", 24 },
                    { "specifications", new Dictionary<string, object>
                        {
                            { "processor", "Intel i7" },
                            { "ram", "16GB" },
                            { "storage", "512GB SSD" }
                        }
                    }
                }
            },
            new OrderItem
            {
                ProductId = DbObjectId.NewId(),
                ProductName = "Wireless Mouse",
                Quantity = 2,
                UnitPrice = 49.99m,
                LineTotal = 99.98m,
                ProductSnapshot = new Dictionary<string, object>
                {
                    { "sku", "MOUSE-WRL-002" },
                    { "brand", "TechCorp" },
                    { "category", "Accessories" },
                    { "color", "Black" },
                    { "battery_life", "6 months" }
                }
            },
            new OrderItem
            {
                ProductId = DbObjectId.NewId(),
                ProductName = "USB-C Hub",
                Quantity = 1,
                UnitPrice = 86.00m,
                LineTotal = 86.00m,
                ProductSnapshot = new Dictionary<string, object>
                {
                    { "sku", "HUB-USBC-003" },
                    { "brand", "ConnectPro" },
                    { "category", "Accessories" },
                    { "ports", new[] { "HDMI", "USB 3.0 x3", "SD Card", "Power Delivery" } }
                }
            }
        });

        // Add detailed addresses
        order.ShippingAddress = new Address
        {
            Street = "789 Enterprise Boulevard, Suite 420",
            City = "Tech City",
            State = "CA",
            PostalCode = "94105",
            Country = "USA"
        };

        order.BillingAddress = new Address
        {
            Street = "321 Corporate Drive",
            City = "Business Park",
            State = "CA",
            PostalCode = "94107",
            Country = "USA"
        };

        // Add detailed payment info
        order.PaymentInfo = new PaymentInfo
        {
            PaymentMethod = PaymentMethod.CreditCard,
            TransactionId = "TXN-COMPLEX-987654",
            PaymentDate = DateTime.UtcNow.AddDays(-2),
            IsPaid = true,
            CardLastFour = "4321"
        };

        return order;
    }
}