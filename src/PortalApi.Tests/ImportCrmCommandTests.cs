using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortalApi.Api;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Tests;

/// <summary>Importação do backup da Fase 0, com arquivos sintéticos (nunca dados reais) no formato do PostgREST.</summary>
public sealed class ImportCrmCommandTests : IDisposable
{
    private static readonly Guid ClienteId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LeadId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid ColabId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "import-crm-" + Guid.NewGuid());
    private readonly ServiceProvider _services;

    public ImportCrmCommandTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(dbName))
            .BuildServiceProvider();

        Directory.CreateDirectory(Path.Combine(_pasta, "tables"));
        Escreve("colaboradores", $$"""
            [{"id":"{{ColabId}}","created_at":"2026-01-05T10:00:00+00:00","nome":"Ana","cargo":"Dev","departamento":null,"email":"ana@x.com","foto_url":null},
             {"id":"cccccccc-0000-0000-0000-000000000002","created_at":"2026-01-06T10:00:00+00:00","nome":null,"cargo":null,"departamento":null,"email":"semnome@x.com","foto_url":null}]
            """);
        Escreve("contatos_clientes", $$"""
            [{"id":"dddddddd-0000-0000-0000-000000000001","cliente_id":"{{ClienteId}}","nome":"Zé","telefone":null,"email":"ze@x.com","created_at":"2026-02-01T10:00:00+00:00"},
             {"id":"dddddddd-0000-0000-0000-000000000002","cliente_id":"99999999-0000-0000-0000-000000000009","nome":"Órfão","telefone":null,"email":null,"created_at":"2026-02-01T10:00:00+00:00"}]
            """);
        Escreve("leads_ia", $$"""
            [{"id":"{{LeadId}}","nome":"Lia","contato":"lia@x.com","desafio_tecnico":"CRM","canal":"email","created_at":"2026-03-01T12:30:00.123456+00:00","visto_pelo_nelson":true,"origem":"Site_Institucional","analise_ia":"ok","empresa":"Acme"}]
            """);
        Escreve("agendamentos", """
            [{"id":"eeeeeeee-0000-0000-0000-000000000001","created_at":"2026-04-01T10:00:00+00:00","cliente_nome":"Bia","cliente_email":null,"cliente_whatsapp":null,"data_reuniao":"2026-04-10T15:00:00+00:00","status":"confirmado","instagram_user_id":null,"expert_responsavel":"Nelson Proença","indicado_por":null,"origem":"site_instagram","valor_projeto":1500.50,"comissao_paga":true}]
            """);
        Escreve("enrich_company", """
            [{"id":"ffffffff-0000-0000-0000-000000000001","company_name":"Acme","segment":"Varejo","output_ai":null,"created_at":"2026-05-01T10:00:00+00:00"}]
            """);
        Escreve("playground_analise", "[]");
    }

    private void Escreve(string tabela, string json) => File.WriteAllText(Path.Combine(_pasta, "tables", tabela + ".json"), json);

    private async Task<PortalDbContext> NovoDbAsync(bool comCliente = true)
    {
        var db = _services.CreateScope().ServiceProvider.GetRequiredService<PortalDbContext>();
        if (comCliente && !await db.Clientes.AnyAsync())
        {
            db.Clientes.Add(new Cliente { Id = ClienteId, Nome = "Acme", Email = "a@acme.com", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        return db;
    }

    [Fact]
    public async Task Importa_preservando_ids_e_datas_e_pula_linhas_invalidas_com_codigo_de_saida_2()
    {
        var db = await NovoDbAsync();

        var codigo = await ImportCrmCommand.RunAsync(["import-crm", _pasta], _services);

        Assert.Equal(2, codigo); // colaborador sem nome e contato órfão foram pulados
        var colab = await db.Colaboradores.SingleAsync();
        Assert.Equal(ColabId, colab.Id);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero), colab.CreatedAt);

        var contato = await db.ContatosClientes.SingleAsync();
        Assert.Equal(ClienteId, contato.ClienteId);

        var lead = await db.LeadsIa.SingleAsync();
        Assert.Equal(LeadId, lead.Id);
        Assert.True(lead.VistoPeloNelson);
        Assert.Equal("ok", lead.AnaliseIa);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 12, 30, 0, TimeSpan.Zero).AddTicks(1234560), lead.CreatedAt);

        var ag = await db.Agendamentos.SingleAsync();
        Assert.Equal(1500.50m, ag.ValorProjeto);
        Assert.True(ag.ComissaoPaga);
        Assert.Equal("site_instagram", ag.Origem);

        Assert.Single(db.EnrichCompanies);
        Assert.Empty(db.PlaygroundAnalises);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_duplica()
    {
        var db = await NovoDbAsync();

        await ImportCrmCommand.RunAsync(["import-crm", _pasta], _services);
        await ImportCrmCommand.RunAsync(["import-crm", _pasta], _services);

        Assert.Single(db.Colaboradores);
        Assert.Single(db.LeadsIa);
        Assert.Single(db.Agendamentos);
        Assert.Single(db.EnrichCompanies);
        Assert.Single(db.ContatosClientes);
    }

    [Fact]
    public async Task Dry_run_nao_grava_nada()
    {
        var db = await NovoDbAsync();

        await ImportCrmCommand.RunAsync(["import-crm", _pasta, "--dry-run"], _services);

        Assert.Empty(db.Colaboradores);
        Assert.Empty(db.LeadsIa);
        Assert.Empty(db.Agendamentos);
        Assert.Empty(db.EnrichCompanies);
    }

    [Fact]
    public async Task Pasta_sem_tables_ou_sem_argumento_falha_com_codigo_1()
    {
        Assert.Equal(1, await ImportCrmCommand.RunAsync(["import-crm"], _services));
        Assert.Equal(1, await ImportCrmCommand.RunAsync(["import-crm", Path.Combine(_pasta, "nao-existe")], _services));
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }
}

