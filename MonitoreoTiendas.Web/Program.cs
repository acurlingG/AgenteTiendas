var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddHttpClient("MonitorApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5080/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseRouting();
app.MapRazorPages();

app.Run();