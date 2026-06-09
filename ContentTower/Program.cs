using ContentTower.Services;
using ContentTower.System;
using Microsoft.Extensions.Options;

namespace ContentTower
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var app = BuildApp(args);

            InitializeOptions(app);
            await InitializeServices(app);

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
            builder.WebHost.UseUrls("http://0.0.0.0:5000");
            // System abstraction modules:
            builder.Services.AddSingleton<ITime, Time>();
            builder.Services.AddSingleton<IFileSystem, FileSystem>();
            // Services:
            builder.Services.AddSingleton<IDeleteService, DeleteService>();
            builder.Services.AddSingleton<ICleanupWorker, CleanupWorker>();
            builder.Services.AddSingleton<ICleanupService, CleanupService>();
            builder.Services.AddSingleton<IHashService, HashService>();
            builder.Services.AddSingleton<ILoadService, LoadService>();
            builder.Services.AddSingleton<IPresenceService, PresenceService>();
            builder.Services.AddSingleton<IQuotaService, QuotaService>();
            builder.Services.AddSingleton<ISaveService, SaveService>();
            builder.Services.AddSingleton<IValidationService, ValidationService>();
            // WebAPI:
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
            return builder.Build();
        }

        private static async Task InitializeServices(WebApplication app)
        {
            await Get<IQuotaService>(app).Initialize();
            Get<ICleanupService>(app).Start();
        }

        private static void InitializeOptions(WebApplication app)
        {
            var logger = Get<ILogger<Program>>(app);
            var optionsMaybe = Get<IOptions<StorageOptions>>(app);
            if (optionsMaybe == null || optionsMaybe.Value == null)
                throw new Exception("Failed to load configuration options.");

            var options = optionsMaybe.Value;
            logger.LogInformation("Starting with options:");
            logger.LogInformation($"DataPath={options.DataPath}");
            logger.LogInformation($"Quota={options.Quota}");
            logger.LogInformation($"CleanupInterval={Utils.FormatDuration(options.CleanupInterval)}");
            logger.LogInformation($"StoreDurationDefaultNominal={Utils.FormatDuration(options.StoreDurationDefaultNominal)}");
            logger.LogInformation($"StoreDurationDefaultPressure={Utils.FormatDuration(options.StoreDurationDefaultPressure)}");
            logger.LogInformation($"StoreDurationTemporaryNominal={Utils.FormatDuration(options.StoreDurationTemporaryNominal)}");
            logger.LogInformation($"StoreDurationTemporaryPressure={Utils.FormatDuration(options.StoreDurationTemporaryPressure)}");

            Get<IValidationService>(app).ValidateOptions();
        }

        private static T Get<T>(WebApplication app)
        {
            return app.Services.GetService<T>()!;
        }
    }
}
