using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

namespace OElite.Restme.MongoDb.IntegrationTests.Models;

/// <summary>
/// Test entity representing an order for complex relationship testing
/// Demonstrates arrays of embedded documents and multiple denormalized fields
/// </summary>
[DbCollection("test_orders")]
public class TestOrder : TestBaseEntity
{
    [DbField("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [DbField("customer_id")]
    public DbObjectId CustomerId { get; set; }

    [DbField("order_date")]
    public DateTime OrderDate { get; set; }

    [DbField("order_status")]
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

    [DbField("items")]
    public List<OrderItem> Items { get; set; } = new();

    [DbField("shipping_address")]
    public Address? ShippingAddress { get; set; }

    [DbField("billing_address")]
    public Address? BillingAddress { get; set; }

    [DbField("payment_info")]
    public PaymentInfo? PaymentInfo { get; set; }

    [DbField("subtotal")]
    public decimal Subtotal { get; set; }

    [DbField("tax_amount")]
    public decimal TaxAmount { get; set; }

    [DbField("shipping_amount")]
    public decimal ShippingAmount { get; set; }

    [DbField("total_amount")]
    public decimal TotalAmount { get; set; }

    [DbField("notes")]
    public string? Notes { get; set; }

    [DenormalizedField("test_customers", "{ '_id': @CustomerId }")]
    public TestCustomer? Customer { get; set; }
}

/// <summary>
/// Embedded document for order items
/// </summary>
public class OrderItem
{
    [DbField("product_id")]
    public DbObjectId ProductId { get; set; }

    [DbField("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [DbField("quantity")]
    public int Quantity { get; set; }

    [DbField("unit_price")]
    public decimal UnitPrice { get; set; }

    [DbField("line_total")]
    public decimal LineTotal { get; set; }

    [DbField("product_snapshot")]
    public Dictionary<string, object> ProductSnapshot { get; set; } = new();
}

/// <summary>
/// Embedded document for payment information
/// </summary>
public class PaymentInfo
{
    [DbField("payment_method")]
    public PaymentMethod PaymentMethod { get; set; }

    [DbField("transaction_id")]
    public string? TransactionId { get; set; }

    [DbField("payment_date")]
    public DateTime? PaymentDate { get; set; }

    [DbField("is_paid")]
    public bool IsPaid { get; set; }

    [DbField("card_last_four")]
    public string? CardLastFour { get; set; }
}

/// <summary>
/// Order status enumeration
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6
}

/// <summary>
/// Payment method enumeration
/// </summary>
public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    PayPal = 2,
    BankTransfer = 3,
    Cash = 4,
    Cryptocurrency = 5
}