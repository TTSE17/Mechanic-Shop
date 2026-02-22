using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Common;

/*
    The real problem
     By default, xUnit creates a new test class instance per test.
    
    Without a collection:
     New WebAppFactory
     New SQL container
     New database
     New app host
     ❌ Slow + flaky 
     
    What ICollectionFixture<WebAppFactory> does
     “Create ONE WebAppFactory
     Share it across ALL tests in this collection”
    ✔ Same SQL container
    ✔ Same ASP.NET Core host
    ✔ Faster tests
    ✔ Controlled lifecycle
*/

[CollectionDefinition(CollectionName)]
public class WebAppFactoryCollection : ICollectionFixture<WebAppFactory>
{
    public const string CollectionName = "WebAppFactoryCollection";
}