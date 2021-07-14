using Consensus.Backend.Data;
using Consensus.Backend.Hive;
using Consensus.Backend.Saved;
using Consensus.Backend.User;
using Consensus.Backend.Yard;
using Microsoft.Extensions.DependencyInjection;

namespace Consensus.Backend
{
    public static class ServiceRegistration
    {
        public static IServiceCollection RegisterBackendServices(this IServiceCollection services)
        {
            services.AddTransient<IArangoDb, ArangoDb>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IHiveService, HiveService>();
            services.AddTransient<IYardService, YardService>();
            services.AddTransient<ISavedHivesService, SavedHivesService>();
            
            return services;
        }
    }
}