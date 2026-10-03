using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PortalApi.Application.Crm;
using PortalApi.Application.Uploads;
using PortalApi.Infrastructure.Storage;

namespace PortalApi.Tests;

internal static class Imagens
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2];
    public static readonly byte[] Gif = "GIF89a....."u8.ToArray();
    public static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
    public static readonly byte[] Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
    public static readonly byte[] Html = "<html><script>alert(1)</script></html>"u8.ToArray();
    public static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 3, 0];
    public static readonly byte[] Pdf = "%PDF-1.7 ..."u8.ToArray();

    public static byte[] ComTamanho(byte[] cabecalho, int total) =>
        [.. cabecalho, .. new byte[total - cabecalho.Length]];
}

internal sealed class MemoriaStorage : IUploadStorage
{
    public Dictionary<string, byte[]> Arquivos { get; } = [];

    public Task SaveAsync(string pasta, string arquivo, byte[] conteudo, CancellationToken ct)
    {
        Arquivos[$"{pasta}/{arquivo}"] = conteudo;
        return Task.CompletedTask;
    }

    public Stream? OpenRead(string pasta, string arquivo) =>
        Arquivos.TryGetValue($"{pasta}/{arquivo}", out var b) ? new MemoryStream(b) : null;

    public bool Delete(string pasta, string arquivo) => Arquivos.Remove($"{pasta}/{arquivo}");

    public IReadOnlyList<ArquivoInfo> List(string pasta) =>
        Arquivos.Where(kv => kv.Key.StartsWith(pasta + "/"))
            .Select(kv => new ArquivoInfo(kv.Key[(pasta.Length + 1)..], kv.Value.Length, DateTimeOffset.UnixEpoch)).ToList();
}

public class UploadServiceTests
{
    private static (UploadService Service, MemoriaStorage Storage) Novo()
    {
        var storage = new MemoriaStorage();
        return (new UploadService(storage), storage);
    }

    [Theory]
    [InlineData(nameof(Imagens.Png), ".png")]
    [InlineData(nameof(Imagens.Jpeg), ".jpg")]
    [InlineData(nameof(Imagens.Gif), ".gif")]
    [InlineData(nameof(Imagens.Webp), ".webp")]
    public async Task Imagem_valida_e_salva_com_nome_gerado_e_extensao_da_assinatura(string tipo, string extensao)
    {
        var (service, storage) = Novo();
        var bytes = (byte[])typeof(Imagens).GetField(tipo)!.GetValue(null)!;

        var r = await service.SalvarAsync("colaboradores", new MemoryStream(bytes), default);

        Assert.True(r.Ok);
        Assert.EndsWith(extensao, r.Valor!.Arquivo);
        Assert.Matches("^[0-9a-f]{32}\\.", r.Valor.Arquivo);
        Assert.Equal($"colaboradores/{r.Valor.Arquivo}", r.Valor.Path);
        Assert.Equal(bytes, storage.Arquivos[r.Valor.Path]);
    }

    [Theory]
    [InlineData(nameof(Imagens.Svg))]   // SVG pode carregar script
    [InlineData(nameof(Imagens.Html))]
    [InlineData(nameof(Imagens.Exe))]
    [InlineData(nameof(Imagens.Pdf))]
    public async Task Conteudo_que_nao_e_imagem_aceita_e_recusado_mesmo_que_o_cliente_diga_que_e_png(string tipo)
    {
        var (service, storage) = Novo();
        var bytes = (byte[])typeof(Imagens).GetField(tipo)!.GetValue(null)!;

        var r = await service.SalvarAsync("clientes", new MemoryStream(bytes), default);

        Assert.Equal(CrmErro.Invalido, r.Erro);
        Assert.Empty(storage.Arquivos);
    }

    [Fact]
    public async Task Limite_de_2_MB_conta_os_bytes_reais()
    {
        var (service, storage) = Novo();

        var noLimite = await service.SalvarAsync("clientes", new MemoryStream(Imagens.ComTamanho(Imagens.Png, 2 * 1024 * 1024)), default);
        var acima = await service.SalvarAsync("clientes", new MemoryStream(Imagens.ComTamanho(Imagens.Png, 2 * 1024 * 1024 + 1)), default);

        Assert.True(noLimite.Ok);
        Assert.Equal(CrmErro.Invalido, acima.Erro);
        Assert.Single(storage.Arquivos);
    }

