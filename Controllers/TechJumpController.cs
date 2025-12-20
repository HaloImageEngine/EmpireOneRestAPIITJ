using EmpireOneRestAPIITJ.DataManager;
using EmpireOneRestAPIITJ.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using Stripe;

namespace EmpireOneRestAPIITJ.Controllers
{
    // Allow calls from your frontend + (optionally) same host
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
    public class ITechJumpController : ApiController
    {
        private readonly DataAccess _data1 = new DataAccess();

        // -------------------- Infra / DB check --------------------

        /// <summary>Ping the DB and return server info (sanity check).</summary>
        [HttpGet, Route("db-ping")]
        public async Task<IHttpActionResult> DbPing(CancellationToken ct)
        {
            var cs = System.Configuration.ConfigurationManager
                .ConnectionStrings["ITechJumpDB"]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
                return BadRequest("Connection string 'ITechJumpDB' not found.");

            try
            {
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand("SELECT SYSUTCDATETIME()", conn))
                {
                    await conn.OpenAsync(ct);
                    var serverUtc = (DateTime)await cmd.ExecuteScalarAsync(ct);

                    return Ok(new
                    {
                        ok = true,
                        serverTimeUtc = serverUtc,
                        dataSource = conn.DataSource,
                        database = conn.Database
                    });
                }
            }
            catch (SqlException ex)
            {
                // Wrap with extra context but keep original as InnerException
                return InternalServerError(
                    new Exception($"SQL error {ex.Number}: {ex.Message}", ex));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // -------------------- Stripe endpoints --------------------

        private SetupIntentService _setupIntentService;
        private CustomerService _customerService;
        private SubscriptionService _subscriptionService;

        private void EnsureStripeInitialized()
        {
            if (!string.IsNullOrEmpty(StripeConfiguration.ApiKey))
            {
                return;
            }

            var secretKey = System.Configuration.ConfigurationManager
                .AppSettings["StripeSecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("StripeSecretKey appSetting is missing.");
            }

            StripeConfiguration.ApiKey = secretKey;
            _setupIntentService = new SetupIntentService();
            _customerService = new CustomerService();
            _subscriptionService = new SubscriptionService();
        }

        public class CreateSetupIntentRequest
        {
            [Required]
            public string UserId { get; set; }

            [Required]
            public string UserAlias { get; set; }
        }

        public class CreateSetupIntentResponse
        {
            public string ClientSecret { get; set; }
            public string CustomerId { get; set; }
        }

        [HttpPost, Route("Stripe/CreateSetupIntent")]
        public async Task<IHttpActionResult> CreateSetupIntent(
            [FromBody][Required] CreateSetupIntentRequest request,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                EnsureStripeInitialized();

                // In a real app, you would typically look up an existing Stripe CustomerId for this user
                // and reuse it; for now we always create a customer.
                var customerOptions = new CustomerCreateOptions
                {
                    Metadata =
                    {
                        { "userId", request.UserId },
                        { "userAlias", request.UserAlias }
                    }
                };

                var customer = await _customerService.CreateAsync(customerOptions, cancellationToken: ct);

                var siOptions = new SetupIntentCreateOptions
                {
                    Customer = customer.Id,
                    Usage = "off_session"
                };

                var setupIntent = await _setupIntentService.CreateAsync(siOptions, cancellationToken: ct);

                var response = new CreateSetupIntentResponse
                {
                    ClientSecret = setupIntent.ClientSecret,
                    CustomerId = customer.Id
                };

                return Ok(response);
            }
            catch (StripeException sex)
            {
                return Content(System.Net.HttpStatusCode.BadRequest, new
                {
                    error = sex.Message,
                    type = sex.StripeError?.Type,
                    code = sex.StripeError?.Code
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        public class CreateSubscriptionRequest
        {
            [Required]
            public string CustomerId { get; set; }

            [Required]
            public string PaymentMethodId { get; set; }

            [Required]
            public string PlanCode { get; set; }
        }

        public class CreateSubscriptionResponse
        {
            public string SubscriptionId { get; set; }
        }

        [HttpPost, Route("Stripe/CreateSubscription")]
        public async Task<IHttpActionResult> CreateSubscription(
            [FromBody][Required] CreateSubscriptionRequest request,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                EnsureStripeInitialized();

                // Attach the payment method to the customer and set as default
                var paymentMethodService = new PaymentMethodService();
                await paymentMethodService.AttachAsync(
                    request.PaymentMethodId,
                    new PaymentMethodAttachOptions { Customer = request.CustomerId },
                    cancellationToken: ct);

                var customerUpdate = new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = request.PaymentMethodId
                    }
                };
                await _customerService.UpdateAsync(request.CustomerId, customerUpdate, cancellationToken: ct);

                var subOptions = new SubscriptionCreateOptions
                {
                    Customer = request.CustomerId,
                    Items =
                    {
                        new SubscriptionItemOptions
                        {
                            Price = request.PlanCode
                        }
                    },
                    Expand = { "latest_invoice.payment_intent" }
                };

                var subscription = await _subscriptionService.CreateAsync(subOptions, cancellationToken: ct);

                var response = new CreateSubscriptionResponse
                {
                    SubscriptionId = subscription.Id
                };

                return Ok(response);
            }
            catch (StripeException sex)
            {
                return Content(System.Net.HttpStatusCode.BadRequest, new
                {
                    error = sex.Message,
                    type = sex.StripeError?.Type,
                    code = sex.StripeError?.Code
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // -------------------- Reads (GET) --------------------

        /// <summary>Get all questions.</summary>
        [HttpGet, Route("Tech/GetAllQuestion")]
        public async Task<IHttpActionResult> Get_AllQuestions(CancellationToken ct)
        {
            var data = await _data1.Get_AllQuestionsAsync(ct);
            return Ok(data);
        }

        /// <summary>Get all questions by category.</summary>
        /// <remarks>Route kept as-is for compatibility: Tech/GetGetQuestionsbyCat</remarks>
        [HttpGet, Route("Tech/GetGetQuestionsbyCat")]
        public async Task<IHttpActionResult> Get_QuestionbyCat(
            [FromUri][Required] string cat,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = await _data1.Get_QuestionsbyCatAsync(cat, ct);
            return Ok(data);
        }

        /// <summary>Get all questions by category.</summary>
        /// <remarks>Route kept as-is for compatibility: Tech/GetGetQuestionsbyCat</remarks>
        [HttpGet, Route("Tech/GetKeywordbyQID")]
        public async Task<IHttpActionResult> Get_KeywordbyQID(
            [FromUri][Required] string qid,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = await _data1.Get_KeyWordByQIDAsync(qid, ct);
            return Ok(data);
        }

        /// <summary>Get dropdown categories by category key.</summary>
        [HttpGet, Route("Tech/GetTestResultsbyUserId")]
        public IHttpActionResult Get_TestResultsbyUserId(
            [FromUri][Required] string userid,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = _data1.Get_TestResultbyUserIdList(userid);
            return Ok(data);
        }

        /// <summary>Get dropdown categories by category key.</summary>
        [HttpGet, Route("Tech/GetTestResultsbyTestId")]
        public IHttpActionResult Get_TestResultsbyTestId(
            [FromUri][Required] string testid,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = _data1.Get_TestResultbyTestId(testid);
            return Ok(data);
        }

        /// <summary>Get dropdown categories by category key.</summary>
        [HttpGet, Route("Tech/GetDropDownCat")]
        public async Task<IHttpActionResult> Get_DropDownCat(
            [FromUri][Required] string cat,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = await _data1.Get_DropDownCatAsync(cat, ct);
            return Ok(data);
        }

        /// <summary>Get dropdown categories by category key.</summary>
        [HttpGet, Route("Tech/GetSearchKeyword")]
        public async Task<IHttpActionResult> Get_SearchbyKeyword(
            [FromUri][Required] string keyword,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var data = await _data1.Get_SearchByKeyword_Async(keyword, ct);
            return Ok(data);
        }

        // Optional: if you later want a POST endpoint to insert questions,
        // you can uncomment and adapt this:
        [HttpPost, Route("Tech/InsertAnswer")]
        public async Task<IHttpActionResult> InsertAnswer(
            [FromBody][Required] InsertAnswerKeywordDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Fix: Call InsertAnswerAsync instead of InsertAnswer (which is sync and returns ModelGradeReturn)
            var result = await _data1.InsertAnswerAsync(dto.QuestionID.ToString(), dto.Answer);

            return Ok(result);
        }


        // Optional: if you later want a POST endpoint to insert questions,
        // you can uncomment and adapt this:
        [HttpPost, Route("Tech/InsertAnswerScore")]
        public async Task<IHttpActionResult> InsertAnswerScore(
            [FromBody][Required] InsertAnswerKeywordDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Fix: Call InsertAnswerAsync instead of InsertAnswer (which is sync and returns ModelGradeReturn)
            var result = await _data1.InsertAnswerScoreAsync(dto.QuestionID.ToString(), dto.Answer, dto.UserID, dto.Category);

            return Ok(result);
        }

        [HttpPost, Route("Tech/InsertQuestion")]
        public async Task<IHttpActionResult> InsertQuestion(
            [FromBody][Required] InsertQuestionDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var rows = await _data1.InsertQuestion(dto.Category, dto.Question, dto.Answer);

            return Ok(new { rowsAffected = rows });
        }

        public class InsertQuestionDto
        {
            [Required]
            public string Category { get; set; }

            [Required, MaxLength(100)]
            public string Question { get; set; }

            [Required, MaxLength(2000)]
            public string Answer { get; set; }
        }

        public class InsertAnswerDto
        {
            [Required]
            public string Category { get; set; }

            public int QuestionID{ get; set; }

            [Required, MaxLength(2000)]
            public string Answer { get; set; }
        }

        public class InsertAnswerKeywordDto
        {
            [Required]
            public int QuestionID { get; set; }

            [Required]
            public string UserID { get; set; }

            [Required]
            public string Category { get; set; }

            [Required, MaxLength(2000)]
            public string Answer { get; set; }
        }

    }
}
