using Finance.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finance.Data.UnitTests.Repositories;

public class RepositoryLifetimeTests
{
    [Fact]
    public void Resolving_repository_twice_from_same_provider_yields_same_instance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
        services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();

        using var provider = services.BuildServiceProvider();

        var transactionsA = provider.GetRequiredService<ITransactionRepository>();
        var transactionsB = provider.GetRequiredService<ITransactionRepository>();
        Assert.Same(transactionsA, transactionsB);

        var categoriesA = provider.GetRequiredService<ICategoryRepository>();
        var categoriesB = provider.GetRequiredService<ICategoryRepository>();
        Assert.Same(categoriesA, categoriesB);
    }
}
