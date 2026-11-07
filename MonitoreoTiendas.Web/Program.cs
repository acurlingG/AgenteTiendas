var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Obtener la URL base desde la configuración.
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? "http://localhost:5241/backend/";

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