using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PortalApi.Application.Artefatos;
using PortalApi.Application.Auth;
using PortalApi.Application.Clientes;
using PortalApi.Application.Crm;
using PortalApi.Application.Email;
using PortalApi.Application.Etapas;
using PortalApi.Application.Pedidos;
using PortalApi.Application.Portal;
using PortalApi.Application.Projetos;
using PortalApi.Application.Storage;
using PortalApi.Application.Uploads;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.Email;
using PortalApi.Infrastructure.N8n;
using PortalApi.Infrastructure.Security;
using PortalApi.Infrastructure.Storage;

namespace PortalApi.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PortalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:PortalDb está vazio ou ausente. Preencha o valor em appsettings.Production.json.");

        // Versão fixa em vez de ServerVersion.AutoDetect: evita exigir uma conexão real só
        // pra descobrir a versão (o que quebraria "dotnet ef migrations add" sem banco
        // acessível, e atrasaria o startup). O banco é sempre MySQL 8.0 (self-hosted, ver
        // docker-compose.yml) — se isso mudar um dia, atualizar aqui.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

        services.AddDbContext<PortalDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<AdminAuthService>();

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ClienteService>();

        services.AddScoped<IProjetoRepository, ProjetoRepository>();
        services.AddScoped<ProjetoService>();
        services.AddScoped<ClientAccessService>();

        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IPedidoRespostaRepository, PedidoRespostaRepository>();
        services.AddScoped<PedidoService>();

        services.AddScoped<IEtapaRepository, EtapaRepository>();
        services.AddScoped<EtapaService>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IDownloadTokenService, JwtDownloadTokenService>();
        services.AddScoped<IArtefatoRepository, ArtefatoRepository>();
        services.AddScoped<ArtefatoService>();

        services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.SectionName));
        services.AddHttpClient<IEmailService, ResendEmailService>((sp, client) =>
        {
            var apiKey = sp.GetRequiredService<IOptions<ResendOptions>>().Value.ApiKey;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        });
        services.AddScoped<PortfolioNotificationService>();

        services.AddSingleton(TimeProvider.System);
        services.Configure<ClienteLoginOptions>(configuration.GetSection(ClienteLoginOptions.SectionName));
        services.AddScoped<IClienteLoginTokenRepository, ClienteLoginTokenRepository>();
        services.AddScoped<ClienteLoginService>();

        services.AddSingleton<IUploadStorage, LocalUploadStorage>();
        services.AddScoped<UploadService>();

        services.AddScoped<ICrmRepository, CrmRepository>();
        services.AddScoped<LeadService>();
        services.AddScoped<AnaliseService>();
        services.AddScoped<AgendamentoService>();
        services.AddScoped<CadastroCrmService>();

        services.Configure<N8nOptions>(configuration.GetSection(N8nOptions.SectionName));
        services.AddHttpClient<INotificadorN8n, N8nNotificador>();

        return services;
    }
}
