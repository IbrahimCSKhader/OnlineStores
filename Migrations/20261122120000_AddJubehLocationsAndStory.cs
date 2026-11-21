using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using onlineStore.Data;

namespace onlineStore.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261122120000_AddJubehLocationsAndStory")]
public class AddJubehLocationsAndStory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO [StoreAppearance] ([StoreId], [OverridesJson])
            SELECT [Id], N'{}' FROM [Stores] s
            WHERE s.[Slug] = N'jubeh-office-furniture'
              AND NOT EXISTS (SELECT 1 FROM [StoreAppearance] a WHERE a.[StoreId] = s.[Id]);
            UPDATE a SET [OverridesJson] = JSON_MODIFY(
                JSON_MODIFY(
                    JSON_MODIFY(a.[OverridesJson], '$.Sections', JSON_QUERY(COALESCE(JSON_QUERY(a.[OverridesJson], '$.Sections'), N'{}'))),
                    '$.Sections.ShowLocations', CAST(1 AS bit)),
                '$.Sections.Locations', JSON_QUERY(N'[{"Name": "الفرع الأول – رام الله", "Address": "رام الله – منطقة الشرفة – مجمع الأماني – الطابق الثاني", "Phone": "0568067006", "WhatsApp": "972528601833"}, {"Name": "الفرع الثاني – الرام", "Address": "الرام – مقابل مسجد معاذ بن جبل", "Phone": "0522266619", "WhatsApp": ""}]'))
            FROM [StoreAppearance] a JOIN [Stores] s ON s.[Id] = a.[StoreId]
            WHERE s.[Slug] = N'jubeh-office-furniture';
            UPDATE [Stores] SET [StoreStory] = N'من نحن | الجعبة للأثاث المكتبي
            16 عاماً من الخبرة والتميز في عالم الأثاث المكتبي
            الجعبة للأثاث المكتبي – أناقة لمكتبك
            في الجعبة للأثاث المكتبي، نؤمن بأن المكتب ليس مجرد مكان للعمل، بل هو مساحة للإبداع والإنجاز والنجاح. ومن هذا المنطلق، انطلقت مسيرتنا قبل أكثر من 16 عاماً، لنقدم لعملائنا حلولاً متكاملة في عالم الأثاث المكتبي، تجمع بين الجودة العالية، والتصاميم العصرية، والراحة، والأناقة، والأسعار المناسبة.
            على مدار سنوات خبرتنا، حرصنا على مواكبة أحدث التصاميم والاتجاهات في عالم الأثاث المكتبي، وتوفير تشكيلة متنوعة تلبي مختلف الأذواق والاحتياجات، سواء لتجهيز المكاتب المنزلية، أو الشركات، أو المؤسسات، أو المساحات التجارية.
            ماذا نقدم لكم؟
            نوفر في معارضنا مجموعة واسعة ومتنوعة من الأثاث المكتبي، تشمل:
            ⭕ الكراسي المكتبية: تصاميم عصرية ومريحة تناسب مختلف بيئات العمل.
            ⭕ الطاولات والمكاتب: خيارات أنيقة وعملية بمقاسات وتصاميم متنوعة.
            ⭕ طاولات الاجتماعات: تصاميم مميزة تناسب قاعات الاجتماعات والشركات.
            ⭕ الخزائن ووحدات التخزين: حلول عملية لتنظيم الملفات والمستلزمات المكتبية.
            ⭕ الرفوف والستاندات: تشكيلات متنوعة تضيف لمسة من التنظيم والجمال إلى مساحات العمل.
            رؤيتنا
            نسعى لأن نكون الخيار المفضل لكل من يبحث عن أثاث مكتبي يجمع بين الأناقة والجودة والعملية، وأن نساهم في تحويل كل مساحة عمل إلى بيئة مريحة وعصرية تساعد على الإنتاج والإبداع.
            لماذا الجعبة للأثاث المكتبي؟
            لأننا نؤمن بأن رضا عملائنا هو أساس نجاحنا، نحرص دائماً على تقديم خيارات متنوعة، وتصاميم متجددة، وجودة تستحق الثقة، مع الاهتمام بأدق التفاصيل لتلبية احتياجات عملائنا.
            خبرتنا الممتدة لـ16 عاماً هي مصدر ثقتنا، وثقة عملائنا هي سر استمرارنا.'
            WHERE [Slug] = N'jubeh-office-furniture';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the owner's branch details and story when rolling back; hide the page.
        migrationBuilder.Sql("""
            UPDATE a SET [OverridesJson] = JSON_MODIFY(a.[OverridesJson], '$.Sections.ShowLocations', CAST(0 AS bit))
            FROM [StoreAppearance] a JOIN [Stores] s ON s.[Id] = a.[StoreId]
            WHERE s.[Slug] = N'jubeh-office-furniture';
            """);
    }
}
