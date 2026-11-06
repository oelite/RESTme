using System.Linq.Expressions;
using System.Reflection;
using OElite;
using OElite.Restme.MongoDb;
using Xunit;

namespace OElite.Restme.UnitTests;

/// <summary>
/// Restme #26 / compatibility: metadata-only checks; no provider or client is constructed.
/// These checks preserve existing caller signatures, not database execution semantics.
/// </summary>
[Trait("Category", "Unit")]
public class MongoDbCollectionCompatibilityContractTests
{
    public static IEnumerable<object[]> ExistingSignatures()
    {
        var expression = typeof(Expression<Func<BaseEntity, bool>>);
        var dictionary = typeof(Dictionary<string, object>);
        var cancellation = typeof(CancellationToken);
        yield return new object[] { "InsertOneAsync", typeof(Task), new[] { typeof(BaseEntity), cancellation } };
        foreach (var filter in new[] { expression, dictionary })
        {
            yield return new object[] { "FindAsync", typeof(Task<List<BaseEntity>>), new[] { filter, cancellation } };
            yield return new object[] { "FindOneAsync", typeof(Task<BaseEntity>), new[] { filter, cancellation } };
            yield return new object[] { "ReplaceOneAsync", typeof(Task<bool>), new[] { filter, typeof(BaseEntity), cancellation } };
            yield return new object[] { "UpdateOneAsync", typeof(Task<bool>), new[] { filter, dictionary, cancellation } };
            yield return new object[] { "UpdateManyAsync", typeof(Task<long>), new[] { filter, dictionary, cancellation } };
            yield return new object[] { "DeleteManyAsync", typeof(Task<long>), new[] { filter, cancellation } };
            yield return new object[] { "DeleteOneAsync", typeof(Task<long>), new[] { filter, cancellation } };
            yield return new object[] { "CountDocumentsAsync", typeof(Task<long>), new[] { filter, cancellation } };
            yield return new object[] { "ExistsAsync", typeof(Task<bool>), new[] { filter, cancellation } };
        }
    }

    [Theory]
    [MemberData(nameof(ExistingSignatures))]
    public void Issue26_ExistingCallerSignaturesRemainAvailable(string name, Type returnType, Type[] parameters)
    {
        var method = typeof(IMongoDbCollection<BaseEntity>).GetMethod(name, parameters);

        Assert.NotNull(method);
        Assert.Equal(returnType, method.ReturnType);
        var cancellation = method.GetParameters()[^1];
        Assert.True(cancellation.IsOptional);
        Assert.True(cancellation.HasDefaultValue);
    }

    [Fact]
    public void Issue26_TypedCollectionSignaturesDoNotExposeMongoDriverOrBsonTypes()
    {
        foreach (var method in typeof(IMongoDbCollection<BaseEntity>).GetMethods())
        {
            AssertProviderNeutral(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AssertProviderNeutral(parameter.ParameterType);
            }
        }
    }

    [Fact]
    public void Issue26_CollectionStillAcceptsExistingBaseEntityTypes()
    {
        var argument = typeof(IMongoDbCollection<>).GetGenericArguments().Single();

        Assert.Contains(typeof(BaseEntity), argument.GetGenericParameterConstraints());
        Assert.Equal(GenericParameterAttributes.None,
            argument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint);
        Assert.True(typeof(IMongoDbCollection<BaseEntity>).IsInterface);
    }

    private static void AssertProviderNeutral(Type type)
    {
        Assert.False(type.Namespace?.StartsWith("MongoDB.", StringComparison.Ordinal) == true,
            $"Consumer signature exposes provider type {type.FullName}.");
        if (type.HasElementType)
        {
            AssertProviderNeutral(type.GetElementType()!);
        }
        foreach (var argument in type.GetGenericArguments())
        {
            AssertProviderNeutral(argument);
        }
    }
}
