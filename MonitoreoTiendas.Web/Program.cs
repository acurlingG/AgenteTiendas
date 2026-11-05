var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// 1. Obtener la URL base desde la configuración (o usar localhost por defecto si no está definida)
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5242/";

// 2. Asignar la URL dinámica al HttpClient
builder.Services.AddHttpClient("MonitorApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseRouting();
app.MapRazorPages();

app.Run();