public sealed class ImportUploadsCommandTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "import-uploads-" + Guid.NewGuid());
    private readonly MemoriaStorage _storage = new();
    private readonly ServiceProvider _services;

    public ImportUploadsCommandTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddSingleton<PortalApi.Application.Uploads.IUploadStorage>(_storage)
            .Configure<PortalApi.Infrastructure.Storage.StorageOptions>(o => o.DownloadTokenSecret = "x")
            .BuildServiceProvider();

        Directory.CreateDirectory(Path.Combine(_pasta, "uploads", "convites"));
        Directory.CreateDirectory(Path.Combine(_pasta, "uploads", "clientes"));
        File.WriteAllBytes(Path.Combine(_pasta, "uploads", "convites", "convite-nelson-proenca.png"), Imagens.Png);
        File.WriteAllBytes(Path.Combine(_pasta, "uploads", "clientes", "logo.png"), Imagens.Png);
    }

    private async Task SemearAsync(string logoUrl, string? fotoUrl = null)
    {
        var db = _services.CreateScope().ServiceProvider.GetRequiredService<PortalDbContext>();
        db.Clientes.Add(new Cliente { Id = Guid.NewGuid(), Nome = "Acme", Email = "a@acme.com", LogoUrl = logoUrl, CreatedAt = DateTimeOffset.UtcNow });
        if (fotoUrl is not null)
            db.Colaboradores.Add(new Colaborador { Id = Guid.NewGuid(), Nome = "Ana", Email = "ana@x.com", FotoUrl = fotoUrl, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private PortalDbContext Db() => _services.CreateScope().ServiceProvider.GetRequiredService<PortalDbContext>();

    [Fact]
    public async Task Copia_as_imagens_e_reescreve_as_urls_do_supabase()
    {
        await SemearAsync("https://abc.supabase.co/storage/v1/object/public/uploads/clientes/logo.png?t=123",
            "https://abc.supabase.co/storage/v1/object/public/uploads/colaboradores/foto.png");
        File.WriteAllBytes(Path.Combine(_pasta, "uploads", "colaboradores.png"), Imagens.Png); // fora de pasta: ignorado

        var codigo = await ImportUploadsCommand.RunAsync(["import-uploads", _pasta], _services);

        Assert.Equal(0, codigo);
        Assert.True(_storage.Arquivos.ContainsKey("convites/convite-nelson-proenca.png"));
        Assert.True(_storage.Arquivos.ContainsKey("clientes/logo.png"));
        Assert.Equal("/api/portal/uploads/clientes/logo.png", (await Db().Clientes.SingleAsync()).LogoUrl);
        Assert.Equal("/api/portal/uploads/colaboradores/foto.png", (await Db().Colaboradores.SingleAsync()).FotoUrl);
    }

    [Fact]
    public async Task Url_externa_que_nao_e_do_supabase_fica_intacta_e_rodar_de_novo_nao_duplica()
    {
        await SemearAsync("https://cdn.exemplo.com/logo.png");

        await ImportUploadsCommand.RunAsync(["import-uploads", _pasta], _services);
        var segundo = await ImportUploadsCommand.RunAsync(["import-uploads", _pasta], _services);

        Assert.Equal(0, segundo);
        Assert.Equal(2, _storage.Arquivos.Count);
        Assert.Equal("https://cdn.exemplo.com/logo.png", (await Db().Clientes.SingleAsync()).LogoUrl);
    }

    [Fact]
    public async Task Arquivo_que_nao_e_imagem_e_recusado_e_url_do_supabase_sem_arquivo_e_avisada()
    {
        File.WriteAllBytes(Path.Combine(_pasta, "uploads", "clientes", "falso.png"), Imagens.Html);
        await SemearAsync("https://abc.supabase.co/outro/caminho/logo.png");

        var codigo = await ImportUploadsCommand.RunAsync(["import-uploads", _pasta], _services);

        Assert.Equal(2, codigo);
        Assert.False(_storage.Arquivos.ContainsKey("clientes/falso.png"));
    }

    [Fact]
    public async Task Dry_run_nao_copia_nem_reescreve()
    {
        await SemearAsync("https://abc.supabase.co/storage/v1/object/public/uploads/clientes/logo.png");

        var codigo = await ImportUploadsCommand.RunAsync(["import-uploads", _pasta, "--dry-run"], _services);

        Assert.Equal(0, codigo);
        Assert.Empty(_storage.Arquivos);
        Assert.Contains("supabase.co", (await Db().Clientes.SingleAsync()).LogoUrl);
    }

    [Fact]
    public async Task Sem_pasta_uploads_falha_com_codigo_1()
    {
        Assert.Equal(1, await ImportUploadsCommand.RunAsync(["import-uploads", Path.Combine(_pasta, "nada")], _services));
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }
}
