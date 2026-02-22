namespace MechanicShop.Api.IntegrationTests.Common;

// This is the Builder Pattern
public interface ITestDataBuilder<out T>
{
    T Build();
}