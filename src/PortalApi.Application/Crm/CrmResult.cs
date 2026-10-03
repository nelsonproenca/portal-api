namespace PortalApi.Application.Crm;

public enum CrmErro
{
    Nenhum,
    Invalido,
    NaoEncontrado,
    Conflito,
}

/// <summary>Resultado de uma operação do CRM: valor, ou o motivo da recusa (a mensagem é segura para o cliente).</summary>
public record CrmResult<T>(T? Valor, CrmErro Erro = CrmErro.Nenhum, string? Mensagem = null)
{
    public bool Ok => Erro == CrmErro.Nenhum;

    public static CrmResult<T> Sucesso(T valor) => new(valor);
    public static CrmResult<T> Invalido(string mensagem) => new(default, CrmErro.Invalido, mensagem);
    public static CrmResult<T> NaoEncontrado() => new(default, CrmErro.NaoEncontrado, "Não encontrado.");
    public static CrmResult<T> Conflito(string mensagem) => new(default, CrmErro.Conflito, mensagem);
}

/// <summary>Validação de campos de texto: recorta espaços e confere o tamanho máximo (o mesmo do banco).
/// Guarda só o primeiro erro, que vira a mensagem da resposta 400.</summary>
internal sealed class Campos
{
    public string? Erro { get; private set; }

    public string Obrigatorio(string? valor, string campo, int max)
    {
        var t = valor?.Trim();
        if (string.IsNullOrEmpty(t)) { Erro ??= $"{campo} é obrigatório."; return string.Empty; }
        if (t.Length > max) { Erro ??= $"{campo} deve ter no máximo {max} caracteres."; return string.Empty; }
        return t;
    }

    /// <summary>Vazio vira null; grande demais gera erro.</summary>
    public string? Opcional(string? valor, string campo, int max)
    {
        var t = valor?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (t.Length > max) { Erro ??= $"{campo} deve ter no máximo {max} caracteres."; return null; }
        return t;
    }
}
