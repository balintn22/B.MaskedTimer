using Microsoft.Extensions.DependencyInjection;

namespace B.MaskedTimers
{
    public static class DiRegistrator
    {
        public static IServiceCollection AddMaskedTimers(this IServiceCollection services)
        {
            services.AddSingleton<IClock, SystemClock>();
            return services;
        }
    }
}
