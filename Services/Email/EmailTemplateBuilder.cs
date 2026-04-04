using System.Net;

namespace onlineStore.Services.Email
{
    internal static class EmailTemplateBuilder
    {
        public static EmailTemplateContent BuildWelcomeEmail(string? firstName)
        {
            var normalizedFirstName = string.IsNullOrWhiteSpace(firstName)
                ? "Customer"
                : firstName.Trim();

            var safeFirstName = WebUtility.HtmlEncode(normalizedFirstName);
            const string subject = "Welcome to Online Store";

            var htmlBody = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>{{subject}}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <div style="width:100%;padding:24px 12px;box-sizing:border-box;">
                        <div style="max-width:600px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
                            <div style="padding:32px 32px 16px;background-color:#0f172a;color:#ffffff;">
                                <h1 style="margin:0;font-size:28px;line-height:1.3;">Welcome, {{safeFirstName}}</h1>
                                <p style="margin:12px 0 0;font-size:16px;line-height:1.6;color:#cbd5e1;">
                                    Your account is ready and you can now start exploring the store.
                                </p>
                            </div>
                            <div style="padding:32px;">
                                <p style="margin:0 0 16px;font-size:16px;line-height:1.7;">
                                    We are happy to have you with us. Your account was created successfully, and you can now browse products, manage your cart, and place orders with confidence.
                                </p>
                                <p style="margin:0 0 16px;font-size:16px;line-height:1.7;">
                                    If you did not create this account, please contact our support team as soon as possible.
                                </p>
                                <div style="margin-top:24px;padding-top:24px;border-top:1px solid #e5e7eb;font-size:14px;line-height:1.7;color:#6b7280;">
                                    <p style="margin:0;">Thank you for choosing Online Store.</p>
                                    <p style="margin:8px 0 0;">This is an automated email. Please do not reply directly to this message.</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """;

            var plainTextBody =
                $"Hello {normalizedFirstName},{Environment.NewLine}{Environment.NewLine}" +
                "Welcome to Online Store. Your account has been created successfully." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "You can now browse products, manage your cart, and place orders." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "If you did not create this account, please contact support immediately." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Thank you for choosing Online Store.";

            return new EmailTemplateContent(subject, htmlBody, plainTextBody);
        }

        public static EmailTemplateContent BuildEmailVerificationCodeEmail(
            string? firstName,
            string code)
        {
            var normalizedFirstName = string.IsNullOrWhiteSpace(firstName)
                ? "Customer"
                : firstName.Trim();

            var safeFirstName = WebUtility.HtmlEncode(normalizedFirstName);
            var safeCode = WebUtility.HtmlEncode(code.Trim());
            const string subject = "Your email verification code";

            var htmlBody = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>{{subject}}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <div style="width:100%;padding:24px 12px;box-sizing:border-box;">
                        <div style="max-width:600px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
                            <div style="padding:32px 32px 16px;background-color:#0f172a;color:#ffffff;">
                                <h1 style="margin:0;font-size:28px;line-height:1.3;">Verify your email</h1>
                                <p style="margin:12px 0 0;font-size:16px;line-height:1.6;color:#cbd5e1;">
                                    Hello {{safeFirstName}}, use the code below to activate your account.
                                </p>
                            </div>
                            <div style="padding:32px;">
                                <div style="margin:0 0 24px;padding:18px 16px;border:1px dashed #94a3b8;border-radius:10px;background-color:#f8fafc;text-align:center;">
                                    <span style="display:block;font-size:32px;letter-spacing:8px;font-weight:700;color:#0f172a;">{{safeCode}}</span>
                                </div>
                                <p style="margin:0 0 12px;font-size:16px;line-height:1.7;">
                                    Enter this code in the app to confirm your email address and complete your account setup.
                                </p>
                                <p style="margin:0;font-size:14px;line-height:1.7;color:#6b7280;">
                                    If you did not create this account, you can safely ignore this message.
                                </p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """;

            var plainTextBody =
                $"Hello {normalizedFirstName},{Environment.NewLine}{Environment.NewLine}" +
                $"Your email verification code is: {code.Trim()}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Enter this code in the app to activate your account." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "If you did not create this account, you can ignore this email.";

            return new EmailTemplateContent(subject, htmlBody, plainTextBody);
        }

        public static EmailTemplateContent BuildPasswordResetCodeEmail(
            string? firstName,
            string code)
        {
            var normalizedFirstName = string.IsNullOrWhiteSpace(firstName)
                ? "Customer"
                : firstName.Trim();

            var safeFirstName = WebUtility.HtmlEncode(normalizedFirstName);
            var safeCode = WebUtility.HtmlEncode(code.Trim());
            const string subject = "Your password reset code";

            var htmlBody = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>{{subject}}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <div style="width:100%;padding:24px 12px;box-sizing:border-box;">
                        <div style="max-width:600px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
                            <div style="padding:32px 32px 16px;background-color:#0f172a;color:#ffffff;">
                                <h1 style="margin:0;font-size:28px;line-height:1.3;">Reset your password</h1>
                                <p style="margin:12px 0 0;font-size:16px;line-height:1.6;color:#cbd5e1;">
                                    Hello {{safeFirstName}}, use the code below to continue resetting your password.
                                </p>
                            </div>
                            <div style="padding:32px;">
                                <div style="margin:0 0 24px;padding:18px 16px;border:1px dashed #94a3b8;border-radius:10px;background-color:#f8fafc;text-align:center;">
                                    <span style="display:block;font-size:32px;letter-spacing:8px;font-weight:700;color:#0f172a;">{{safeCode}}</span>
                                </div>
                                <p style="margin:0 0 12px;font-size:16px;line-height:1.7;">
                                    Enter this code in the app, then choose a new password for your account.
                                </p>
                                <p style="margin:0;font-size:14px;line-height:1.7;color:#6b7280;">
                                    If you did not request a password reset, please ignore this email.
                                </p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """;

            var plainTextBody =
                $"Hello {normalizedFirstName},{Environment.NewLine}{Environment.NewLine}" +
                $"Your password reset code is: {code.Trim()}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Enter this code in the app and choose a new password." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "If you did not request this, you can ignore this email.";

            return new EmailTemplateContent(subject, htmlBody, plainTextBody);
        }
    }
}
