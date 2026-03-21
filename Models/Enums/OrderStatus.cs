namespace onlineStore.Models.Enums
{
    public enum OrderStatus
    {
        Pending = 0,      // طلب جديد
        Confirmed = 1,    // تم التأكيد من صاحب المتجر
        Processing = 2,   // جاري التجهيز
        Shipped = 3,      // تم الشحن
        Delivered = 4,    // تم التسليم
        Cancelled = 5,    // ملغي
        Refunded = 6      // مسترجع
    }
}