    [Fact]
    public async Task Arquivo_vazio_e_recusado()
    {
        var (service, _) = Novo();
        Assert.Equal(CrmErro.Invalido, (await service.SalvarAsync("clientes", new MemoryStream(), default)).Erro);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("outra")]
    [InlineData("../clientes")]
    [InlineData("clientes/../../etc")]
    [InlineData("CLIENTES")]
    public async Task Pasta_fora_da_lista_e_recusada(string? pasta)
    {
        var (service, storage) = Novo();

        Assert.Equal(CrmErro.Invalido, (await service.SalvarAsync(pasta, new MemoryStream(Imagens.Png), default)).Erro);
        Assert.Empty(storage.Arquivos);
    }

    [Theory]
    [InlineData("convite-nelson-proenca.png", true)]
    [InlineData("a1b2c3.JPG", true)]
    [InlineData("foto_1-final.webp", true)]
    [InlineData("../segredo.png", false)]
    [InlineData("a/b.png", false)]
    [InlineData("a\\b.png", false)]
    [InlineData("..\\..\\x.png", false)]
    [InlineData("a..png", false)]
    [InlineData(".htaccess", false)]
    [InlineData(".png", false)]
    [InlineData("logo.svg", false)]
    [InlineData("script.html", false)]
    [InlineData("a.png.exe", false)]
    [InlineData("sem-extensao", false)]
    [InlineData("a b.png", false)]
    [InlineData("%2e%2e%2fx.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NomeSeguro_aceita_so_nomes_simples_com_extensao_de_imagem(string? nome, bool esperado)
    {
        Assert.Equal(esperado, UploadService.NomeSeguro(nome));
    }

    [Fact]
    public void Nome_gigante_e_recusado()
    {
        Assert.False(UploadService.NomeSeguro(new string('a', 120) + ".png"));
    }

    [Fact]
    public async Task Abrir_excluir_e_listar_ignoram_nomes_e_pastas_inseguros_sem_tocar_no_storage()
    {
        var (service, storage) = Novo();
        storage.Arquivos["clientes/a.png"] = Imagens.Png;

        Assert.Null(service.Abrir("clientes", "../clientes/a.png"));
        Assert.Null(service.Abrir("outra", "a.png"));
        Assert.False(service.Excluir("clientes", "../clientes/a.png"));
        Assert.Empty(service.Listar("outra"));
        Assert.Single(storage.Arquivos);

        Assert.NotNull(service.Abrir("clientes", "a.png"));
        Assert.True(service.Excluir("clientes", "a.png"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Convite_com_nome_fixo_e_regravado_no_mesmo_nome()
    {
        var (service, storage) = Novo();

        var primeira = await service.SalvarConviteAsync("convite-ana-silva.png", new MemoryStream(Imagens.Png), default);
        var segunda = await service.SalvarConviteAsync("convite-ana-silva.png", new MemoryStream([.. Imagens.Png, 9, 9]), default);

        Assert.True(primeira.Ok);
        Assert.True(segunda.Ok);
        Assert.Equal("convites/convite-ana-silva.png", segunda.Valor!.Path);
        Assert.Equal(Imagens.Png.Length + 2, storage.Arquivos["convites/convite-ana-silva.png"].Length); // sobrescreveu
        Assert.Single(storage.Arquivos);
    }

    [Theory]
    [InlineData("../convite.png")]
    [InlineData("a/b.png")]
    [InlineData("convite.svg")]
    [InlineData("convite.png.exe")]
    [InlineData(".png")]
    [InlineData(null)]
    public async Task Convite_com_nome_inseguro_e_recusado(string? nome)
    {
        var (service, storage) = Novo();

        Assert.Equal(CrmErro.Invalido, (await service.SalvarConviteAsync(nome, new MemoryStream(Imagens.Png), default)).Erro);
        Assert.Empty(storage.Arquivos);
    }

    [Fact]
    public async Task Convite_exige_que_a_extensao_do_nome_corresponda_ao_conteudo()
    {
        var (service, storage) = Novo();

        // HTML com nome .png, e um JPEG com nome .png: nenhum entra.
        Assert.Equal(CrmErro.Invalido, (await service.SalvarConviteAsync("convite-x.png", new MemoryStream(Imagens.Html), default)).Erro);
        Assert.Equal(CrmErro.Invalido, (await service.SalvarConviteAsync("convite-x.png", new MemoryStream(Imagens.Jpeg), default)).Erro);
        Assert.True((await service.SalvarConviteAsync("convite-x.jpeg", new MemoryStream(Imagens.Jpeg), default)).Ok); // .jpeg vale como jpg
        Assert.Single(storage.Arquivos);
    }

    [Fact]
    public async Task Convite_acima_de_2_MB_e_recusado()
    {
        var (service, _) = Novo();
        var grande = Imagens.ComTamanho(Imagens.Png, 2 * 1024 * 1024 + 1);

        Assert.Equal(CrmErro.Invalido, (await service.SalvarConviteAsync("convite-x.png", new MemoryStream(grande), default)).Erro);
    }

    [Fact]
    public void Listar_ordena_pelo_nome_e_esconde_arquivos_com_nome_estranho()
    {
        var (service, storage) = Novo();
        storage.Arquivos["convites/b.png"] = Imagens.Png;
        storage.Arquivos["convites/a.png"] = Imagens.Png;
        storage.Arquivos["convites/.gitkeep"] = [];
        storage.Arquivos["convites/x.svg"] = Imagens.Svg;

        Assert.Equal(["a.png", "b.png"], service.Listar("convites").Select(a => a.Arquivo));
    }
}

public class LocalUploadStorageTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "upload-storage-" + Guid.NewGuid());
    private LocalUploadStorage Storage => new(Options.Create(new StorageOptions { UploadsRoot = _raiz, DownloadTokenSecret = "x" }));

    [Fact]
    public async Task Grava_le_lista_e_apaga_no_disco()
    {
        var s = Storage;

        await s.SaveAsync("convites", "a.png", Imagens.Png, default);

        using (var aberto = s.OpenRead("convites", "a.png"))
            Assert.Equal(Imagens.Png.Length, aberto!.Length);
        Assert.Equal("a.png", Assert.Single(s.List("convites")).Arquivo);
        Assert.True(s.Delete("convites", "a.png"));
        Assert.False(s.Delete("convites", "a.png"));
        Assert.Null(s.OpenRead("convites", "a.png"));
        Assert.Empty(s.List("convites"));
        Assert.Empty(s.List("pasta-que-nao-existe"));
    }

    [Theory]
    [InlineData("..", "x.png")]
    [InlineData("convites", "../../x.png")]
    [InlineData("../fora", "x.png")]
    public async Task Path_traversal_na_camada_de_disco_lanca(string pasta, string arquivo)
    {
        var s = Storage;

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.SaveAsync(pasta, arquivo, Imagens.Png, default));
        Assert.Throws<InvalidOperationException>(() => s.OpenRead(pasta, arquivo));
        Assert.Throws<InvalidOperationException>(() => s.Delete(pasta, arquivo));
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }
}

public class UploadEndpointsTests
{
    private static HttpRequestMessage Envio(string pasta, byte[] conteudo, string? cookie, bool csrf, string nome = "foto.png")
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(conteudo), "file", nome } };
        var req = new HttpRequestMessage(HttpMethod.Post, $"/uploads/{pasta}") { Content = form };
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        if (csrf) req.Headers.Add("X-Portal-Admin", "1");
        return req;
    }

    private sealed record Criado(string Pasta, string Arquivo, string Path, string Url);

    [Fact]
    public async Task Admin_envia_imagem_recebe_a_url_publica_e_qualquer_um_a_le_com_cabecalhos_seguros()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");

        var r = await api.Http.SendAsync(Envio("colaboradores", Imagens.Png, admin, csrf: true));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var criado = (await r.Content.ReadFromJsonAsync<Criado>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
        Assert.Equal($"/api/portal/uploads/colaboradores/{criado.Arquivo}", criado.Url);

        // O Caddy remove /api/portal antes de chegar na API; sem cookie nenhum (visitante do site).
        var leitura = await api.Http.GetAsync($"/uploads/colaboradores/{criado.Arquivo}");
        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);
        Assert.Equal("image/png", leitura.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", leitura.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("max-age", leitura.Headers.CacheControl!.ToString());
        Assert.Equal(Imagens.Png, await leitura.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Envio_sem_cookie_ou_com_sessao_de_cliente_responde_401_e_sem_csrf_403()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");
        var cliente = await api.LoginClienteAsync("ana@acme.com");
        var admin = await api.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(Envio("clientes", Imagens.Png, null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(Envio("clientes", Imagens.Png, cliente, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Http.SendAsync(Envio("clientes", Imagens.Png, admin, false))).StatusCode);
    }

    [Fact]
    public async Task Envio_recusa_nao_imagem_pasta_invalida_e_arquivo_sem_campo_file()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(Envio("clientes", Imagens.Svg, admin, true, "logo.png"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(Envio("clientes", Imagens.Exe, admin, true, "x.png"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(Envio("segredos", Imagens.Png, admin, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await api.Http.SendAsync(Envio("clientes", Imagens.ComTamanho(Imagens.Png, 2 * 1024 * 1024 + 1), admin, true))).StatusCode);

        var semArquivo = new HttpRequestMessage(HttpMethod.Post, "/uploads/clientes")
        {
            Content = new MultipartFormDataContent { { new StringContent("oi"), "outro" } },
        };
        semArquivo.Headers.Add("Cookie", admin);
        semArquivo.Headers.Add("X-Portal-Admin", "1");
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(semArquivo)).StatusCode);
    }

    [Theory]
    [InlineData("/uploads/clientes/nao-existe.png")]
    [InlineData("/uploads/clientes/logo.svg")]
    [InlineData("/uploads/outra/a.png")]
    [InlineData("/uploads/clientes/..%2f..%2fappsettings.json")]
    [InlineData("/uploads/..%2fclientes/a.png")]
    public async Task Leitura_de_arquivo_inexistente_ou_nome_perigoso_responde_404(string url)
    {
        await using var api = await CrmApi.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Admin_apaga_a_imagem_e_ela_some()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");
        var criado = (await (await api.Http.SendAsync(Envio("clientes", Imagens.Jpeg, admin, true)))
            .Content.ReadFromJsonAsync<Criado>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, $"/uploads/clientes/{criado.Arquivo}", cookie: admin))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, $"/uploads/clientes/{criado.Arquivo}", csrf: true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, $"/uploads/clientes/{criado.Arquivo}", cookie: admin, csrf: true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.GetAsync($"/uploads/clientes/{criado.Arquivo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, $"/uploads/clientes/{criado.Arquivo}", cookie: admin, csrf: true))).StatusCode);
    }

    private static HttpRequestMessage PutConvite(byte[] bytes, string nome, string? cookie, bool csrf)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, $"/uploads/convites/{nome}")
        {
            Content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", nome } },
        };
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        if (csrf) req.Headers.Add("X-Portal-Admin", "1");
        return req;
    }

    [Fact]
    public async Task Admin_regrava_convite_com_nome_fixo_e_a_leitura_publica_mostra_a_versao_nova()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(PutConvite(Imagens.Png, "convite-ana.png", null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Http.SendAsync(PutConvite(Imagens.Png, "convite-ana.png", admin, false))).StatusCode);

        var v1 = await api.Http.SendAsync(PutConvite(Imagens.Png, "convite-ana.png", admin, true));
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.Contains("/api/portal/uploads/convites/convite-ana.png", await v1.Content.ReadAsStringAsync());

        var novo = Imagens.ComTamanho(Imagens.Png, 40);
        Assert.Equal(HttpStatusCode.OK, (await api.Http.SendAsync(PutConvite(novo, "convite-ana.png", admin, true))).StatusCode);
        Assert.Equal(novo, await api.Http.GetByteArrayAsync("/uploads/convites/convite-ana.png"));

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(PutConvite(Imagens.Svg, "convite-ana.png", admin, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Http.SendAsync(PutConvite(Imagens.Png, "convite.svg", admin, true))).StatusCode);
    }

    [Fact]
    public async Task Convites_lista_publicamente_so_a_pasta_de_convites()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");
        await api.Http.SendAsync(Envio("convites", Imagens.Png, admin, true));
        await api.Http.SendAsync(Envio("clientes", Imagens.Png, admin, true));

        var r = await api.Http.GetAsync("/convites");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var itens = await r.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        var item = Assert.Single(itens!);
        Assert.StartsWith("/api/portal/uploads/convites/", item.GetProperty("url").GetString());
    }
}
