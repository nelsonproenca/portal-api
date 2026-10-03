using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Api;

/// <summary>
/// Importação única das 6 tabelas do CRM a partir do backup da Fase 0 (arquivos <c>tables/*.json</c>),
/// preservando <c>id</c> e <c>created_at</c>. Idempotente: linha cujo id já existe é pulada, então pode rodar
/// de novo sem duplicar. Imprime só contagens, nunca o conteúdo (os dados são de terceiros). Uso:
/// <c>dotnet PortalApi.Api.dll import-crm &lt;pasta-do-backup&gt; [--dry-run]</c>
/// </summary>
public static class ImportCrmCommand
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var pasta = args.ElementAtOrDefault(1);
        var dryRun = args.Contains("--dry-run");
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(Path.Combine(pasta, "tables")))
        {
            Console.Error.WriteLine("Uso: import-crm <pasta-do-backup> [--dry-run]  (a pasta precisa ter tables/*.json)");
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        if (dryRun) Console.WriteLine("DRY-RUN: nada será gravado.");

        var ok = true;
        // Ordem importa: contatos dependem de clientes (já no banco).
        ok &= await ImportAsync<SbColaborador, Colaborador>(db, pasta, "colaboradores", dryRun, r =>
            string.IsNullOrWhiteSpace(r.Nome) || string.IsNullOrWhiteSpace(r.Email) ? null : new Colaborador
            {
                Id = r.Id, Nome = r.Nome, Email = r.Email, Cargo = r.Cargo, Departamento = r.Departamento,
                // foto_url ainda aponta para o storage do Supabase: reescrever na fase dos uploads.
                FotoUrl = r.FotoUrl, CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
            });

        var clientes = await db.Clientes.Select(c => c.Id).ToHashSetAsync();
        ok &= await ImportAsync<SbContato, ContatoCliente>(db, pasta, "contatos_clientes", dryRun, r =>
            string.IsNullOrWhiteSpace(r.Nome) || r.ClienteId is null || !clientes.Contains(r.ClienteId.Value) ? null : new ContatoCliente
            {
                Id = r.Id, ClienteId = r.ClienteId.Value, Nome = r.Nome, Telefone = r.Telefone, Email = r.Email,
                CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
            });

        ok &= await ImportAsync<SbLead, LeadIa>(db, pasta, "leads_ia", dryRun, r => new LeadIa
        {
            Id = r.Id, Nome = r.Nome, Empresa = r.Empresa, Contato = r.Contato, Canal = r.Canal,
            DesafioTecnico = r.DesafioTecnico, Origem = r.Origem, AnaliseIa = r.AnaliseIa,
            VistoPeloNelson = r.Visto ?? false, CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
        });

        ok &= await ImportAsync<SbAgendamento, Agendamento>(db, pasta, "agendamentos", dryRun, r => r.DataReuniao is null ? null : new Agendamento
        {
            Id = r.Id, ClienteNome = r.ClienteNome, ClienteEmail = r.ClienteEmail, ClienteWhatsapp = r.ClienteWhatsapp,
            DataReuniao = r.DataReuniao.Value, Status = r.Status, InstagramUserId = r.InstagramUserId,
            ExpertResponsavel = r.ExpertResponsavel, IndicadoPor = r.IndicadoPor, Origem = r.Origem,
            ValorProjeto = r.ValorProjeto, ComissaoPaga = r.ComissaoPaga ?? false,
            CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
        });

        ok &= await ImportAsync<SbEnrich, EnrichCompany>(db, pasta, "enrich_company", dryRun, r => new EnrichCompany
        {
            Id = r.Id, CompanyName = r.CompanyName, Segment = r.Segment, OutputAi = r.OutputAi,
            CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
        });

        ok &= await ImportAsync<SbPlayground, PlaygroundAnalise>(db, pasta, "playground_analise", dryRun, r => new PlaygroundAnalise
        {
            Id = r.Id, InputTecnico = r.InputTecnico, TipoAnalise = r.TipoAnalise, OutputIa = r.OutputIa,
            Status = r.Status, CreatedAt = r.CreatedAt ?? DateTimeOffset.UtcNow,
        });

        Console.WriteLine(ok ? "Importação concluída." : "Importação concluída COM LINHAS PULADAS (veja acima).");
        return ok ? 0 : 2;
    }

    /// <returns>false se alguma linha foi pulada por dado inválido (já existente não conta como problema).</returns>
    private static async Task<bool> ImportAsync<TRow, TEntity>(
        PortalDbContext db, string pasta, string tabela, bool dryRun, Func<TRow, TEntity?> map)
        where TRow : SbRow where TEntity : class
    {
        var arquivo = Path.Combine(pasta, "tables", $"{tabela}.json");
        if (!File.Exists(arquivo))
        {
            Console.Error.WriteLine($"{tabela}: arquivo ausente ({arquivo}).");
            return false;
        }

        var linhas = JsonSerializer.Deserialize<List<TRow>>(await File.ReadAllTextAsync(arquivo), Json) ?? [];
        int novas = 0, jaExistiam = 0, puladas = 0;

        foreach (var linha in linhas)
        {
            if (await db.Set<TEntity>().FindAsync(linha.Id) is not null) { jaExistiam++; continue; }

            var entidade = map(linha);
            if (entidade is null) { puladas++; continue; }

            if (!dryRun) db.Set<TEntity>().Add(entidade);
            novas++;
        }

        if (!dryRun) await db.SaveChangesAsync();
        Console.WriteLine($"{tabela}: {linhas.Count} no backup, {novas} {(dryRun ? "a importar" : "importadas")}, {jaExistiam} já existiam, {puladas} puladas.");
        return puladas == 0;
    }

    // Linhas como o PostgREST as devolveu (snake_case).
    public abstract record SbRow([property: JsonPropertyName("id")] Guid Id);

    private record SbColaborador(Guid Id, string? Nome, string? Cargo, string? Departamento, string? Email,
        [property: JsonPropertyName("foto_url")] string? FotoUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);

    private record SbContato(Guid Id,
        [property: JsonPropertyName("cliente_id")] Guid? ClienteId, string? Nome, string? Telefone, string? Email,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);

    private record SbLead(Guid Id, string? Nome, string? Empresa, string? Contato, string? Canal, string? Origem,
        [property: JsonPropertyName("desafio_tecnico")] string? DesafioTecnico,
        [property: JsonPropertyName("analise_ia")] string? AnaliseIa,
        [property: JsonPropertyName("visto_pelo_nelson")] bool? Visto,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);

    private record SbAgendamento(Guid Id, string? Status, string? Origem,
        [property: JsonPropertyName("cliente_nome")] string? ClienteNome,
        [property: JsonPropertyName("cliente_email")] string? ClienteEmail,
        [property: JsonPropertyName("cliente_whatsapp")] string? ClienteWhatsapp,
        [property: JsonPropertyName("data_reuniao")] DateTimeOffset? DataReuniao,
        [property: JsonPropertyName("instagram_user_id")] string? InstagramUserId,
        [property: JsonPropertyName("expert_responsavel")] string? ExpertResponsavel,
        [property: JsonPropertyName("indicado_por")] string? IndicadoPor,
        [property: JsonPropertyName("valor_projeto")] decimal? ValorProjeto,
        [property: JsonPropertyName("comissao_paga")] bool? ComissaoPaga,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);

    private record SbEnrich(Guid Id,
        [property: JsonPropertyName("company_name")] string? CompanyName, string? Segment,
        [property: JsonPropertyName("output_ai")] string? OutputAi,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);

    private record SbPlayground(Guid Id,
        [property: JsonPropertyName("input_tecnico")] string? InputTecnico,
        [property: JsonPropertyName("tipo_analise")] string? TipoAnalise,
        [property: JsonPropertyName("output_ia")] string? OutputIa, string? Status,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt) : SbRow(Id);
}
