using System.Text.Json.Serialization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;
using SimpleDiscordNet.Sharding;
using SimpleDiscordNet.Sharding.Models;

namespace SimpleDiscordNet.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
                             PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             WriteIndented = false)]
[JsonSerializable(typeof(ApplicationInfo))]
[JsonSerializable(typeof(DiscordGuild))]
[JsonSerializable(typeof(DiscordGuild[]))]
[JsonSerializable(typeof(DiscordChannel))]
[JsonSerializable(typeof(DiscordChannel[]))]
[JsonSerializable(typeof(DiscordRole))]
[JsonSerializable(typeof(DiscordRole[]))]
[JsonSerializable(typeof(DiscordMember))]
[JsonSerializable(typeof(DiscordMember[]))]
[JsonSerializable(typeof(DiscordUser))]
[JsonSerializable(typeof(DiscordUser[]))]
[JsonSerializable(typeof(DiscordEmoji))]
[JsonSerializable(typeof(DiscordEmoji[]))]
[JsonSerializable(typeof(DiscordMessage))]
[JsonSerializable(typeof(Embed))]
[JsonSerializable(typeof(CreateMessageRequest))]
[JsonSerializable(typeof(WebhookMessageRequest))]
[JsonSerializable(typeof(OpenModalRequest))]
[JsonSerializable(typeof(BulkDeleteMessagesRequest))]
[JsonSerializable(typeof(BanMemberRequest))]
[JsonSerializable(typeof(ModalData))]
[JsonSerializable(typeof(MessagePayload))]
[JsonSerializable(typeof(ApplicationCommandDefinition))]
[JsonSerializable(typeof(ApplicationCommandDefinition[]))]
[JsonSerializable(typeof(CommandChoice))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(InteractionResponseData))]
[JsonSerializable(typeof(InteractionCreateEvent))]
[JsonSerializable(typeof(InteractionOption))]
[JsonSerializable(typeof(MessageComponentData))]
[JsonSerializable(typeof(ActionRow))]
[JsonSerializable(typeof(Button))]
[JsonSerializable(typeof(StringSelect))]
[JsonSerializable(typeof(SelectOption))]
[JsonSerializable(typeof(UserSelect))]
[JsonSerializable(typeof(RoleSelect))]
[JsonSerializable(typeof(MentionableSelect))]
[JsonSerializable(typeof(ChannelSelect))]
[JsonSerializable(typeof(TextInput))]
[JsonSerializable(typeof(GatewayPayload))]
[JsonSerializable(typeof(Identify))]
[JsonSerializable(typeof(IdentifyPayload))]
[JsonSerializable(typeof(IdentifyConnectionProperties))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(Resume))]
[JsonSerializable(typeof(ResumePayload))]
[JsonSerializable(typeof(RequestGuildMembers))]
[JsonSerializable(typeof(RequestGuildMembersPayload))]
// Sharding models
[JsonSerializable(typeof(WorkerRegistrationRequest))]
[JsonSerializable(typeof(WorkerRegistrationResponse))]
[JsonSerializable(typeof(SuccessionEntry))]
[JsonSerializable(typeof(WorkerCapabilities))]
[JsonSerializable(typeof(WorkerMetrics))]
[JsonSerializable(typeof(ShardMetrics))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(ShardAssignment))]
[JsonSerializable(typeof(SuccessionUpdate))]
[JsonSerializable(typeof(ShardMigrationRequest))]
[JsonSerializable(typeof(PeerNodeState))]
[JsonSerializable(typeof(ClusterState))]
[JsonSerializable(typeof(CoordinatorResumptionRequest))]
[JsonSerializable(typeof(CoordinatorHandoffData))]
[JsonSerializable(typeof(CoordinatorResumedAnnouncement))]
[JsonSerializable(typeof(HttpErrorResponse))]
// Sharding model arrays/lists
[JsonSerializable(typeof(List<SuccessionEntry>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<ShardMetrics>))]
[JsonSerializable(typeof(List<PeerNodeState>))]
[JsonSerializable(typeof(Dictionary<int, string>))]

internal partial class DiscordJsonContext : JsonSerializerContext;
