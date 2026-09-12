namespace PortalApi.Domain.Enums;

// Nomes em snake_case de propósito: casam com o valor de fio (JSON) que o
// frontend já espera (ver features/portfolio/api.ts), evitando reescrever
// os componentes que consomem isso. Ver ProjetoDto.
public enum StatusPublico
{
    em_andamento,
    concluido,
}
