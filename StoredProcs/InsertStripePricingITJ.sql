INSERT INTO [dbo].[Products]
(
    PlanCode, ProductName, ProductDescription, StripeProductId, StripePriceId, ProductTaxCode, Price, Currency, BillingInterval, IntervalCount, IsActive, EffectiveStartDate, EffectiveEndDate, CreatedAt, UpdatedAt
)
VALUES
(
    'PRO-YEAR',
    'ITJ-Pro-Year',
    'Full pro training course with voice mode for audio learning, and testing scores for a full year at $49.99',
    'prod_Td0XI17Y9Mqxt5',
    'price_1SfkWfDZoJAP570SrWNmsDXU',
    'txcd_20060058',
    49.99,
    'USD',
    'year',
    1,
    1,
    SYSUTCDATETIME(),
    NULL,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
);

SELECT*
  FROM [haloimag_techinterview].[dbo].[Products]

SELECT*
  FROM [haloimag_techinterview].[dbo].[SubscriptionPlans]


  update [haloimag_techinterview].[dbo].[Products] set Price = 19.99 where productid = 1002

  update [haloimag_techinterview].[dbo].[SubscriptionPlans] set Price = 29.99 where PlanCode = 'BASIC-YEAR'
