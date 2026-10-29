using MediatR;
using Pricing.Engine.Api;
using Pricing.Engine.Application;
using Pricing.Engine.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddSingleton<PriceCache>();

var app = builder.Build();
app.MapPriceEndpoints();
app.Run();
