using WebApiApp;

var builder = WebApplication.CreateBuilder(args);

// Initialize configuration and validate environment variables
try
{
    AppConfiguration.Initialize();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Configuration error: {ex.Message}");
    Environment.Exit(1);
}

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.MapControllers();

app.Run();
