namespace onlineStore.Services.Email
{
    public sealed record EmailTemplateContent(
        string Subject,
        string HtmlBody,
        string PlainTextBody);
}
