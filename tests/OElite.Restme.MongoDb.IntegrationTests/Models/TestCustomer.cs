using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

namespace OElite.Restme.MongoDb.IntegrationTests.Models;

/// <summary>
/// Test entity representing a customer for relationship testing
/// Used with denormalized fields in TestOrder
/// </summary>
[DbCollection("test_customers")]
public class TestCustomer : TestBaseEntity
{
    [DbField("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [DbField("last_name")]
    public string LastName { get; set; } = string.Empty;

    [DbField("email")]
    public string Email { get; set; } = string.Empty;

    [DbField("phone")]
    public string? Phone { get; set; }

    [DbField("date_of_birth")]
    public DateTime? DateOfBirth { get; set; }

    [DbField("gender")]
    public Gender? Gender { get; set; }

    [DbField("is_active")]
    public bool IsActive { get; set; } = true;

    [DbField("is_verified")]
    public bool IsVerified { get; set; }

    [DbField("registration_date")]
    public DateTime RegistrationDate { get; set; }

    [DbField("last_login_date")]
    public DateTime? LastLoginDate { get; set; }

    [DbField("preferred_language")]
    public string PreferredLanguage { get; set; } = "en";

    [DbField("addresses")]
    public List<CustomerAddress> Addresses { get; set; } = new();

    [DbField("preferences")]
    public CustomerPreferences? Preferences { get; set; }

    [DbField("loyalty_points")]
    public int LoyaltyPoints { get; set; }

    [DbField("total_orders")]
    public int TotalOrders { get; set; }

    [DbField("total_spent")]
    public decimal TotalSpent { get; set; }

    [DenormalizedCollection("test_orders", "{ 'customer_id': @Id }", limit: 100, sort: "{ 'order_date': -1 }")]
    public List<TestOrder> Orders { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Embedded document for customer addresses
/// </summary>
public class CustomerAddress
{
    [DbField("address_type")]
    public AddressType AddressType { get; set; }

    [DbField("is_default")]
    public bool IsDefault { get; set; }

    [DbField("address")]
    public Address Address { get; set; } = new();
}

/// <summary>
/// Embedded document for customer preferences
/// </summary>
public class CustomerPreferences
{
    [DbField("newsletter_subscribed")]
    public bool NewsletterSubscribed { get; set; }

    [DbField("sms_notifications")]
    public bool SmsNotifications { get; set; }

    [DbField("email_notifications")]
    public bool EmailNotifications { get; set; }

    [DbField("preferred_categories")]
    public List<DbObjectId> PreferredCategories { get; set; } = new();

    [DbField("favorite_brands")]
    public List<string> FavoriteBrands { get; set; } = new();

    [DbField("budget_range")]
    public BudgetRange? BudgetRange { get; set; }
}

/// <summary>
/// Embedded document for budget range
/// </summary>
public class BudgetRange
{
    [DbField("min_amount")]
    public decimal MinAmount { get; set; }

    [DbField("max_amount")]
    public decimal MaxAmount { get; set; }

    [DbField("currency")]
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Gender enumeration
/// </summary>
public enum Gender
{
    Male = 0,
    Female = 1,
    Other = 2,
    PreferNotToSay = 3
}

/// <summary>
/// Address type enumeration
/// </summary>
public enum AddressType
{
    Home = 0,
    Work = 1,
    Shipping = 2,
    Billing = 3,
    Other = 4
}