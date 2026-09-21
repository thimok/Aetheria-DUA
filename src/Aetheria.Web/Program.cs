
using Aetheria.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder
    .Services
    .AddHttpClient<ParkOperationsClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://operations-api");
    });

builder
    .CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();

app.UseHttpsRedirection();

app.MapDefaultEndpoints(); //Aspire health endpoints


app
    .UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
