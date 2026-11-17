namespace onlineStore.Models.Reviews;

public class StoreRating
{
    public Guid StoreId { get; set; }
    public Guid VoterId { get; set; }
    public bool IsGuest { get; set; }
    public int Score { get; set; }
}
