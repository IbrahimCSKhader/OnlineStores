using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Order;

public class UpdateReceivedAmountDto
{
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? ReceivedAmount { get; set; }
}
