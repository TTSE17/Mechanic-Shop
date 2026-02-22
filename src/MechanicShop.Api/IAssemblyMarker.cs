namespace MechanicShop.Api;

/*
    What problem does this solve?
     WebApplicationFactory<TEntryPoint> needs a type that lives in the API assembly to know:
     ❝ Which ASP.NET Core app should I boot for tests? ❞
     Normally you'd pass:  WebApplicationFactory<Program>

    But:
     Program might be internal
     Or you don’t want to couple tests to Program
     Or you're following Clean Architecture
     Solution → Assembly Marker
     
    IAssemblyMarker is:
     Empty
     Lives in MechanicShop.Api
     Used only to point to the API assembly
     public class WebAppFactory : WebApplicationFactory<IAssemblyMarker>

    ➡️ This tells ASP.NET Test Host:
     “Spin up the app defined in MechanicShop.Api”
    
    📌 Rule of thumb
     Assembly marker = “Hey tests, this is the assembly where the API starts.” 
*/

public class IAssemblyMarker;