
using Microsoft.Extensions.Options;

namespace ContentTower
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = BuildApp(args);

            InitializeOptions(app);

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }

        private static WebApplication BuildApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddTransient<IValidationService, ValidationService>();
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
            return builder.Build();
        }

        private static void InitializeOptions(WebApplication app)
        {
            var logger = app.Services.GetService<ILogger<Program>>()!;
            var optionsMaybe = app.Services.GetService<IOptions<StorageOptions>>();
            if (optionsMaybe == null || optionsMaybe.Value == null)
                throw new Exception("Failed to load configuration options.");

            var options = optionsMaybe.Value;
            logger.LogInformation("Starting with options:");
            logger.LogInformation($"DataPath={options.DataPath}");
            logger.LogInformation($"Quota={options.Quota}");
            logger.LogInformation($"StoreDurationDefaultNominal={Utils.FormatDuration(options.StoreDurationDefaultNominal)}");
            logger.LogInformation($"StoreDurationDefaultPressure={Utils.FormatDuration(options.StoreDurationDefaultPressure)}");
            logger.LogInformation($"StoreDurationTemporaryNominal={Utils.FormatDuration(options.StoreDurationTemporaryNominal)}");
            logger.LogInformation($"StoreDurationTemporaryPressure={Utils.FormatDuration(options.StoreDurationTemporaryPressure)}");

            app.Services.GetService<IValidationService>()!.ValidateOptions();
        }
    }
}
