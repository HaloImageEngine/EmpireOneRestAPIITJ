using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmpireOneRestAPIITJ.Models
{
    [Table("SubscriptionPlans")]
    public class ModelSubscriptionPlansFull
    {
        // PK
        [Key]
        [Required]
        [StringLength(50)]
        public string PlanCode { get; set; }

        [Required]
        [StringLength(100)]
        public string PlanName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(3)]
        public string Currency { get; set; }

        [Required]
        [StringLength(20)]
        public string BillingPeriod { get; set; }

        public int? TrialDays { get; set; }

        public int? MaxUsers { get; set; }

        public int? MinNumbers { get; set; }

        public int? MaxNumbers { get; set; }

        public int? TicketSets { get; set; }

        [Required]
        public bool IsActive { get; set; }

        // Note: datetime2(0) precision is enforced at the DB level; 
        // [Column(TypeName = "datetime2")] is included for clarity.
        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime UpdatedAt { get; set; }
    }

    public class ModelSubscriptionPlans
    {
        // PK
        [Key]
        [Required]
        [StringLength(50)]
        public string PlanCode { get; set; }

        [Required]
        [StringLength(100)]
        public string PlanName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        public int SubscriptionId { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; }

       
        public DateTime? PlanStartDate { get; set; }

        public DateTime? PlanEndDate { get; set; }

        public string BillingPeriod { get; set; }

        [Required]
        public bool IsActive { get; set; }

        
    }

    public class ModelProductPlansFull
    {
        // PK
        [Key]
        [Required]
        public int ProductID { get; set; }

        [Required]
        [StringLength(50)]
        public string PlanCode { get; set; }

        [Required]
        [StringLength(150)]
        public string ProductName { get; set; }

        [StringLength(255)]
        public string ProductDescription { get; set; }

        [Required]
        [StringLength(100)]
        public string StripeProductId { get; set; }

        [Required]
        [StringLength(100)]
        public string StripePriceId { get; set; }

        [StringLength(50)]
        public string ProductTaxCode { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(3)]
        public string Currency { get; set; }

        [Required]
        [StringLength(10)]
        public string BillingInterval { get; set; }

        [Required]
        public int IntervalCount { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime EffectiveStartDate { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? EffectiveEndDate { get; set; }

        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }

    public class ModelProductPlansGet
    {
        public int ProductID { get; set; }
        
        public string PlanCode { get; set; }

        public string ProductName { get; set; }
        
        public string ProductDescription { get; set; }

        public string StripeProductId { get; set; }

        public string StripePriceId { get; set; }

        public string ProductTaxCode { get; set; }


        public decimal Price { get; set; }

        public string Currency { get; set; }

        public string BillingInterval { get; set; }

        public bool IsActive { get; set; }


    }



    public class ModelSubscriptionPlansShort
    {
        // PK
        [Key]
        [Required]
        [StringLength(50)]
        public string PlanCode { get; set; }

        [Required]
        [StringLength(100)]
        public string PlanName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int? TicketSets { get; set; }

        [Required]
        public bool IsActive { get; set; }


    }

    public class UserSubscriptionDto
    {
        [Key]
        public int UserId { get; set; }
        public string UserAlias { get; set; }
        public string PlanCode { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PlanName { get; set; }
        public string BillingPeriod { get; set; }
        public string DisplayName { get; set; }
    }
}
