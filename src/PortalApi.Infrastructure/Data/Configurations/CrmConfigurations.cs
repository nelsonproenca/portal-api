using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

// As 6 tabelas do CRM que viviam no Supabase. Ids preservados no import; o text do Postgres
// vira longtext (saídas de IA) ou varchar com limite (campos curtos).

public class ColaboradorConfiguration : IEntityTypeConfiguration<Colaborador>
{
    public void Configure(EntityTypeBuilder<Colaborador> builder)
    {
        builder.ToTable("Colaboradores");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Cargo).HasMaxLength(200);
        builder.Property(c => c.Departamento).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.FotoUrl).HasMaxLength(500);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
    }
}

public class ContatoClienteConfiguration : IEntityTypeConfiguration<ContatoCliente>
{
    public void Configure(EntityTypeBuilder<ContatoCliente> builder)
    {
        builder.ToTable("ContatosClientes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Telefone).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasOne<Cliente>().WithMany().HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.ClienteId);
    }
}

public class LeadIaConfiguration : IEntityTypeConfiguration<LeadIa>
{
    public void Configure(EntityTypeBuilder<LeadIa> builder)
    {
        builder.ToTable("LeadsIa");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Nome).HasMaxLength(100);
        builder.Property(l => l.Empresa).HasMaxLength(200);
        builder.Property(l => l.Contato).HasMaxLength(255);
        builder.Property(l => l.Canal).HasMaxLength(50);
        builder.Property(l => l.DesafioTecnico).HasMaxLength(2000);
        builder.Property(l => l.Origem).HasMaxLength(100);
        builder.Property(l => l.AnaliseIa).HasColumnType("longtext");
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.HasIndex(l => l.CreatedAt);
    }
}

public class AgendamentoConfiguration : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("Agendamentos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ClienteNome).HasMaxLength(200);
        builder.Property(a => a.ClienteEmail).HasMaxLength(320);
        builder.Property(a => a.ClienteWhatsapp).HasMaxLength(50);
        builder.Property(a => a.Status).HasMaxLength(50);
        builder.Property(a => a.InstagramUserId).HasMaxLength(100);
        builder.Property(a => a.ExpertResponsavel).HasMaxLength(200);
        builder.Property(a => a.IndicadoPor).HasMaxLength(200);
        builder.Property(a => a.Origem).HasMaxLength(100);
        builder.Property(a => a.ValorProjeto).HasPrecision(10, 2);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasIndex(a => a.DataReuniao);
    }
}

public class EnrichCompanyConfiguration : IEntityTypeConfiguration<EnrichCompany>
{
    public void Configure(EntityTypeBuilder<EnrichCompany> builder)
    {
        builder.ToTable("EnrichCompany");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CompanyName).HasMaxLength(200);
        builder.Property(e => e.Segment).HasMaxLength(200);
        builder.Property(e => e.OutputAi).HasColumnType("longtext");
        builder.Property(e => e.CreatedAt).IsRequired();
    }
}

public class PlaygroundAnaliseConfiguration : IEntityTypeConfiguration<PlaygroundAnalise>
{
    public void Configure(EntityTypeBuilder<PlaygroundAnalise> builder)
    {
        builder.ToTable("PlaygroundAnalises");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.InputTecnico).HasMaxLength(5000);
        builder.Property(p => p.TipoAnalise).HasMaxLength(100);
        builder.Property(p => p.OutputIa).HasColumnType("longtext");
        builder.Property(p => p.Status).HasMaxLength(50);
        builder.Property(p => p.CreatedAt).IsRequired();
    }
}
