namespace onlineStore.Models;

public sealed class StoreRegistrationVisit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime VisitedAtUtc { get; set; } = DateTime.UtcNow;
}
