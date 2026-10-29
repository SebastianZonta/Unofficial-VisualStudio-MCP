using MediatR;

var builder = WebApplication.CreateBuilder(args);
// Listening URL lives in appsettings.json ("Urls"); launchSettings mirrors it.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddHttpClient("pricing", c => c.BaseAddress = new Uri(builder.Configuration["Pricing:BaseUrl"]!));
builder.Services.AddScoped<IPricingClient, PricingClient>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
var app = builder.Build();

app.MapOrderEndpoints();

app.Run();
