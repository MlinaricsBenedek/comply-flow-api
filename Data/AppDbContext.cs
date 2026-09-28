using comply_flow_api.Models;
using Microsoft.EntityFrameworkCore;

namespace comply_flow_api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Processing> Processings => Set<Processing>();
    public DbSet<PreProcessing> PreProcessings => Set<PreProcessing>();
    public DbSet<Rules> Rules => Set<Rules>();
    public DbSet<MatchedRules> MatchedRules => Set<MatchedRules>();
    public DbSet<ResponsePlan> ResponsePlans => Set<ResponsePlan>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<Configuration> Configurations => Set<Configuration>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>()
            .HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Conversation>()
            .HasMany(conversation => conversation.Processings)
            .WithOne(processing => processing.Conversation)
            .HasForeignKey(processing => processing.ConversationId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasOne(processing => processing.InputMessage)
            .WithMany(message => message.InputForProcessings)
            .HasForeignKey(processing => processing.InputMessageId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Configuration>()
            .HasMany(configuration => configuration.Processings)
            .WithOne(processing => processing.Configuration)
            .HasForeignKey(processing => processing.ConfigurationId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasMany(processing => processing.Messages)
            .WithOne(message => message.Processing)
            .HasForeignKey(message => message.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasOne(processing => processing.PreProcessing)
            .WithOne(preProcessing => preProcessing.Processing)
            .HasForeignKey<PreProcessing>(preProcessing => preProcessing.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasOne(processing => processing.Rules)
            .WithOne(rules => rules.Processing)
            .HasForeignKey<Rules>(rules => rules.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Rules>()
            .HasMany(rules => rules.MatchedRules)
            .WithOne(matchedRules => matchedRules.Rule)
            .HasForeignKey(matchedRules => matchedRules.RuleId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasOne(processing => processing.ResponsePlan)
            .WithOne(responsePlan => responsePlan.Processing)
            .HasForeignKey<ResponsePlan>(responsePlan => responsePlan.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasOne(processing => processing.Response)
            .WithOne(response => response.Processing)
            .HasForeignKey<Response>(response => response.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Processing>()
            .HasMany(processing => processing.EvaluationResults)
            .WithOne(result => result.Processing)
            .HasForeignKey(result => result.ProcessingId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}