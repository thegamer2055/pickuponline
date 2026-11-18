using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json.Serialization;
using PickUp.Server;
using PickUp.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// The browser game is hosted by itch.io while SignalR runs on this server.
// Configure additional origins with the AllowedOrigins environment variable
// (comma-separated, e.g. https://html-classic.itch.zone,https://yourname.itch.io).
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "https://html-classic.itch.zone")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("ItchGame", policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .WithMethods("GET", "POST", "DELETE")
          .AllowCredentials()));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // In Produktion bei einem Proxy außerhalb von Loopback dessen IP als KnownProxy/Network eintragen.
});
builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<GameCoordinator>();
builder.Services.AddSingleton<RoomNotifier>();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 16 * 1024;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHostedService<GameClockService>();

var app = builder.Build();
app.UseForwardedHeaders();
var webSocketOptions = new WebSocketOptions();
foreach (var origin in allowedOrigins) webSocketOptions.AllowedOrigins.Add(origin);
app.UseWebSockets(webSocketOptions);
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("ItchGame");
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));
app.MapHub<GameHub>("/gamehub", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
});
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
