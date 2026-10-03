using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortalApi.Application.Uploads;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.Storage;

namespace PortalApi.Api;

/// <summary>
/// Importação única das imagens do bucket <c>uploads</c> do Supabase (backup da Fase 0, pasta <c>uploads/</c>)
/// para o storage do portal-api, e reescrita das URLs que ainda apontam para o Supabase
/// (<c>clientes.LogoUrl</c> e <c>colaboradores.FotoUrl</c>). Idempotente. Imprime só contagens. Uso:
/// <c>dotnet PortalApi.Api.dll import-uploads &lt;pasta-do-backup&gt; [--dry-run]</c>
/// </summary>
public static class ImportUploadsCommand
{
    private const string MarcadorSupabase = "/storage/v1/object/public/uploads/";

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var pasta = args.ElementAtOrDefault(1);
        var dryRun = args.Contains("--dry-run");
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(Path.Combine(pasta, "uploads")))
        {
            Console.Error.WriteLine("Uso: import-uploads <pasta-do-backup> [--dry-run]  (a pasta precisa ter uploads/<pasta>/<arquivo>)");
            return 1;
        }

        using var scope = services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IUploadStorage>();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var publico = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.UploadsPublicBasePath.TrimEnd('/');
        if (dryRun) Console.WriteLine("DRY-RUN: nada será gravado.");

        int copiados = 0, jaExistiam = 0, recusados = 0;
        foreach (var destino in UploadService.Pastas)
        {
            var origem = Path.Combine(pasta, "uploads", destino);
            if (!Directory.Exists(origem)) continue;

            foreach (var arquivo in Directory.EnumerateFiles(origem))
            {
                var nome = Path.GetFileName(arquivo);
                var bytes = await File.ReadAllBytesAsync(arquivo);
                if (!UploadService.NomeSeguro(nome) || bytes.Length > UploadService.TamanhoMaximo
                    || UploadService.ExtensaoPelaAssinatura(bytes) is null)
                {
                    recusados++;
                    continue;
                }

                using (var existente = storage.OpenRead(destino, nome))
                    if (existente is not null) { jaExistiam++; continue; }

                if (!dryRun) await storage.SaveAsync(destino, nome, bytes, CancellationToken.None);
                copiados++;
            }
        }
        Console.WriteLine($"uploads: {copiados} {(dryRun ? "a copiar" : "copiados")}, {jaExistiam} já existiam, {recusados} recusados (tipo, nome ou tamanho).");

        // URLs que ainda apontam para o storage do Supabase: reescreve para o caminho novo (se o arquivo veio no backup).
        var reescritas = 0;
        foreach (var cliente in await db.Clientes.Where(c => c.LogoUrl != null && c.LogoUrl.Contains(MarcadorSupabase)).ToListAsync())
        {
            cliente.LogoUrl = Reescrever(cliente.LogoUrl!, publico);
            reescritas++;
        }
        foreach (var colab in await db.Colaboradores.Where(c => c.FotoUrl != null && c.FotoUrl.Contains(MarcadorSupabase)).ToListAsync())
        {
            colab.FotoUrl = Reescrever(colab.FotoUrl!, publico);
            reescritas++;
        }
        if (!dryRun) await db.SaveChangesAsync();
        Console.WriteLine($"urls: {reescritas} {(dryRun ? "a reescrever" : "reescritas")}.");

        // Sobras: qualquer URL do Supabase que continue no banco (não estava no bucket).
        var sobras = await db.Clientes.CountAsync(c => c.LogoUrl != null && c.LogoUrl.Contains("supabase.co"))
                     + await db.Colaboradores.CountAsync(c => c.FotoUrl != null && c.FotoUrl.Contains("supabase.co"));
        if (dryRun) sobras -= reescritas;
        Console.WriteLine(sobras == 0 ? "Nenhuma URL do Supabase restante." : $"ATENÇÃO: {sobras} URL(s) do Supabase ainda no banco.");

        return recusados == 0 && sobras == 0 ? 0 : 2;
    }

    /// <summary>https://x.supabase.co/storage/v1/object/public/uploads/clientes/a.png → /api/portal/uploads/clientes/a.png</summary>
    internal static string Reescrever(string url, string publico)
    {
        var resto = url[(url.IndexOf(MarcadorSupabase, StringComparison.Ordinal) + MarcadorSupabase.Length)..];
        return $"{publico}/{resto.Split('?')[0]}";
    }
}
