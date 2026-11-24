-- Prices for the three books in the live Alaa Rob library.
-- The "Without Filter" game remains price-on-request.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE Products
SET Price = CASE Id
        WHEN 'd57b2e20-903a-47c3-9aab-1c4e99dbb86e' THEN 80
        WHEN '5bd2356a-7651-47c7-93bb-098f0c1c488c' THEN 65
        WHEN '7655ded5-6ffc-4eae-bb26-8ba8fd1a404d' THEN 50
    END,
    PriceOnRequest = 0,
    Description = REPLACE(
        Description,
        N'للاستفسار عن السعر والطلب راسلنا على إنستغرام.',
        N'أضفه إلى السلة وأكمل الطلب عبر إنستغرام.'
    )
WHERE StoreId = '0d77c4bc-cfd6-45ad-9088-e8cf3e4213cf'
  AND Id IN (
      'd57b2e20-903a-47c3-9aab-1c4e99dbb86e',
      '5bd2356a-7651-47c7-93bb-098f0c1c488c',
      '7655ded5-6ffc-4eae-bb26-8ba8fd1a404d'
  );

IF @@ROWCOUNT <> 3
    THROW 50001, 'Expected three Alaa Rob books; no prices were committed.', 1;

COMMIT TRANSACTION;
