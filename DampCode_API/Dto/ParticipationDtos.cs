namespace DampCode_API.Dto;

// Saídas da inscrição e das equipes, com os nomes de JSON que o front lê (seção 5.3 do design).
public sealed record TeamMemberDto(Guid AccountId, string Nome);

public sealed record TeamPendingInvitationDto(Guid Id, string Email, DateTime ExpiraEm);

public sealed record TeamPendingRequestDto(Guid Id, Guid AccountId, string Nome);

public sealed record MyTeamDto(
    Guid TeamId,
    string Nome,
    bool Aberta,
    IReadOnlyList<TeamMemberDto> Membros,
    IReadOnlyList<TeamPendingInvitationDto> ConvitesPendentes,
    IReadOnlyList<TeamPendingRequestDto> PedidosPendentes);

public sealed record MyParticipationDto(
    Guid HackathonId,
    string Titulo,
    string Empresa,
    string Fase,
    string Formato,
    DateTime? DataInicio,
    MyTeamDto? Equipe);

public sealed record TeamInvitationReceivedDto(
    Guid Id,
    Guid TeamId,
    string Equipe,
    Guid HackathonId,
    string Hackathon,
    string ConvidadoPor,
    DateTime ExpiraEm);

public sealed record TeamJoinRequestSentDto(Guid Id, Guid TeamId, string Equipe, Guid HackathonId, string Hackathon);

public sealed record OpenTeamDto(Guid TeamId, string Nome, int Membros, int Vagas);

public sealed record ParticipationsOverviewDto(
    IReadOnlyList<MyParticipationDto> Inscricoes,
    IReadOnlyList<TeamInvitationReceivedDto> ConvitesRecebidos,
    IReadOnlyList<TeamJoinRequestSentDto> PedidosEnviados);
