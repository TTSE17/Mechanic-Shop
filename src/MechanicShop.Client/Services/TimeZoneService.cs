using Microsoft.JSInterop;

namespace MechanicShop.Client.Services;

public sealed class TimeZoneService(IJSRuntime js)
{
    public async Task<string> GetLocalTimeZoneAsync()
    {
        return await js.InvokeAsync<string>("getLocalTimeZone");
    }
}