using DocumentFormat.OpenXml.Spreadsheet;
using EmpireOneRestAPIITJ.DataManager;
using EmpireOneRestAPIITJ.Models;
using EmpireOneRestAPIITJ.Services;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using SubscriptionService = EmpireOneRestAPIITJ.Services.SubscriptionService;

namespace EmpireOneRestAPIITJ.Controllers
{ 
    // CORS for TechJump frontends + localhost dev
    [EnableCors(
        origins:
            "http://localhost:4200," +
            "https://localhost:4200," +
            "https://itechjump.com," +
            "https://www.itechjump.com," +
            "https://techinterviewjump.com," +
            "https://www.techinterviewjump.com",
        headers: "*",
        methods: "*")]
    [RoutePrefix("api/ITechJump")]
    public class ITechJumpSubscriptionController : ApiController
    {
        private readonly DataAccess _data1 = new DataAccess();
        private readonly DataAccess02 _data = new DataAccess02();
        private readonly string _connString =
            ConfigurationManager.ConnectionStrings["ITechJumpDB"]?.ConnectionString;

        // GET /api/ITechjumpsubscription/db-ping
        [HttpGet, Route("db-ping")]
        public async Task<IHttpActionResult> DbPing()
        {
            if (string.IsNullOrWhiteSpace(_connString))
                return BadRequest("Connection string 'ITechJumpDB' not found.");

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("SELECT SYSUTCDATETIME()", conn))
                    {
                        var serverUtc = (DateTime)await cmd.ExecuteScalarAsync();
                        return Ok(new
                        {
                            ok = true,
                            serverTimeUtc = serverUtc,
                            dataSource = conn.DataSource,
                            database = conn.Database
                        });
                    }
                }
            }
            catch (SqlException ex)
            {
                return InternalServerError(new Exception($"SQL error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST /api/ITechjumpsubscription
        // Body: CreateSubscriptionRequest
        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Create(CreateSubscriptionRequest req)
        {
            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand("Create_UserSubscriptions", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = req.UserId;
                    cmd.Parameters.Add("@UserAlias", SqlDbType.VarChar,8).Value = req.UserAlias.ToUpper();
                    cmd.Parameters.Add("@PlanCode", SqlDbType.VarChar,50).Value = req.PlanCode.ToUpper();
                    cmd.Parameters.Add("@StripePaymentIntentId", SqlDbType.VarChar,50).Value = (object)req.StripePaymentIntentId ?? DBNull.Value;
                    cmd.Parameters.Add("@StripeChargeId", SqlDbType.VarChar,50).Value = (object)req.StripeChargeId ?? DBNull.Value;
                    cmd.Parameters.Add("@StripeCustomerId", SqlDbType.VarChar,50).Value = (object)req.StripeCustomerId ?? DBNull.Value;
                    cmd.Parameters.Add("@StripePaymentMethodId", SqlDbType.VarChar,50).Value = (object)req.StripePaymentMethodId ?? DBNull.Value;

                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        if (!reader.HasRows)
                            return InternalServerError(new Exception("Create_UserSubscriptions did not return a row."));

                        SubscriptionWithPlan created = null;
                        if (await reader.ReadAsync())
                            created = MapSubscriptionWithPlan(reader);

                        if (created == null)
                            return InternalServerError(new Exception("Failed to map created subscription."));

                        var location = new Uri(Request.RequestUri, created.SubscriptionId.ToString());
                        return Created(location, new { ok = true, subscription = created });
                    }
                }
            }
            catch (SqlException ex)
            {
                return InternalServerError(new Exception($"SQL error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>Get all questions by category.</summary>
        /// <remarks>Route kept as-is for compatibility: Tech/GetGetQuestionsbyCat</remarks>
        [HttpGet, Route("Stripe/GetProductYearly")]
        public async Task<IHttpActionResult> Get_ProductYearly(
            [FromUri][Required] string BillingInterval,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = await _data1.Get_ProductPlansYearlyAsync(BillingInterval, ct);
            return Ok(data);
        }

        // GET /api/ITechjumpsubscription/{userid}
        [HttpGet, Route("userid/{userid:int}")]
        public async Task<IHttpActionResult> GetById(int userid)
        {
            if (string.IsNullOrWhiteSpace(_connString))
                return BadRequest("Connection string 'ITechJumpDB' not found.");

            if (userid ==0)
                return BadRequest("UserID is required.");

            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand("dbo.Get_Subscription_ByUserID_FullInfo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@userid", SqlDbType.Int,50).Value = userid;

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (!reader.HasRows)
                            return NotFound();

                        ModelSubscriptionPlans found = null;

                        if (await reader.ReadAsync())
                        {
                            int oPlanCode = reader.GetOrdinal("PlanCode");
                            int oPlanName = reader.GetOrdinal("PlanName");
                            int oDescription = reader.GetOrdinal("PlanDescription");
                            int oPrice = reader.GetOrdinal("PlanCurrentPrice");
                            int oPlanStartDate = reader.GetOrdinal("CurrentPeriodStartUtc");
                            int oPlanEndDate = reader.GetOrdinal("CurrentPeriodEndUtc");
                            int oBillingPeriod = reader.GetOrdinal("BillingPeriod");
                            int oIsActive = reader.GetOrdinal("PlanIsActive");
                            int osubscriptid = reader.GetOrdinal("SubscriptionId");

                            found = new ModelSubscriptionPlans
                            {
                                PlanCode = reader.IsDBNull(oPlanCode) ? null : reader.GetString(oPlanCode),
                                PlanName = reader.IsDBNull(oPlanName) ? null : reader.GetString(oPlanName),
                                Description = reader.IsDBNull(oDescription) ? null : reader.GetString(oDescription),
                                Price = reader.IsDBNull(oPrice) ?0m : reader.GetDecimal(oPrice),
                                PlanStartDate = reader.IsDBNull(oPlanStartDate) ? (DateTime?)null : reader.GetDateTime(oPlanStartDate),
                                PlanEndDate = reader.IsDBNull(oPlanEndDate) ? (DateTime?)null : reader.GetDateTime(oPlanEndDate),
                                BillingPeriod = reader.IsDBNull(oBillingPeriod) ? null : reader.GetString(oBillingPeriod),
                                IsActive = reader.GetBoolean(oIsActive),
                                SubscriptionId = reader.GetInt32(osubscriptid)
                            }; 
                        }

                        return Ok(new { ok = true, subscription = found });
                    }
                }
            }
            catch (SqlException ex)
            {
                return InternalServerError(new Exception($"SQL error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>Get UserSubscriptions (single definitive endpoint).</summary>
        [HttpGet, Route("subscription/usersubscriptions")]
        public IHttpActionResult Get_UserSubscriptionsController(int userid, string plancode)
        {
            try
            {
                var service = new SubscriptionService();
                var result = service.Get_UserSubscriptions(userid, plancode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET /api/ITechjumpsubscription/{plancode}
        [HttpGet, Route("{plancode:maxlength(50)}")]
        public async Task<IHttpActionResult> Get_PlanDetail_ByPlanCode(string plancode)
        {
            if (string.IsNullOrWhiteSpace(_connString))
                return BadRequest("Connection string 'ITechJumpDB' not found.");

            if (string.IsNullOrWhiteSpace(plancode))
                return BadRequest("PlanCode is required.");

            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand("dbo.Get_Subscription_byPlanCode", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@PlanCode", SqlDbType.VarChar,50).Value = plancode.Trim();

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (!reader.HasRows)
                            return NotFound();

                        ModelSubscriptionPlans found = null;

                        if (await reader.ReadAsync())
                        {
                            int oPlanCode = reader.GetOrdinal("PlanCode");
                            int oPlanName = reader.GetOrdinal("PlanName");
                            int oDescription = reader.GetOrdinal("Description");
                            int oPrice = reader.GetOrdinal("Price");
                            int oIsActive = reader.GetOrdinal("IsActive");

                            found = new ModelSubscriptionPlans
                            {
                                PlanCode = reader.IsDBNull(oPlanCode) ? null : reader.GetString(oPlanCode),
                                PlanName = reader.IsDBNull(oPlanName) ? null : reader.GetString(oPlanName),
                                Description = reader.IsDBNull(oDescription) ? null : reader.GetString(oDescription),
                                Price = reader.IsDBNull(oPrice) ?0m : reader.GetDecimal(oPrice),
                                IsActive = !reader.IsDBNull(oIsActive) && reader.GetBoolean(oIsActive)
                            };
                        }

                        return Ok(new { ok = true, subscription = found });
                    }
                }
            }
            catch (SqlException ex)
            {
                return InternalServerError(new Exception($"SQL error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // --------------------------
        // Helpers / DTOs
        // --------------------------

        private static SubscriptionWithPlan MapSubscriptionWithPlan(SqlDataReader r)
        {
            DateTime? dt(object o) => o == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(o);
            int? ni(object o) => o == DBNull.Value ? (int?)null : Convert.ToInt32(o);
            string ns(object o) => o == DBNull.Value ? null : Convert.ToString(o);

            return new SubscriptionWithPlan
            {
                SubscriptionId = Convert.ToInt32(r["SubscriptionId"]),
                UserId = Convert.ToInt32(r["UserId"]),
                UserAlias = Convert.ToString(r["UserAlias"]),
                PlanCode = Convert.ToString(r["PlanCode"]),
                Status = Convert.ToString(r["Status"]),
                AutoRenew = Convert.ToBoolean(r["AutoRenew"]),
                Quantity = Convert.ToInt32(r["Quantity"]),
                StartUtc = Convert.ToDateTime(r["StartUtc"]),
                CurrentPeriodStartUtc = dt(r["CurrentPeriodStartUtc"]),
                CurrentPeriodEndUtc = dt(r["CurrentPeriodEndUtc"]),
                TrialEndUtc = dt(r["TrialEndUtc"]),
                CanceledAtUtc = dt(r["CanceledAtUtc"]),
                EndedAtUtc = dt(r["EndedAtUtc"]),
                PriceAtPurchase = Convert.ToDecimal(r["PriceAtPurchase"]),
                Currency = Convert.ToString(r["Currency"]),
                ExternalProvider = ns(r["ExternalProvider"]),
                ExternalSubscriptionId = ns(r["ExternalSubscriptionId"]),
                CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(r["UpdatedAt"]),
                PlanName = Convert.ToString(r["PlanName"]),
                Description = ns(r["Description"]),
                TicketSets = ni(r["TicketSets"])
            };
        }

        // Create a Stripe Checkout Session for hosted payment page
        [HttpPost, Route("stripe/create-checkout-session")]
        public IHttpActionResult CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest req)
        {
            if (req == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(req.PlanCode))
                return BadRequest("PlanCode (Stripe Price ID) is required.");

            if (string.IsNullOrWhiteSpace(req.UserEmail))
                return BadRequest("UserEmail is required.");

            var secretKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                return InternalServerError(new Exception("StripeSecretKey appSetting is missing."));

            var successUrl = ConfigurationManager.AppSettings["StripeCheckoutSuccessUrl"];
            var cancelUrl  = ConfigurationManager.AppSettings["StripeCheckoutCancelUrl"];

            if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
                return InternalServerError(new Exception("StripeCheckoutSuccessUrl or StripeCheckoutCancelUrl appSettings are missing."));

            StripeConfiguration.ApiKey = secretKey;

            try
            {
                var options = new SessionCreateOptions
                {
                    Mode = "subscription",
                    SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = cancelUrl,
                    CustomerEmail = req.UserEmail,
                    ClientReferenceId = req.UserId.ToString(),
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Price = req.PlanCode,
                            Quantity = 1,
                        }
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "userId", req.UserId.ToString() },
                        { "userAlias", req.UserAlias ?? string.Empty },
                        { "planCode", req.PlanCode ?? string.Empty }
                    }
                };

                var service = new SessionService();
                var session = service.Create(options);

                return Ok(new
                {
                    ok = true,
                    id = session.Id,
                    url = session.Url
                });
            }
            catch (StripeException ex)
            {
                return Content(System.Net.HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // --------- DTOs ---------

    public class CreateSubscriptionRequest
    {
        public int UserId { get; set; }                 // required
        public string UserAlias { get; set; } = "";     // exactly 8 chars
        public string PlanCode { get; set; } = "";      // required
        public string StripePaymentIntentId { get; set; } = "";
        public string StripeChargeId { get; set; } = "";            // ch_xxx
        public string StripeCustomerId { get; set; } = "";         // cus_xxx(optional)
        public string StripePaymentMethodId { get; set; } = ""; // optional for Checkout
    }

    public class CreateCheckoutSessionRequest
    {
        public int UserId { get; set; }
        public string UserAlias { get; set; }
        public string UserEmail { get; set; }
        public string PlanCode { get; set; } // Stripe Price ID
    }




    public class SubscriptionWithPlan
    {
        // From UserSubscriptions
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public string UserAlias { get; set; }
        public string PlanCode { get; set; }
        public string Status { get; set; }
        public bool AutoRenew { get; set; }
        public int Quantity { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime? CurrentPeriodStartUtc { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
        public DateTime? TrialEndUtc { get; set; }
        public DateTime? CanceledAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public decimal PriceAtPurchase { get; set; }
        public string Currency { get; set; }
        public string ExternalProvider { get; set; }
        public string ExternalSubscriptionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // From SubscriptionPlans join
        public string PlanName { get; set; }
        public string Description { get; set; }
        public int? TicketSets { get; set; }
    }
}
